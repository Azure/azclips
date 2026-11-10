// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <remarks>Lives under <c>Customization/</c> so codegen won't overwrite it.</remarks>
    public abstract partial class ResourcesProcessorBase<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        // TODO: Consolidate common send/tracing and polling behavior in HttpRuntime, with forwarding support
        // in ProcessorBaseGenerator. Keep deployment-specific response/error interpretation in the orchestrators.

        /// <summary>
        /// POSTs, then polls Location until it stops answering 202 and returns that response untouched.
        /// Used by what-if: the shared LRO poller turns ARM's 200 "status: Failed" body into a body-less
        /// exception and re-GETs the POST-only /whatIf URI (404). The caller inspects the result.
        /// </summary>
        internal async Task<HttpResponseMessage> SendAsyncPollLocation()
        {
            HttpResponseMessage response = await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
            string location = string.Empty;
            while (response.StatusCode == HttpStatusCode.Accepted)
            {
                // A later 202 may omit Location; keep polling the last one ARM gave.
                location = response.GetFirstHeader("Location") is { Length: > 0 } header ? header : location;
                int delay = (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? HttpPipeline.PollingInterval);
                response.Dispose();

                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new InvalidOperationException(
                        "The operation returned 202 Accepted without a Location header, so its result cannot be retrieved.");
                }

                await Publisher.Publish(Events.DelayPolling, $"Delaying {delay} seconds before polling.");
                await Task.Delay(delay * 1000, Publisher.Token);
                response = await HttpRuntime.SendAsync(() => Task.FromResult(new HttpRequestMessage(HttpMethod.Get, location)), Publisher, HttpPipeline);
            }

            return response;
        }

        /// <summary>
        /// Sends the request but returns HTTP 400 untouched instead of throwing, and continues a 202 by polling Location.
        /// Used by validate: ARM reports a failed template as 400 whose body is a typed <c>DeploymentValidateResult</c>, which
        /// the shared <see cref="HttpRuntime"/> helpers would reduce to text in an exception. The caller deserializes the 400.
        /// </summary>
        internal async Task<HttpResponseMessage> SendAsyncAllowBadRequest()
        {
            HttpResponseMessage response;
            using (HttpRequestMessage request = await CreateRequestMessage())
            {
                response = await HttpPipeline.SendAsync(request, Publisher);
            }

            await Publisher.Publish(Events.TracingHttpResponseMessage, response);

            // A 400 can only come from the initial POST: ARM checks the template before starting async work.
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return response;
            }

            // Continue the LRO with GETs on Location; the POST URI itself cannot be re-fetched.
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                string? location = GetLocationUri(response);
                response.Dispose();

                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new InvalidOperationException(
                        "The validate operation returned 202 Accepted without a Location header, so its result cannot be retrieved.");
                }

                return await HttpRuntime.SendAsyncLRO(
                    () => Task.FromResult(new HttpRequestMessage(HttpMethod.Get, location)),
                    Publisher,
                    HttpPipeline,
                    GetFinalUri,
                    GetOperationLocationUri,
                    GetAsyncOperationUri,
                    GetLocationUri);
            }

            try
            {
                await response.EnsureSuccessAsync(Publisher);
            }
            catch
            {
                response.Dispose();
                throw;
            }

            return response;
        }

        /// <summary>
        /// Sends the request once and returns the response whatever its status: no polling and no status check,
        /// which is left to the caller. Used by tenant create <c>--no-wait</c> (the PUT submission only) and by
        /// tenant wait (one GET per poll, where a 404 is an expected answer rather than an error).
        /// </summary>
        internal async Task<HttpResponseMessage> SendAsyncOnce()
        {
            HttpResponseMessage response;
            using (HttpRequestMessage request = await CreateRequestMessage())
            {
                response = await HttpPipeline.SendAsync(request, Publisher);
            }

            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            return response;
        }
    }
}
