// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Polls a subscription-scoped deployment until a condition is met, matching
/// <c>az deployment sub wait</c>: any combination of <see cref="Created"/>, <see cref="Updated"/>,
/// <see cref="Deleted"/>, <see cref="Exists"/>, and a <see cref="Custom"/> JMESPath condition, checked
/// every <see cref="Interval"/> seconds for at most <see cref="Timeout"/> seconds. The wait ends at the
/// first poll where any of them is met. A <c>Failed</c> provisioning state is an error unless
/// <see cref="Exists"/> is set, and a timeout is an error.
/// </summary>
public class WaitSubscriptionDeploymentProcessor_Orchestrator : GetSubscriptionDeploymentProcessor
{
    public bool Created { get; set; }

    public bool Updated { get; set; }

    public bool Deleted { get; set; }

    public bool Exists { get; set; }

    /// <summary>
    /// A JMESPath query over the deployment's JSON, as <c>deployment sub show</c> prints it. The
    /// condition is met when the result is truthy, and a missing deployment keeps the wait polling.
    /// </summary>
    public string? Custom { get; set; }

    public int Interval { get; set; } = 30;

    public int Timeout { get; set; } = 3600;

    public override async Task ProcessAsync()
    {
        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            if (!(Created || Updated || Deleted || Exists || !string.IsNullOrEmpty(Custom)))
            {
                throw AzclipsErrorException.InvalidInput("Specify at least one of --created, --updated, --deleted, --exists, or --custom.");
            }
            if (Interval <= 0 || Timeout < 0)
            {
                throw AzclipsErrorException.InvalidInput("--interval must be positive and --timeout must not be negative.");
            }
            if (!string.IsNullOrEmpty(Custom))
            {
                JmesPathCondition.Validate(Custom);
            }

            TimeSpan timeout = TimeSpan.FromSeconds(Timeout);
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                bool conditionMet = await IsConditionMetAsync();
                if (conditionMet && stopwatch.Elapsed < timeout)
                {
                    return;
                }

                TimeSpan remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                TimeSpan delay = TimeSpan.FromSeconds(Interval);
                bool lastDelay = delay >= remaining;
                if (lastDelay)
                {
                    delay = remaining;
                }

                await Publisher.Publish(Events.DelayPolling, $"Delaying {delay.TotalSeconds:0.###} seconds before polling.");
                await Task.Delay(delay, Publisher.Token);

                // Task.Delay can return a hair before the stopwatch reaches the timeout, so stop here
                // rather than racing it into one more poll.
                if (lastDelay)
                {
                    break;
                }
            }

            throw new TimeoutException($"Wait operation timed-out after {Timeout} seconds.");
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private async Task<bool> IsConditionMetAsync()
    {
        using HttpRequestMessage request = await CreateRequestMessage();
        using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            if (Deleted)
            {
                return true;
            }
            if (Created || Exists || !string.IsNullOrEmpty(Custom))
            {
                return false;
            }
        }

        await response.EnsureSuccessAsync(Publisher);
        if (Exists)
        {
            return true;
        }

        string json = await response.Content.ReadAsStringAsync();
        DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(json, JsonOptions);
        string? state = deployment?.Properties?.ProvisioningState;
        if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Deployment '{Name}' failed.");
        }

        return ((Created || Updated) && string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrEmpty(Custom) && JmesPathCondition.IsMet(json, Custom));
    }
}
