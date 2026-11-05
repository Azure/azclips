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
/// Deployment progress, like New-AzResourceGroupDeployment -Verbose: while a deployment runs, list its operations on Az
/// PowerShell's schedule and publish each resource's provisioning state as <see cref="Events.Progress"/> the first time it
/// is seen. Progress is best effort: it never changes the deployment's result or holds it up.
/// </summary>
internal sealed class DeploymentProgressReporter(
    IEventPublisher publisher,
    Func<CancellationToken, Task<List<DeploymentOperation>>> readOperations,
    Func<TimeSpan, CancellationToken, Task> delay)
{
    // Like Az PowerShell: check every 5 seconds for the first 400 seconds, then every 60 seconds.
    private const int FastPhaseSeconds = 400;
    private const int FastStepSeconds = 5;
    private const int SlowStepSeconds = 60;

    // The catch-up read is optional, so it gets a short budget rather than holding up the deployment's result.
    private static readonly TimeSpan CatchUpTimeout = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource submitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HashSet<(string? OperationId, string State)> reported = [];
    private bool checkedWhileRunning;
    private bool unavailable;

    /// <summary>
    /// Wraps the deployment PUT's publisher to notice when ARM has accepted the deployment: the shared long-running-operation
    /// poller publishes <see cref="Events.DelayPolling"/> only after the PUT succeeded, before its first wait.
    /// </summary>
    internal IEventPublisher WatchForSubmission(IEventPublisher inner) =>
        new DeploymentProgressPublisher(inner, inner.Token, id =>
        {
            if (id == Events.DelayPolling)
            {
                submitted.TrySetResult();
            }
        });

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
            // Stops the timer and any read in flight, so progress never holds up the deployment's result.
            stopChecks.Cancel();
            await checks;

            // Catch up on states that changed after the last check. Skipped when no check has run, so a deployment that
            // returns before the first check sends no extra request; recorded scenario playback relies on this, since it
            // answers every poll immediately.
            if (checkedWhileRunning && !unavailable && !token.IsCancellationRequested)
            {
                using CancellationTokenSource catchUp = CancellationTokenSource.CreateLinkedTokenSource(token);
                catchUp.CancelAfter(CatchUpTimeout);
                await ReportNewStatesAsync(catchUp.Token);
            }
        }
    }

    /// <summary>
    /// Lists every page of a deployment's operations. Unlike <see cref="HttpRuntime.SendAsync"/>, a failed read throws without
    /// publishing <c>ResourceOperationFailed</c>, so a best-effort progress check never writes to the error stream. The
    /// publisher's token cancels the requests, their retries, and the body reads.
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

                // The pipeline returns once the headers arrive, so read the body here, where the token can stop it. The
                // engine's trace subscriber logs every response body without a token; this way it reads it from memory.
                string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(publisher.Token);
                await publisher.Publish(Events.TracingHttpResponseMessage, response);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);
                }

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
        try
        {
            // Like Az, the first check waits until the deployment has been submitted: before that its operations are
            // missing (404) or, for a redeployment, still the previous run's.
            await submitted.Task.WaitAsync(token);

            int elapsedSeconds = 0;
            while (!unavailable && !token.IsCancellationRequested)
            {
                int step = elapsedSeconds < FastPhaseSeconds ? FastStepSeconds : SlowStepSeconds;
                await PublishAsync($"Checking deployment status in {step} seconds");
                await delay(TimeSpan.FromSeconds(step), token);
                elapsedSeconds += step;
                checkedWhileRunning = true;
                await ReportNewStatesAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // The deployment finished, or the caller cancelled it.
        }
    }

    // Like Az PowerShell, failed operations are left to the deployment's error, and operations without a target resource are skipped.
    private async Task ReportNewStatesAsync(CancellationToken token)
    {
        List<DeploymentOperation> operations;
        try
        {
            operations = await readOperations(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The deployment finished while this read was in flight, or the catch-up read ran out of time.
            return;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The deployment's operations aren't visible yet; try again at the next check.
            return;
        }
        catch (Exception ex)
        {
            // Any other failure to read the operations (HTTP status, authentication, malformed response) only ends the
            // progress report: the deployment's own result must not change because of it.
            unavailable = true;
            await PublishAsync($"Unable to read the deployment operations, so resource progress is not shown: {ex.Message}");
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

            await PublishAsync($"Resource {target.ResourceType} '{target.ResourceName}' provisioning status is {state.ToLowerInvariant()}");
        }
    }

    private async Task PublishAsync(string message)
    {
        try
        {
            await publisher.Publish(Events.Progress, message);
        }
        catch (OperationCanceledException)
        {
            // The caller cancelled the command, so there is nothing left to show.
        }
    }
}
