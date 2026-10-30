// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     A deployment as the PowerShell subscription deployment cmdlets output it. Mirrors
    //     Az.Resources PSDeployment, flattening the ARM deployment properties to the top level.
    public class PSDeployment
    {
        // The ID of the deployment.
        public string? Id { get; set; }

        // The name of the deployment.
        public string? DeploymentName { get; set; }

        // The location of the deployment metadata.
        public string? Location { get; set; }

        // The management group of the deployment. Not set at the subscription scope.
        public string? ManagementGroupId { get; set; }

        // The resource group of the deployment. Not set at the subscription scope.
        public string? ResourceGroupName { get; set; }

        // The correlation ID of the deployment.
        public string? CorrelationId { get; set; }

        // The state of the provisioning.
        public string? ProvisioningState { get; set; }

        // The timestamp of the template deployment.
        public DateTime Timestamp { get; set; }

        // The deployment mode. Incremental or Complete.
        public DeploymentMode? Mode { get; set; }

        // The URI referencing the template.
        public TemplateLink? TemplateLink { get; set; }

        // The template link rendered as text.
        public string? TemplateLinkString { get; set; }

        // The debug log level of the deployment.
        public string? DeploymentDebugLogLevel { get; set; }

        // Deployment parameters.
        public Dictionary<string, DeploymentVariable>? Parameters { get; set; }

        // Deployment parameters rendered as a table.
        public string? ParametersString => PSDeploymentFormat.VariableTable(Parameters);

        // Deployment outputs.
        public Dictionary<string, DeploymentVariable>? Outputs { get; set; }

        // Deployment outputs rendered as a table.
        public string? OutputsString => PSDeploymentFormat.VariableTable(Outputs);

        // The deployment on error behavior.
        public OnErrorDeployment? OnErrorDeployment { get; set; }

        // Deployment tags.
        public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        public static PSDeployment From(DeploymentExtended deployment)
        {
            DeploymentPropertiesExtended? properties = deployment.Properties;
            return new()
            {
                Id = deployment.Id,
                DeploymentName = deployment.Name,
                Location = deployment.Location,
                Tags = deployment.Tags is null ? new Dictionary<string, string>() : new Dictionary<string, string>(deployment.Tags),
                Mode = properties?.Mode,
                ProvisioningState = properties?.ProvisioningState,
                TemplateLink = properties?.TemplateLink,
                TemplateLinkString = PSDeploymentFormat.TemplateLinkView(properties?.TemplateLink),
                Timestamp = properties?.Timestamp ?? default,
                CorrelationId = properties?.CorrelationId,
                DeploymentDebugLogLevel = properties?.DebugSetting?.DetailLevel is { Length: > 0 } level ? level : null,
                Outputs = properties?.Outputs,
                Parameters = properties?.Parameters,
                OnErrorDeployment = properties?.OnErrorDeployment,
            };
        }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    // The text views Az.Resources builds for a deployment's template link, parameters and outputs.
    internal static class PSDeploymentFormat
    {
        private static readonly JsonSerializerOptions s_valueJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        internal static string? TemplateLinkView(TemplateLink? templateLink)
        {
            if (templateLink is null)
            {
                return null;
            }

            var result = new StringBuilder();
            result.AppendLine();
            result.AppendLine(string.Format("{0, -15}: {1}", "Uri", templateLink.Uri));
            result.AppendLine(string.Format("{0, -15}: {1}", "ContentVersion", templateLink.ContentVersion));
            return result.ToString();
        }

        internal static string? VariableTable(Dictionary<string, DeploymentVariable>? variables)
        {
            if (variables is null)
            {
                return null;
            }

            var result = new StringBuilder();
            if (variables.Count == 0)
            {
                return result.ToString();
            }

            int nameWidth = Math.Max(15, variables.Keys.Max(k => k.Length + 2));
            int typeWidth = Math.Max(25, variables.Values.Max(v => (v?.Type?.Length ?? 0) + 2));
            string rowFormat = "{0, -" + nameWidth + "}  {1, -" + typeWidth + "}  {2, -10}\r\n";

            result.AppendLine();
            result.AppendFormat(rowFormat, "Name", "Type", "Value");
            result.AppendFormat(rowFormat, new string('=', nameWidth), new string('=', typeWidth), new string('=', 10));
            foreach (KeyValuePair<string, DeploymentVariable> pair in variables)
            {
                result.AppendFormat(rowFormat, pair.Key, pair.Value?.Type, JsonSerializer.Serialize(pair.Value?.Value, s_valueJson));
            }
            return result.ToString();
        }
    }

    internal static class PSDeploymentOutput
    {
        /// <summary>
        /// Wraps an output writer so a written <see cref="DeploymentExtended"/>, or set of them, is passed on as
        /// <see cref="PSDeployment"/> in the same order; any other value passes through unchanged.
        /// </summary>
        internal static OutputStreamDelegate Project(OutputStreamDelegate writeOutput) =>
            (value, cancellationToken) => writeOutput(
                value switch
                {
                    DeploymentExtended deployment => PSDeployment.From(deployment),
                    IEnumerable<DeploymentExtended> deployments => deployments.Select(PSDeployment.From).ToList(),
                    _ => value,
                },
                cancellationToken);
    }
}
