using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;
using Microsoft.Azure.Cirrus.ExtensionRuntime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any resources processor" stays type-safe.
    /// All real processors should derive from <see cref="ResourcesProcessorBase{TResponse}"/>.
    /// </summary>
    public abstract class ResourcesProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected AuthContext AuthContext = null!;
        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            // TODO: temporarily initialize arbitrary json options here, get from context in the future
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;
            AuthContext = context.AuthHandler.AuthContext;

            AfterInit();
            return this;
        }
        public virtual Task ProcessAsync() => Task.CompletedTask;
        protected virtual Task<HttpRequestMessage> CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, AuthContext)
        /// as this processor. Use the optional <paramref name="configure"/> action to set the
        /// extended processor's own properties.
        /// </summary>
        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : ResourcesProcessorBase, new()
        {
            T child = new T
            {
                HttpPipeline = HttpPipeline,
                Publisher = Publisher,
                JsonOptions = JsonOptions,
            };
            child.WriteOutput = WriteOutput;
            child.AuthContext = AuthContext;
            configure?.Invoke(child);
            return child;
        }

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            // Validate runtime context before CreateRequestMessage(), since derived implementations
            // may publish events (using Publisher) while building the request.
            EnsureRuntimeContext();
            using HttpRequestMessage _request = await CreateRequestMessage();
            HttpResponseMessage _response = await HttpPipeline.SendAsync(_request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, _response);
            // Surface non-2xx ARM responses (e.g. 404 on a missing resource group) as a thrown
            // HttpRequestException after publishing ResourceOperationFailed, instead of deserializing
            // an error body into a default response. EnsureSuccessAsync only reads the body on failure,
            // so the success-path content stream stays intact for the deserialize in ProcessAsync.
            try
            {
                await _response.EnsureSuccessAsync(Publisher);
            }
            catch
            {
                _response.Dispose();
                throw;
            }
            return _response;
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

        /// <summary>
        /// Whether, after polling reaches a terminal status, the poller issues a final GET on
        /// <c>_finalUri</c> (the original request URI by default) to fetch the terminal resource
        /// state. True for resources whose canonical state lives at the original URI (e.g. a PUT
        /// create). Override to false for operations whose poll already returns the full result and
        /// whose original URI is not GET-able (e.g. the POST-only what-if endpoint).
        /// </summary>
        protected virtual bool FetchFinalState => true;

        /// <summary>
        /// Long-running-operation transport: issues the request, then polls the ARM async-operation /
        /// location / operation-location URIs until the resource reaches a terminal provisioningState
        /// (Succeeded/Canceled) or the operation fails. Mirrors the proven Compute poller. Atomic LRO
        /// processors override <see cref="SendAsync"/> to delegate here.
        /// </summary>
        internal async virtual Task<HttpResponseMessage> SendAsyncLRO()
        {
            // Validate runtime context before CreateRequestMessage(), since derived implementations
            // may publish events (using Publisher) while building the request.
            EnsureRuntimeContext();
            IRuntimeHttpPipeline _httpPipeline = HttpPipeline;
            IEventPublisher _publisher = Publisher;
            HttpRequestMessage _request = await CreateRequestMessage();
            try
            {
                string _originalUri = _request.RequestUri!.AbsoluteUri;
                HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _publisher);
                await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
                // declared final-state-via: location
                var _declaredFinalUri = GetFinalUri(_response);
                var _finalUri = string.IsNullOrEmpty(_declaredFinalUri) ? _originalUri : _declaredFinalUri;
                var _asyncOperationUri = GetAsyncOperationUri(_response);
                var _locationUri = GetLocationUri(_response);
                var _operationLocationUri = GetOperationLocationUri(_response);
                // Carries the ARM error detail captured during the status peek (while the body is
                // still available) so the terminal-failure throw site below can surface it. Must be
                // declared outside the loop to be in scope at both the peek and the throw.
                string? failureDetail = null;

                //polling
                while ((_request.Method == HttpMethod.Put && _response.StatusCode == HttpStatusCode.OK)
                    || _response.StatusCode == HttpStatusCode.Created
                    || _response.StatusCode == HttpStatusCode.Accepted)
                {
                    // TODO: get default value from config service: engine get default from configservice and pass to processor via ProcessorContext
                    // Floor the delay at 1 second: a fractional Retry-After (<1s) truncates to 0, which
                    // would otherwise busy-poll ARM with no wait between calls.
                    int delay = Math.Max(1, (int)(_response.Headers.RetryAfter?.Delta?.TotalSeconds ?? _httpPipeline.PollingInterval));
                    await _publisher.Publish(Events.DelayPolling, $"Delaying {delay} seconds before polling.");
                    Task delayTask = Task.Delay(delay * 1000, _publisher.Token);

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
                            // Read the body once so it can be both parsed for status and reused for
                            // error-detail extraction without re-reading the (already buffered) stream.
                            string bodyText = await _response.Content.ReadAsStringAsync();
                            if (JsonNode.Parse(bodyText) is JsonObject json)
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
                                        // Capture the ARM error detail now, while the body is in hand.
                                        // ARM deployment errors live at properties.error; some payloads
                                        // use a top-level error. Fall back to the (truncated) raw body.
                                        try
                                        {
                                            var errorObj = json.Property("properties")?.Property("error") ?? json.Property("error");
                                            var code = errorObj?.PropertyT<JsonString>("code")?.ToString();
                                            var detailMessage = errorObj?.PropertyT<JsonString>("message")?.ToString();
                                            if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(detailMessage))
                                            {
                                                failureDetail = $"{code}: {detailMessage}";
                                            }
                                            else if (!string.IsNullOrWhiteSpace(bodyText))
                                            {
                                                failureDetail = bodyText.Length > 2000 ? bodyText.Substring(0, 2000) : bodyText;
                                            }
                                        }
                                        catch
                                        {
                                            // Best-effort: never let extraction change control flow.
                                            failureDetail = string.IsNullOrWhiteSpace(bodyText)
                                                ? failureDetail
                                                : (bodyText.Length > 2000 ? bodyText.Substring(0, 2000) : bodyText);
                                        }
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
                            //TODO: redesign exceptions for extension runtime
                            // Capture the message before disposing the response, then dispose the
                            // current response (handle/socket) exactly once on this throw path.
                            // The method exits here, so the surrounding finally only disposes _request.
                            // failureDetail was captured during the peek (above), while the body was
                            // still available; the response is only disposed here, never re-read.
                            string message = string.IsNullOrWhiteSpace(failureDetail)
                                ? $"Long running operation failed (terminal provisioningState 'Failed') for {_request?.RequestUri}."
                                : $"Long running operation failed (terminal provisioningState 'Failed') for {_request?.RequestUri}. {failureDetail}";
                            _response.Dispose();
                            throw new InvalidOperationException(message);
                        }
                    }

                    // check for terminal status code
                    if (_response.StatusCode == HttpStatusCode.Created || _response.StatusCode == HttpStatusCode.Accepted)
                    {
                        continue;
                    }
                    // we are done polling, do a request on final target?
                    // create a new request with the final uri
                    if (FetchFinalState && !string.IsNullOrEmpty(_finalUri))
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
        /// Verifies the processor was initialized with the runtime context required to issue requests
        /// and publish events. Throws a deterministic <see cref="InvalidOperationException"/> (rather
        /// than letting a later access throw <see cref="NullReferenceException"/>) when it was not.
        /// </summary>
        private protected void EnsureRuntimeContext()
        {
            if (HttpPipeline is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
            }
            if (Publisher is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");
            }
            if (WriteOutput is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(WriteOutput)}.");
            }
            if (JsonOptions is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(JsonOptions)}.");
            }
        }
    }

    /// <summary>
    /// Generic resources processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) Publishes start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) Publishes end.
    /// <para>
    /// <see cref="SendAsync"/> is the internal "give me the final response" transport primitive.
    /// Atomic processors don't override it (the inherited <see cref="SendAsync"/>
    /// resolves <see cref="SubscriptionId"/> then dispatches a single HTTP request).
    /// </para>
    /// </summary>
    public abstract class ResourcesProcessorBase<TResponse> : ResourcesProcessorBase where TResponse : class
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// Resolves <see cref="SubscriptionId"/> from the property if set, otherwise from <see cref="AuthContext"/>.
        /// Throws if neither has a value. Idempotent. Called automatically before any HTTP transport.
        /// </summary>
        /// TODO: update processor context to accept tenantId and subscriptionId instead of auth context
        protected void ResolveSubscriptionId()
        {
            if (!string.IsNullOrWhiteSpace(SubscriptionId))
            {
                return;
            }

            if (AuthContext is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(AuthContext)}, so SubscriptionId cannot be resolved. Provide it via the property instead.");
            }

            if (string.IsNullOrWhiteSpace(AuthContext.SubscriptionId))
            {
                throw new InvalidOperationException("SubscriptionId must be provided either via property or auth context.");
            }

            SubscriptionId = AuthContext.SubscriptionId;
        }

        internal override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return base.SendAsync();
        }

        public override async Task ProcessAsync()
        {
            EnsureRuntimeContext();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                using HttpResponseMessage response = await SendAsync();

                if (response.Content is null)
                {
                    await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                    return;
                }

                string rawBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                    await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
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
                catch (JsonException ex)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}", ex);
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}