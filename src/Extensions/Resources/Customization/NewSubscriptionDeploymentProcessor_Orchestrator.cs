// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Creates or updates a deployment at subscription scope
    /// (PUT /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}).
    /// AzClips equivalent of <c>az deployment sub create</c> / <c>New-AzDeployment</c>.
    /// Builds the request from the shared <see cref="DeploymentOrchestratorBase{TRequest, TResponse}"/>
    /// inputs plus the required <see cref="Location"/>, validates it with
    /// <see cref="TestSubscriptionDeploymentProcessor"/> (a validation error stops before the PUT), and delegates
    /// the PUT + long-running-operation wait to <see cref="NewSubscriptionDeploymentProcessor"/>. When the deployment fails, it raises the
    /// deployment's ARM error, up to 3 failed operation errors and the correlation id.
    /// </summary>
    public class NewSubscriptionDeploymentProcessor_Orchestrator : DeploymentOrchestratorBase<Deployment, DeploymentExtended>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>Where ARM stores the deployment data; required at subscription scope.</summary>
        public string Location { get; set; } = string.Empty;

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            ValidateCommonInputs();

            DeploymentProperties properties = new DeploymentProperties
            {
                Template = BuildTemplate(out TemplateLink? templateLink),
                TemplateLink = templateLink,
                Parameters = BuildParameters(out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                ValidationLevel = NormalizeValidationLevel(ValidationLevel),
            };

            await ValidateDeployment(properties);

            NewSubscriptionDeploymentProcessor processor = CreateExtendedProcessor<NewSubscriptionDeploymentProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = Name;
                p.Location = Location;
                p.Properties = properties;
            });

            try
            {
                return await processor.SendAsync();
            }
            // The runtime reports a failed LRO as a plain Exception; HTTP errors and cancellation have their own types.
            catch (Exception ex) when (ex.GetType() == typeof(Exception))
            {
                DeploymentExtended? deployment = null;
                try
                {
                    deployment = await ReadAsync<GetSubscriptionDeploymentProcessor, DeploymentExtended>(p =>
                    {
                        p.SubscriptionId = SubscriptionId;
                        p.Name = Name;
                    });
                }
                catch (HttpRequestException)
                {
                }

                if (deployment is null)
                {
                    throw;
                }

                throw new InvalidOperationException(await BuildDeploymentFailureMessage(deployment), ex);
            }
        }

        // Preflight validation, as for resource-group deployments: an invalid deployment fails here without a PUT.
        // ARM reports a failed validation as 400 carrying a DeploymentValidateResult, so the 400 is parsed, not thrown.
        private async Task ValidateDeployment(DeploymentProperties properties)
        {
            using HttpResponseMessage response = await CreateExtendedProcessor<TestSubscriptionDeploymentProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = Name;
                p.Location = Location;
                p.Properties = properties;
            }).SendAsyncAllowBadRequest();

            string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body)
                && JsonSerializer.Deserialize<DeploymentValidateResult>(body, JsonOptions)?.Error is { } error)
            {
                throw new InvalidOperationException(BuildErrorMessage("Deployment validation failed.", error));
            }

            if (!response.IsSuccessStatusCode)
            {
                await response.EnsureSuccessAsync(Publisher);
            }
        }

        // Deployment error, up to 3 failed operation errors, then the correlation id.
        private async Task<string> BuildDeploymentFailureMessage(DeploymentExtended? deployment)
        {
            // Codegen types properties.error as object; it deserializes to a JsonElement.
            ErrorResponse? deploymentError = deployment?.Properties?.Error is JsonElement { ValueKind: JsonValueKind.Object } element
                ? element.Deserialize<ErrorResponse>(JsonOptions)
                : null;
            StringBuilder message = new StringBuilder(BuildErrorMessage($"Deployment '{Name}' failed.", deploymentError));

            try
            {
                // Reads only the first page of operations (pagination lives in the output hook), so a failed operation past page one is missed.
                DeploymentOperationsListResult? operations = await ReadAsync<ListSubscriptionDeploymentOperationProcessor, DeploymentOperationsListResult>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.DeploymentName = Name;
                });

                IEnumerable<ErrorResponse> errors = (operations?.Value ?? [])
                    .Where(o => string.Equals(o.Properties?.ProvisioningState, "Failed", StringComparison.OrdinalIgnoreCase))
                    .Select(o => o.Properties?.StatusMessage?.Error)
                    .OfType<ErrorResponse>()
                    .Take(3);

                foreach (ErrorResponse error in errors)
                {
                    message.Append(Environment.NewLine).Append(BuildErrorMessage("Operation failed:", error));
                }
            }
            catch (HttpRequestException)
            {
                // The deployment error alone is still useful.
            }

            return message.Append(Environment.NewLine).Append("Correlation ID: ").Append(deployment?.Properties?.CorrelationId).ToString();
        }

        private async Task<T?> ReadAsync<TProcessor, T>(Action<TProcessor> configure)
            where TProcessor : ResourcesProcessorBase, new()
            where T : class
        {
            using HttpResponseMessage response = await CreateExtendedProcessor(configure).SendAsync();
            return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
        }
    }
}
