// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Legacy PS <c>PSDeployment</c> (<c>DeploymentExtended.ToPSDeployment</c>), output by the MG deployment cmdlets.
/// </summary>
public class PSDeployment
{
    private static readonly JsonSerializerOptions s_valueJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string? Id { get; set; }
    public string? Location { get; set; }
    public string? ManagementGroupId { get; set; }
    public string? ResourceGroupName { get; set; }
    public OnErrorDeploymentExtended? OnErrorDeployment { get; set; }
    public string? DeploymentName { get; set; }
    public string? CorrelationId { get; set; }
    public string? ProvisioningState { get; set; }
    public DateTime Timestamp { get; set; }
    public DeploymentMode Mode { get; set; }
    public TemplateLink? TemplateLink { get; set; }
    public string? TemplateLinkString { get; set; }
    public string? DeploymentDebugLogLevel { get; set; }
    public Dictionary<string, DeploymentVariable>? Parameters { get; set; }
    public IDictionary<string, string>? Tags { get; set; }
    public string? ParametersString => ToTable(Parameters);
    public Dictionary<string, DeploymentVariable>? Outputs { get; set; }
    public string? OutputsString => ToTable(Outputs);

    internal static bool ForPowerShell =>
        AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType == ClientType.PowerShell;

    public static PSDeployment From(DeploymentExtended deployment, string managementGroupId)
    {
        DeploymentPropertiesExtended? properties = deployment.Properties;
        TemplateLink? templateLink = properties?.TemplateLink;
        string? detailLevel = properties?.DebugSetting?.DetailLevel;
        return new PSDeployment
        {
            Id = deployment.Id,
            DeploymentName = deployment.Name,
            Location = deployment.Location,
            ManagementGroupId = managementGroupId,
            OnErrorDeployment = properties?.OnErrorDeployment,
            Tags = deployment.Tags is null ? [] : new Dictionary<string, string>(deployment.Tags),
            Mode = properties?.Mode ?? default,
            ProvisioningState = properties?.ProvisioningState,
            TemplateLink = templateLink,
            TemplateLinkString = templateLink is null ? null
                : $"{Environment.NewLine}{"Uri",-15}: {templateLink.Uri}{Environment.NewLine}{"ContentVersion",-15}: {templateLink.ContentVersion}{Environment.NewLine}",
            Timestamp = properties?.Timestamp ?? default,
            CorrelationId = properties?.CorrelationId,
            DeploymentDebugLogLevel = string.IsNullOrEmpty(detailLevel) ? null : detailLevel,
            Parameters = ToVariables(properties?.Parameters),
            Outputs = ToVariables(properties?.Outputs),
        };
    }

    private static Dictionary<string, DeploymentVariable>? ToVariables(object? json) =>
        json is JsonElement { ValueKind: JsonValueKind.Object } element
            ? element.EnumerateObject().ToDictionary(
                p => p.Name,
                p => new DeploymentVariable
                {
                    Type = p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("type", out JsonElement type) ? type.GetString() : null,
                    Value = p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("value", out JsonElement value) ? ToObject(value) : null,
                })
            : null;

    private static object? ToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => element.TryGetInt64(out long l) ? (object)l : element.GetDouble(),
        _ => null,
    };

    // Legacy PS ResourcesExtensions.ConstructDeploymentVariableTable.
    private static string? ToTable(Dictionary<string, DeploymentVariable>? variables)
    {
        if (variables is null)
        {
            return null;
        }
        StringBuilder result = new();
        if (variables.Count > 0)
        {
            int nameWidth = Math.Max(15, variables.Keys.Max(k => k.Length + 2));
            int typeWidth = Math.Max(25, variables.Values.Max(v => (v.Type?.Length ?? 0) + 2));
            string Row(string? name, string? type, string? value) =>
                $"{(name ?? string.Empty).PadRight(nameWidth)}  {(type ?? string.Empty).PadRight(typeWidth)}  {(value ?? string.Empty).PadRight(10)}\r\n";
            result.AppendLine();
            result.Append(Row("Name", "Type", "Value"));
            result.Append(Row(new string('=', nameWidth), new string('=', typeWidth), new string('=', 10)));
            foreach ((string name, DeploymentVariable variable) in variables)
            {
                result.Append(Row(name, variable.Type, JsonSerializer.Serialize(variable.Value, s_valueJson)));
            }
        }
        return result.ToString();
    }
}

/// <summary>Legacy PS <c>DeploymentVariable</c>.</summary>
public class DeploymentVariable
{
    public string? Type { get; set; }

    public object? Value { get; set; }

    public override string ToString()
    {
        int typeWidth = Math.Max(10, (Type?.Length ?? 0) + 2);
        string Row(string? type, string? value) =>
            $"{(type ?? string.Empty).PadRight(typeWidth)}  {(value ?? string.Empty).PadRight(10)}{Environment.NewLine}";
        return Environment.NewLine + Row("Type", "Value") + Row(new string('-', typeWidth), new string('-', 10)) + Row(Type, Value?.ToString()?.Trim());
    }
}
