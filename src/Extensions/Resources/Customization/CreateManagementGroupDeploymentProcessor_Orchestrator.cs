// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary><c>az deployment mg create</c>: legacy CLI naming, --what-if/--confirm-with-what-if, errors and output.</summary>
public class CreateManagementGroupDeploymentProcessor_Orchestrator : NewManagementGroupDeploymentProcessor_Orchestrator
{
    public bool ConfirmWithWhatIf { get; set; }

    public bool ProceedIfNoChange { get; set; }

    protected override string DefaultDeploymentName() => CliDefaultDeploymentName();

    protected override bool PromptsForMissingParameters => true;

    // Legacy CLI: show what-if, stop for --what-if, ask before deploying for --confirm-with-what-if.
    protected override async Task<bool> PreviewAndStopAsync()
    {
        if (!WhatIf && !ConfirmWithWhatIf)
        {
            return false;
        }
        (WhatIfOperationResult? result, string? raw) = await RunWhatIfAsync();
        if (result?.Error is { } error)
        {
            throw new InvalidOperationException(BuildPreflightErrorMessage(error));
        }
        // ponytail: legacy pretty-prints the what-if; JSON until the pretty printer is added.
        JsonObject? preview = WhatIfManagementGroupDeploymentProcessor_Orchestrator.ToCliResult(raw, WhatIfExcludeChangeType);
        if (preview is not null)
        {
            await WriteOutput(preview);
        }
        if (WhatIf)
        {
            return true;
        }
        bool hasChange = preview?["changes"] is JsonArray changes && changes.Any(c => IsChange((string?)c?["changeType"]));
        if (!ProceedIfNoChange || hasChange)
        {
            if (Console.IsInputRedirected)
            {
                throw new InvalidOperationException("Unable to prompt for confirmation as no tty available.");
            }
            if (!CliPrompt.Bool("\nAre you sure you want to execute the deployment?", "y", "n"))
            {
                return true;
            }
        }
        await AskMissingParametersAgainAsync(DeploymentProperties);
        return false;
    }

    protected override Task OnValidationFailedAsync(ErrorResponse error, bool httpFailed) =>
        throw new InvalidOperationException(httpFailed ? BuildHttpResponseErrorMessage(error) : BuildPreflightErrorMessage(error));

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtended? body)
    {
        if (FailureOf(body) is { } failure)
        {
            throw new InvalidOperationException(ToODataV4String(failure));
        }
        if (body is not null)
        {
            await WriteOutput(body);
        }
        return true;
    }

    // Legacy azure-core ODataV4Format.__str__ for a failed long-running operation (its spacing quirks included).
    private static string ToODataV4String(ErrorResponse error)
    {
        StringBuilder text = new($"({error.Code}) {error.Message}\nCode: {error.Code}\nMessage: {error.Message}");
        if (!string.IsNullOrEmpty(error.Target))
        {
            text.Append($"\nTarget: {error.Target}");
        }
        if (error.Details is { Count: > 0 })
        {
            text.Append("\nException Details:");
            foreach (ErrorResponse detail in error.Details)
            {
                text.Append(string.Join('\n', ToODataV4String(detail).Split('\n').Select(line => "\t" + line)));
            }
        }
        return text.ToString();
    }
}
