// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant validate</c>: POST validate; an HTTP validation failure or a result carrying an error is
/// raised like legacy CLI, otherwise the raw result is written.
/// <see cref="ValidateTenantDeploymentProcessor_PSOrchestrator"/> adds the <c>Test-AzTenantDeployment</c> behavior.
/// </summary>
public class ValidateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    private bool _httpFailed;

    // Raw ARM body: the generated model drops fields (providers, validatedResources, diagnostics, ...).
    protected string? RawBody { get; private set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        string name = ResolveName();
        ScopedDeployment body = BuildDeployment(await BuildPropertiesAsync());
        HttpResponseMessage response = await CreateExtendedProcessor<ValidationResultProcessor>(p =>
        {
            p.DeploymentName = name;
            p.Body = body;
        }).SendAsync();
        _httpFailed = !response.IsSuccessStatusCode;
        RawBody = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        return response;
    }

    // Legacy CLI raises on both an HTTP validation failure and a 200 result carrying an error, with different formats.
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
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

    // Returns a 4xx validation failure (ARM error body) as the result instead of throwing, so each surface reports it like its legacy.
    internal sealed class ValidationResultProcessor : ValidateAtTenantScopeDeploymentsProcessor
    {
        internal override async Task<HttpResponseMessage> SendAsync()
        {
            HttpResponseMessage response;
            using (HttpRequestMessage request = await CreateRequestMessage())
            {
                response = await HttpPipeline.SendAsync(request, Publisher);
            }
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);

            if (response.StatusCode == HttpStatusCode.Accepted && GetLocationUri(response) is { } location)
            {
                response.Dispose();
                return await HttpRuntime.SendAsyncLRO(
                    () => Task.FromResult(new HttpRequestMessage(HttpMethod.Get, location)),
                    Publisher, HttpPipeline, GetFinalUri, GetOperationLocationUri, GetAsyncOperationUri, GetLocationUri);
            }
            if ((int)response.StatusCode is < 400 or >= 500)
            {
                try
                {
                    await response.EnsureSuccessAsync(Publisher);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }
            }
            return response;
        }
    }
}

/// <summary>
/// <c>Test-AzTenantDeployment</c>: like legacy PS, writes validation errors as a <see cref="PSResourceManagerError"/> list
/// instead of throwing, outputs nothing on success, and shows diagnostics as a warning unless suppressed.
/// </summary>
public class ValidateTenantDeploymentProcessor_PSOrchestrator : ValidateTenantDeploymentProcessor_Orchestrator
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
