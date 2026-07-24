// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Non-generic base for ACR data-plane processors. Mirrors the structure of
/// <c>ComputeProcessorBase</c>:
/// <list type="bullet">
///   <item><see cref="CreateRequestMessage"/> — builds the outgoing HTTP request.</item>
///   <item><see cref="GetScope"/> — returns the ACR scope required for the request.</item>
///   <item><see cref="SendAsync"/> — acquires the ACR token and sends the request.</item>
/// </list>
/// </summary>
public abstract class AcrProcessorBase : IProcessor
{
    public IRuntimeHttpPipeline HttpPipeline = null!;
    public IEventPublisher Publisher = null!;
    public IAuthHandler AuthHandler = null!;
    public JsonSerializerOptions JsonOptions = null!;
    protected OutputStreamDelegate WriteOutput = null!;

    private AcrAuthOrchestrator _authOrchestrator = null!;

    protected virtual void BeforeInit() { }
    protected virtual void AfterInit() { }

    public CancellationToken Token => CancellationToken.None;
    public Action Cancel => throw new NotImplementedException();

    public IProcessor Init(ProcessorContext context)
    {
        BeforeInit();

        HttpPipeline = context.HttpPipeline;
        Publisher = context.Publisher;
        AuthHandler = context.AuthHandler;
        WriteOutput = context.OutputHandler.WriteOutput;
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Wire the orchestrator as the per-request auth provider
        _authOrchestrator = new AcrAuthOrchestrator(AuthHandler, HttpPipeline, Publisher, JsonOptions);
        AuthHandler.RequestAuthProvider = _authOrchestrator;

        AfterInit();
        return this;
    }

    public virtual Task ProcessAsync() => Task.CompletedTask;

    /// <summary>
    /// Resolves the login server FQDN from a registry name (e.g., "myregistry" → "myregistry.azurecr.io").
    /// </summary>
    protected static string ResolveLoginServer(string registryName)
    {
        if (registryName.Contains('.'))
        {
            return registryName;
        }
        return $"{registryName}.azurecr.io";
    }

    /// <summary>
    /// Processors override to build the outgoing <see cref="HttpRequestMessage"/>.
    /// </summary>
    protected virtual Task<HttpRequestMessage> CreateRequestMessage() =>
        throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

    /// <summary>
    /// Returns the ACR OAuth scope required for the current request (e.g. "repository:myrepo:metadata_read").
    /// </summary>
    protected virtual string GetScope() =>
        throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(GetScope)}");

    /// <summary>
    /// Returns the login server FQDN for the current request.
    /// </summary>
    protected virtual string GetLoginServer() =>
        throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(GetLoginServer)}");

    /// <summary>
    /// Internal transport primitive. Acquires an ACR access token for <see cref="GetScope"/>,
    /// then sends the request built by <see cref="CreateRequestMessage"/> through the pipeline.
    /// </summary>
    internal virtual async Task<HttpResponseMessage> SendAsync()
    {
        string loginServer = GetLoginServer();
        string scope = GetScope();

        // Acquire scoped ACR token — orchestrator caches per (loginServer, scope)
        await _authOrchestrator.AcquireAcrAccessTokenAsync(loginServer, scope);

        using HttpRequestMessage request = await CreateRequestMessage();
        HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }

    /// <summary>
    /// Sends a paginated GET, following the Link header. Each page is deserialized and accumulated.
    /// </summary>
    internal async Task<List<string>> SendAsyncPaged(string loginServer, string path, string scope)
    {
        await _authOrchestrator.AcquireAcrAccessTokenAsync(loginServer, scope);

        var pages = new List<string>();
        string? currentPath = path;

        while (currentPath is not null)
        {
            string url = currentPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? currentPath
                : $"https://{loginServer}{currentPath}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            response.EnsureSuccessStatusCode();

            if (response.Content is not null)
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    pages.Add(body);
                }
            }

            currentPath = GetNextLinkPath(response);
        }

        return pages;
    }

    /// <summary>
    /// Creates a child processor initialized with the same runtime context.
    /// </summary>
    protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : AcrProcessorBase, new()
    {
        T child = new T
        {
            HttpPipeline = HttpPipeline,
            Publisher = Publisher,
            AuthHandler = AuthHandler,
            JsonOptions = JsonOptions,
        };
        child.WriteOutput = WriteOutput;
        configure?.Invoke(child);
        return child;
    }

    private static string? GetNextLinkPath(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var linkValues))
            return null;

        string? linkHeader = linkValues.FirstOrDefault();
        if (string.IsNullOrEmpty(linkHeader))
            return null;

        int start = linkHeader.IndexOf('<');
        int end = linkHeader.IndexOf('>');
        if (start < 0 || end < 0 || end <= start)
            return null;

        return linkHeader.Substring(start + 1, end - start - 1);
    }
}

/// <summary>
/// Generic ACR processor base. Default <see cref="ProcessAsync"/> pipeline:
/// 1) Publish start, 2) <see cref="AcrProcessorBase.SendAsync"/>,
/// 3) deserialize into <typeparamref name="TResponse"/>, 4) <see cref="SelectOutput"/>, 5) write output, 6) Publish end.
/// <para>
/// Leaf processors only need to implement <see cref="AcrProcessorBase.CreateRequestMessage"/>,
/// <see cref="AcrProcessorBase.GetScope"/>, and <see cref="AcrProcessorBase.GetLoginServer"/>.
/// </para>
/// </summary>
public class AcrProcessorBase<TResponse> : AcrProcessorBase where TResponse : class
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    protected override string GetLoginServer() => ResolveLoginServer(RegistryName);

    /// <summary>
    /// Projects the deserialized response before writing to output.
    /// Override to unwrap envelopes or select sub-properties.
    /// </summary>
    protected virtual object SelectOutput(TResponse body) => body;

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        using HttpResponseMessage response = await SendAsync();

        if (response.Content is null)
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            return;
        }

        string rawBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            return;
        }

        try
        {
            TResponse? body = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
            if (body is not null)
            {
                await WriteOutput(SelectOutput(body));
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
