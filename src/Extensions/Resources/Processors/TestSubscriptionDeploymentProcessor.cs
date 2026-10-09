// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: validates a subscription-scoped deployment
    /// (POST /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}/validate) and returns
    /// the <see cref="DeploymentValidateResult"/>. Pure transport; inputs are built by
    /// <see cref="TestSubscriptionDeploymentProcessor_Orchestrator"/>. Read-only.
    /// </summary>
    public class TestSubscriptionDeploymentProcessor : ResourcesProcessorBase<Deployment, DeploymentValidateResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public DeploymentProperties Properties { get; set; } = new();

        // Same as the resource-group variant: final-state-via location, and the validate result is
        // POST-only, so Location drives both polling and the final GET.
        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/validate?api-version={ApiVersion}";

        protected override Deployment? CreateRequestBody() => new Deployment { Location = Location, Properties = Properties };

        /// <summary>
        /// Sends the validate request, returning HTTP 400 untouched (ARM reports a failed validation as
        /// 400 with a typed <see cref="DeploymentValidateResult"/> body) and continuing a 202 via the
        /// Location header. See <see cref="TestResourceGroupDeploymentProcessor"/> for the rationale.
        /// </summary>
        internal override async Task<HttpResponseMessage> SendAsync()
        {
            HttpResponseMessage response;
            using (HttpRequestMessage request = await CreateRequestMessage())
            {
                response = await HttpPipeline.SendAsync(request, Publisher);
            }

            await Publisher.Publish(Events.TracingHttpResponseMessage, response);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return response;
            }

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
    }
}
