// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Extensions.Tests;

public sealed class AssemblyReferenceSelectionTests
{
    private sealed class Reference(string key) : AssemblyReference(key,
        () => throw new InvalidOperationException("Selection must not validate or load assemblies."));

    private static ExtensionRoutingInfo Candidate(string key, string version = "1.0.0")
        => new("Widget", version, ["widget"], ["widget show"], null)
        {
            AssemblyReference = new Reference(key),
        };

    [Fact]
    public void DistinctReferencesWithIdenticalMetadataAreNotDeduplicated()
    {
        var first = Candidate("a");
        var second = Candidate("z");
        foreach (var candidates in new[] { new[] { first, second }, new[] { second, first } })
        {
            var selection = ExtensionCandidateSelector.Select(candidates);
            Assert.Same(first, selection.Selected);
            Assert.Same(second, Assert.Single(selection.Ignored));
        }
    }

    [Theory]
    [InlineData("1.0.0", null, true)]
    [InlineData("2.0.0", null, false)]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", true)]
    public void ClientComparerBreaksTiesButDoesNotOverrideVersionsOrPins(
        string firstVersion, string? pin, bool secondWins)
    {
        var first = Candidate("a", firstVersion);
        var second = Candidate("z");
        var comparer = Comparer<AssemblyReference>.Create(
            (left, right) => StringComparer.Ordinal.Compare(right.CacheKey, left.CacheKey));
        var preference = pin is null ? null : new ExtensionRegistryEntry { Version = pin, Pinned = true };
        var selection = ExtensionCandidateSelector.Select([first, second], preference, comparer);
        Assert.Same(secondWins ? second : first, selection.Selected);
        Assert.Single(selection.Ignored);
    }

    [Fact]
    public void EqualClientPriorityFallsBackToOpaqueIdentityNotCommandSpelling()
    {
        var first = Candidate("a") with { Leaves = ["widget zzz"] };
        var second = Candidate("z") with { Leaves = ["widget aaa"] };
        var comparer = Comparer<AssemblyReference>.Create((_, _) => 0);
        Assert.Same(first, ExtensionCandidateSelector.Select([second, first], null, comparer).Selected);
    }
}
