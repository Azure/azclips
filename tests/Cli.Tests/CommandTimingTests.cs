// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli;

namespace Cli.Tests;

public sealed class CommandTimingTests
{
    [Fact]
    public void Summary_IsInvariantAndEmittedOnceThroughConfiguredDebugWriter()
    {
        var timing = new CommandTiming();
        var messages = new List<string>();
        var streams = CreateStreams((value, _) =>
            {
                messages.Add(value?.ToString() ?? "");
                return Task.CompletedTask;
            });
        timing.Configure(streams, LogLevel.Debug);
        using (timing.Timeline.MeasureLocal("operation"))
        {
        }
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            timing.EmitSummary(2);
            timing.EmitSummary(0);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.All(messages, line => Assert.StartsWith("[CommandTiming] ", line));
        Assert.Single(messages, line => line.Contains("Managed invocation:"));
        Assert.Contains("exit=2", messages[0]);
        Assert.Matches(@"Managed invocation: \d+\.\dms", messages[0]);
        Assert.Contains(messages, line => line.Contains("Uninstrumented (interval union)"));
    }

    [Fact]
    public void Summary_WithoutDebugOrConfiguration_IsSilent()
    {
        var timing = new CommandTiming();
        var streams = CreateStreams((_, _) => throw new InvalidOperationException("Debug is disabled."));

        timing.EmitSummary(0);
        timing.Configure(streams, LogLevel.Warn);
        timing.EmitSummary(0);
    }

    [Fact]
    public void Summary_ReportsUnhandledExitAndOpenPhaseWithoutClaimingSuccess()
    {
        var timing = new CommandTiming();
        var messages = new List<string>();
        timing.Configure(CreateStreams((value, _) =>
        {
            messages.Add(value?.ToString() ?? "");
            return Task.CompletedTask;
        }), LogLevel.Debug);
        using var phase = timing.Timeline.MeasureLocal("interrupted");

        timing.EmitSummary(null);

        Assert.Contains("exit=unhandled", messages[0]);
        Assert.Contains(messages, line => line.Contains("interrupted (incomplete)"));
    }

    private static OutputStreams CreateStreams(OutputStreamDelegate writeDebug)
    {
        OutputStreamDelegate unexpected = (_, _) => throw new InvalidOperationException("Timing must only use the debug stream.");
        return new OutputStreams(unexpected, unexpected, writeDebug, unexpected, unexpected, unexpected);
    }
}
