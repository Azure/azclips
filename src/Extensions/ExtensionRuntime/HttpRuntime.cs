// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System.Net.Http;
using System.Threading.Tasks;
using Core.Abstractions.Runtime;
using System.Net;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

public static class HttpRuntime
{
    public static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline)
    {
        using HttpRequestMessage request = await getRequest();
        HttpResponseMessage response = await httpPipeline.SendAsync(request, publisher);
        await publisher.Publish(Events.TracingHttpResponseMessage, response);
        try
        {
            await response.EnsureSuccessAsync(publisher);
        }
        catch
        {
            response.Dispose();
            throw;
        }
        return response;
    }

    public static Task<HttpResponseMessage> SendAsyncLRO(Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, Func<HttpResponseMessage, string?> getFinalUri, Func<HttpResponseMessage, string?> getOperationLocationUri, Func<HttpResponseMessage, string?> getAsyncOperationUri, Func<HttpResponseMessage, string?> getLocationUri)
        => SendAsyncLRO(getRequest, publisher, httpPipeline, getFinalUri, getOperationLocationUri, getAsyncOperationUri, getLocationUri, beforePoll: null);

    /// <param name="beforePoll">
    /// Runs after each polling delay, right before the status GET, so a caller can report progress while it waits.
    /// </param>
    public static async Task<HttpResponseMessage> SendAsyncLRO(Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, Func<HttpResponseMessage, string?> getFinalUri, Func<HttpResponseMessage, string?> getOperationLocationUri, Func<HttpResponseMessage, string?> getAsyncOperationUri, Func<HttpResponseMessage, string?> getLocationUri, Func<Task>? beforePoll)
    {
        HttpRequestMessage request = await getRequest();
        try
        {
            string _originalUri = request.RequestUri!.AbsoluteUri;
            HttpResponseMessage response = await httpPipeline.SendAsync(request, publisher);
            await publisher.Publish(Events.TracingHttpResponseMessage, response);
            // declared final-state-via: location
            var _finalUri = getFinalUri(response);
            if (string.IsNullOrEmpty(_finalUri) && request.Method != HttpMethod.Delete)
            {
                _finalUri = _originalUri;
            }
            var _asyncOperationUri = getAsyncOperationUri(response);
            var _locationUri = getLocationUri(response);
            var _operationLocationUri = getOperationLocationUri(response);

            //polling
            while (request.Method == HttpMethod.Put && response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.Accepted)
            {
                // TODO(Yabo): get default value from config service: engine get default from configservice and pass to processor via ProcessorContext
                int delay = (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? httpPipeline.PollingInterval);
                await publisher.Publish(Events.DelayPolling, $"Delaying {delay} seconds before polling.");
                Task delayTask = Task.Delay(delay*1000, publisher.Token);

                // while we wait, let's grab the headers and get ready to poll.
                if (!string.IsNullOrEmpty(getAsyncOperationUri(response)))
                {
                    _asyncOperationUri = getAsyncOperationUri(response);
                }
                if (!string.IsNullOrEmpty(getLocationUri(response)))
                {
                    _locationUri = getLocationUri(response);
                }
                if (!string.IsNullOrEmpty(getOperationLocationUri(response)))
                {
                    _operationLocationUri = getOperationLocationUri(response);
                }
                var _uri = string.IsNullOrEmpty(_asyncOperationUri) ? string.IsNullOrEmpty(_locationUri) ? string.IsNullOrEmpty(_operationLocationUri) ? _originalUri : _operationLocationUri : _locationUri : _asyncOperationUri;

                // drop the old response
                response?.Dispose();

                await delayTask;

                if (beforePoll is not null)
                {
                    await beforePoll();
                }

                request = request.CloneAndDispose(new Uri(_uri), HttpMethod.Get);
                await publisher.Publish(Events.BeforePollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                // make the polling call
                response = await httpPipeline.SendAsync(request, publisher);
                await publisher.Publish(Events.TracingHttpResponseMessage, response);
                await publisher.Publish(Events.AfterPollingCall);

                // if we got back an OK, take a peek inside and see if it's done
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var error = false;
                    try
                    {
                        if (JsonNode.Parse(await response.Content.ReadAsStringAsync()) is JsonObject json)
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
                                    response.StatusCode = HttpStatusCode.Created;
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
                        throw new Exception($"Long running operation failed with response: {response}");
                    }
                }

                // check for terminal status code
                if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.Accepted)
                {
                    continue;
                }
                // we are done polling, do a request on final target?
                // create a new request with the final uri
                if (!string.IsNullOrEmpty(_finalUri))
                {
                    // drop the old response
                    response?.Dispose();
                    request = request.CloneAndDispose(new Uri(_finalUri), HttpMethod.Get);
                    await publisher.Publish(Events.BeforeFinalPollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                    // make the final call
                    response = await httpPipeline.SendAsync(request, publisher);
                    await publisher.Publish(Events.TracingHttpResponseMessage, response);
                    await publisher.Publish(Events.AfterFinalPollingCall);
                    break;
                }
            }
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.NoContent:
                {
                    await publisher.Publish(Events.PollingFinished);
                    break;
                }
                default:
                {
                    await publisher.Publish(Events.PollingFailed);
                    try
                    {
                        await response.EnsureSuccessAsync(publisher);
                    }
                    catch
                    {
                        response.Dispose();
                        throw;
                    }
                    break;
                }
            }
            return response;
        }
        finally
        {
            request?.Dispose();
        }
    }
}