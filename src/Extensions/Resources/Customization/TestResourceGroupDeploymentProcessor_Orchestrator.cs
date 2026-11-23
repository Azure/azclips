// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Validates a deployment at the resource group scope
    /// (POST .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}/validate)
    /// without submitting it. This is the AzClips equivalent of
    /// <c>az deployment group validate</c> / <c>Test-AzResourceGroupDeployment</c>.
    /// Validate is read-only: it never creates or modifies resources.
    /// <para>
    /// Orchestrator (handwritten/customized) half of the deployment-validate command: it owns the
    /// rollback argument surface on top of the shared template-source, parameters-source, mode,
    /// inline parameters, and validation-level options inherited from
    /// <see cref="DeploymentOrchestratorBase{TRequest, TResponse}"/>, together with the validation and the
    /// template/parameters/bicep building. It builds the request body and delegates the actual
    /// POST transport to the atomic <see cref="TestResourceGroupDeploymentProcessor"/> via
    /// <c>CreateExtendedProcessor</c>.
    /// </para>
    /// </summary>
    public class TestResourceGroupDeploymentProcessor_Orchestrator : DeploymentOrchestratorBase<Deployment, DeploymentValidateResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        public bool RollbackOnError { get; set; }

        public string? RollbackToDeploymentName { get; set; }

        // Builds the deployment request body (after validating the inputs) and delegates the POST to
        // the atomic TestResourceGroupDeploymentProcessor. SendAsyncAllowBadRequest returns the HTTP 400
        // untouched and polls Location without an extra final GET of the one-shot result.
        internal override async Task<HttpResponseMessage> SendAsync()
        {
            ValidateInputs();

            DeploymentProperties properties = new DeploymentProperties
            {
                Mode = ParseMode(Mode),
                Template = BuildTemplate(out TemplateLink? templateLink),
                TemplateLink = templateLink,
                Parameters = BuildParameters(out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                OnErrorDeployment = BuildOnErrorDeployment(),
                ValidationLevel = NormalizeValidationLevel(ValidationLevel),
            };

            TestResourceGroupDeploymentProcessor processor = CreateExtendedProcessor<TestResourceGroupDeploymentProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
                p.Name = Name;
                p.Properties = properties;
            });

            return await processor.SendAsyncAllowBadRequest();
        }

        // ARM reports a template that fails validation as HTTP 400 whose body is a regular
        // DeploymentValidateResult, with the detail in result.error. Override ProcessAsync so that
        // error is surfaced as a readable, flattened exception instead of raw ARM JSON (which is what
        // the shared EnsureSuccessAsync would produce) or a misleading success. A result without an
        // error is written through unchanged.
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                using HttpResponseMessage response = await SendAsync();

                if (response.Content is null)
                {
                    await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                    await EnsureSuccessIfNoVerdict(response);
                    return;
                }

                string rawBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                    await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                    await EnsureSuccessIfNoVerdict(response);
                    return;
                }

                DeploymentValidateResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<DeploymentValidateResult>(rawBody, JsonOptions);
                }
                catch (JsonException ex)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {nameof(DeploymentValidateResult)}. Raw body: {rawBody}", ex);
                }

                if (result is null)
                {
                    await EnsureSuccessIfNoVerdict(response);
                    return;
                }

                if (result.Error is not null)
                {
                    throw new InvalidOperationException(BuildErrorMessage(result.Error));
                }

                await EnsureSuccessIfNoVerdict(response);
                await WriteOutput(result);
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        // A non-success status (the 400 passed through by the atomic processor) without an error in
        // its body is not a validation verdict we can report — fall back to the standard transport
        // failure rather than claiming success.
        private async Task EnsureSuccessIfNoVerdict(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await response.EnsureSuccessAsync(Publisher);
            }
        }

        // Validates the shared template/parameters/mode/inline/validation-level rules together with
        // the rollback rule. Fails fast with deterministic ArgumentExceptions.
        private void ValidateInputs()
        {
            ValidateCommonInputs();

            if (RollbackOnError && !string.IsNullOrWhiteSpace(RollbackToDeploymentName))
            {
                throw new ArgumentException("Provide at most one of -RollbackOnError or -RollbackToDeploymentName; they are mutually exclusive.");
            }
        }

        private OnErrorDeployment? BuildOnErrorDeployment()
        {
            if (!string.IsNullOrWhiteSpace(RollbackToDeploymentName))
            {
                return new OnErrorDeployment
                {
                    Type = "SpecificDeployment",
                    DeploymentName = RollbackToDeploymentName,
                };
            }

            if (RollbackOnError)
            {
                return new OnErrorDeployment { Type = "LastSuccessful" };
            }

            return null;
        }

        // Builds a clear, flattened message from the (possibly nested) ARM error so a template error
        // is readable instead of a wall of raw JSON.
        internal static string BuildErrorMessage(ErrorResponse? error)
        {
            StringBuilder builder = new StringBuilder("Deployment validation failed.");
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
