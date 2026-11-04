// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Runs an ARM what-if at subscription scope
    /// (POST /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}/whatIf).
    /// AzClips equivalent of <c>az deployment sub what-if</c>. Read-only. Delegates the POST +
    /// long-running-operation wait to <see cref="NewSubscriptionDeploymentWhatIfProcessor"/>.
    /// </summary>
    public class NewSubscriptionDeploymentWhatIfProcessor_Orchestrator : DeploymentOrchestratorBase<DeploymentWhatIf, WhatIfOperationResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>Where ARM stores the deployment data; required at subscription scope.</summary>
        public string Location { get; set; } = string.Empty;

        public string? ResultFormat { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            ValidateCommonInputs(requireResourceGroup: false);
            ValidateResultFormat(ResultFormat);

            DeploymentWhatIf body = new DeploymentWhatIf
            {
                Location = Location,
                Properties = new DeploymentWhatIfProperties
                {
                    Template = BuildTemplate(out TemplateLink? templateLink),
                    TemplateLink = templateLink,
                    Parameters = BuildParameters(out ParametersLink? parametersLink),
                    ParametersLink = parametersLink,
                    ValidationLevel = NormalizeValidationLevel(ValidationLevel),
                    WhatIfSettings = new DeploymentWhatIfSettings { ResultFormat = NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.NormalizeResultFormat(ResultFormat) },
                }
            };

            NewSubscriptionDeploymentWhatIfProcessor processor = CreateExtendedProcessor<NewSubscriptionDeploymentWhatIfProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = Name;
                p.WhatIf = body;
            });

            return await processor.SendAsyncPollLocation();
        }

        // Same as the resource-group variant: what-if answers HTTP 200 even when the template has an
        // error, so the result is inspected and a failure thrown instead of shown as "no change".
        public override async Task ProcessAsync()
        {
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

                WhatIfOperationResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<WhatIfOperationResult>(rawBody, JsonOptions);
                }
                catch (JsonException ex)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {nameof(WhatIfOperationResult)}. Raw body: {rawBody}", ex);
                }

                if (result is null)
                {
                    return;
                }

                if (string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase) || result.Error is not null)
                {
                    throw new InvalidOperationException(BuildErrorMessage("What-if operation failed.", result.Error));
                }

                await WriteOutput(result);
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
