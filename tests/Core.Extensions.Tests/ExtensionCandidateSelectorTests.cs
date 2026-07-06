// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Extensions;

namespace Core.Extensions.Tests;

public sealed class ExtensionCandidateSelectorTests
{
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
    public void Select_ShouldTreatEqualVersionAsEquivalent_AndChooseDeterministically()
    {
        var create = CreateInfo("Contoso.Widget", "1.2.0", ["widget create"]);
        var delete = CreateInfo("Contoso.Widget", "1.2.0", ["widget delete"]);

        var selected = ExtensionCandidateSelector.Select([delete, create]);

        Assert.Same(create, selected.Selected);
        Assert.Equal(ExtensionSelectionReason.EquivalentVersion, selected.Reason);
        Assert.Single(selected.Ignored);
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

        return new DiscoveredExtensionInfo(packageId, version, verbs, commands);
    }
}