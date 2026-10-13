// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli;

namespace Cli.Tests.AssemblyLoading;

public sealed class ExtensionLoadTrackerTests
{
    [Fact]
    public void EmitSummary_WhenNoEntriesRecorded_EmitsNothing()
    {
        ExtensionLoadTracker.Reset();
        var logs = new List<string>();

        ExtensionLoadTracker.EmitSummary(logs.Add);

        Assert.Empty(logs);
    }

    [Fact]
    public void EmitSummary_WhenEntriesRecorded_EmitsFormattedTableWithTrackerPrefix()
    {
        ExtensionLoadTracker.Reset();
        var logs = new List<string>();

        ExtensionLoadTracker.Record("Compute", "Bundled", 42.5, "0.1.0", "Microsoft.Azure.Azclips.Extensions.Compute.dll");
        ExtensionLoadTracker.Record("Storage", "Cached", 12.3, "0.2.0", "Microsoft.Azure.Azclips.Extensions.Storage.dll");

        ExtensionLoadTracker.EmitSummary(logs.Add);

        Assert.NotEmpty(logs);
        // Every single log line must have the [ExtensionLoadTracker] prefix
        Assert.All(logs, line => Assert.StartsWith("[ExtensionLoadTracker] ", line));

        // Must contain column headers
        Assert.Contains(logs, l => l.Contains("Extension") && l.Contains("Loader") && l.Contains("Time (ms)") && l.Contains("Version"));
        Assert.DoesNotContain(logs, l => l.Contains("Path"));
        Assert.DoesNotContain(logs, l => l.Contains("---"));

        // Must contain data rows
        Assert.Contains(logs, l => l.Contains("Compute") && l.Contains("Bundled") && l.Contains("42.5"));
        Assert.Contains(logs, l => l.Contains("Storage") && l.Contains("Cached") && l.Contains("12.3"));

        // Must contain the total line
        Assert.Contains(logs, l => l.Contains("Total (2)") && l.Contains("54.8"));
    }
}
