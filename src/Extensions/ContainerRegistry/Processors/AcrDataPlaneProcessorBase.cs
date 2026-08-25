// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Non-generic base for ACR data-plane processors. Mirrors the structure of
/// <c>ComputeProcessorBase</c>:
/// <list type="bullet">
///   <item><see cref="CreateRequestMessage"/> — builds the outgoing HTTP request.</item>
///   <item><see cref="SendAsync"/> — sends the request through the pipeline.</item>
/// </list>
/// </summary>
public abstract partial class AcrDataPlaneProcessorBase : IProcessor
{
    public IRuntimeHttpPipeline HttpPipeline = null!;
    public IEventPublisher Publisher = null!;
    public IAuthHandler AuthHandler = null!;
    public JsonSerializerOptions JsonOptions = null!;
    protected OutputStreamDelegate WriteOutput = null!;

    public string Endpoint { get; set; } = string.Empty;

    protected virtual void BeforeInit() { }
    protected virtual void AfterInit() { }

    public CancellationToken Token => CancellationToken.None;
    public Action Cancel => throw new NotImplementedException();

    public IProcessor Init(ProcessorContext context)
    {
        BeforeInit();

        /*
        Yabo:  
            Dataplane should not reuse default http pipeline, 
            because it will inject ARM token to the request, 
            which dataplane endpoint not supposed to receive.
        */
        HttpPipeline = context.HttpPipeline;
        Publisher = context.Publisher;
        AuthHandler = context.AuthHandler;
        WriteOutput = context.OutputHandler.WriteOutput;
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        AfterInit();
        return this;
    }

    public virtual Task ProcessAsync() => Task.CompletedTask;

    /// <summary>
    /// Processors override to build the outgoing <see cref="HttpRequestMessage"/>.
    /// </summary>
    protected virtual Task<HttpRequestMessage> CreateRequestMessage() =>
        throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

    protected virtual Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected virtual Func<HttpResponseMessage, string?> GetOperationLocationUri => _ => null;
    protected virtual Func<HttpResponseMessage, string?> GetAsyncOperationUri => _ => null;
    protected virtual Func<HttpResponseMessage, string?> GetLocationUri => _ => null;

    /// <summary>
    /// Sends the request through the pipeline with authentication.
    /// Auth must be configured on <see cref="IAuthHandler"/> before calling.
    /// </summary>
     internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            IRuntimeHttpPipeline _httpPipeline =  HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
            IEventPublisher _publisher = Publisher ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");

            using HttpRequestMessage _request = await CreateRequestMessage();
            HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _publisher);
            await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
            try
            {
                await _response.EnsureSuccessAsync(_publisher);
            }
            catch
            {
                _response.Dispose();
                throw;
            }
            return _response;
        }

        internal async virtual Task<HttpResponseMessage> SendAsyncLRO()
        {
            HttpRequestMessage _request = await CreateRequestMessage();
            try
            {
                IRuntimeHttpPipeline _httpPipeline =  HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
                IEventPublisher _publisher = Publisher ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");
                string _originalUri = _request.RequestUri!.AbsoluteUri;
                HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _publisher);
                await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
                // declared final-state-via: location
                var _finalUri = string.IsNullOrEmpty(GetFinalUri(_response)) ? _originalUri : GetFinalUri(_response);
                var _asyncOperationUri = GetAsyncOperationUri(_response);
                var _locationUri = GetLocationUri(_response);
                var _operationLocationUri = GetOperationLocationUri(_response);

                //polling
                while (_request.Method == HttpMethod.Put && _response.StatusCode == HttpStatusCode.OK || _response.StatusCode == HttpStatusCode.Created || _response.StatusCode == HttpStatusCode.Accepted)
                {
                    // TODO(Yabo): get default value from config service: engine get default from configservice and pass to processor via ProcessorContext
                    int delay = (int)(_response.Headers.RetryAfter?.Delta?.TotalSeconds ?? _httpPipeline.PollingInterval);
                    await _publisher.Publish(Events.DelayPolling, $"Delaying {delay} seconds before polling.");
                    Task delayTask = Task.Delay(delay*1000, _publisher.Token);

                    // while we wait, let's grab the headers and get ready to poll.
                    if (!string.IsNullOrEmpty(GetAsyncOperationUri(_response)))
                    {
                        _asyncOperationUri = GetAsyncOperationUri(_response);
                    }
                    if (!string.IsNullOrEmpty(GetLocationUri(_response)))
                    {
                        _locationUri = GetLocationUri(_response);
                    }
                    if (!string.IsNullOrEmpty(GetOperationLocationUri(_response)))
                    {
                        _operationLocationUri = GetOperationLocationUri(_response);
                    }
                    var _uri = string.IsNullOrEmpty(_asyncOperationUri) ? string.IsNullOrEmpty(_locationUri) ? string.IsNullOrEmpty(_operationLocationUri) ? _originalUri : _operationLocationUri : _locationUri : _asyncOperationUri;

                    // drop the old response
                    _response?.Dispose();

                    await delayTask;

                    _request = _request.CloneAndDispose(new Uri(_uri), HttpMethod.Get);
                    await _publisher.Publish(Events.BeforePollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                    // make the polling call
                    _response = await _httpPipeline.SendAsync(_request, _publisher);
                    await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
                    await _publisher.Publish(Events.AfterPollingCall);

                    // if we got back an OK, take a peek inside and see if it's done
                    if (_response.StatusCode == global::System.Net.HttpStatusCode.OK)
                    {
                        var error = false;
                        try
                        {
                            if (JsonNode.Parse(await _response.Content.ReadAsStringAsync()) is JsonObject json)
                            {
                                var state = json.Property("properties")?.PropertyT<JsonString>("provisioningState") ?? json.PropertyT<JsonString>("status");
                                if (state is null)
                                {
                                    // the body doesn't contain any information that has the state of the LRO
                                    // we're going to just get out, and let the consumer have the result
                                    break;
                                }

                                switch (state?.ToString()?.ToLower())
                                {
                                    case "failed":
                                        error = true;
                                        break;
                                    case "succeeded":
                                    case "canceled":
                                        // we're done polling.
                                        break;

                                    default:
                                        // need to keep polling!
                                        _response.StatusCode = HttpStatusCode.Created;
                                        continue;
                                }
                            }
                        }
                        catch
                        {
                            // if we run into a problem peeking into the result,
                            // we really don't want to do anything special.
                        }
                        if (error)
                        {
                            //TODO(Yabo): redesign exceptions for extension runtime
                            throw new Exception($"Long running operation failed with response: {_response}");
                        }
                    }

                    // check for terminal status code
                    if (_response.StatusCode == HttpStatusCode.Created || _response.StatusCode == HttpStatusCode.Accepted)
                    {
                        continue;
                    }
                    // we are done polling, do a request on final target?
                    // create a new request with the final uri
                    if (!string.IsNullOrEmpty(_finalUri))
                    {
                        // drop the old response
                        _response?.Dispose();
                        _request = _request.CloneAndDispose(new Uri(_finalUri), HttpMethod.Get);
                        await _publisher.Publish(Events.BeforeFinalPollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                        // make the final call
                        _response = await _httpPipeline.SendAsync(_request, _publisher);
                        await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
                        await _publisher.Publish(Events.AfterFinalPollingCall);
                        break;
                    }
                }
                switch (_response.StatusCode)
                {
                    case HttpStatusCode.OK:
                    case HttpStatusCode.NoContent:
                    {
                        await _publisher.Publish(Events.PollingFinished);
                        break;
                    }
                    default:
                    {
                        await _publisher.Publish(Events.PollingFailed);
                        try
                        {
                            await _response.EnsureSuccessAsync(_publisher);
                        }
                        catch
                        {
                            _response.Dispose();
                            throw;
                        }
                        break;
                    }
                }
                return _response;
            }
            finally
            {
                _request?.Dispose();
            }
        }

    /// <summary>
    /// Creates a child processor initialized with the same runtime context.
    /// </summary>
    protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : AcrDataPlaneProcessorBase, new()
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

    /// <summary>
    /// Sends paginated GET requests to an ACR data-plane endpoint, collecting all pages.
    /// Auth must be configured on <see cref="IAuthHandler"/> before calling.
    /// </summary>
    protected async Task<List<string>> SendAsyncPaged(string loginServer, string path)
    {
        IRuntimeHttpPipeline pipeline = HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
        IEventPublisher publisher = Publisher ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");

        var pages = new List<string>();
        string? nextLink = $"https://{loginServer}{path}";

        while (!string.IsNullOrEmpty(nextLink))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
            using var response = await pipeline.SendAsync(request, publisher);
            await response.EnsureSuccessAsync(publisher);

            string body = await response.Content.ReadAsStringAsync();
            pages.Add(body);

            // Follow Link header for pagination
            nextLink = null;
            if (response.Headers.TryGetValues("Link", out var linkValues))
            {
                foreach (string link in linkValues)
                {
                    // Parse Link header: </acr/v1/_catalog?last=x&n=100>; rel="next"
                    int start = link.IndexOf('<');
                    int end = link.IndexOf('>');
                    if (start >= 0 && end > start)
                    {
                        string relative = link[(start + 1)..end];
                        nextLink = relative.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? relative
                            : $"https://{loginServer}{relative}";
                    }
                }
            }
        }

        return pages;
    }
}

/// <summary>
/// Generic ACR processor base. Default <see cref="ProcessAsync"/> pipeline:
/// 1) Publish start, 2) <see cref="AcrDataPlaneProcessorBase.SendAsync"/>,
/// 3) deserialize into <typeparamref name="TResponse"/>, 4) <see cref="SelectOutput"/>, 5) write output, 6) Publish end.
/// <para>
/// Auth must be configured on <see cref="IAuthHandler"/> before <see cref="ProcessAsync"/> runs.
/// Leaf processors implement <see cref="AcrDataPlaneProcessorBase.CreateRequestMessage"/>.
/// </para>
/// </summary>
public partial class AcrDataPlaneProcessorBase<TResponse> : AcrDataPlaneProcessorBase where TResponse : class
{
    /// <summary>
    /// Projects the deserialized response before writing to output.
    /// Override to unwrap envelopes or select sub-properties.
    /// </summary>
    protected virtual object SelectOutput(TResponse body) => body;
    partial void AuthOverride();
    private partial Task AuthOverrideAsync();

    public override async Task ProcessAsync()
    {
        AuthOverride();
        await AuthOverrideAsync();

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
