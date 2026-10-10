// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant create</c> (legacy <c>deploy_arm_template_at_tenant_scope</c>): optional what-if preview and
/// confirmation, then validate and PUT. <see cref="NewTenantDeploymentProcessor_PSOrchestrator"/> adds the
/// <c>New-AzTenantDeployment</c> behavior.
/// </summary>
public class NewTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    private static readonly string[] s_noChangeTypes = ["NoChange", "Ignore"];

    private ScopedDeployment _deployment = null!;

    /// <summary>CLI <c>-w</c>; PS sets it for the <c>-WhatIf</c>/<c>-Confirm</c> preview run.</summary>
    public bool WhatIf { get; set; }

    public bool ConfirmWithWhatIf { get; set; }

    public bool ProceedIfNoChange { get; set; }

    public string? WhatIfResultFormat { get; set; }

    public string[]? WhatIfExcludeChangeTypes { get; set; }

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
        WhatIfTenantDeploymentProcessor_Orchestrator.CheckExcludeChangeTypes(WhatIfExcludeChangeTypes, "what-if-exclude-change-types");
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
            await WriteOutput(TenantDeploymentWhatIfFormatter.Format(preview, cli: true, TenantDeploymentWhatIfFormatter.CliColor()));
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
        ScopedDeploymentWhatIf body = WhatIfTenantDeploymentProcessor_Orchestrator.CreateWhatIfBody(Location, await BuildPropertiesAsync(), WhatIfResultFormat);
        using HttpResponseMessage response = await SendWhatIfAsync(body);
        string? raw = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        ErrorResponse? error = string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<WhatIfOperationResult>(raw, JsonOptions)?.Error;
        return (WhatIfTenantDeploymentProcessor_Orchestrator.ToCliResult(raw, WhatIfExcludeChangeTypes), error);
    }

    protected virtual async Task<ScopedDeployment> BuildDeploymentAsync() => BuildDeployment(await BuildPropertiesAsync());

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
        using HttpResponseMessage response = await CreateExtendedProcessor<ValidateTenantDeploymentProcessor_Orchestrator.ValidationResultProcessor>(p =>
        {
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
            return await CreateExtendedProcessor<CreateOrUpdateAtTenantScopeDeploymentsProcessor>(p =>
            {
                p.DeploymentName = Name!;
                p.Body = _deployment;
            }).SendAsync();
        }
        // The runtime LRO throws without the body. Legacy CLI prints the failed polling response as-is;
        // PS reads the error from the failed deployment.
        catch (Exception ex) when (TenantDeploymentErrorPublisher.IsLroFailure(ex))
        {
            if (IsCli && TenantDeploymentErrorPublisher.LastResponse(Publisher)?.Content is { } content)
            {
                throw new InvalidOperationException(await content.ReadAsStringAsync());
            }
            return await CreateExtendedProcessor<GetAtTenantScopeDeploymentsProcessor>(p => p.DeploymentName = Name!).SendAsync();
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

/// <summary>
/// <c>New-AzTenantDeployment</c> (legacy <c>DeploymentCreateCmdlet.ExecuteDeployment</c>): validate, create with progress,
/// write the failed operations' errors and output a <see cref="PSDeployment"/>. With <see cref="NewTenantDeploymentProcessor_Orchestrator.WhatIf"/>
/// it only outputs the what-if result; the cmdlet owns the ShouldProcess confirmation.
/// </summary>
public class NewTenantDeploymentProcessor_PSOrchestrator : NewTenantDeploymentProcessor_Orchestrator
{
    private const int MaxErrorsToShow = 3;

    private const string DeploymentsApiVersion = "2025-04-01";

    public object? Tags { get; set; }

    public string? DeploymentDebugLogLevel { get; set; }

    protected override bool IsCli => false;

    // Legacy -WhatIf/-Confirm preview: the filtered result (template errors included, not thrown).
    protected override async Task<bool> PreviewAndStopAsync()
    {
        if (!WhatIf)
        {
            return false;
        }
        TenantDeploymentErrorPublisher.UsePowerShellWhatIfText(Publisher);
        (JsonObject? preview, _) = await RunWhatIfAsync();
        if (preview is not null)
        {
            await WriteOutput(preview);
        }
        return true;
    }

    // Legacy CreateBasicDeployment: no validation level (only the what-if sends it), plus tags and debug setting.
    protected override async Task<ScopedDeployment> BuildDeploymentAsync()
    {
        ScopedDeployment deployment = await base.BuildDeploymentAsync();
        deployment.Properties.ValidationLevel = null;
        deployment.Tags = ToTags(Tags);
        if (DebugDetailLevel(DeploymentDebugLogLevel) is { } detailLevel)
        {
            deployment.Properties.DebugSetting = new DebugSetting { DetailLevel = detailLevel };
            Warn("The DeploymentDebug setting has been enabled. This can potentially log secrets like passwords used in resource property or listKeys operations when you retrieve the deployment operations through Get-AzResourceGroupDeploymentOperation");
        }
        return deployment;
    }

    // Legacy RunDeploymentValidation: one error record per error and nested detail, then a terminating error.
    protected override async Task OnValidationFailedAsync(ErrorResponse error, bool httpFailed)
    {
        await WriteValidationErrorAsync(error);
        throw new InvalidOperationException("The deployment validation failed");
    }

    private async Task WriteValidationErrorAsync(ErrorResponse error)
    {
        await Publisher.Publish(Events.ResourceOperationFailed, $"Error: Code={error.Code}; Message={error.Message}\r\n");
        foreach (ErrorResponse detail in error.Details ?? [])
        {
            await WriteValidationErrorAsync(detail);
        }
    }

    protected override async Task<HttpResponseMessage> CreateAsync()
    {
        Verbose("Template is valid.");
        using CancellationTokenSource stopProgress = new();
        HashSet<(string?, string?)> reported = [];
        Task progress = WriteProgressAsync(reported, stopProgress.Token);
        HttpResponseMessage response;
        try
        {
            response = await base.CreateAsync();
        }
        finally
        {
            stopProgress.Cancel();
            await progress;
        }
        await WriteNewOperationsAsync(reported); // legacy lists operations once more before the final status check
        return response;
    }

    // Legacy ProvisionDeploymentStatus: write the failed operations' errors (non-terminating), still output the deployment.
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtended? body)
    {
        List<ErrorResponse> errors = await GetFailedOperationErrorsAsync();
        if (errors.Count > 0)
        {
            int shown = Math.Min(MaxErrorsToShow, errors.Count);
            string message = $"The deployment '{Name}' failed with error(s). Showing {shown} out of {errors.Count} error(s)."
                + string.Concat(errors.Take(shown).Select(e => Environment.NewLine + "Status Message: " + ToFormattedString(e) + Environment.NewLine))
                + Environment.NewLine + $"CorrelationId: {body?.Properties?.CorrelationId}";
            await Publisher.Publish(Events.ResourceOperationFailed, message);
        }
        if (body is not null)
        {
            await WriteOutput(PSDeployment.From(body));
        }
        return true;
    }

    // Legacy WaitDeploymentStatus progress (verbose stream): poll every 5s for 400s, then every 60s,
    // and write each new non-failed operation status. The runtime LRO waits for the result meanwhile.
    private async Task WriteProgressAsync(HashSet<(string?, string?)> reported, CancellationToken token)
    {
        Verbose($"Create template deployment '{Name}'");
        int phaseOne = 400;
        try
        {
            while (true)
            {
                int step = phaseOne > 0 ? 5 : 60;
                Verbose($"Checking deployment status in {step} seconds");
                await Task.Delay(TimeSpan.FromSeconds(step), token);
                phaseOne -= step;
                await WriteNewOperationsAsync(reported);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WriteNewOperationsAsync(HashSet<(string?, string?)> reported)
    {
        List<JsonObject> operations = [];
        try
        {
            await AddOperationsAsync(OperationsUrl(), operations, reported, nested: false, quiet: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            // Progress is best effort; the deployment result decides success.
        }
        foreach (JsonNode? properties in operations.Select(o => o["properties"]))
        {
            string? state = (string?)properties?["provisioningState"];
            if (!string.Equals(state, "Failed", StringComparison.Ordinal) && properties?["targetResource"] is JsonObject target)
            {
                Verbose($"Resource {(string?)target["resourceType"]} '{(string?)target["resourceName"]}' provisioning status is {state?.ToLowerInvariant()}");
            }
        }
    }

    private string OperationsUrl() =>
        $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name!)}/operations?api-version={DeploymentsApiVersion}";

    // Legacy GetNewOperations + DeploymentOperationErrorInfo.ProcessError: list the deployment operations
    // (nested deployments included), keep statusMessage.error of the failed ones.
    // ponytail: codegen has no deployment-operations processor, so the list API is called directly.
    private async Task<List<ErrorResponse>> GetFailedOperationErrorsAsync()
    {
        List<JsonObject> operations = [];
        await AddOperationsAsync(OperationsUrl(), operations, [], nested: false);
        return operations
            .Where(o => string.Equals((string?)o["properties"]?["provisioningState"], "Failed", StringComparison.Ordinal))
            .Select(o => o["properties"]?["statusMessage"]?["error"]?.Deserialize<ErrorResponse>(JsonOptions))
            .OfType<ErrorResponse>()
            .ToList();
    }

    private async Task AddOperationsAsync(string? url, List<JsonObject> operations, HashSet<(string?, string?)> seen, bool nested, bool quiet = false)
    {
        while (!string.IsNullOrEmpty(url))
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            if ((nested || quiet) && !response.IsSuccessStatusCode)
            {
                return; // legacy checks that the nested deployment exists first
            }
            await response.EnsureSuccessAsync(Publisher);
            JsonNode? page = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            foreach (JsonObject operation in (page?["value"] as JsonArray ?? []).OfType<JsonObject>())
            {
                JsonNode? properties = operation["properties"];
                if (seen.Add(((string?)operation["operationId"], (string?)properties?["provisioningState"])))
                {
                    operations.Add(operation);
                }
                // Legacy: an unparsable status code counts as 0, so only 4xx skips the nested deployment.
                if (string.Equals((string?)properties?["targetResource"]?["resourceType"], "Microsoft.Resources/deployments", StringComparison.OrdinalIgnoreCase)
                    && (string?)properties?["targetResource"]?["id"] is { Length: > 0 } id
                    && !(int.TryParse((string?)properties?["statusCode"], out int code) && code is >= 400 and < 500))
                {
                    await AddOperationsAsync($"https://management.azure.com{id}/operations?api-version={DeploymentsApiVersion}", operations, seen, nested: true, quiet);
                }
            }
            // Legacy follows nextLink for the deployment itself, and reads only the first page of a nested deployment.
            url = nested ? null : (string?)page?["nextLink"];
        }
    }

    // Legacy ErrorResponse.ToFormattedString.
    private static string ToFormattedString(ErrorResponse error, int level = 0)
    {
        if (error.Details is null)
        {
            return $"{error.Message} (Code:{error.Code})";
        }
        string details = string.Concat(error.Details.Select(d => new string(' ', level * 2) + " - " + ToFormattedString(d, level + 1) + Environment.NewLine));
        return $"{error.Message} (Code: {error.Code}){Environment.NewLine}{details}";
    }

    // Legacy GetDeploymentDebugLogLevel; "None" or an unknown value means no debug setting.
    private static string? DebugDetailLevel(string? level) => level?.ToLowerInvariant() switch
    {
        "all" => "RequestContent,ResponseContent",
        "requestcontent" => "RequestContent",
        "responsecontent" => "ResponseContent",
        _ => null,
    };

    private IDictionary<string, string>? ToTags(object? tags) => tags switch
    {
        null => null,
        IDictionary<string, string> dictionary => dictionary,
        _ => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(tags, JsonOptions), JsonOptions),
    };
}
