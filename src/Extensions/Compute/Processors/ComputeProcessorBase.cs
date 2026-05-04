using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using System.Net;
using Extensions.Runtime;
using Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors
{
    public class ComputeProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventListener EventListener = null!;
        protected JsonSerializerOptions _jsonOptions = null!;
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
            _jsonOptions = new JsonSerializerOptions
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
        public virtual Task<HttpRequestMessage> CreateRequestMessage(string? subscriptionId = null, IEventListener? eventListener = null, JsonSerializerOptions? jsonOptions = null) => throw new NotImplementedException();

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
        

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage? request = null, IRuntimeHttpPipeline? httpPipeline = null, IEventListener? eventListener = null)
        {
            IRuntimeHttpPipeline _httpPipeline = httpPipeline ?? HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with HttpPipeline.");
            IEventListener _eventListener = eventListener ?? EventListener;

            using HttpRequestMessage _request = request ?? await CreateRequestMessage();
            return await _httpPipeline.SendAsync(_request, _eventListener);
        }

        public async Task<HttpResponseMessage> SendAsyncLRO(HttpRequestMessage? request = null, IRuntimeHttpPipeline? httpPipeline = null, IEventListener? eventListener = null)
        {
            HttpRequestMessage _request = request ?? await CreateRequestMessage();
            try
            {
                IRuntimeHttpPipeline _httpPipeline = httpPipeline ?? HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with HttpPipeline.");
                IEventListener _eventListener = eventListener ?? EventListener;
                string _originalUri = _request.RequestUri!.AbsoluteUri;
                HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _eventListener);
                await EventListener.Signal(Events.TracingHttpResponseMessage, _response);
                // declared final-state-via: location
                var _finalUri = GetFinalUri(_response) ?? _originalUri;
                var _asyncOperationUri = GetAsyncOperationUri(_response);
                var _locationUri = GetLocationUri(_response);
                var _operationLocationUri = GetOperationLocationUri(_response);

                //polling
                while (_request.Method == HttpMethod.Put && _response.StatusCode == HttpStatusCode.OK || _response.StatusCode == HttpStatusCode.Created || _response.StatusCode == HttpStatusCode.Accepted)
                {
                    // TODO(Yabo): get default value from config service: engine get default from configservice and pass to processor via ProcessorContext
                    int delay = (int)(_response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 30);
                    await _eventListener.Signal(Events.DelayPolling, $"Delaying {delay} seconds before polling.");
                    // start the delay timer (we'll await later...)
                    Task delayTask = Task.Delay(delay * 1000, _eventListener.Token);

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
                    await _eventListener.Signal(Events.BeforePollingCall, $"Polling status: _finalUri: {_finalUri}, _asyncOperationUri: {_asyncOperationUri}, _locationUri: {_locationUri}, _operationLocationUri: {_operationLocationUri}");
                    // make the polling call
                    _response = await _httpPipeline.SendAsync(_request, _eventListener);
                    await EventListener.Signal(Events.TracingHttpResponseMessage, _response);
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
                        await _eventListener.Signal(Events.BeforePollingCall, $"Polling status: _finalUri: {_finalUri}, _asyncOperationUri: {_asyncOperationUri}, _locationUri: {_locationUri}, _operationLocationUri: {_operationLocationUri}");
                        // make the final call
                        _response = await _httpPipeline.SendAsync(_request, _eventListener);
                        await EventListener.Signal(Events.TracingHttpResponseMessage, _response);
                        await _eventListener.Signal(Events.AfterPollingCall);
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
}