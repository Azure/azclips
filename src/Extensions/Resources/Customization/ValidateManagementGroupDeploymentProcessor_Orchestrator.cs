// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg validate</c> / <c>Test-AzManagementGroupDeployment</c>: POST validate. Only the result handling differs:
/// CLI raises a validation failure or a result carrying an error like legacy CLI, otherwise writes the raw result;
/// PS writes validation errors as a <see cref="PSResourceManagerError"/> list instead of throwing, outputs nothing on success,
/// and shows diagnostics as a warning unless suppressed.
/// </summary>
public class ValidateManagementGroupDeploymentProcessor_Orchestrator : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    private const string Reset = "\u001b[0m";

    private bool _httpFailed;

    /// <summary>PS <c>-SuppressDiagnostics</c>.</summary>
    public bool SuppressDiagnostics { get; set; }

    protected override bool IsCli => !PSDeployment.ForPowerShell;

    // Raw ARM body: the generated model drops fields (providers, validatedResources, diagnostics, ...).
    protected string? RawBody { get; private set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        string name = ResolveName();
        ScopedDeployment body = BuildDeployment(await BuildPropertiesAsync());
        HttpResponseMessage response = await CreateExtendedProcessor<ValidateAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = name;
            p.Body = body;
        }).SendAsyncAllowBadRequest();
        _httpFailed = !response.IsSuccessStatusCode;
        RawBody = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        return response;
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (!IsCli)
        {
            await WritePowerShellResultAsync(body);
            return true;
        }
        // Legacy CLI raises on both an HTTP validation failure and a 200 result carrying an error, with different formats.
        if (body?.Error is { } error)
        {
            throw new InvalidOperationException(_httpFailed ? BuildHttpResponseErrorMessage(error) : BuildPreflightErrorMessage(error));
        }
        if (body is not null && !string.IsNullOrWhiteSpace(RawBody))
        {
            await WriteOutput(JsonSerializer.Deserialize<JsonElement>(RawBody));
        }
        return true;
    }

    // Legacy Test-AzManagementGroupDeployment: errors are written, not thrown; success writes nothing but diagnostics.
    private async Task WritePowerShellResultAsync(DeploymentValidateResult? body)
    {
        if (body?.Error is { } error)
        {
            await WriteOutput(new List<PSResourceManagerError> { PSResourceManagerError.From(error) });
            return;
        }
        Verbose("Template is valid.");
        if (!SuppressDiagnostics && ReadDiagnostics() is { Count: > 0 } diagnostics)
        {
            Warn(FormatDiagnostics(diagnostics));
        }
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
