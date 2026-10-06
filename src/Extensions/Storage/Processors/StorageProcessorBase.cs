using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public abstract partial class StorageProcessorBase : IProcessor
{
    public IRuntimeHttpPipeline HttpPipeline = null!;
    public IEventPublisher Publisher = null!;
    public JsonSerializerOptions JsonOptions = null!;
    protected OutputStreamDelegate WriteOutput = null!;
    protected IAuthHandler AuthHandler = null!;
    protected ProfileModel? ResolvedProfile = null!;

    public string? Profile { get; set; }

    protected virtual void BeforeInit() { }
    protected virtual void AfterInit() { }

    public CancellationToken Token => CancellationToken.None;

    public Action Cancel => throw new NotImplementedException();

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

    public virtual Task ProcessAsync() => Task.CompletedTask;

    protected virtual HttpMethod Method => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(Method)}.");

    protected virtual string CreateRequestUri() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestUri)}.");

    protected virtual object? CreateRequestBody() => null;

    protected virtual void CreateRequestHeaders(HttpRequestMessage request) { }

    protected virtual async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        HttpRequestMessage request = new HttpRequestMessage(Method, CreateRequestUri());
        if (CreateRequestBody() is { } body)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }
        CreateRequestHeaders(request);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : StorageProcessorBase, new()
    {
        T child = new T
        {
            HttpPipeline = HttpPipeline,
            Publisher = Publisher,
            JsonOptions = JsonOptions,
            AuthHandler = AuthHandler,
        };
        child.WriteOutput = WriteOutput;
        child.ResolvedProfile = ResolvedProfile;
        configure?.Invoke(child);
        return child;
    }

    protected virtual Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected virtual Func<HttpResponseMessage, string?> GetOperationLocationUri => _ => null;
    protected virtual Func<HttpResponseMessage, string?> GetAsyncOperationUri => _ => null;
    protected virtual Func<HttpResponseMessage, string?> GetLocationUri => _ => null;

    internal async virtual Task<HttpResponseMessage> SendAsync()
    {
        return await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
    }

    internal async virtual Task<HttpResponseMessage> SendAsyncLRO()
    {
        return await HttpRuntime.SendAsyncLRO(CreateRequestMessage, Publisher, HttpPipeline, GetFinalUri, GetOperationLocationUri, GetAsyncOperationUri, GetLocationUri);
    }
}

public partial class StorageProcessorBase<TRequest, TResponse> : StorageProcessorBase
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
            string? rawBody = null;
            TResponse? body = default;

            if (response.Content is null) await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
            else if (string.IsNullOrWhiteSpace(rawBody = await response.Content.ReadAsStringAsync())) await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
            else
            {
                try
                {
                    body = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
                }
                catch (JsonException)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}");
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