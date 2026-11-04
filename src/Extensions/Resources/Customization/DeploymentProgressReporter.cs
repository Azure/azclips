// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>-Verbose</c> deployment progress, like New-AzResourceGroupDeployment: while a deployment runs, list its operations on
/// Az PowerShell's schedule and write each resource's provisioning state the first time it is seen. Progress is best effort:
/// a failed read writes one verbose note and stops reporting, and never changes the deployment's own result.
/// </summary>
internal sealed class DeploymentProgressReporter(
    Func<Task<List<DeploymentOperation>>> readOperations,
    Func<TimeSpan, CancellationToken, Task> delay)
{
    // Like Az PowerShell: check every 5 seconds for the first 400 seconds, then every 60 seconds.
    private const int FastPhaseSeconds = 400;
    private const int FastStepSeconds = 5;
    private const int SlowStepSeconds = 60;

    private readonly ILoggingService logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
    private readonly HashSet<(string? OperationId, string State)> reported = [];
    private bool checkedWhileRunning;
    private bool unavailable;

    /// <summary>Runs <paramref name="runDeployment"/>, reporting progress while it waits, and returns its result unchanged.</summary>
    internal async Task<T> RunAsync<T>(Func<Task<T>> runDeployment, CancellationToken token)
    {
        using CancellationTokenSource stopChecks = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task checks = CheckWhileRunningAsync(stopChecks.Token);
        try
        {
            return await runDeployment();
        }
        finally
        {
            stopChecks.Cancel();
            await checks;

            // Catch up on states that changed after the last check. Skipped when no check has run, so a deployment that
            // returns before the first check sends no extra request; recorded scenario playback relies on this, since it
            // answers every poll immediately.
            if (checkedWhileRunning && !unavailable && !token.IsCancellationRequested)
            {
                await ReportNewStatesAsync();
            }
        }
    }

    /// <summary>
    /// Lists every page of a deployment's operations. Unlike <see cref="HttpRuntime.SendAsync"/>, a failed read throws without
    /// publishing <c>ResourceOperationFailed</c>, so a best-effort progress check never writes to the error stream.
    /// </summary>
    internal static async Task<List<DeploymentOperation>> ReadAllPagesAsync(
        Func<Task<HttpRequestMessage>> createRequest,
        IEventPublisher publisher,
        IRuntimeHttpPipeline httpPipeline,
        JsonSerializerOptions jsonOptions)
    {
        List<DeploymentOperation> operations = [];
        HttpRequestMessage request = await createRequest();
        try
        {
            while (true)
            {
                using HttpResponseMessage response = await httpPipeline.SendAsync(request, publisher);
                await publisher.Publish(Events.TracingHttpResponseMessage, response);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);
                }

                string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
                DeploymentOperationsListResult? page = string.IsNullOrWhiteSpace(body)
                    ? null
                    : JsonSerializer.Deserialize<DeploymentOperationsListResult>(body, jsonOptions);
                operations.AddRange(page?.Value ?? []);
                if (string.IsNullOrWhiteSpace(page?.NextLink))
                {
                    return operations;
                }
                request = request.CloneAndDispose(new Uri(page.NextLink), HttpMethod.Get);
            }
        }
        finally
        {
            request.Dispose();
        }
    }

    private async Task CheckWhileRunningAsync(CancellationToken token)
    {
        int elapsedSeconds = 0;
        try
        {
            while (!unavailable && !token.IsCancellationRequested)
            {
                int step = elapsedSeconds < FastPhaseSeconds ? FastStepSeconds : SlowStepSeconds;
                logger.Verbose($"Checking deployment status in {step} seconds");
                await delay(TimeSpan.FromSeconds(step), token);
                elapsedSeconds += step;
                checkedWhileRunning = true;
                await ReportNewStatesAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // The deployment finished, or the caller cancelled it.
        }
    }

    // Like Az PowerShell, failed operations are left to the deployment's error, and operations without a target resource are skipped.
    private async Task ReportNewStatesAsync()
    {
        List<DeploymentOperation> operations;
        try
        {
            operations = await readOperations();
        }
        catch (OperationCanceledException)
        {
            unavailable = true;
            return;
        }
        catch (Exception ex)
        {
            // Any failure to read the operations (HTTP status, authentication, malformed response) only ends the progress
            // report: the deployment's own result must not change because of it.
            unavailable = true;
            logger.Verbose($"Unable to read the deployment operations, so resource progress is not shown: {ex.Message}");
            return;
        }

        foreach (DeploymentOperation operation in operations)
        {
            string? state = operation.Properties?.ProvisioningState;
            if (state is null
                || string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase)
                || operation.Properties?.TargetResource is not { } target
                || !reported.Add((operation.OperationId, state.ToLowerInvariant())))
            {
                continue;
            }

            logger.Verbose($"Resource {target.ResourceType} '{target.ResourceName}' provisioning status is {state.ToLowerInvariant()}");
        }
    }
}
