// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary><c>az deployment mg validate</c>. <see cref="TestManagementGroupDeploymentProcessor_Orchestrator"/> adds the PS behavior.</summary>
public class ValidateManagementGroupDeploymentProcessor_Orchestrator
    : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    private bool _httpFailed;

    protected virtual string DefaultDeploymentName() => CliDefaultDeploymentName();

    protected override bool PromptsForMissingParameters => true;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = DefaultDeploymentName();
        }
        ScopedDeployment body = new() { Location = Location, Properties = await BuildPropertiesAsync() };
        HttpResponseMessage response = await CreateExtendedProcessor<ValidationResultProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = body;
        }).SendAsync();
        _httpFailed = !response.IsSuccessStatusCode;
        return response;
    }

    // Legacy CLI raises on both an HTTP validation failure and a 200 result carrying an error, with different formats.
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (body?.Error is { } error)
        {
            throw new InvalidOperationException(_httpFailed ? BuildHttpResponseErrorMessage(error) : BuildPreflightErrorMessage(error));
        }
        if (body is not null)
        {
            await WriteOutput(body);
        }
        return true;
    }

    // Returns a 4xx validation failure (ARM error body) as the result instead of throwing, so each surface reports it like its legacy.
    internal sealed class ValidationResultProcessor : ValidateAtManagementGroupScopeDeploymentsProcessor
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
                // ponytail: a failure reported only at the end of polling still surfaces as a runtime error.
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
/// <c>Test-AzManagementGroupDeployment</c>: like legacy PS, writes validation errors as output instead of
/// throwing, outputs nothing on success, and shows diagnostics as a warning unless suppressed.
/// </summary>
public class TestManagementGroupDeploymentProcessor_Orchestrator : ValidateManagementGroupDeploymentProcessor_Orchestrator
{
    public bool SuppressDiagnostics { get; set; }

    protected override bool PromptsForMissingParameters => false;

    // Legacy PS rule: template URI/file basename without extension, else a new GUID.
    protected override string DefaultDeploymentName()
    {
        string? source = string.IsNullOrWhiteSpace(TemplateUri) ? TemplateFile : TemplateUri;
        return string.IsNullOrWhiteSpace(source) ? Guid.NewGuid().ToString() : Path.GetFileNameWithoutExtension(source);
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (body?.Error is { } error)
        {
            await WriteOutput(new List<ErrorResponse> { error });
            return true;
        }
        Verbose("Template is valid.");
        if (!SuppressDiagnostics && body?.Properties?.Diagnostics is { Count: > 0 } diagnostics)
        {
            Warn($"\n\nDiagnostics ({diagnostics.Count}): \n" + string.Concat(diagnostics.Select(d => $"({d.Target}) {d.Message} ({d.Code})\n")));
        }
        return true;
    }

}
