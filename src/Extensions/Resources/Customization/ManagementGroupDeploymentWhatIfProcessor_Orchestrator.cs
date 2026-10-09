// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary><c>az deployment mg what-if</c>.</summary>
public class ManagementGroupDeploymentWhatIfProcessor_Orchestrator
    : ManagementGroupDeploymentOrchestratorBase<ScopedDeploymentWhatIf, WhatIfOperationResult>
{
    private static readonly string[] ChangeTypes = ["Create", "Delete", "Ignore", "Deploy", "NoChange", "Modify", "Unsupported"];

    public string? ResultFormat { get; set; }

    public string[]? ExcludeChangeTypes { get; set; }

    protected override bool PromptsForMissingParameters => true;

    private string? _rawBody;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        CheckExcludeChangeTypes(ExcludeChangeTypes);
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = CliDefaultDeploymentName();
        }
        ScopedDeploymentWhatIf body = CreateBody(Location, await BuildPropertiesAsync(), ResultFormat);
        HttpResponseMessage response = await CreateExtendedProcessor<WhatIfAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = body;
        }).SendAsync();
        _rawBody = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        return response;
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(WhatIfOperationResult? body)
    {
        // ARM returns 200 with an "error" payload for template errors (legacy _build_preflight_error_message).
        if (body?.Error is { } error)
        {
            throw new InvalidOperationException(BuildPreflightErrorMessage(error));
        }
        if (body is not null && ToCliResult(_rawBody, ExcludeChangeTypes) is { } result)
        {
            await WriteOutput(result);
        }
        return true;
    }

    internal static void CheckExcludeChangeTypes(string[]? types)
    {
        foreach (string type in types ?? [])
        {
            if (!ChangeTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"ExcludeChangeTypes value '{type}' is not allowed. Allowed values: {string.Join(", ", ChangeTypes)}");
            }
        }
    }

    // What-if always runs Incremental (legacy CLI and PS), whatever the deployment mode.
    internal static ScopedDeploymentWhatIf CreateBody(string location, DeploymentProperties properties, string? resultFormat) => new()
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
            // ponytail: casing is checked by the CLI AllowedValues; ARM default is FullResourcePayloads.
            WhatIfSettings = string.IsNullOrWhiteSpace(resultFormat) ? null : new DeploymentWhatIfSettings { ResultFormat = resultFormat },
        },
    };

    // Built from the raw body because the generated model drops change fields (identifiers, extension).
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
