// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
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

        await Core.Cli.Program.StopRootThenFallbackAsync(root, (_, _, _) =>
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
        await Core.Cli.Program.StopRootThenFallbackAsync(root, (traceId, spanId, _) =>
        {
            seenTrace = traceId;
            seenSpan = spanId;
            return Task.FromResult(1);
        });

        Assert.Equal(expectedTrace, seenTrace);
        Assert.Equal(expectedSpan, seenSpan);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_HandsTheFallbackTheParserResolvedCommandName()
    {
        // TagInvocation stamps the dispatcher-resolved path on the root once parsing
        // succeeds. Reusing it is what keeps positional parameter values — which raw argv
        // cannot distinguish from command-path tokens — out of the recovery span.
        using var source = new ActivitySource("cli-fallback-cmd-" + Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var root = source.StartActivity("CLI.Activity.Root", ActivityKind.Server);
        Assert.NotNull(root);
        root.SetTag(TelemetryTags.Command, "profile show");

        string? seenCommand = null;
        await Core.Cli.Program.StopRootThenFallbackAsync(root, (_, _, commandName) =>
        {
            seenCommand = commandName;
            return Task.FromResult(1);
        });

        Assert.Equal("profile show", seenCommand);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_PassesNoCommandName_WhenParsingNeverResolvedOne()
    {
        // A parse failure throws before TagInvocation runs, so the root carries no command
        // tag and the caller falls back to the command group alone.
        using var source = new ActivitySource("cli-fallback-nocmd-" + Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var root = source.StartActivity("CLI.Activity.Root", ActivityKind.Server);
        Assert.NotNull(root);

        string? seenCommand = "unset";
        await Core.Cli.Program.StopRootThenFallbackAsync(root, (_, _, commandName) =>
        {
            seenCommand = commandName;
            return Task.FromResult(1);
        });

        Assert.Null(seenCommand);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_PassesNulls_WhenTelemetryIsDisabled()
    {
        // StartRootActivity returns null when telemetry.enabled=false. The fallback
        // must still run, and must still return the agent's exit code.
        string? seenTrace = "unset";
        string? seenSpan = "unset";

        var exitCode = await Core.Cli.Program.StopRootThenFallbackAsync(null, (traceId, spanId, _) =>
        {
            seenTrace = traceId;
            seenSpan = spanId;
            return Task.FromResult(7);
        });

        Assert.Null(seenTrace);
        Assert.Null(seenSpan);
        Assert.Equal(7, exitCode);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_ReturnsNull_WhenTheFallbackDidNotLaunchTheAgent()
    {
        // A null result tells the caller to print the original typed error instead of an agent exit code.
        var exitCode = await Core.Cli.Program.StopRootThenFallbackAsync<int?>(null, (_, _, _) => Task.FromResult<int?>(null));

        Assert.Null(exitCode);
    }
}
