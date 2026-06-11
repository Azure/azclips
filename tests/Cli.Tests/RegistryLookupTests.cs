using Core.Extensions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Verifies <see cref="RegistryLookup.TryFind"/> matches by every supported identifier
/// (package id, short suffix, legacy verb, namespace, contribution verb). Centralized lookup
/// fixes the prior duplication in <c>extension show/update/remove/pin/unpin</c> where only the
/// legacy single <c>Verb</c> field was inspected, so multi-namespace extensions were addressable
/// only by their first declared namespace. (PR #253 review feedback.)
/// </summary>
public class RegistryLookupTests
{
    private const string PackageId = "Microsoft.Azure.Cirrus.Extensions.Resources";

    private static ExtensionRegistry RegistryWith(ExtensionRegistryEntry entry)
    {
        var registry = new ExtensionRegistry();
        registry.Extensions[PackageId] = entry;
        return registry;
    }

    private static ExtensionRegistryEntry MultiNamespaceEntry() => new()
    {
        Verb = "deployment",
        Version = "1.0.0",
        InstalledAt = DateTimeOffset.UtcNow,
        Contributions =
        {
            new Contribution { Verb = "deployment", Path = "group create" },
            new Contribution { Verb = "group", Path = "create" },
        },
        Namespaces = { "deployment", "group" },
    };

    [Fact]
    public void TryFind_ShouldMatchExactPackageId_CaseInsensitive()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.True(RegistryLookup.TryFind(registry, "MICROSOFT.AZURE.CIRRUS.EXTENSIONS.RESOURCES", out var match));
        Assert.Equal(PackageId, match.Key);
    }

    [Fact]
    public void TryFind_ShouldMatchShortSuffix()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.True(RegistryLookup.TryFind(registry, "Resources", out var match));
        Assert.Equal(PackageId, match.Key);
    }

    [Fact]
    public void TryFind_ShouldMatchLegacyVerb()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.True(RegistryLookup.TryFind(registry, "deployment", out var match));
        Assert.Equal(PackageId, match.Key);
    }

    [Fact]
    public void TryFind_ShouldMatchSecondaryNamespace()
    {
        // Pre-fix: this would miss because the helper only checked the legacy single `Verb`.
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.True(RegistryLookup.TryFind(registry, "group", out var match));
        Assert.Equal(PackageId, match.Key);
    }

    [Fact]
    public void TryFind_ShouldMatchContributionVerb_WhenNamespacesNotYetDerived()
    {
        // Defensive fallback: hand-built entries (test fixtures) may have Contributions
        // populated but Namespaces empty (NormalizeEntry hasn't run yet).
        var entry = MultiNamespaceEntry();
        entry.Namespaces.Clear();
        var registry = RegistryWith(entry);

        Assert.True(RegistryLookup.TryFind(registry, "group", out var match));
        Assert.Equal(PackageId, match.Key);
    }

    [Fact]
    public void TryFind_ShouldReturnFalse_WhenNoMatch()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.False(RegistryLookup.TryFind(registry, "no-such-extension", out var match));
        Assert.Null(match.Key);
    }

    [Fact]
    public void TryFind_ShouldReturnFalse_OnEmptyOrNullName()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.False(RegistryLookup.TryFind(registry, string.Empty, out _));
        Assert.False(RegistryLookup.TryFind(registry, "   ", out _));
    }

    [Fact]
    public void TryFind_ShouldThrowArgumentNullException_OnNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(
            () => RegistryLookup.TryFind(null!, "anything", out _));
    }
}
