// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg validate</c>: POST validate; an HTTP validation failure or a result carrying an error is
/// raised like legacy CLI, otherwise the raw result is written.
/// <see cref="TestManagementGroupDeploymentProcessor_Orchestrator"/> adds the <c>Test-AzManagementGroupDeployment</c> behavior.
/// </summary>
public class ValidateManagementGroupDeploymentProcessor_Orchestrator : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
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
            p.GroupId = ManagementGroupId;
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
