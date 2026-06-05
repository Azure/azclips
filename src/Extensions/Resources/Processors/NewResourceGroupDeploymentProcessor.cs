using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;
using Microsoft.Azure.Cirrus.Extensions.Resources;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    /// <summary>
    /// Creates or updates a deployment at the resource group scope
    /// (PUT .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}).
    /// This is the Cirrus equivalent of <c>az deployment group create</c> /
    /// <c>New-AzResourceGroupDeployment</c>.
    /// </summary>
    public class NewResourceGroupDeploymentProcessor : DeploymentRequestProcessorBase<DeploymentExtended>
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Roll the resource group back to the last successful deployment on error.",
        SerializedName = @"RollbackOnError")]
        public bool RollbackOnError { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Roll the resource group back to a specific named deployment on error. Mutually exclusive with RollbackOnError.",
        SerializedName = @"RollbackToLastDeploymentName")]
        public string? RollbackToLastDeploymentName { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The validation level of the deployment. Template, Provider, or ProviderNoRbac.",
        SerializedName = @"ValidationLevel")]
        public string? ValidationLevel { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Return as soon as the deployment is accepted, without waiting for it to complete.",
        SerializedName = @"NoWait")]
        public bool NoWait { get; set; }

        // ARM resource-group deployments are long-running (final-state-via: azure-async-operation).
        // The deployment resource itself carries provisioningState, so the async-operation/location
        // headers drive polling and a final GET on the deployment URI (the default _finalUri) yields
        // the terminal DeploymentExtended. GetFinalUri/GetOperationLocationUri stay null (default).
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        // Default behavior waits for the deployment to reach a terminal provisioningState by polling
        // (matches `az deployment group create`). -NoWait returns on the initial accepted response.
        // SubscriptionId is resolved here so both branches build the deployment URI correctly.
        internal override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return NoWait ? base.SendAsync() : SendAsyncLRO();
        }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            ValidateInputs();

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            Deployment body = new Deployment
            {
                Properties = new DeploymentProperties
                {
                    Mode = ParseMode(Mode),
                    Template = BuildTemplate(out TemplateLink? templateLink),
                    TemplateLink = templateLink,
                    Parameters = BuildParameters(out ParametersLink? parametersLink),
                    ParametersLink = parametersLink,
                    OnErrorDeployment = BuildOnErrorDeployment(),
                    ValidationLevel = NormalizeValidationLevel(ValidationLevel),
                }
            };

            string uri = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override void ValidateInputs()
        {
            base.ValidateInputs();

            if (RollbackOnError && !string.IsNullOrWhiteSpace(RollbackToLastDeploymentName))
            {
                throw new ArgumentException("Provide at most one of -RollbackOnError or -RollbackToLastDeploymentName; they are mutually exclusive.");
            }

            if (!string.IsNullOrWhiteSpace(ValidationLevel) && NormalizeValidationLevel(ValidationLevel) is null)
            {
                throw new ArgumentException("ValidationLevel must be one of 'Template', 'Provider', or 'ProviderNoRbac'.");
            }
        }

        private OnErrorDeployment? BuildOnErrorDeployment()
        {
            if (!string.IsNullOrWhiteSpace(RollbackToLastDeploymentName))
            {
                return new OnErrorDeployment
                {
                    Type = "SpecificDeployment",
                    DeploymentName = RollbackToLastDeploymentName,
                };
            }

            if (RollbackOnError)
            {
                return new OnErrorDeployment { Type = "LastSuccessful" };
            }

            return null;
        }

        // Returns the canonical-cased validation level when valid, or null when the input is
        // empty (not supplied) or unrecognized. ValidateInputs rejects unrecognized values.
        private static string? NormalizeValidationLevel(string? validationLevel) =>
            string.IsNullOrWhiteSpace(validationLevel)
                ? null
                : validationLevel.Trim().ToLowerInvariant() switch
                {
                    "template" => "Template",
                    "provider" => "Provider",
                    "providernorbac" => "ProviderNoRbac",
                    _ => null,
                };
    }
}
