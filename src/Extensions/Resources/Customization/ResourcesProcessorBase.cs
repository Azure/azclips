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
        /// <summary>
        /// Sends the request once and returns as soon as ARM accepts it, without polling the long-running operation.
        /// Used by <c>--no-wait</c>; a non-success status still throws.
        /// </summary>
        internal Task<HttpResponseMessage> SendAsyncNoPoll() => HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);

        /// <summary>
        /// POSTs, then polls Location until it stops answering 202 and returns that response untouched.
        /// Used by what-if: the shared LRO poller turns ARM's 200 "status: Failed" body into a body-less
        /// exception and re-GETs the POST-only /whatIf URI (404). The caller inspects the result.
        /// </summary>
        internal async Task<HttpResponseMessage> SendAsyncPollLocation()
        {
            HttpResponseMessage response = await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
            return await PollLocationAsync(response);
        }

        /// <summary>
        /// GETs Location while the response is 202 and returns the first non-202 response untouched.
        /// Unlike <see cref="HttpRuntime.SendAsyncLRO"/>, it makes no extra "final" GET after the terminal
        /// response, because ARM operation results under Location are one-shot and can answer 404 when re-read.
        /// </summary>
        private async Task<HttpResponseMessage> PollLocationAsync(HttpResponseMessage response)
        {
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

            // Continue with GETs on Location; the POST URI itself cannot be re-fetched.
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return await PollLocationAsync(response);
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
    }
}
