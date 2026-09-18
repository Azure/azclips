// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
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
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            if (!Created && !Updated && !Deleted && !Exists && string.IsNullOrWhiteSpace(Custom))
            {
                throw new ArgumentException(
                    "Specify one of --created, --updated, --deleted, --exists, or --custom.");
            }
            if (Interval <= 0 || Timeout <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Interval), "Interval and timeout must be greater than zero.");
            }

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
                        || MatchesCustom(state))
                    {
                        return;
                    }
                    if (state?.Equals("Failed", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        throw new InvalidOperationException("The deployment operation failed.");
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    if (Deleted)
                    {
                        return;
                    }
                    if (!Created && !Exists && string.IsNullOrWhiteSpace(Custom))
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

    private Task DelayAsync(long started, TimeSpan timeout)
    {
        TimeSpan remaining = timeout - Stopwatch.GetElapsedTime(started);
        TimeSpan delay = TimeSpan.FromSeconds(Interval);
        return remaining > TimeSpan.Zero
            ? Task.Delay(delay < remaining ? delay : remaining, Publisher.Token)
            : Task.CompletedTask;
    }

    private bool MatchesCustom(string? state)
    {
        if (string.IsNullOrWhiteSpace(Custom))
        {
            return false;
        }

        string expression = Custom.Replace("properties.", "", StringComparison.OrdinalIgnoreCase);
        string operation = expression.Contains("!=", StringComparison.Ordinal) ? "!=" : "==";
        string[] parts = expression.Split(operation, 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[0].Equals("provisioningState", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "--custom currently accepts provisioningState equality, for example provisioningState==Canceled.");
        }

        string expected = parts[1].Trim('\'', '"');
        bool equal = string.Equals(state, expected, StringComparison.OrdinalIgnoreCase);
        return operation == "==" ? equal : !equal;
    }
}
