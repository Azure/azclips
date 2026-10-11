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
    public string? ParametersString => DeploymentVariable.ToTable(Parameters);
    public Dictionary<string, DeploymentVariable>? Outputs { get; set; }
    public string? OutputsString => DeploymentVariable.ToTable(Outputs);
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
            Outputs = DeploymentVariable.FromJson(p?.Outputs),
            Parameters = DeploymentVariable.FromJson(p?.Parameters),
            OnErrorDeployment = p?.OnErrorDeployment,
        };
    }
}

/// <summary>Legacy Az.Resources <c>DeploymentVariable</c>: a typed deployment parameter or output value.</summary>
public class DeploymentVariable
{
    private static readonly JsonSerializerOptions s_valueJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string Type { get; set; } = string.Empty;
    public object? Value { get; set; }

    internal static Dictionary<string, DeploymentVariable>? FromJson(object? json)
    {
        if (json is not JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        var result = new Dictionary<string, DeploymentVariable>();
        foreach (var entry in element.EnumerateObject())
        {
            var variable = new DeploymentVariable();
            if (entry.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in entry.Value.EnumerateObject())
                {
                    if (string.Equals(field.Name, "type", StringComparison.OrdinalIgnoreCase)) variable.Type = field.Value.GetString() ?? string.Empty;
                    else if (string.Equals(field.Name, "value", StringComparison.OrdinalIgnoreCase)) variable.Value = ToObject(field.Value);
                }
            }
            result[entry.Name] = variable;
        }
        return result;
    }

    // Legacy formatter: ResourcesExtensions.ConstructDeploymentVariableTable.
    internal static string? ToTable(Dictionary<string, DeploymentVariable>? variables)
    {
        if (variables is null) return null;

        var nameWidth = Math.Max(15, variables.Keys.Select(k => k.Length + 2).DefaultIfEmpty(0).Max());
        var typeWidth = Math.Max(25, variables.Values.Select(v => v.Type.Length + 2).DefaultIfEmpty(0).Max());
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
                result.Append(Row(name, variable.Type, value));
            }
        }
        return result.ToString();
    }

    private static object? ToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value)),
        _ => null,
    };
}
