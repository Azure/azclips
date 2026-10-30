// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Runs an ARM what-if operation at the resource group scope
    /// (POST .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}/whatIf) and
    /// renders the predicted change list. This is the AzClips equivalent of
    /// <c>az deployment group what-if</c> / <c>New-AzResourceGroupDeploymentWhatIf</c>.
    /// What-if is read-only: it never creates or modifies resources.
    /// <para>
    /// Orchestrator (handwritten/customized) half of the deployment what-if command: it owns the
    /// what-if-specific option (result-format) on top of the shared template-source,
    /// parameters-source, mode, inline parameters, and validation-level options inherited from
    /// <see cref="DeploymentOrchestratorBase{TRequest, TResponse}"/>, together with the validation and the
    /// template/parameters/bicep building. It builds the request body and delegates the actual
    /// POST + long-running-operation transport to the atomic
    /// <see cref="NewResourceGroupDeploymentWhatIfProcessor"/> via <c>CreateExtendedProcessor</c>.
    /// </para>
    /// </summary>
    public class NewResourceGroupDeploymentWhatIfProcessor_Orchestrator : DeploymentOrchestratorBase<DeploymentWhatIf, WhatIfOperationResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string? ResultFormat { get; set; }

        // Builds the what-if request body (after validating the inputs) and delegates the POST +
        // long-running-operation wait to the atomic NewResourceGroupDeploymentWhatIfProcessor.
        // What-if always waits for the LRO to complete; there is no no-wait branch.
        internal override async Task<HttpResponseMessage> SendAsync()
        {
            ValidateInputs();
            await DownloadRemoteInputsAsync();

            DeploymentWhatIf body = new DeploymentWhatIf
            {
                Properties = new DeploymentWhatIfProperties
                {
                    Mode = ParseMode(Mode),
                    Template = BuildTemplate(out TemplateLink? templateLink),
                    TemplateLink = templateLink,
                    Parameters = BuildParameters(out ParametersLink? parametersLink),
                    ParametersLink = parametersLink,
                    ValidationLevel = NormalizeValidationLevel(ValidationLevel),
                    WhatIfSettings = new DeploymentWhatIfSettings { ResultFormat = NormalizeResultFormat(ResultFormat) },
                }
            };

            NewResourceGroupDeploymentWhatIfProcessor processor = CreateExtendedProcessor<NewResourceGroupDeploymentWhatIfProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
                p.Name = Name;
                p.WhatIf = body;
            });

            return await processor.SendAsyncPollLocation();
        }

        // The what-if API returns HTTP 200 with status Succeeded even when the template has an error;
        // the error lives in result.error. Override ProcessAsync so a template error is surfaced as a
        // thrown exception instead of being silently presented as a successful no-change result. (The
        // generic base ProcessAsync would deserialize and write the result without inspecting it.)
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

                bool failed = string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase);
                if (failed || result.Error is not null)
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

        // Validates the shared template/parameters/mode/inline/validation-level rules together with
        // the what-if-specific result-format rule. Fails fast with deterministic ArgumentExceptions.
        private void ValidateInputs()
        {
            ValidateResourceGroupName(ResourceGroupName);
            ValidateCommonInputs();
            ValidateResultFormat(ResultFormat);
        }

        // Builds a clear, flattened message from the (possibly nested) ARM error so a template error
        // is not silently presented as a successful no-change result.
        internal static string BuildErrorMessage(ErrorResponse? error)
        {
            StringBuilder builder = new StringBuilder("What-if operation failed.");
            AppendError(builder, error, indent: 1);
            return builder.ToString();
        }

        private static void AppendError(StringBuilder builder, ErrorResponse? error, int indent)
        {
            if (error is null)
            {
                return;
            }

            string pad = new string(' ', indent * 2);
            string code = string.IsNullOrWhiteSpace(error.Code) ? "(no code)" : error.Code!;
            string message = string.IsNullOrWhiteSpace(error.Message) ? "(no message)" : error.Message!;
            builder.Append(Environment.NewLine).Append(pad).Append(code).Append(": ").Append(message);
            if (!string.IsNullOrWhiteSpace(error.Target))
            {
                builder.Append(" (target: ").Append(error.Target).Append(')');
            }

            if (error.Details is { Count: > 0 })
            {
                foreach (ErrorResponse detail in error.Details)
                {
                    AppendError(builder, detail, indent + 1);
                }
            }
        }
    }
}
