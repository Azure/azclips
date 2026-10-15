// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Legacy Az.Resources <c>PSDeployment</c> shape returned by the *-AzManagementGroupDeployment cmdlets.</summary>
public class PSDeployment
{
    public string? Id { get; set; }
    public string? DeploymentName { get; set; }
    public string? ResourceGroupName { get; set; }
    public string? ManagementGroupId { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public DateTime Timestamp { get; set; }
    public DeploymentMode Mode { get; set; }
    public TemplateLink? TemplateLink { get; set; }
    public string? TemplateLinkString { get; set; }
    public Dictionary<string, DeploymentVariable>? Parameters { get; set; }
    public string? ParametersString => ToTable(Parameters);
    public Dictionary<string, DeploymentVariable>? Outputs { get; set; }
    public string? OutputsString => ToTable(Outputs);
    public string? DeploymentDebugLogLevel { get; set; }
    public string? CorrelationId { get; set; }
    public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    public OnErrorDeploymentExtended? OnErrorDeployment { get; set; }

    internal static bool ForPowerShell =>
        AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType == ClientType.PowerShell;

    public static PSDeployment From(DeploymentExtended deployment, string? managementGroupId = null)
    {
        var p = deployment.Properties;
        return new PSDeployment
        {
            Id = deployment.Id,
            DeploymentName = deployment.Name,
            Location = deployment.Location,
            ManagementGroupId = managementGroupId,
            Tags = deployment.Tags is null ? new Dictionary<string, string>() : new Dictionary<string, string>(deployment.Tags),
            Mode = p?.Mode ?? default,
            ProvisioningState = p?.ProvisioningState,
            TemplateLink = p?.TemplateLink,
            TemplateLinkString = p?.TemplateLink is { } link
                ? $"{Environment.NewLine}{"Uri",-15}: {link.Uri}{Environment.NewLine}{"ContentVersion",-15}: {link.ContentVersion}{Environment.NewLine}"
                : null,
            Timestamp = p?.Timestamp ?? default,
            CorrelationId = p?.CorrelationId,
            DeploymentDebugLogLevel = string.IsNullOrEmpty(p?.DebugSetting?.DetailLevel) ? null : p.DebugSetting.DetailLevel,
            Outputs = p?.Outputs,
            Parameters = p?.Parameters,
            OnErrorDeployment = p?.OnErrorDeployment,
        };
    }

    private static readonly JsonSerializerOptions s_valueJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Legacy formatter: ResourcesExtensions.ConstructDeploymentVariableTable.
    private static string? ToTable(Dictionary<string, DeploymentVariable>? variables)
    {
        if (variables is null) return null;

        var nameWidth = Math.Max(15, variables.Keys.Select(k => k.Length + 2).DefaultIfEmpty(0).Max());
        var typeWidth = Math.Max(25, variables.Values.Select(v => (v.Type ?? string.Empty).Length + 2).DefaultIfEmpty(0).Max());
        var result = new StringBuilder();
        if (variables.Count > 0)
        {
            string Row(string name, string type, string value) => $"{name.PadRight(nameWidth)}  {type.PadRight(typeWidth)}  {value,-10}\r\n";
            var indent = new string(' ', nameWidth + typeWidth + 4);
            result.AppendLine();
            result.Append(Row("Name", "Type", "Value"));
            result.Append(Row(new string('=', nameWidth), new string('=', typeWidth), new string('=', 10)));
            foreach (var (name, variable) in variables)
            {
                var value = JsonSerializer.Serialize(variable.Value, s_valueJson).Replace("\n", "\n" + indent).Trim();
                result.Append(Row(name, variable.Type ?? string.Empty, value));
            }
        }
        return result.ToString();
    }
}
