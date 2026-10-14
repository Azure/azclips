// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Polls a subscription-scoped deployment until a condition is met, matching
/// <c>az deployment sub wait</c>: exactly one of <see cref="Created"/>, <see cref="Updated"/>,
/// <see cref="Deleted"/>, or <see cref="Exists"/>, checked every <see cref="Interval"/> seconds for at
/// most <see cref="Timeout"/> seconds. A <c>Failed</c> provisioning state or a timeout is an error.
/// </summary>
public class WaitSubscriptionDeploymentProcessor_Orchestrator : GetSubscriptionDeploymentProcessor
{
    public bool Created { get; set; }

    public bool Updated { get; set; }

    public bool Deleted { get; set; }

    public bool Exists { get; set; }

    public int Interval { get; set; } = 30;

    public int Timeout { get; set; } = 3600;

    public override async Task ProcessAsync()
    {
        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            if ((Created ? 1 : 0) + (Updated ? 1 : 0) + (Deleted ? 1 : 0) + (Exists ? 1 : 0) != 1)
            {
                throw AzclipsErrorException.InvalidInput("Specify exactly one of --created, --updated, --deleted, or --exists.");
            }
            if (Interval <= 0 || Timeout < 0)
            {
                throw AzclipsErrorException.InvalidInput("--interval must be positive and --timeout must not be negative.");
            }

            // Same loop shape as azure-cli: one check per interval within the timeout.
            for (int elapsed = 0; elapsed < Timeout; elapsed += Interval)
            {
                if (await IsConditionMetAsync())
                {
                    return;
                }

                await Publisher.Publish(Events.DelayPolling, $"Delaying {Interval} seconds before polling.");
                await Task.Delay(Interval * 1000, Publisher.Token);
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
            if (Created || Exists)
            {
                return false;
            }
        }

        await response.EnsureSuccessAsync(Publisher);
        if (Exists)
        {
            return true;
        }

        DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(await response.Content.ReadAsStringAsync(), JsonOptions);
        string? state = deployment?.Properties?.ProvisioningState;
        if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Deployment '{Name}' failed.");
        }

        return (Created || Updated) && string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase);
    }
}
