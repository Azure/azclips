// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any storage processor" stays type-safe.
    /// All real processors should derive from <see cref="StorageProcessorBase{TRequest, TResponse}"/>.
    /// </summary>
    public abstract partial class StorageProcessorBase : IProcessor
    {
        public IRuntimeHttpPipeline HttpPipeline = null!;
        public IEventPublisher Publisher = null!;
        public JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected IAuthHandler AuthHandler = null!;

        /// <summary>The resolved connection profile from the processor context; null when none is available.</summary>
        protected ProfileModel? ResolvedProfile = null!;

        /// <summary>
        /// Bound <c>--profile</c>/<c>-Profile</c> parameter. The Engine resolves the named profile into
        /// <see cref="ResolvedProfile"/> before the processor runs, so this property exists only to accept
        /// the value during argument binding (per-processor workaround until a global-parameter framework exists).
        /// </summary>
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
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Storage options are tri-state: several service defaults are true
                // (supportsHttpsTrafficOnly, allowBlobPublicAccess), so an unset option must be
                // omitted from the payload rather than sent as the CLR default.
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

        /// <summary>HTTP method for the request. Every processor must override this (GET/PUT/DELETE/etc.).</summary>
        protected virtual HttpMethod Method => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(Method)}.");

        /// <summary>Builds the request URI. Override this seam alone for most read/delete operations.</summary>
        protected virtual string CreateRequestUri() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestUri)}.");

        /// <summary>
        /// Builds the request body, serialized with <see cref="JsonOptions"/> using its runtime type.
        /// Returns null (no content) by default; override for create/update operations.
        /// </summary>
        protected virtual object? CreateRequestBody() => null;

        /// <summary>
        /// Adds request headers beyond what the pipeline supplies.
        /// No-op by default; override to mutate <paramref name="request"/> for header-carrying operations.
        /// </summary>
        protected virtual void CreateRequestHeaders(HttpRequestMessage request) { }

        /// <summary>
        /// Assembles the request from <see cref="Method"/>, <see cref="CreateRequestUri"/>, <see cref="CreateRequestBody"/>
        /// and <see cref="CreateRequestHeaders"/>, and publishes the before/after create events. Override the smaller
        /// seams above so this event wiring stays centralized; override this method only when a request needs fully custom construction.
        /// </summary>
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

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, ResolvedProfile, AuthHandler)
        /// as this processor. Use the optional <paramref name="configure"/> action to set the
        /// extended processor's own properties.
        /// </summary>
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

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            return await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
        }

        internal async virtual Task<HttpResponseMessage> SendAsyncLRO()
        {
            return await HttpRuntime.SendAsyncLRO(CreateRequestMessage, Publisher, HttpPipeline, GetFinalUri, GetOperationLocationUri, GetAsyncOperationUri, GetLocationUri);
        }
    }

    /// <summary>
    /// Generic storage processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) publishes start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) publishes end.
    /// </summary>
    public partial class StorageProcessorBase<TRequest, TResponse> : StorageProcessorBase
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Typed request-body seam. Returns null (no body) by default; write processors override to return
        /// their <typeparamref name="TRequest"/> model. Covariantly overrides the non-generic
        /// <see cref="StorageProcessorBase.CreateRequestBody"/> that the request template consumes.
        /// </summary>
        protected override TRequest? CreateRequestBody() => default;

        // TODO: temporary local override. The shared HttpRuntime.SendAsyncLRO owns polling for
        // every extension, but it still throws a bare Exception carrying only the response object
        // and leaks the response, so a failed create reports "HttpResponseMessage, StatusCode: 200"
        // instead of the ARM error. This copy adds the failure-detail capture and the Retry-After
        // floor. Delete it and fall back to base once those two fixes land in HttpRuntime.
        internal override async Task<HttpResponseMessage> SendAsyncLRO()
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
                // Carries the ARM error detail captured during the status peek (while the body is
                // still available) so the terminal-failure throw site below can surface it. Must be
                // declared outside the loop to be in scope at both the peek and the throw.
                string? failureDetail = null;

                //polling
                // Parenthesised for clarity; the semantics match ComputeProcessorBase. A 200 on the
                // initial PUT still needs polling because ARM reports the operation as accepted,
                // whereas a 200 on a subsequent poll is terminal and ends the loop.
                while ((_request.Method == HttpMethod.Put && _response.StatusCode == HttpStatusCode.OK)
                    || _response.StatusCode == HttpStatusCode.Created
                    || _response.StatusCode == HttpStatusCode.Accepted)
                {
                    // TODO(Yabo): get default value from config service: engine get default from configservice and pass to processor via ProcessorContext
                    // Floor the delay at 1 second: a fractional Retry-After (<1s) truncates to 0, which
                    // would otherwise busy-poll ARM with no wait between calls.
                    int delay = Math.Max(1, (int)(_response.Headers.RetryAfter?.Delta?.TotalSeconds ?? _httpPipeline.PollingInterval));
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
                                        // Capture the ARM error while the body is still readable; the
                                        // response is disposed at the throw site below.
                                        var errorObj = json.Property("error") as JsonObject
                                            ?? json.Property("properties")?.Property("error") as JsonObject;
                                        var code = errorObj?.PropertyT<JsonString>("code")?.ToString();
                                        var detailMessage = errorObj?.PropertyT<JsonString>("message")?.ToString();
                                        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(detailMessage))
                                        {
                                            failureDetail = $"{code}: {detailMessage}";
                                        }
                                        else if (!string.IsNullOrWhiteSpace(code))
                                        {
                                            failureDetail = code;
                                        }
                                        else if (!string.IsNullOrWhiteSpace(detailMessage))
                                        {
                                            failureDetail = detailMessage;
                                        }
                                        else if (!string.IsNullOrWhiteSpace(bodyText))
                                        {
                                            failureDetail = bodyText.Length > 2000 ? bodyText[..2000] : bodyText;
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
                            // Capture the message before disposing the response, then dispose the
                            // current response (handle/socket) exactly once on this throw path.
                            // The method exits here, so the surrounding finally only disposes _request.
                            // Name the resource, not the poll URL: after the first poll _request
                            // targets the signed async-operation URL, whose c/s/h query values must
                            // not reach error output or logs.
                            string message = string.IsNullOrWhiteSpace(failureDetail)
                                ? $"Long running operation failed (terminal provisioningState 'Failed') for {_originalUri}."
                                : $"Long running operation failed (terminal provisioningState 'Failed') for {_originalUri}. {failureDetail}";
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
                    catch (JsonException ex)
                    {
                        await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                        throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}", ex);
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
}
