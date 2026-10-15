// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Core.Agent.Telemetry;
using Tests.Common.Utilities;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contracts around <c>properties.recovery.failureReason</c> that no single run can show:
/// the value set is closed, the published doc matches it, and the cross-thread signal it is
/// derived from is published safely.
/// </summary>
public class RecoveryContractTests
{
    [Fact]
    internal void EveryFailureReason_HasADistinctWireForm()
    {
        // The tag is a dashboard dimension. Two members sharing a wire form silently merge two
        // different failures into one column, and an uppercase one splits a column in two.
        var members = Enum.GetValues<RecoveryFailure>();
        var seen = new Dictionary<string, RecoveryFailure>(StringComparer.Ordinal);

        foreach (var member in members)
        {
            var wire = member.Wire();

            Assert.False(string.IsNullOrWhiteSpace(wire), $"{member} has no wire form.");
            Assert.True(wire == wire.ToLowerInvariant(), $"{member} exports \"{wire}\", which is not lowercase.");
            Assert.False(
                seen.TryGetValue(wire, out var owner),
                $"{member} and {owner} both export \"{wire}\"; the two failures cannot be told apart.");

            seen[wire] = member;
        }

        Assert.Equal(members.Length, seen.Count);
    }

    [Fact]
    public void TelemetryDoc_DoesNotPromiseExceptionTypeNames()
    {
        // The doc used to sanction "the exception type name for a host one" as a failureReason
        // value. That is what let the open vocabulary survive review: the published contract said
        // the dimension was allowed to be whatever the runtime threw. The check is a taboo on the
        // phrase anywhere in the file, denials included — a scan cannot read intent, and the value
        // table below is the contract, so prose naming runtime types can only confuse it.
        var doc = File.ReadAllText(
            Path.Combine(RepoLocator.GetRepoRoot(), "eng-docs", "spec", "telemetry.md"));

        Assert.DoesNotContain("exception type", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("properties.recovery.failureReason", doc, StringComparison.Ordinal);

        // Every member, not a sample. Checking two of six left the class doc's claim -- that the
        // published doc matches the enum -- unenforced: a seventh member, or deleting four rows
        // from the table, would both have kept this green. This is the same shape as the defect
        // where a doc promised a new value "cannot reach the exporter without being documented"
        // with nothing behind it.
        foreach (var failure in Enum.GetValues<RecoveryFailure>())
        {
            Assert.True(
                doc.Contains(failure.Wire(), StringComparison.Ordinal),
                $"telemetry.md documents recovery.failureReason as a closed set, so every " +
                $"RecoveryFailure member must appear in it. Missing: '{failure.Wire()}'.");
        }
    }

    [Fact]
    public void LastError_IsOnlyEverTouchedThroughVolatile()
    {
        // A memory-model race cannot be reproduced deterministically, so this guards the shape
        // instead: the field is written on the SDK's dispatch thread and read on the host thread,
        // so every touch has to carry a barrier. A plain assignment anywhere here means the host
        // is not guaranteed to see the failure, and the session exports ok.
        const string declaration = "private SessionErrored? _lastError;";
        var source = File.ReadAllLines(
            Path.Combine(RepoLocator.GetRepoRoot(), "Agent", "ChatAgent.cs"));

        var declarations = 0;
        var reads = 0;
        var writes = 0;

        for (var i = 0; i < source.Length; i++)
        {
            var line = source[i];
            var trimmed = line.Trim();

            if (!trimmed.Contains("_lastError", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
            if (trimmed == declaration) { declarations++; continue; }

            var isRead = Regex.IsMatch(line, @"Volatile\.Read\(\s*ref\s+[A-Za-z0-9_.]*_lastError\b");
            var isWrite = Regex.IsMatch(line, @"Volatile\.Write\(\s*ref\s+[A-Za-z0-9_.]*_lastError\b");

            Assert.True(
                isRead || isWrite,
                $"ChatAgent.cs({i + 1}) touches _lastError without a barrier: {trimmed}");

            if (isRead) reads++;
            if (isWrite) writes++;
        }

        Assert.True(declarations == 1, $"Expected exactly one '{declaration}'; found {declarations}.");
        Assert.True(reads >= 1, "LastError must project the field through Volatile.Read.");
        Assert.True(
            writes >= 2,
            "Both the dispatch-thread handler and SendAsync's per-turn clear must publish through Volatile.Write.");
    }
}
