// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Extensions;

namespace Core.Extensions.Tests;

public sealed class InstallationCandidateSelectionTests
{
    private const string Package = "Contoso.Widget";

    private static ExtensionInstallation Handle(string source, string version, int root = 0, string locator = "widget.dll")
        => new("Widget", version, Path.GetFullPath(locator), source, source == "Bundled" ? 0 : 1, root,
            $"{source}:{root}:{locator}:{version}",
            () => throw new InvalidOperationException("Selection must not load assemblies."),
            () => throw new InvalidOperationException("Selection must not validate files."));

    private static ExtensionRoutingInfo Candidate(ExtensionInstallation handle, params string[] leaves)
        => new(Package, handle.Version, ["widget"], leaves, handle.Location) { Installation = handle };

    [Theory]
    [InlineData("widget aaa")]
    [InlineData("widget zzz")]
    [InlineData("widget shared")]
    public void EqualVersionPrefersBundledWithoutDiscardingIdenticalMetadata(string installedCommand)
    {
        var bundled = Candidate(Handle("Bundled", "1.0.0"), "widget shared");
        var installed = Candidate(Handle("Installed", "1.0.0"), installedCommand);
        foreach (var candidates in new[] { new[] { installed, bundled }, new[] { bundled, installed } })
        {
            var selection = ExtensionCandidateSelector.Select(candidates);
            Assert.Same(bundled, selection.Selected);
            Assert.Same(installed, Assert.Single(selection.Ignored));
            Assert.Equal(ExtensionSelectionReason.EquivalentVersion, selection.Reason);
        }
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", null, "Installed")]
    [InlineData("2.0.0", "1.0.0", null, "Bundled")]
    [InlineData("2.0.0", "1.0.0", "1.0.0", "Installed")]
    [InlineData("1.0.0", "1.0.0", "1.0.0", "Bundled")]
    [InlineData("1.0.0", "1.0.0", "9.0.0", "Bundled")]
    [InlineData("1.0.0+bundled", "1.0.0+installed", null, "Bundled")]
    [InlineData("1.0", "1.0.0", null, "Bundled")]
    [InlineData("1.0", "1.0.0", "1.0.0", "Installed")]
    [InlineData("", "1.0.0", null, "Installed")]
    [InlineData("1.0.0", "", null, "Bundled")]
    [InlineData("", "", null, "Bundled")]
    [InlineData("unparseable", "unknown", null, "Bundled")]
    public void VersionAndPinRulesPrecedeSourcePriority(string bundledVersion, string installedVersion,
        string? pin, string expectedSource)
    {
        var bundled = Candidate(Handle("Bundled", bundledVersion), "widget zzz");
        var installed = Candidate(Handle("Installed", installedVersion), "widget aaa");
        var preference = pin is null ? null : new ExtensionRegistryEntry { Version = pin, Pinned = true };
        var selection = ExtensionCandidateSelector.Select([installed, bundled], preference);
        Assert.Equal(expectedSource, selection.Selected.Installation!.Source);
        Assert.Single(selection.Ignored);
    }

    [Theory]
    [InlineData("Bundled")]
    [InlineData("Installed")]
    public void RootPriorityPrecedesLocatorAndCommandOrdering(string source)
    {
        var preferred = Candidate(Handle(source, "1.0.0", root: 0, locator: "zzz.dll"), "widget zzz");
        var other = Candidate(Handle(source, "1.0.0", root: 1, locator: "aaa.dll"), "widget aaa");
        Assert.Same(preferred, ExtensionCandidateSelector.Select([other, preferred]).Selected);
        Assert.Same(preferred, ExtensionCandidateSelector.Select([preferred, other]).Selected);
    }

    [Fact]
    public void SamePriorityUsesLocatorNotCommandFingerprint()
    {
        var preferred = Candidate(Handle("Bundled", "1.0.0", locator: "aaa.dll"), "widget zzz");
        var other = Candidate(Handle("Bundled", "1.0.0", locator: "zzz.dll"), "widget aaa");
        Assert.Same(preferred, ExtensionCandidateSelector.Select([other, preferred]).Selected);
    }

    [Fact]
    public void FullDiscoveryAndRoutingSelectTheSameInstallation()
    {
        var bundledHandle = Handle("Bundled", "1.0.0");
        var installedHandle = Handle("Installed", "1.0.0");
        var candidates = new[] { installedHandle, bundledHandle }.Select(handle =>
            new DiscoveredExtensionInfo(Package, handle.Version, ["widget"],
                new Dictionary<string, CommandMetadata>(), handle.Location) { Installation = handle }).ToArray();

        var full = ExtensionCandidateSelector.Select(candidates);
        var routing = ExtensionCandidateSelector.Select(candidates.Select(ExtensionRoutingInfo.FromDiscovery).ToArray());
        Assert.Same(bundledHandle, full.Selected.Installation);
        Assert.Same(full.Selected.Installation, routing.Selected.Installation);
        Assert.Single(full.Ignored);
        Assert.Single(routing.Ignored);
    }

    [Fact]
    public void LegacyMetadataCannotEraseResolvedInstallation()
    {
        var resolved = Candidate(Handle("Bundled", "1.0.0"), "widget shared");
        var legacy = resolved with { Installation = null };
        Assert.Same(resolved, ExtensionCandidateSelector.Select([legacy, resolved]).Selected);
        Assert.Same(resolved, ExtensionCandidateSelector.Select([resolved, legacy]).Selected);
    }

    [Fact]
    public void RegistryLeavesAndResolverRetainHandleButJsonDoesNot()
    {
        var handle = Handle("Bundled", "1.0.0");
        var registry = new ExtensionRegistry();
        registry.Extensions[Package] = new ExtensionRegistryEntry
        {
            Version = handle.Version,
            Installation = handle,
            Location = handle.Location,
            Namespaces = ["widget"],
            Contributions = [new Contribution { Verb = "widget", Path = "show" }],
        };
        var index = LeafIndex.Build(registry);
        Assert.True(index.TryGet("widget", "show", out var leaf));
        Assert.Same(handle, leaf!.Installation);
        var store = new MemoryStore(registry);
        Assert.True(new ExtensionResolver(store).TryResolve("widget", out var resolved));
        Assert.Same(handle, resolved!.Installation);

        var json = JsonSerializer.Serialize(registry);
        Assert.DoesNotContain("Installation", json);
        Assert.DoesNotContain(handle.CacheKey, json);
        Assert.Null(JsonSerializer.Deserialize<ExtensionRegistry>(json)!.Extensions[Package].Installation);
        var invocation = new CommandInvocation("Widget", "widget show", new Dictionary<string, object>())
        {
            Installation = handle,
        };
        Assert.DoesNotContain("Installation", JsonSerializer.Serialize(invocation));
        Assert.DoesNotContain("Installation", JsonSerializer.Serialize(leaf));
        Assert.DoesNotContain("Installation", JsonSerializer.Serialize(resolved));
        Assert.DoesNotContain("Installation", JsonSerializer.Serialize(Candidate(handle, "widget show")));
        var (name, command, _, _, _, _) = invocation;
        Assert.Equal("Widget", name);
        Assert.Equal("widget show", command);
    }

    private sealed class MemoryStore(ExtensionRegistry registry) : IExtensionRegistryStore
    {
        public ExtensionRegistry Load() => registry;
        public void Save(ExtensionRegistry value) => throw new NotSupportedException();
    }
}
