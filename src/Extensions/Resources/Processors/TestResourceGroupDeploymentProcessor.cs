// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: validates a resource-group-scoped deployment
    /// (POST .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}/validate)
    /// and returns the <see cref="DeploymentValidateResult"/>. Pure transport: it serializes the
    /// already-built <see cref="DeploymentProperties"/> and does no validation, file IO, or bicep
    /// compilation. All of that (and the full user-facing argument surface) lives in
    /// <see cref="TestResourceGroupDeploymentProcessor_Orchestrator"/>, which builds the properties
    /// and delegates here via <c>CreateExtendedProcessor</c>. Validate is read-only: it never
    /// creates or modifies resources.
    /// </summary>
    public class TestResourceGroupDeploymentProcessor : ResourcesProcessorBase<Deployment, DeploymentValidateResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public DeploymentProperties Properties { get; set; } = new();

        // Validate is long-running with final-state-via: location, so the Location header drives both
        // polling and the final GET. Unlike the deployment resource itself, the validate result is not
        // addressable at the request URI (it is POST-only), so GetFinalUri is wired to Location as well
        // — leaving it null would make the poller fall back to the original POST URI and issue an
        // invalid GET against it. Azure-AsyncOperation is intentionally NOT wired: it carries only a
        // bare {status} without the validation payload, and the poller prefers it when present.
        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionIdValidator.Require(SubscriptionId))}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/validate?api-version={ApiVersion}";

        protected override Deployment? CreateRequestBody() => new Deployment { Properties = Properties };

        /// <summary>
        /// Sends the validate request, treating HTTP 400 as a normal (typed) outcome rather than a
        /// transport failure.
        /// <para>
        /// ARM reports a template that fails validation as <c>400 Bad Request</c> whose body is a
        /// regular <see cref="DeploymentValidateResult"/> — unlike what-if, which reports failures on
        /// HTTP 200. The shared <see cref="HttpRuntime"/> helpers call <c>EnsureSuccessAsync</c>
        /// internally, which disposes the response and reduces that typed body to text glued onto an
        /// exception message, so they cannot be used for the initial call. The request is therefore
        /// sent directly here and the 400 returned untouched for the orchestrator to deserialize.
        /// </para>
        /// <para>
        /// Exactly one POST is ever issued: a 400 can only come from the initial request (ARM
        /// evaluates the template before starting async work), and the 202 branch continues the
        /// long-running operation by polling the Location header with GET rather than re-POSTing.
        /// </para>
        /// </summary>
        internal override async Task<HttpResponseMessage> SendAsync()
        {
            HttpResponseMessage response;
            using (HttpRequestMessage request = await CreateRequestMessage())
            {
                response = await HttpPipeline.SendAsync(request, Publisher);
            }

            await Publisher.Publish(Events.TracingHttpResponseMessage, response);

            // A failed validation: a well-formed DeploymentValidateResult carrying the error.
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return response;
            }

            // Validation is running asynchronously; continue the LRO from the Location header.
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

            // Anything else (including genuine 403/404/CloudError failures) keeps the standard behaviour.
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
