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
/// <c>New-AzManagementGroupDeployment</c>: optional what-if preview, then validate and create like legacy PS.
/// <see cref="CreateManagementGroupDeploymentProcessor_Orchestrator"/> adds the <c>az deployment mg create</c> behavior.
/// </summary>
public class NewManagementGroupDeploymentProcessor_Orchestrator
    : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    private const int MaxErrorsToShow = 3;

    private const string DeploymentsApiVersion = "2025-04-01";

    private static readonly string[] NoChangeTypes = ["NoChange", "Ignore"];

    private ScopedDeployment _deployment = null!;

    public object? Tags { get; set; }

    public string? DeploymentDebugLogLevel { get; set; }

    public bool WhatIf { get; set; }

    public string? WhatIfResultFormat { get; set; }

    public string[]? WhatIfExcludeChangeType { get; set; }

    // The error of a deployment that ended in the Failed state; the CLI shows it like legacy azure-core.
    protected ErrorResponse? FailureOf(DeploymentExtended? body) =>
        string.Equals(body?.Properties?.ProvisioningState, "Failed", StringComparison.OrdinalIgnoreCase)
        && body!.Properties!.Error is JsonElement { ValueKind: JsonValueKind.Object } error
            ? error.Deserialize<ErrorResponse>(JsonOptions)
            : null;

    // Used by the PS cmdlet for -Confirm with -ProceedIfNoChange.
    public static bool HasChanges(WhatIfOperationResult result) =>
        result.Properties?.Changes?.Any(c => IsChange(c.ChangeType)) == true;

    protected static bool IsChange(string? changeType) => !NoChangeTypes.Contains(changeType, StringComparer.OrdinalIgnoreCase);

    // Legacy PS rule: template URI/file basename without extension, else a new GUID.
    protected virtual string DefaultDeploymentName()
    {
        string? source = string.IsNullOrWhiteSpace(TemplateUri) ? TemplateFile : TemplateUri;
        return string.IsNullOrWhiteSpace(source) ? Guid.NewGuid().ToString() : Path.GetFileNameWithoutExtension(source);
    }

    public override async Task ProcessAsync()
    {
        ManagementGroupDeploymentWhatIfProcessor_Orchestrator.CheckExcludeChangeTypes(WhatIfExcludeChangeType);
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = DefaultDeploymentName();
        }
        DeploymentProperties properties = await BuildPropertiesAsync();
        properties.Mode = ParseMode(Mode);
        if (DebugDetailLevel(DeploymentDebugLogLevel) is { } detailLevel)
        {
            properties.DebugSetting = new DebugSetting { DetailLevel = detailLevel };
            Warn("The DeploymentDebug setting has been enabled. This can potentially log secrets like passwords used in resource property or listKeys operations when you retrieve the deployment operations through Get-AzResourceGroupDeploymentOperation");
        }
        _deployment = new ScopedDeployment { Location = Location, Properties = properties, Tags = ToTags(Tags) };

        if (!await PreviewAndStopAsync())
        {
            await base.ProcessAsync();
        }
    }

    protected DeploymentProperties DeploymentProperties => _deployment.Properties;

    // Legacy PS -WhatIf: output the what-if result (errors included, not thrown) and stop.
    protected virtual async Task<bool> PreviewAndStopAsync()
    {
        if (!WhatIf)
        {
            return false;
        }
        (WhatIfOperationResult? result, _) = await RunWhatIfAsync();
        if (result is not null)
        {
            result.Properties?.Changes?.RemoveAll(c => WhatIfExcludeChangeType?.Contains(c.ChangeType, StringComparer.OrdinalIgnoreCase) == true);
            await WriteOutput(result);
        }
        return true;
    }

    protected async Task<(WhatIfOperationResult? Result, string? RawBody)> RunWhatIfAsync()
    {
        ScopedDeploymentWhatIf body = ManagementGroupDeploymentWhatIfProcessor_Orchestrator.CreateBody(Location, _deployment.Properties, WhatIfResultFormat);
        using HttpResponseMessage response = await CreateExtendedProcessor<WhatIfAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = body;
        }).SendAsync();
        string? raw = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        return (string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<WhatIfOperationResult>(raw, JsonOptions), raw);
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        await ValidateAsync();
        bool progress = PSDeployment.ForPowerShell;
        if (progress)
        {
            Verbose("Template is valid.");
        }
        using CancellationTokenSource stopProgress = new();
        HashSet<(string?, string?)> reported = [];
        Task progressTask = progress ? WriteProgressAsync(reported, stopProgress.Token) : Task.CompletedTask;
        HttpResponseMessage response;
        try
        {
            response = await CreateExtendedProcessor<CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = _deployment;
            }).SendAsync();
        }
        // The runtime LRO throws without the ARM error; read it from the failed deployment like legacy.
        catch (Exception ex) when (ex.Message.StartsWith("Long running operation failed", StringComparison.Ordinal))
        {
            response = await CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
            }).SendAsync();
        }
        finally
        {
            stopProgress.Cancel();
            await progressTask;
        }
        if (progress)
        {
            await WriteNewOperationsAsync(reported); // legacy lists operations once more before the final status check
        }
        return response;
    }

    // Legacy validates before creating; a validation error stops the deployment.
    private async Task ValidateAsync()
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<ValidateManagementGroupDeploymentProcessor_Orchestrator.ValidationResultProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = _deployment;
        }).SendAsync();
        string? raw = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(raw) && JsonSerializer.Deserialize<DeploymentValidateResult>(raw, JsonOptions)?.Error is { } error)
        {
            await OnValidationFailedAsync(error, !response.IsSuccessStatusCode);
        }
    }

    // Legacy PS RunDeploymentValidation: one error record per error and nested detail, then a terminating error.
    protected virtual async Task OnValidationFailedAsync(ErrorResponse error, bool httpFailed)
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

    // Legacy PS ProvisionDeploymentStatus: write the failed operations' errors (non-terminating), still output the deployment.
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
            await WriteOutput(PSDeployment.From(body, ManagementGroupId));
        }
        return true;
    }

    // Legacy PS WaitDeploymentStatus progress (verbose stream): poll every 5s for 400s, then every 60s,
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
        $"https://management.azure.com/providers/Microsoft.Management/managementGroups/{Uri.EscapeDataString(ManagementGroupId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/operations?api-version={DeploymentsApiVersion}";

    // Legacy PS WriteDeploymentProgress + GetNewOperations + DeploymentOperationErrorInfo.ProcessError:
    // list the deployment operations (nested deployments included), keep statusMessage.error of the failed ones.
    // ponytail: codegen has no deployment-operations processor, so the list API is called directly.
    private async Task<List<ErrorResponse>> GetFailedOperationErrorsAsync()
    {
        List<JsonObject> operations = [];
        HashSet<(string?, string?)> seen = [];
        await AddOperationsAsync(OperationsUrl(), operations, seen, nested: false);

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

    // Legacy PS ErrorResponse.ToFormattedString.
    private static string ToFormattedString(ErrorResponse error, int level = 0)
    {
        if (error.Details is null)
        {
            return $"{error.Message} (Code:{error.Code})";
        }
        string details = string.Concat(error.Details.Select(d => new string(' ', level * 2) + " - " + ToFormattedString(d, level + 1) + Environment.NewLine));
        return $"{error.Message} (Code: {error.Code}){Environment.NewLine}{details}";
    }

    // Legacy PS DeploymentDebugLogLevel mapping.
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
        JsonObject? preview = ManagementGroupDeploymentWhatIfProcessor_Orchestrator.ToCliResult(raw, WhatIfExcludeChangeType);
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
