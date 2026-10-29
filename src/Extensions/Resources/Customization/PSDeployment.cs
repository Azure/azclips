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
    //     A deployment as the PowerShell deployment cmdlets output it, like legacy Az.Resources PSDeployment.
    //     Shared by every deployment scope; the scope supplies ManagementGroupId or ResourceGroupName when it has one.
    public class PSDeployment
    {
        public string? Id { get; set; }

        public string? DeploymentName { get; set; }

        public string? Location { get; set; }

        public string? ManagementGroupId { get; set; }

        public string? ResourceGroupName { get; set; }

        public OnErrorDeployment? OnErrorDeployment { get; set; }

        public string? CorrelationId { get; set; }

        public string? ProvisioningState { get; set; }

        public DateTime Timestamp { get; set; }

        public DeploymentMode? Mode { get; set; }

        public TemplateLink? TemplateLink { get; set; }

        // TemplateLink as "Uri" and "ContentVersion" rows.
        public string? TemplateLinkString { get; set; }

        public string? DeploymentDebugLogLevel { get; set; }

        public Dictionary<string, DeploymentVariable>? Parameters { get; set; }

        public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        // Parameters as a "Name Type Value" table.
        public string? ParametersString => DeploymentVariableTable.Format(Parameters);

        public Dictionary<string, DeploymentVariable>? Outputs { get; set; }

        // Outputs as a "Name Type Value" table.
        public string? OutputsString => DeploymentVariableTable.Format(Outputs);

        public static PSDeployment From(DeploymentExtended deployment, string? managementGroupId = null, string? resourceGroupName = null)
        {
            DeploymentPropertiesExtended? properties = deployment.Properties;
            TemplateLink? templateLink = properties?.TemplateLink;
            return new()
            {
                Id = deployment.Id,
                DeploymentName = deployment.Name,
                Location = deployment.Location,
                ManagementGroupId = managementGroupId,
                ResourceGroupName = resourceGroupName,
                Tags = deployment.Tags is null ? new Dictionary<string, string>() : new Dictionary<string, string>(deployment.Tags),
                OnErrorDeployment = properties?.OnErrorDeployment,
                CorrelationId = properties?.CorrelationId,
                ProvisioningState = properties?.ProvisioningState,
                Timestamp = properties?.Timestamp ?? default,
                Mode = properties?.Mode,
                TemplateLink = templateLink,
                TemplateLinkString = templateLink is null
                    ? null
                    : $"{Environment.NewLine}{"Uri",-15}: {templateLink.Uri}{Environment.NewLine}{"ContentVersion",-15}: {templateLink.ContentVersion}{Environment.NewLine}",
                DeploymentDebugLogLevel = properties?.DebugSetting?.DetailLevel is { Length: > 0 } level ? level : null,
                Parameters = properties?.Parameters,
                Outputs = properties?.Outputs,
            };
        }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    internal static class DeploymentVariableTable
    {
        private static readonly JsonSerializerOptions ValueOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        internal static string? Format(Dictionary<string, DeploymentVariable>? variables)
        {
            if (variables is null)
            {
                return null;
            }
            if (variables.Count == 0)
            {
                return string.Empty;
            }

            int nameWidth = Math.Max(15, variables.Keys.Max(k => k.Length + 2));
            int typeWidth = Math.Max(25, variables.Values.Max(v => (v?.Type?.Length ?? 0) + 2));
            string Row(string name, string type, string value) => $"{name.PadRight(nameWidth)}  {type.PadRight(typeWidth)}  {value,-10}\r\n";

            StringBuilder table = new StringBuilder().AppendLine()
                .Append(Row("Name", "Type", "Value"))
                .Append(Row(new string('=', nameWidth), new string('=', typeWidth), new string('=', 10)));
            foreach ((string name, DeploymentVariable? variable) in variables)
            {
                table.Append(Row(name, variable?.Type ?? string.Empty, JsonSerializer.Serialize(variable?.Value, ValueOptions)));
            }
            return table.ToString();
        }
    }

    internal static class PSDeploymentOutput
    {
        /// <summary>
        /// Wraps an output writer so a written <see cref="DeploymentExtended"/>, or set of them, is passed on as
        /// <see cref="PSDeployment"/> in the same order; any other value passes through unchanged.
        /// </summary>
        internal static OutputStreamDelegate Project(OutputStreamDelegate writeOutput, Func<DeploymentExtended, PSDeployment>? convert = null)
        {
            convert ??= deployment => PSDeployment.From(deployment);
            return (value, cancellationToken) => writeOutput(
                value switch
                {
                    DeploymentExtended deployment => convert(deployment),
                    IEnumerable<DeploymentExtended> deployments => deployments.Select(convert).ToList(),
                    _ => value,
                },
                cancellationToken);
        }
    }
}
