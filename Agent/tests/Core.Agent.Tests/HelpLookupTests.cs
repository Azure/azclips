// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Agent.Tools.Help;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit coverage for <see cref="HelpLookup"/> — the name-matching backbone of
/// <c>az agent tool help --name &lt;x&gt;</c>. Pure over an in-memory manifest set,
/// so no assembly loading / installed extensions required.
/// </summary>
public sealed class HelpLookupTests
{
    // Two verbs, four commands: enough to exercise cross-manifest search,
    // cliName vs psName matching, and substring-suggestion behavior.
    private static IReadOnlyList<HelpManifest> Manifests() => new[]
    {
        Manifest("vm",
            Cmd("vm create", "New-AzVM"),
            Cmd("vm delete", "Remove-AzVM"),
            Cmd("vm list", "Get-AzVM")),
        Manifest("group",
            Cmd("group create", "New-AzResourceGroup")),
    };

    // ── FindCommand ──

    [Fact]
    public void FindCommand_MatchesCliName_ExactAndCaseInsensitive()
    {
        var m = Manifests();

        Assert.Equal("vm create", HelpLookup.FindCommand(m, "vm create")!.CliName);
        Assert.Equal("vm create", HelpLookup.FindCommand(m, "VM CREATE")!.CliName);
    }

    [Fact]
    public void FindCommand_MatchesPsName()
    {
        var m = Manifests();

        var hit = HelpLookup.FindCommand(m, "Remove-AzVM");

        Assert.NotNull(hit);
        Assert.Equal("vm delete", hit!.CliName);
    }

    [Fact]
    public void FindCommand_FindsAcrossManifests()
    {
        // "group create" lives in the second manifest, not the first.
        var hit = HelpLookup.FindCommand(Manifests(), "group create");

        Assert.NotNull(hit);
        Assert.Equal("New-AzResourceGroup", hit!.PsName);
    }

    [Fact]
    public void FindCommand_ReturnsNull_WhenNoMatch()
    {
        Assert.Null(HelpLookup.FindCommand(Manifests(), "storage account create"));
    }

    // ── FindSuggestions ──

    [Fact]
    public void FindSuggestions_QueryContainedInCandidate_ReturnsAllUnderThatPrefix()
    {
        // "vm" is a substring of vm create / vm delete / vm list.
        var hits = HelpLookup.FindSuggestions(Manifests(), "vm");

        Assert.Contains("vm create", hits);
        Assert.Contains("vm delete", hits);
        Assert.Contains("vm list", hits);
    }

    [Fact]
    public void FindSuggestions_CandidateContainedInQuery_StillMatches()
    {
        // Reverse direction: candidate "vm list" is contained in a longer query.
        var hits = HelpLookup.FindSuggestions(Manifests(), "vm list --extra-typo");

        Assert.Contains("vm list", hits);
    }

    [Fact]
    public void FindSuggestions_MatchesPsNameToo()
    {
        var hits = HelpLookup.FindSuggestions(Manifests(), "New-AzVM");

        Assert.Contains("New-AzVM", hits);
    }

    [Fact]
    public void FindSuggestions_EmptyQuery_ReturnsEmpty()
    {
        Assert.Empty(HelpLookup.FindSuggestions(Manifests(), "   "));
    }

    [Fact]
    public void FindSuggestions_NoMatch_ReturnsEmpty()
    {
        Assert.Empty(HelpLookup.FindSuggestions(Manifests(), "zzz-nonexistent"));
    }

    [Fact]
    public void FindSuggestions_DeduplicatesCaseInsensitively()
    {
        // Two commands whose cliName == psName (same string) must not double up.
        var manifests = new[]
        {
            Manifest("dup", Cmd("dup cmd", "dup cmd"), Cmd("dup cmd", "DUP CMD")),
        };

        var hits = HelpLookup.FindSuggestions(manifests, "dup");

        Assert.Single(hits);
    }

    // ── fixture builders ──

    private static HelpManifest Manifest(string verb, params HelpCommand[] commands) =>
        new(verb, "1.0.0", $"{verb} root summary", commands.ToList());

    private static HelpCommand Cmd(string cliName, string psName) =>
        new(cliName, psName, $"{cliName} summary", $"{cliName} description",
            new List<HelpParameter>(),
            new HelpExampleSet(new List<HelpExample>(), new List<HelpExample>()),
            Array.Empty<HelpTag>());
}
