// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public abstract class StorageBlobProcessorBase : IProcessor
{
    private static readonly string[] s_storageScopes = ["https://storage.azure.com/.default"];
    protected IRuntimeHttpPipeline HttpPipeline { get; private set; } = null!;
    protected IEventPublisher Publisher { get; private set; } = null!;
    protected OutputStreamDelegate WriteOutput { get; private set; } = null!;
    protected Profile? ResolvedProfile { get; private set; }
    internal bool UseProfileAuthentication { get; set; } = true;

    public string? Profile { get; set; }

    protected virtual HttpMethod Method =>
        throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(Method)}.");

    protected virtual string CreateRequestUri() =>
        throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestUri)}.");

    protected virtual object? CreateRequestBody() => null;

    protected virtual void CreateRequestHeaders(HttpRequestMessage request)
    {
        if (!UseProfileAuthentication)
        {
            request.Options.Set(RuntimeHttpRequestOptions.DisableProfileAuthentication, true);
            request.Options.Set(RuntimeHttpRequestOptions.DisableQueryLogging, true);
        }
        else if (ResolvedProfile is not null)
        {
            request.Options.Set(RuntimeHttpRequestOptions.Scopes, s_storageScopes);
        }
        request.Headers.TryAddWithoutValidation(
            "x-ms-date",
            DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));
    }

    public IProcessor Init(ProcessorContext context)
    {
        HttpPipeline = context.HttpPipeline;
        Publisher = context.Publisher;
        WriteOutput = context.OutputHandler.WriteOutput;
        ResolvedProfile = context.Profile;
        return this;
    }

    public abstract Task ProcessAsync();

    protected T CreateExtendedProcessor<T>(Action<T>? configure = null)
        where T : StorageBlobProcessorBase, new()
    {
        T child = new()
        {
            HttpPipeline = HttpPipeline,
            Publisher = Publisher,
            WriteOutput = WriteOutput,
            ResolvedProfile = ResolvedProfile,
        };
        configure?.Invoke(child);
        return child;
    }

    protected virtual async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        var request = new HttpRequestMessage(
            Method,
            new Uri(
                CreateRequestUri(),
                new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true }));
        request.Content = CreateRequestBody() switch
        {
            null => null,
            Stream stream => new StreamContent(stream),
            HttpContent content => content,
            object body => System.Net.Http.Json.JsonContent.Create(body, body.GetType()),
        };
        CreateRequestHeaders(request);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    internal virtual async Task<HttpResponseMessage> SendAsync()
    {
        using HttpRequestMessage request = await CreateRequestMessage();
        HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && Method == HttpMethod.Get
            && ResolvedProfile is not null
            && request.RequestUri!.Host.StartsWith("md-", StringComparison.OrdinalIgnoreCase)
            && response.Headers.TryGetValues("WWW-Authenticate", out IEnumerable<string>? challenges))
        {
            string? audience = null;
            foreach (string challenge in challenges)
            {
                int start = challenge.IndexOf("resource_id=", StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    continue;
                }
                string candidate = challenge[(start + "resource_id=".Length)..].TrimStart('"');
                int end = candidate.IndexOfAny(['"', ' ', ',']);
                candidate = end < 0 ? candidate : candidate[..end];
                if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
                    && uri.Scheme == Uri.UriSchemeHttps
                    && string.IsNullOrEmpty(uri.UserInfo))
                {
                    audience = uri.AbsoluteUri.TrimEnd('/');
                    break;
                }
            }
            if (audience is not null)
            {
                response.Dispose();
                using HttpRequestMessage retry = await CreateRequestMessage();
                retry.Options.Set(RuntimeHttpRequestOptions.DisableProfileAuthentication, false);
                retry.Options.Set(RuntimeHttpRequestOptions.Scopes, [$"{audience}/.default"]);
                response = await HttpPipeline.SendAsync(retry, Publisher);
                await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            }
        }
        try
        {
            await response.EnsureSuccessAsync(Publisher);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    protected static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : response.Content?.Headers.TryGetValues(name, out values) == true
                ? values.FirstOrDefault()
                : null;
}

public abstract partial class StorageBlobProcessorBase<TRequest, TResponse> : StorageBlobProcessorBase
    where TRequest : class
    where TResponse : class
{
    protected override TRequest? CreateRequestBody() => default;

    partial void OverrideBeforeProcessAsyncBase();

    protected virtual Task BeforeProcessAsync()
    {
        OverrideBeforeProcessAsyncBase();
        return Task.CompletedTask;
    }

    protected virtual Task<bool> BeforeWriteOutputAsyncAndReturn(TResponse? body) =>
        Task.FromResult(false);

    public override async Task ProcessAsync()
    {
        HttpResponseMessage? response = null;
        bool disposeResponse = true;
        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            response = await SendAsync();
            TResponse? body = default;
            if (response.Content is null)
            {
                await Publisher.Publish(
                    Events.ResponseNoContent,
                    $"response has no content for {response.RequestMessage?.RequestUri}");
            }
            else if (typeof(TResponse) == typeof(Stream))
            {
                body = (TResponse)(object)await response.Content.ReadAsStreamAsync(Publisher.Token);
            }
            else
            {
                string rawBody = await response.Content.ReadAsStringAsync(Publisher.Token);
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                    await Publisher.Publish(
                        Events.ResponseEmptyBody,
                        $"response has empty body for {response.RequestMessage?.RequestUri}");
                }
                else
                {
                    try
                    {
                        body = System.Text.Json.JsonSerializer.Deserialize<TResponse>(rawBody);
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        await Publisher.Publish(
                            Events.ResponseInvalidJson,
                            $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                        throw new InvalidOperationException(
                            $"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}");
                    }
                }
            }

            if (!await BeforeWriteOutputAsyncAndReturn(body) && body is not null)
            {
                await WriteOutput(body, Publisher.Token);
                disposeResponse = body is not Stream;
            }
        }
        finally
        {
            if (disposeResponse)
            {
                response?.Dispose();
            }
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
