using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Abstractions.Runtime;
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
    public class NewResourceGroupDeploymentProcessor : ResourcesProcessorBase<DeploymentExtended>
    {
        private const string ApiVersion = "2025-04-01";
        private static readonly Regex s_deploymentNamePattern = new("^[-\\w\\._\\(\\)]+$", RegexOptions.Compiled);

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the resource group to deploy into.",
        SerializedName = @"ResourceGroupName")]
        public string ResourceGroupName { get; set; } = string.Empty;

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the deployment.",
        SerializedName = @"Name")]
        public string Name { get; set; } = string.Empty;

        [ArgumentInfo(
        Required = false,
        Description = @"The URI of the ARM template to deploy.",
        SerializedName = @"TemplateUri")]
        public string? TemplateUri { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The path to a local ARM template file to deploy inline.",
        SerializedName = @"TemplateFile")]
        public string? TemplateFile { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The resource id of a Template Spec to deploy.",
        SerializedName = @"TemplateSpecId")]
        public string? TemplateSpecId { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The query string (for example, a SAS token) to be used with the template URI.",
        SerializedName = @"QueryString")]
        public string? QueryString { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The URI of a parameters file.",
        SerializedName = @"ParametersUri")]
        public string? ParametersUri { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The path to a local parameters file to deploy inline.",
        SerializedName = @"ParametersFile")]
        public string? ParametersFile { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The deployment mode. Incremental (default) or Complete.",
        SerializedName = @"Mode")]
        public string? Mode { get; set; }

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

        private void ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroupName))
            {
                throw new ArgumentException("ResourceGroupName must be provided.");
            }
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 64 || !s_deploymentNamePattern.IsMatch(Name))
            {
                throw new ArgumentException("Deployment name must be 1-64 characters and match the pattern ^[-\\w\\._\\(\\)]+$.");
            }

            int templateSources = (string.IsNullOrWhiteSpace(TemplateUri) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateFile) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateSpecId) ? 0 : 1);
            if (templateSources != 1)
            {
                throw new ArgumentException("Exactly one of TemplateUri, TemplateFile, or TemplateSpecId must be provided.");
            }

            int parameterSources = (string.IsNullOrWhiteSpace(ParametersUri) ? 0 : 1)
                                  + (string.IsNullOrWhiteSpace(ParametersFile) ? 0 : 1);
            if (parameterSources > 1)
            {
                throw new ArgumentException("Provide at most one of ParametersUri or ParametersFile.");
            }

            if (!string.IsNullOrWhiteSpace(Mode)
                && !Mode.Equals("Incremental", StringComparison.OrdinalIgnoreCase)
                && !Mode.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Mode must be either 'Incremental' or 'Complete'.");
            }
        }

        private static DeploymentMode ParseMode(string? mode) =>
            string.IsNullOrWhiteSpace(mode)
                ? DeploymentMode.Incremental
                : Enum.Parse<DeploymentMode>(mode, ignoreCase: true);

        private object? BuildTemplate(out TemplateLink? templateLink)
        {
            templateLink = null;

            if (!string.IsNullOrWhiteSpace(TemplateUri))
            {
                templateLink = new TemplateLink
                {
                    Uri = TemplateUri,
                    QueryString = string.IsNullOrWhiteSpace(QueryString) ? null : QueryString,
                };
                return null;
            }

            if (!string.IsNullOrWhiteSpace(TemplateSpecId))
            {
                templateLink = new TemplateLink
                {
                    Id = TemplateSpecId,
                    QueryString = string.IsNullOrWhiteSpace(QueryString) ? null : QueryString,
                };
                return null;
            }

            // Local file: read and inline. A .bicep source is compiled to ARM JSON first.
            string templatePath = TemplateFile!;
            if (IsBicepFile(templatePath))
            {
                EnsureFileExists(templatePath);
                string armJson = BicepCompiler.BuildTemplate(templatePath);
                return JsonSerializer.Deserialize<JsonElement>(armJson);
            }

            return ReadJsonFile(templatePath);
        }

        private object? BuildParameters(out ParametersLink? parametersLink)
        {
            parametersLink = null;

            if (!string.IsNullOrWhiteSpace(ParametersUri))
            {
                parametersLink = new ParametersLink { Uri = ParametersUri };
                return null;
            }

            if (!string.IsNullOrWhiteSpace(ParametersFile))
            {
                // A .bicepparam source is compiled to an ARM parameters file first.
                if (IsBicepParamFile(ParametersFile))
                {
                    EnsureFileExists(ParametersFile);
                    return ReadBicepParamFile(ParametersFile);
                }

                return ReadParametersFile(ParametersFile);
            }

            return null;
        }

        private static object ReadJsonFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}", path);
            }
            string content = File.ReadAllText(path);
            return JsonSerializer.Deserialize<JsonElement>(content);
        }

        // ARM parameters files are wrapped as { "$schema": ..., "contentVersion": ..., "parameters": { ... } }.
        // The deployment body expects the inner "parameters" object. Unwrap it when present.
        private static object ReadParametersFile(string path)
        {
            JsonElement root = (JsonElement)ReadJsonFile(path);
            return UnwrapParameters(root);
        }

        // Compile a .bicepparam file to ARM parameters JSON and unwrap to the inner "parameters" object.
        // 'bicep build-params --stdout' emits an envelope:
        //   { "parametersJson": "<stringified ARM params file>", "templateJson": ..., "templateSpecId": ... }
        // The ARM params file inside "parametersJson" has the standard { $schema, contentVersion, parameters } shape.
        private static object ReadBicepParamFile(string path)
        {
            string envelopeJson = BicepCompiler.BuildParams(path);
            using JsonDocument envelope = JsonDocument.Parse(envelopeJson);
            if (!envelope.RootElement.TryGetProperty("parametersJson", out JsonElement parametersJsonElement)
                || parametersJsonElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "Unexpected 'bicep build-params --stdout' output: missing 'parametersJson' string.");
            }

            string parametersJson = parametersJsonElement.GetString()!;
            JsonElement root = JsonSerializer.Deserialize<JsonElement>(parametersJson);
            return UnwrapParameters(root);
        }

        private static object UnwrapParameters(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("parameters", out JsonElement inner))
            {
                return inner;
            }
            return root;
        }

        private static bool IsBicepFile(string path) =>
            path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);

        private static bool IsBicepParamFile(string path) =>
            path.EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase);

        private static void EnsureFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}", path);
            }
        }
    }
}
