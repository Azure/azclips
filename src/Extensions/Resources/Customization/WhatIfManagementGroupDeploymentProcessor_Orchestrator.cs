// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg what-if</c> (legacy <c>_what_if_deploy_arm_template_at_management_group_core</c>): POST whatIf in
/// Incremental mode, raise on a template error (returned with HTTP 200), drop <c>--exclude-change-types</c>, then write
/// the pretty-printed text, or the legacy flattened result with <c>--no-pretty-print</c>.
/// </summary>
public class WhatIfManagementGroupDeploymentProcessor_Orchestrator : ManagementGroupDeploymentOrchestratorBase<ScopedDeploymentWhatIf, WhatIfOperationResult>
{
    private static readonly string[] s_resultFormats = ["FullResourcePayloads", "ResourceIdOnly"];
    private static readonly string[] s_changeTypes = ["Create", "Delete", "Deploy", "Ignore", "Modify", "NoChange", "Unsupported"];

    public string? ResultFormat { get; set; }

    public string[]? ExcludeChangeTypes { get; set; }

    /// <summary>CLI <c>--no-pretty-print</c>: write the JSON result instead of the formatted text.</summary>
    public bool NoPrettyPrint { get; set; }

    private string? _rawBody;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        CheckExcludeChangeTypes(ExcludeChangeTypes);
        ResolveName();
        ScopedDeploymentWhatIf body = CreateWhatIfBody(Location, await BuildPropertiesAsync(), ResultFormat);
        HttpResponseMessage response = await SendWhatIfAsync(body);
        _rawBody = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        return response;
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(WhatIfOperationResult? body)
    {
        if (body?.Error is { } error)
        {
            throw new InvalidOperationException(BuildPreflightErrorMessage(error));
        }
        if (body is not null && ToCliResult(_rawBody, ExcludeChangeTypes) is { } result)
        {
            await WriteOutput(NoPrettyPrint ? result : DeploymentWhatIfFormatter.Format(result, cli: true, DeploymentWhatIfFormatter.CliColor()));
        }
        return true;
    }

    // Legacy argparse choices (get_enum_type, case-insensitive); the CLI parser only checks single string values.
    internal static void CheckExcludeChangeTypes(string[]? types, string parameterName = "exclude-change-types")
    {
        foreach (string type in types ?? [])
        {
            if (!s_changeTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Parameter '{parameterName}' value '{type}' is not allowed. Allowed values: {string.Join(", ", s_changeTypes)}");
            }
        }
    }

    // Legacy _prepare_deployment_what_if_properties: always Incremental; result format defaults to FullResourcePayloads.
    internal static ScopedDeploymentWhatIf CreateWhatIfBody(string location, DeploymentProperties properties, string? resultFormat) => new()
    {
        Location = location,
        Properties = new DeploymentWhatIfProperties
        {
            Mode = DeploymentMode.Incremental,
            Template = properties.Template,
            TemplateLink = properties.TemplateLink,
            Parameters = properties.Parameters,
            ParametersLink = properties.ParametersLink,
            ValidationLevel = properties.ValidationLevel,
            WhatIfSettings = new DeploymentWhatIfSettings
            {
                ResultFormat = s_resultFormats.FirstOrDefault(f => f.Equals(resultFormat, StringComparison.OrdinalIgnoreCase)) ?? s_resultFormats[0],
            },
        },
    };

    // Built from the raw body because the generated model drops change fields (identifiers, extension, ...).
    // Legacy CLI (SDK WhatIfOperationResult) flattens "properties" into the top level.
    internal static JsonObject? ToCliResult(string? rawBody, string[]? excludeChangeTypes)
    {
        if (string.IsNullOrWhiteSpace(rawBody) || JsonNode.Parse(rawBody) is not JsonObject raw)
        {
            return null;
        }
        JsonNode? properties = raw["properties"];
        JsonObject result = new()
        {
            ["changes"] = properties?["changes"]?.DeepClone(),
            ["diagnostics"] = properties?["diagnostics"]?.DeepClone(),
            ["error"] = raw["error"]?.DeepClone(),
            ["potentialChanges"] = properties?["potentialChanges"]?.DeepClone(),
            ["status"] = raw["status"]?.DeepClone(),
        };
        if (excludeChangeTypes is { Length: > 0 } && result["changes"] is JsonArray changes)
        {
            foreach (JsonNode? change in changes.ToList())
            {
                if (excludeChangeTypes.Contains((string?)change?["changeType"], StringComparer.OrdinalIgnoreCase))
                {
                    changes.Remove(change);
                }
            }
        }
        return result;
    }
}
