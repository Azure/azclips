// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg create</c> (legacy <c>deploy_arm_template_at_management_group</c>): optional what-if preview and
/// confirmation, then validate and PUT. <see cref="NewManagementGroupDeploymentProcessor_Orchestrator"/> adds the
/// <c>New-AzManagementGroupDeployment</c> behavior.
/// </summary>
public class CreateManagementGroupDeploymentProcessor_Orchestrator : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    private static readonly string[] s_noChangeTypes = ["NoChange", "Ignore"];

    private ScopedDeployment _deployment = null!;

    /// <summary>CLI <c>-w</c>; PS sets it for the <c>-WhatIf</c>/<c>-Confirm</c> preview run.</summary>
    public bool WhatIf { get; set; }

    public bool ConfirmWithWhatIf { get; set; }

    public bool ProceedIfNoChange { get; set; }

    public string? WhatIfResultFormat { get; set; }

    public string[]? WhatIfExcludeChangeTypes { get; set; }

    /// <summary>CLI <c>--mode</c> (Incremental or Complete); legacy PS always deploys Incremental.</summary>
    public string? Mode { get; set; }

    // Raw final deployment body: the generated model drops fields, so the CLI writes this like legacy.
    protected string? RawBody { get; private set; }

    // Set when the deployment ends in the Failed state.
    protected bool Failed { get; private set; }

    /// <summary>Legacy: any change other than NoChange/Ignore in the (filtered) what-if result.</summary>
    public static bool HasChanges(JsonObject? whatIfResult) =>
        whatIfResult?["changes"] is JsonArray changes
        && changes.Any(c => !s_noChangeTypes.Contains((string?)c?["changeType"], StringComparer.OrdinalIgnoreCase));

    public override async Task ProcessAsync()
    {
        WhatIfManagementGroupDeploymentProcessor_Orchestrator.CheckExcludeChangeTypes(WhatIfExcludeChangeTypes, "what-if-exclude-change-types");
        ResolveName();
        if (await PreviewAndStopAsync())
        {
            return;
        }
        // Legacy CLI prepares the properties again after the preview, so missing parameters are asked twice.
        _deployment = await BuildDeploymentAsync();
        await base.ProcessAsync();
    }

    // Legacy CLI: show what-if, stop for --what-if, ask before deploying for --confirm-with-what-if.
    protected virtual async Task<bool> PreviewAndStopAsync()
    {
        if (!WhatIf && !ConfirmWithWhatIf)
        {
            return false;
        }
        (JsonObject? preview, ErrorResponse? error) = await RunWhatIfAsync();
        if (error is not null)
        {
            throw new InvalidOperationException(BuildPreflightErrorMessage(error));
        }
        if (preview is not null)
        {
            await WriteOutput(DeploymentWhatIfFormatter.Format(preview, cli: true, DeploymentWhatIfFormatter.CliColor()));
        }
        if (WhatIf)
        {
            return true;
        }
        if (!ProceedIfNoChange || HasChanges(preview))
        {
            if (Console.IsInputRedirected)
            {
                throw new InvalidOperationException("Unable to prompt for confirmation as no tty available.");
            }
            if (!Prompt.Bool("\nAre you sure you want to execute the deployment?", "y", "n"))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>POST whatIf; returns the legacy flattened result (filtered by the exclude list) and the template error, if any.</summary>
    protected async Task<(JsonObject? Result, ErrorResponse? Error)> RunWhatIfAsync()
    {
        ScopedDeploymentWhatIf body = WhatIfManagementGroupDeploymentProcessor_Orchestrator.CreateWhatIfBody(Location, await BuildPropertiesAsync(), WhatIfResultFormat);
        using HttpResponseMessage response = await SendWhatIfAsync(body);
        string? raw = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        ErrorResponse? error = string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<WhatIfOperationResult>(raw, JsonOptions)?.Error;
        return (WhatIfManagementGroupDeploymentProcessor_Orchestrator.ToCliResult(raw, WhatIfExcludeChangeTypes), error);
    }

    // Legacy CLI validates and deploys with --mode; the what-if preview always uses Incremental.
    protected virtual async Task<ScopedDeployment> BuildDeploymentAsync()
    {
        DeploymentProperties properties = await BuildPropertiesAsync();
        if (!string.IsNullOrWhiteSpace(Mode))
        {
            properties.Mode = Enum.Parse<DeploymentMode>(Mode!, ignoreCase: true);
        }
        return BuildDeployment(properties);
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        await ValidateAsync();
        HttpResponseMessage response = await CreateAsync();
        RawBody = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        Failed = !string.IsNullOrWhiteSpace(RawBody)
            && string.Equals((string?)JsonNode.Parse(RawBody)?["properties"]?["provisioningState"], "Failed", StringComparison.OrdinalIgnoreCase);
        return response;
    }

    // Both legacy clients validate before creating; a validation error stops the deployment.
    private async Task ValidateAsync()
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<ValidateManagementGroupDeploymentProcessor_Orchestrator.ValidationResultProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name!;
            p.Body = _deployment;
        }).SendAsync();
        string? raw = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(raw) && JsonSerializer.Deserialize<DeploymentValidateResult>(raw, JsonOptions)?.Error is { } error)
        {
            await OnValidationFailedAsync(error, !response.IsSuccessStatusCode);
        }
    }

    // Legacy CLI raises an HTTP validation failure and a 200 result carrying an error with different formats.
    protected virtual Task OnValidationFailedAsync(ErrorResponse error, bool httpFailed) =>
        throw new InvalidOperationException(httpFailed ? BuildHttpResponseErrorMessage(error) : BuildPreflightErrorMessage(error));

    protected virtual async Task<HttpResponseMessage> CreateAsync()
    {
        try
        {
            return await CreateExtendedProcessor<CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name!;
                p.Body = _deployment;
            }).SendAsync();
        }
        // The runtime LRO throws without the body. Legacy CLI prints the failed polling response as-is;
        // PS reads the error from the failed deployment.
        catch (Exception ex) when (DeploymentErrorPublisher.IsLroFailure(ex))
        {
            if (IsCli && DeploymentErrorPublisher.LastResponse(Publisher)?.Content is { } content)
            {
                throw new InvalidOperationException(await content.ReadAsStringAsync());
            }
            return await CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name!;
            }).SendAsync();
        }
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtended? body)
    {
        // Legacy CLI: a deployment that fails without polling raises the raw response body.
        if (Failed)
        {
            throw new InvalidOperationException(RawBody);
        }
        if (body is not null && !string.IsNullOrWhiteSpace(RawBody))
        {
            await WriteOutput(JsonSerializer.Deserialize<JsonElement>(RawBody));
        }
        return true;
    }
}
