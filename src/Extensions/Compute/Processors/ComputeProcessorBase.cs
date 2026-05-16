using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using System.Net;
using Microsoft.Azure.Cirrus.Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any compute processor" stays type-safe.
    /// All real processors should derive from <see cref="ComputeProcessorBase{TResponse}"/>.
    /// </summary>
    /// <remarks>
    /// TODO(Yabo): no rollback / compensation story yet. If an orchestrator's first child
    /// succeeds and a later child fails, earlier resources are orphaned. Document as a non-goal
    /// for now; revisit with an OnFailureAsync hook or a saga-style step list when needed.
    /// </remarks>
    public abstract class ComputeProcessorBase : IProcessor
    {
        public IRuntimeHttpPipeline HttpPipeline = null!;
        public IEventListener EventListener = null!;
        public JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected AuthContext AuthContext = null!;
        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            // TODO(Yabo): temporarily initialize arbitrary json options here, get from context in the future
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            HttpPipeline = context.HttpPipeline;
            EventListener = context.EventListener;
            WriteOutput = context.OutputHandler.WriteOutput;
            AuthContext = context.AuthHandler.AuthContext;

            AfterInit();
            return this;
        }
        public virtual Task ProcessAsync() => Task.CompletedTask;
        public virtual Task<HttpRequestMessage> CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, EventListener, JsonOptions, WriteOutput, AuthContext)
        /// as this processor. Use the optional <paramref name="configure"/> action to set the
        /// extended processor's own properties.
        /// </summary>
        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : ComputeProcessorBase, new()
        {
            T child = new T
            {
                HttpPipeline = HttpPipeline,
                EventListener = EventListener,
                JsonOptions = JsonOptions,
            };
            child.WriteOutput = WriteOutput;
            child.AuthContext = AuthContext;
            configure?.Invoke(child);
            return child;
        }

        /* 
            For LRO API has
                "x-ms-long-running-operation-options": {
                "final-state-via": "location/azure-async-operation"
                }
            "finaluri" is response.headers["Location"]
            "operationLocationUri" is response.headers["Operation-Location"]
            otherwise both null
        */
        protected virtual Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetOperationLocationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetAsyncOperationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetLocationUri => _ => null;
        

        public async virtual Task<HttpResponseMessage> SendAsync()
        {
            IRuntimeHttpPipeline _httpPipeline =  HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
            IEventListener _eventListener = EventListener ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(EventListener)}.");
            using HttpRequestMessage _request = await CreateRequestMessage();
            HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _eventListener);
            await _eventListener.Signal(Events.TracingHttpResponseMessage, _response);
            // TODO(Yabo): handle status codess
            return _response;
        }

        public async virtual Task<HttpResponseMessage> SendAsyncLRO()
        {
            HttpRequestMessage _request = await CreateRequestMessage();
            try
            {
                IRuntimeHttpPipeline _httpPipeline =  HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
                IEventListener _eventListener = EventListener ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(EventListener)}.");
                string _originalUri = _request.RequestUri!.AbsoluteUri;
                HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _eventListener);
                await _eventListener.Signal(Events.TracingHttpResponseMessage, _response);
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
                    await _eventListener.Signal(Events.DelayPolling, $"Delaying {delay} seconds before polling.");
                    Task delayTask = Task.Delay(delay*1000, _eventListener.Token);

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
                    await _eventListener.Signal(Events.BeforePollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                    // make the polling call
                    _response = await _httpPipeline.SendAsync(_request, _eventListener);
                    await _eventListener.Signal(Events.TracingHttpResponseMessage, _response);
                    await _eventListener.Signal(Events.AfterPollingCall);

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
                        await _eventListener.Signal(Events.BeforeFinalPollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                        // make the final call
                        _response = await _httpPipeline.SendAsync(_request, _eventListener);
                        await EventListener.Signal(Events.TracingHttpResponseMessage, _response);
                        await _eventListener.Signal(Events.AfterFinalPollingCall);
                        break;
                    }
                }
                switch (_response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        {
                            await _eventListener.Signal(Events.PollingFinished);
                            break;
                        }
                    default:
                        {
                            await _eventListener.Signal(Events.PollingFailed);
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
    }

    /// <summary>
    /// Generic compute processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) signals start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) signals end.
    /// <para>
    /// <see cref="SendAsync"/> is the universal "give me the final response" primitive.
    /// Atomic LRO processors override it as a one-liner that delegates to <see cref="ComputeProcessorBase.SendAsyncLRO"/>.
    /// Atomic non-LRO processors don't override at all (the inherited <see cref="SendAsync"/>
    /// resolves <see cref="SubscriptionId"/> then dispatches a single HTTP request).
    /// Orchestrators override <see cref="SendAsync"/> to compose children.
    /// </para>
    /// <para>
    /// <see cref="SubscriptionId"/> is resolved lazily inside <see cref="SendAsync"/> /
    /// <see cref="SendAsyncLRO"/>, so callers that bypass <see cref="ProcessAsync"/> still get a
    /// populated value via <see cref="AuthContext"/> fallback. Orchestrators don't need to copy
    /// <see cref="SubscriptionId"/> into children since each child resolves from the shared
    /// <see cref="AuthContext"/>.
    /// </para>
    /// </summary>
    public class ComputeProcessorBase<TResponse> : ComputeProcessorBase where TResponse : class
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// Resolves <see cref="SubscriptionId"/> from the property if set, otherwise from <see cref="AuthContext"/>.
        /// Throws if neither has a value. Idempotent. Called automatically before any HTTP transport;
        /// custom <see cref="SendAsync"/>/<see cref="SendAsyncLRO"/> overrides that don't call <c>base</c>
        /// are responsible for invoking this themselves if they build URIs from <see cref="SubscriptionId"/>.
        /// </summary>
        /// 
        /// TODO(Yabo): update processor context to accept tenantId and subscriptionId instead of auth context
        protected void ResolveSubscriptionId() =>
            SubscriptionId = !string.IsNullOrEmpty(SubscriptionId)
                ? SubscriptionId
                : AuthContext.SubscriptionId
                  ?? throw new InvalidOperationException("SubscriptionId must be provided either via property or auth context.");

        public override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return base.SendAsync();
        }

        public override Task<HttpResponseMessage> SendAsyncLRO()
        {
            ResolveSubscriptionId();
            return base.SendAsyncLRO();
        }

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            using HttpResponseMessage response = await SendAsync();

            if (response.Content is null)
            {
                await EventListener.Signal(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
                return;
            }

            string rawBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                await EventListener.Signal(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
                return;
            }

            try
            {
                TResponse? body = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
                if (body is not null)
                {
                    await WriteOutput(body);
                }
            }
            catch (JsonException)
            {
                await EventListener.Signal(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}");
            }
            finally
            {
                await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}