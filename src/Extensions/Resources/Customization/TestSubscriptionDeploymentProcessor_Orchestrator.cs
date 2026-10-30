// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Validates a deployment at subscription scope
    /// (POST /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}/validate) without
    /// submitting it. AzClips equivalent of <c>az deployment sub validate</c> /
    /// <c>Test-AzDeployment</c>. Delegates the POST to <see cref="TestSubscriptionDeploymentProcessor"/>.
    /// </summary>
    public class TestSubscriptionDeploymentProcessor_Orchestrator : DeploymentOrchestratorBase<Deployment, DeploymentValidateResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>Where ARM stores the deployment data; required at subscription scope.</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>Tags sent as the deployment's top-level <c>tags</c> on the validate.</summary>
        public IDictionary<string, string>? Tags { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            ValidateLocation(Location);
            ValidateCommonInputs();

            DeploymentProperties properties = new DeploymentProperties
            {
                Template = BuildTemplate(out TemplateLink? templateLink),
                TemplateLink = templateLink,
                Parameters = BuildParameters(out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                ValidationLevel = NormalizeValidationLevel(ValidationLevel),
            };

            TaggedTestSubscriptionDeploymentProcessor processor = CreateExtendedProcessor<TaggedTestSubscriptionDeploymentProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = Name;
                p.Location = Location;
                p.Properties = properties;
                p.Tags = Tags;
            });

            return await processor.SendAsyncAllowBadRequest();
        }

        // Same as the resource-group variant: ARM reports a failed validation as HTTP 400 carrying a
        // DeploymentValidateResult, so the error is surfaced as a readable exception instead of raw JSON
        // or a misleading success.
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                using HttpResponseMessage response = await SendAsync();

                if (response.Content is null)
                {
                    await response.EnsureSuccessAsync(Publisher);
                    await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                    return;
                }

                string rawBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                    await response.EnsureSuccessAsync(Publisher);
                    await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                    return;
                }

                DeploymentValidateResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<DeploymentValidateResult>(rawBody, JsonOptions);
                }
                catch (JsonException ex)
                {
                    await response.EnsureSuccessAsync(Publisher);
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {nameof(DeploymentValidateResult)}. Raw body: {rawBody}", ex);
                }

                if (result is null)
                {
                    await response.EnsureSuccessAsync(Publisher);
                    return;
                }

                if (result.Error is null && !response.IsSuccessStatusCode)
                {
                    await response.EnsureSuccessAsync(Publisher);
                }

                await WriteValidationResult(result);
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>
        /// Writes the validate result: a failed validation is raised as an error, and a passing one is
        /// written as the full result.
        /// </summary>
        protected virtual async Task WriteValidationResult(DeploymentValidateResult result)
        {
            if (result.Error is not null)
            {
                throw new InvalidOperationException(BuildErrorMessage("Deployment validation failed.", result.Error));
            }

            await WriteOutput(result);
        }
    }
}
