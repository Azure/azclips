// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Cli;

namespace Cli.Tests;

public sealed class CommandTimingTests
{
    [Fact]
    public void Summary_IsInvariantAndEmittedOnceThroughSuppliedWriter()
    {
        var timing = new CommandTiming();
        var messages = new List<string>();
        using (timing.Timeline.MeasureLocal("operation"))
        {
        }
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            timing.EmitSummary(2, messages.Add);
            timing.EmitSummary(0, _ => throw new InvalidOperationException("The summary must only be emitted once."));
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
    public void Summary_RequiresWriterWithoutConsumingEmission()
    {
        var timing = new CommandTiming();
        Assert.Throws<ArgumentNullException>(() => timing.EmitSummary(0, null!));
        var messages = new List<string>();
        timing.EmitSummary(0, messages.Add);
        Assert.NotEmpty(messages);
    }

    [Fact]
    public void Summary_ReportsUnhandledExitAndOpenPhaseWithoutClaimingSuccess()
    {
        var timing = new CommandTiming();
        var messages = new List<string>();
        using var phase = timing.Timeline.MeasureLocal("interrupted");

        timing.EmitSummary(null, messages.Add);

        Assert.Contains("exit=unhandled", messages[0]);
        Assert.Contains(messages, line => line.Contains("interrupted (incomplete)"));
    }

}
