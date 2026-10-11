// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>Test-AzManagementGroupDeployment</c>: like legacy PS, writes validation errors as a <see cref="PSResourceManagerError"/> list
/// instead of throwing, outputs nothing on success, and shows diagnostics as a warning unless suppressed.
/// </summary>
public class TestManagementGroupDeploymentProcessor_Orchestrator : ValidateManagementGroupDeploymentProcessor_Orchestrator
{
    private const string Reset = "\u001b[0m";

    public bool SuppressDiagnostics { get; set; }

    protected override bool IsCli => false;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (body?.Error is { } error)
        {
            await WriteOutput(new List<PSResourceManagerError> { PSResourceManagerError.From(error) });
            return true;
        }
        Verbose("Template is valid.");
        if (!SuppressDiagnostics && ReadDiagnostics() is { Count: > 0 } diagnostics)
        {
            Warn(FormatDiagnostics(diagnostics));
        }
        return true;
    }

    // Legacy WhatIfOperationResultFormatter.FormatDiagnostics: one line per diagnostic, colored by level.
    private static string FormatDiagnostics(List<Diagnostic> diagnostics)
    {
        string newLine = Environment.NewLine;
        StringBuilder builder = new($"{newLine}{newLine}Diagnostics ({diagnostics.Count}): {newLine}");
        foreach (Diagnostic d in diagnostics)
        {
            string color = d.Level switch
            {
                "Error" => "\u001b[38;5;203m",
                "Warning" => "\u001b[38;5;136m",
                "Info" => Reset,
                _ => "\u001b[38;5;246m",
            };
            builder.Append(color).Append($"({d.Target}) {d.Message} ({d.Code}){newLine}").Append(Reset);
        }
        return builder.ToString();
    }

    // The generated DeploymentValidateResult model has no diagnostics, so read them from the raw body.
    private List<Diagnostic>? ReadDiagnostics() =>
        string.IsNullOrWhiteSpace(RawBody) ? null : JsonSerializer.Deserialize<DiagnosticsResult>(RawBody, JsonOptions)?.Properties?.Diagnostics;

    private sealed class DiagnosticsResult
    {
        public DiagnosticsProperties? Properties { get; set; }
    }

    private sealed class DiagnosticsProperties
    {
        public List<Diagnostic>? Diagnostics { get; set; }
    }

    private sealed class Diagnostic
    {
        public string? Level { get; set; }

        public string? Code { get; set; }

        public string? Message { get; set; }

        public string? Target { get; set; }
    }
}
