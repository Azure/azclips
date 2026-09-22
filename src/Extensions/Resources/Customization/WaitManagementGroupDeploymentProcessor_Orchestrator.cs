// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Customization;

public class WaitManagementGroupDeploymentProcessor_Orchestrator : ResourcesProcessorBase<object, object>
{
    public string ManagementGroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Created { get; set; }

    public bool Updated { get; set; }

    public bool Deleted { get; set; }

    public bool Exists { get; set; }

    public string? Custom { get; set; }

    public int Interval { get; set; } = 30;

    public int Timeout { get; set; } = 3600;

    public override async Task ProcessAsync()
    {
        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            int conditionCount = (Created ? 1 : 0)
                + (Updated ? 1 : 0)
                + (Deleted ? 1 : 0)
                + (Exists ? 1 : 0)
                + (string.IsNullOrWhiteSpace(Custom) ? 0 : 1);
            if (conditionCount != 1)
            {
                throw new ArgumentException(
                    "Specify exactly one of --created, --updated, --deleted, --exists, or --custom.");
            }
            if (Interval <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Interval), "Interval must be greater than zero.");
            }
            if (Timeout <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be greater than zero.");
            }
            (string Operation, string Expected)? customCondition = ParseCustomCondition();

            long started = Stopwatch.GetTimestamp();
            TimeSpan timeout = TimeSpan.FromSeconds(Timeout);
            while (Stopwatch.GetElapsedTime(started) < timeout)
            {
                try
                {
                    GetAtManagementGroupScopeDeploymentsProcessor get =
                        CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(processor =>
                        {
                            processor.ManagementGroupId = ManagementGroupId;
                            processor.Name = Name;
                        });
                    using HttpResponseMessage response = await get.SendAsync();
                    string json = await response.Content.ReadAsStringAsync();
                    DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(json, JsonOptions);
                    string? state = deployment?.Properties?.ProvisioningState;

                    if (Exists || ((Created || Updated) && state?.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) == true)
                        || MatchesCustom(state, customCondition))
                    {
                        return;
                    }
                    if ((Created || Updated)
                        && state is not null
                        && (state.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                            || state.Equals("Canceled", StringComparison.OrdinalIgnoreCase)
                            || state.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException(
                            $"The deployment operation reached terminal state '{state}'.");
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    if (Deleted)
                    {
                        return;
                    }
                    if (!Created && !Updated && !Exists && string.IsNullOrWhiteSpace(Custom))
                    {
                        throw;
                    }
                }

                await DelayAsync(started, timeout);
            }

            throw new TimeoutException($"Wait operation timed out after {Timeout} seconds.");
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    protected virtual Task DelayAsync(long started, TimeSpan timeout)
    {
        TimeSpan remaining = timeout - Stopwatch.GetElapsedTime(started);
        TimeSpan delay = TimeSpan.FromSeconds(Interval);
        return remaining > TimeSpan.Zero
            ? Task.Delay(delay < remaining ? delay : remaining, Publisher.Token)
            : Task.CompletedTask;
    }

    private (string Operation, string Expected)? ParseCustomCondition()
    {
        if (string.IsNullOrWhiteSpace(Custom))
        {
            return null;
        }

        Match match = Regex.Match(
            Custom,
            @"^\s*(?:properties\.)?provisioningState\s*(==|!=)\s*(?:""([^""]+)""|'([^']+)'|([^=!]+))\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new ArgumentException(
                "--custom requires exactly one provisioningState comparison using == or != with a non-empty expected value, for example provisioningState==Canceled.");
        }

        string expected = match.Groups[2].Success
            ? match.Groups[2].Value
            : match.Groups[3].Success
                ? match.Groups[3].Value
                : match.Groups[4].Value;
        expected = expected.Trim();
        if (expected.Length == 0)
        {
            throw new ArgumentException(
                "--custom requires exactly one provisioningState comparison using == or != with a non-empty expected value, for example provisioningState==Canceled.");
        }

        return (match.Groups[1].Value, expected);
    }

    private static bool MatchesCustom(
        string? state,
        (string Operation, string Expected)? customCondition)
    {
        if (state is null || customCondition is null)
        {
            return false;
        }

        bool equal = string.Equals(state, customCondition.Value.Expected, StringComparison.OrdinalIgnoreCase);
        return customCondition.Value.Operation == "==" ? equal : !equal;
    }
}
