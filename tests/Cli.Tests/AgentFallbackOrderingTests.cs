// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// AI error recovery blocks on a subprocess for as long as the model takes. The command
/// root span must be stopped first, or that wait is billed to the command's duration.
/// </summary>
public class AgentFallbackOrderingTests
{
    [Fact]
    public async Task StopRootThenFallbackAsync_StopsTheRoot_BeforeRunningTheFallback()
    {
        var order = new List<string>();
        using var source = new ActivitySource("cli-fallback-order-" + Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = _ => order.Add("root-stopped"),
        };
        ActivitySource.AddActivityListener(listener);

        var root = source.StartActivity("CLI.Activity.Root", ActivityKind.Server);
        Assert.NotNull(root);

        await Core.Cli.Program.StopRootThenFallbackAsync(root, (_, _) =>
        {
            order.Add("fallback-ran");
            return Task.FromResult(1);
        });

        Assert.Equal(new[] { "root-stopped", "fallback-ran" }, order);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_HandsTheFallbackTheRootIdentifiers()
    {
        using var source = new ActivitySource("cli-fallback-ids-" + Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var root = source.StartActivity("CLI.Activity.Root", ActivityKind.Server);
        Assert.NotNull(root);
        var expectedTrace = root.TraceId.ToString();
        var expectedSpan = root.SpanId.ToString();

        string? seenTrace = null;
        string? seenSpan = null;
        await Core.Cli.Program.StopRootThenFallbackAsync(root, (traceId, spanId) =>
        {
            seenTrace = traceId;
            seenSpan = spanId;
            return Task.FromResult(1);
        });

        Assert.Equal(expectedTrace, seenTrace);
        Assert.Equal(expectedSpan, seenSpan);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_PassesNulls_WhenTelemetryIsDisabled()
    {
        // StartRootActivity returns null when telemetry.enabled=false. The fallback
        // must still run, and must still return the agent's exit code.
        string? seenTrace = "unset";
        string? seenSpan = "unset";

        var exitCode = await Core.Cli.Program.StopRootThenFallbackAsync(null, (traceId, spanId) =>
        {
            seenTrace = traceId;
            seenSpan = spanId;
            return Task.FromResult(7);
        });

        Assert.Null(seenTrace);
        Assert.Null(seenSpan);
        Assert.Equal(7, exitCode);
    }
}
