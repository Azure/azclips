// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Extensions;

namespace Core.Extensions.Tests;

public sealed class ExtensionCandidateSelectorTests
{
    public static TheoryData<string, DiscoveredExtensionInfo[], ExtensionRegistryEntry?, string, ExtensionSelectionReason, int> PrecedenceCases => new()
    {
        {
            "P1_HigherVersionFirstCandidateWins",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.2.0", ["widget alpha"]),
                CreateInfo("Contoso.Widget", "1.1.9", ["widget beta"]),
            },
            null,
            "widget alpha",
            ExtensionSelectionReason.HigherVersion,
            1
        },
        {
            "P2_HigherVersionSecondCandidateWins",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.1.9", ["widget alpha"]),
                CreateInfo("Contoso.Widget", "1.2.0", ["widget beta"]),
            },
            null,
            "widget beta",
            ExtensionSelectionReason.HigherVersion,
            1
        },
        {
            "P3_EqualVersionFirstCandidateWins",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.2.0", ["widget zeta"]),
                CreateInfo("Contoso.Widget", "1.2.0", ["widget alpha"]),
            },
            null,
            "widget zeta",
            ExtensionSelectionReason.EquivalentVersion,
            1
        },
        {
            "P4_PinBeatsHigherUnpinnedFirstCandidate",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.2.0", ["widget alpha"]),
                CreateInfo("Contoso.Widget", "1.3.0", ["widget beta"]),
            },
            new ExtensionRegistryEntry
            {
                Version = "1.2.0",
                Pinned = true,
                InstalledAt = DateTimeOffset.UtcNow,
            },
            "widget alpha",
            ExtensionSelectionReason.Pinned,
            1
        },
        {
            "P5_PinSelectsMatchingHigherVersionCandidate",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.2.0", ["widget alpha"]),
                CreateInfo("Contoso.Widget", "1.3.0", ["widget beta"]),
            },
            new ExtensionRegistryEntry
            {
                Version = "1.3.0",
                Pinned = true,
                InstalledAt = DateTimeOffset.UtcNow,
            },
            "widget beta",
            ExtensionSelectionReason.Pinned,
            1
        },
        {
            "P6_PinBeatsHigherOtherCandidate",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.4.0", ["widget alpha"]),
                CreateInfo("Contoso.Widget", "1.3.0", ["widget beta"]),
            },
            new ExtensionRegistryEntry
            {
                Version = "1.3.0",
                Pinned = true,
                InstalledAt = DateTimeOffset.UtcNow,
            },
            "widget beta",
            ExtensionSelectionReason.Pinned,
            1
        },
        {
            "P7_OnlyFirstCandidateExists",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.2.0", ["widget alpha"]),
            },
            null,
            "widget alpha",
            ExtensionSelectionReason.SingleCandidate,
            0
        },
        {
            "P8_OnlySecondLocationConceptuallyExists",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.2.0", ["widget beta"]),
            },
            null,
            "widget beta",
            ExtensionSelectionReason.SingleCandidate,
            0
        },
        {
            "P9_UnresolvedPinFallsBackToEquivalentSelection",
            new[]
            {
                CreateInfo("Contoso.Widget", "1.2.0", ["widget zeta"]),
                CreateInfo("Contoso.Widget", "1.2.0", ["widget alpha"]),
            },
            new ExtensionRegistryEntry
            {
                Version = "9.9.9",
                Pinned = true,
                InstalledAt = DateTimeOffset.UtcNow,
            },
            "widget zeta",
            ExtensionSelectionReason.EquivalentVersion,
            1
        },
    };

    [Theory]
    [MemberData(nameof(PrecedenceCases))]
    public void Select_ShouldHonorPrecedenceTable(
        string caseName,
        DiscoveredExtensionInfo[] candidates,
        ExtensionRegistryEntry? persisted,
        string expectedInvocation,
        ExtensionSelectionReason expectedReason,
        int expectedIgnoredCount)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseName));

        var selected = ExtensionCandidateSelector.Select(candidates, persisted);

        Assert.Equal(expectedReason, selected.Reason);
        Assert.Equal(expectedIgnoredCount, selected.Ignored.Count);
        Assert.Contains(expectedInvocation, selected.Selected.Commands.Keys, StringComparer.OrdinalIgnoreCase);

        var routing = ExtensionCandidateSelector.Select(
            candidates.Select(ExtensionRoutingInfo.FromDiscovery).ToArray(), persisted);
        Assert.Equal(selected.Reason, routing.Reason);
        Assert.Equal(selected.Selected.ResolvedVersion, routing.Selected.ResolvedVersion);
        Assert.Equal(selected.Selected.Commands.Keys, routing.Selected.Leaves);
        Assert.Equal(selected.Ignored.Count, routing.Ignored.Count);
    }

    [Fact]
    public void Select_ShouldPreferPinnedVersion_WhenPinnedPreferenceMatchesCandidate()
    {
        var candidates = new[]
        {
            CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]),
            CreateInfo("Contoso.Widget", "1.3.0", ["widget create"]),
        };
        var persisted = new ExtensionRegistryEntry
        {
            Version = "1.2.0",
            Pinned = true,
            InstalledAt = DateTimeOffset.UtcNow,
        };

        var selected = ExtensionCandidateSelector.Select(candidates, persisted);

        Assert.Equal("1.2.0", selected.Selected.ResolvedVersion);
        Assert.Equal(ExtensionSelectionReason.Pinned, selected.Reason);
        Assert.Single(selected.Ignored);
    }

    [Fact]
    public void Select_ShouldPreferHigherVersion_WhenNotPinned()
    {
        var candidates = new[]
        {
            CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]),
            CreateInfo("Contoso.Widget", "1.4.0", ["widget create"]),
        };

        var selected = ExtensionCandidateSelector.Select(candidates);

        Assert.Equal("1.4.0", selected.Selected.ResolvedVersion);
        Assert.Equal(ExtensionSelectionReason.HigherVersion, selected.Reason);
    }

    [Fact]
    public void Select_ShouldPreferFirstCandidate_WhenVersionsAreEqual()
    {
        var create = CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]);
        var delete = CreateInfo("Contoso.Widget", "1.2.0", ["widget delete"]);

        var selected = ExtensionCandidateSelector.Select([delete, create]);
        var reversed = ExtensionCandidateSelector.Select([create, delete]);

        Assert.Same(delete, selected.Selected);
        Assert.Same(create, reversed.Selected);
        Assert.Equal(ExtensionSelectionReason.EquivalentVersion, selected.Reason);
        Assert.Single(selected.Ignored);
    }

    [Fact]
    public void Select_ShouldPreferFirstCandidate_WhenVersionsDifferOnlyByBuildMetadata()
    {
        var first = CreateInfo("Contoso.Widget", "1.2.0+build.2", ["widget zeta"]);
        var second = CreateInfo("Contoso.Widget", "1.2.0+build.1", ["widget alpha"]);

        var selected = ExtensionCandidateSelector.Select([first, second]);

        Assert.Same(first, selected.Selected);
        Assert.Equal(ExtensionSelectionReason.EquivalentVersion, selected.Reason);
    }

    [Fact]
    public void Select_ShouldPreferFirstPinnedCandidate_WhenSeveralMatchThePin()
    {
        var first = CreateInfo("Contoso.Widget", "1.2.0", ["widget zeta"]);
        var second = CreateInfo("Contoso.Widget", "1.2.0", ["widget alpha"]);
        var higher = CreateInfo("Contoso.Widget", "1.3.0", ["widget beta"]);
        var persisted = new ExtensionRegistryEntry { Version = "1.2.0", Pinned = true, InstalledAt = DateTimeOffset.UtcNow };

        var selected = ExtensionCandidateSelector.Select([higher, first, second], persisted);

        Assert.Same(first, selected.Selected);
        Assert.Equal(ExtensionSelectionReason.Pinned, selected.Reason);
    }

    [Fact]
    public void Select_ShouldCollapseIdenticalCandidates_AndAvoidSelfDuplicateIgnored()
    {
        var original = CreateInfo("Contoso.Widget", "1.2.0", ["widget create", "widget delete"]);
        var duplicate = CreateInfo("Contoso.Widget", "1.2.0", ["widget create", "widget delete"]);

        var selected = ExtensionCandidateSelector.Select([original, duplicate]);

        Assert.Equal("1.2.0", selected.Selected.ResolvedVersion);
        Assert.Equal(ExtensionSelectionReason.SingleCandidate, selected.Reason);
        Assert.Empty(selected.Ignored);
    }

    [Fact]
    public void Select_ShouldCollapseCandidates_WhenDifferencesAreOnlyCasing()
    {
        var upper = CreateInfo("Contoso.Widget", "1.0.0-ALPHA", ["Widget Create"]);
        var lower = CreateInfo("contoso.widget", "1.0.0-alpha", ["widget create"]);

        var selected = ExtensionCandidateSelector.Select([upper, lower]);

        Assert.Equal(ExtensionSelectionReason.SingleCandidate, selected.Reason);
        Assert.Empty(selected.Ignored);
    }

    // ---------------------------------------------------------------------------------------------
    // ReportUnresolvablePin — user-visible diagnostic added in #522.
    //
    // When the file registry pins a specific version but the pinned version is not present in the
    // discovered candidate set, ExtensionCandidateSelector silently falls back to the highest
    // available version (see P9 above). ReportUnresolvablePin makes that drift visible.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ReportUnresolvablePin_ShouldEmitError_WhenPinnedVersionMissing()
    {
        var candidates = new[]
        {
            CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]),
            CreateInfo("Contoso.Widget", "1.3.0", ["widget create"]),
        };
        var persisted = new ExtensionRegistryEntry
        {
            Version = "9.9.9",
            Pinned = true,
            InstalledAt = DateTimeOffset.UtcNow,
        };
        var selection = ExtensionCandidateSelector.Select(candidates, persisted);
        Assert.NotEqual(ExtensionSelectionReason.Pinned, selection.Reason);

        var logger = new RecordingLoggingService();

        ExtensionCandidateSelector.ReportUnresolvablePin("Contoso.Widget", selection, persisted, logger);

        var error = Assert.Single(logger.Errors);
        Assert.Contains("9.9.9", error, StringComparison.Ordinal);
        Assert.Contains("Contoso.Widget", error, StringComparison.Ordinal);
        Assert.Contains("Falling back to", error, StringComparison.Ordinal);
        Assert.Contains(selection.Selected.ResolvedVersion, error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportUnresolvablePin_ShouldBeSilent_WhenPinResolves()
    {
        var candidates = new[]
        {
            CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]),
            CreateInfo("Contoso.Widget", "1.3.0", ["widget create"]),
        };
        var persisted = new ExtensionRegistryEntry
        {
            Version = "1.2.0",
            Pinned = true,
            InstalledAt = DateTimeOffset.UtcNow,
        };
        var selection = ExtensionCandidateSelector.Select(candidates, persisted);
        Assert.Equal(ExtensionSelectionReason.Pinned, selection.Reason);

        var logger = new RecordingLoggingService();

        ExtensionCandidateSelector.ReportUnresolvablePin("Contoso.Widget", selection, persisted, logger);

        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void ReportUnresolvablePin_ShouldBeSilent_WhenSingleCandidateMatchesPin()
    {
        // Regression: when only one candidate exists AND it happens to match the pinned version,
        // Select short-circuits with SingleCandidate reason (never entering the Pinned branch).
        // The diagnostic must still stay silent — the selected version DOES match the pin.
        // Discovered when testing `az extension pin --name Compute` against a single-source install.
        var candidates = new[]
        {
            CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]),
        };
        var persisted = new ExtensionRegistryEntry
        {
            Version = "1.2.0",
            Pinned = true,
            InstalledAt = DateTimeOffset.UtcNow,
        };
        var selection = ExtensionCandidateSelector.Select(candidates, persisted);
        Assert.Equal(ExtensionSelectionReason.SingleCandidate, selection.Reason);

        var logger = new RecordingLoggingService();

        ExtensionCandidateSelector.ReportUnresolvablePin("Contoso.Widget", selection, persisted, logger);

        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void ReportUnresolvablePin_ShouldBeSilent_WhenNotPinned()
    {
        var candidates = new[]
        {
            CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]),
            CreateInfo("Contoso.Widget", "1.3.0", ["widget create"]),
        };
        var persisted = new ExtensionRegistryEntry
        {
            Version = "9.9.9",
            Pinned = false, // <- not pinned; version drift is not a user concern
            InstalledAt = DateTimeOffset.UtcNow,
        };
        var selection = ExtensionCandidateSelector.Select(candidates, persisted);
        var logger = new RecordingLoggingService();

        ExtensionCandidateSelector.ReportUnresolvablePin("Contoso.Widget", selection, persisted, logger);

        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void ReportUnresolvablePin_ShouldTolerateNullLogger()
    {
        var candidates = new[]
        {
            CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]),
            CreateInfo("Contoso.Widget", "1.3.0", ["widget create"]),
        };
        var persisted = new ExtensionRegistryEntry
        {
            Version = "9.9.9",
            Pinned = true,
            InstalledAt = DateTimeOffset.UtcNow,
        };
        var selection = ExtensionCandidateSelector.Select(candidates, persisted);

        // Must not throw.
        ExtensionCandidateSelector.ReportUnresolvablePin("Contoso.Widget", selection, persisted, loggingService: null);
    }

    private sealed class RecordingLoggingService : ILoggingService
    {
        public List<string> Errors { get; } = [];
        public void Log(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) { }
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) => Errors.Add(message);
    }

    private static DiscoveredExtensionInfo CreateInfo(string packageId, string version, IEnumerable<string> commandNames)
    {
        var commands = commandNames.ToDictionary(
            command => command,
            command => new CommandMetadata(
                Name: command,
                InvocationName: command,
                Aliases: [],
                FullName: command,
                AssemblyName: packageId,
                ProcessorType: typeof(object),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()),
            StringComparer.OrdinalIgnoreCase);

        var verbs = commandNames
            .Select(command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DiscoveredExtensionInfo(packageId, version, verbs, commands, Location: null);
    }
}