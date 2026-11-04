// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant wait</c>: GETs the deployment every <see cref="Interval"/> seconds until the requested
/// condition holds or <see cref="Timeout"/> seconds elapse, following azure-cli's generic wait command. Writes no output.
/// </summary>
public class WaitTenantDeploymentProcessor_Orchestrator : GetAtTenantScopeDeploymentsProcessor
{
    public bool Created { get; set; }

    public bool Updated { get; set; }

    public bool Deleted { get; set; }

    public bool Exists { get; set; }

    /// <summary>A JMESPath expression evaluated against the deployment's ARM JSON; waiting stops once it is truthy.</summary>
    public string? Custom { get; set; }

    public int Interval { get; set; } = 30;

    public int Timeout { get; set; } = 3600;

    private bool HasCustom => !string.IsNullOrWhiteSpace(Custom);

    // Overrides the whole lifecycle: the command writes nothing, and the base would otherwise deserialize the last
    // response, including the 404 error body that ends a --deleted wait.
    public override async Task ProcessAsync()
    {
        if (!Created && !Updated && !Deleted && !Exists && !HasCustom)
        {
            throw AzclipsErrorException.InvalidInput("incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH");
        }
        if (HasCustom)
        {
            JmesPathCondition.Validate(Custom!);
        }

        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            Stopwatch elapsed = Stopwatch.StartNew();
            while (!await IsConditionMetAsync())
            {
                if (elapsed.Elapsed.TotalSeconds >= Timeout)
                {
                    throw new InvalidOperationException($"Wait operation timed-out after {Timeout} seconds");
                }

                await Publisher.Publish(Events.DelayPolling, $"Delaying {Interval} seconds before polling.");
                await Task.Delay(TimeSpan.FromSeconds(Interval), Publisher.Token);
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private async Task<bool> IsConditionMetAsync()
    {
        using HttpResponseMessage response = await SendAsyncOnce();

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            if (Deleted)
            {
                return true;
            }
            if (Created || Exists || HasCustom)
            {
                return false;
            }
            // Only --updated is left: a missing deployment cannot become updated.
            await response.EnsureSuccessAsync(Publisher);
        }

        await response.EnsureSuccessAsync(Publisher);

        // --exists is satisfied by any successful GET, even one reporting a Failed deployment.
        if (Exists)
        {
            return true;
        }

        string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        string? provisioningState = ReadProvisioningState(body);
        if (string.Equals(provisioningState, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The operation failed");
        }
        if ((Created || Updated) && string.Equals(provisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasCustom && !string.IsNullOrWhiteSpace(body) && JmesPathCondition.IsTruthy(body, Custom!);
    }

    private static string? ReadProvisioningState(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("properties", out JsonElement properties)
            && properties.ValueKind == JsonValueKind.Object
            && properties.TryGetProperty("provisioningState", out JsonElement state)
            && state.ValueKind == JsonValueKind.String
                ? state.GetString()
                : null;
    }
}

/// <summary>
/// The only code that touches JmesPath.Net, so its types (and Newtonsoft.Json) stay out of the wait command's fields
/// and signatures and are loaded only when <c>--custom</c> is used.
/// </summary>
internal static class JmesPathCondition
{
    // JmesPath.Net has no dedicated exception type for invalid syntax, so a broad catch is the only option.
    private static readonly DevLab.JmesPath.JmesPath s_jmesPath = new();

    internal static void Validate(string expression)
    {
        try
        {
            _ = s_jmesPath.Parse(expression);
        }
        catch (Exception ex)
        {
            throw AzclipsErrorException.InvalidInput($"--custom expression '{expression}' is not valid JMESPath: {ex.Message}");
        }
    }

    // JMESPath truthiness: null, false, "", [] and {} are false; every other value is true.
    internal static bool IsTruthy(string json, string expression)
    {
        using JsonDocument result = JsonDocument.Parse(s_jmesPath.Transform(json, expression));
        JsonElement value = result.RootElement;
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.False => false,
            JsonValueKind.String => value.GetString()!.Length > 0,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => true,
        };
    }
}
