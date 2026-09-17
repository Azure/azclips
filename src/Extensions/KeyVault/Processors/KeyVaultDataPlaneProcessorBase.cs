using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;

public abstract partial class KeyVaultDataPlaneProcessorBase : IProcessor
{
    public IRuntimeHttpPipeline HttpPipeline = null!;
    public IEventPublisher Publisher = null!;
    public JsonSerializerOptions JsonOptions = null!;
    protected OutputStreamDelegate WriteOutput = null!;
    protected IAuthHandler AuthHandler = null!;
    protected ProfileModel? ResolvedProfile;

    public string? Profile { get; set; }
    public Uri? VaultBaseUrl { get; set; }

    public CancellationToken Token => Publisher.Token;
    public Action Cancel => Publisher.Cancel;

    protected virtual void BeforeInit() { }
    protected virtual void AfterInit() { }

    public IProcessor Init(ProcessorContext context)
    {
        BeforeInit();
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        HttpPipeline = context.HttpPipeline;
        Publisher = context.Publisher;
        WriteOutput = context.OutputHandler.WriteOutput;
        ResolvedProfile = context.Profile;
        AuthHandler = context.AuthHandler;
        AfterInit();
        return this;
    }

    public abstract Task ProcessAsync();

    protected abstract HttpMethod Method { get; }
    protected abstract string CreateRequestUri();
    protected virtual object? CreateRequestBody() => null;
    protected virtual void CreateRequestHeaders(HttpRequestMessage request) { }

    protected virtual async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        HttpRequestMessage request = new(Method, CreateRequestUri());
        if (CreateRequestBody() is { } body)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }
        CreateRequestHeaders(request);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    protected T CreateExtendedProcessor<T>(Action<T>? configure = null)
        where T : KeyVaultDataPlaneProcessorBase, new()
    {
        T child = new()
        {
            HttpPipeline = HttpPipeline,
            Publisher = Publisher,
            JsonOptions = JsonOptions,
            AuthHandler = AuthHandler,
            Profile = Profile,
            VaultBaseUrl = VaultBaseUrl
        };
        child.WriteOutput = WriteOutput;
        child.ResolvedProfile = ResolvedProfile;
        configure?.Invoke(child);
        return child;
    }

    internal virtual Task<HttpResponseMessage> SendAsync() =>
        HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
}

public abstract partial class KeyVaultDataPlaneProcessorBase<TRequest, TResponse> : KeyVaultDataPlaneProcessorBase
    where TRequest : class
    where TResponse : class
{
    protected override TRequest? CreateRequestBody() => default;

    partial void OverrideBeforeProcessBase();

    protected virtual Task BeforeProcessAsync()
    {
        OverrideBeforeProcessBase();
        return Task.CompletedTask;
    }

    protected virtual Task<bool> BeforeWriteOutputAsyncAndReturn(TResponse? body) => Task.FromResult(false);

    public override async Task ProcessAsync()
    {
        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            using HttpResponseMessage response = await SendAsync();
            TResponse? body = default;

            if (response.Content is null)
            {
                await Publisher.Publish(Events.ResponseNoContent, "Response has no content.");
            }
            else
            {
                string rawBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                    await Publisher.Publish(Events.ResponseEmptyBody, "Response has an empty body.");
                }
                else
                {
                    try
                    {
                        body = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
                    }
                    catch (JsonException)
                    {
                        await Publisher.Publish(Events.ResponseInvalidJson, "Response has invalid JSON.");
                        throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}.");
                    }
                }
            }

            if (!await BeforeWriteOutputAsyncAndReturn(body) && body is not null)
            {
                await WriteOutput(body);
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}