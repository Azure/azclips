// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Extensions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Verifies <see cref="RegistryLookup.TryFind"/> matches **only** by package identifier (full
/// or short suffix). Verbs and namespaces are intentionally rejected so destructive ops like
/// <c>extension remove --name disk</c> can't silently uninstall the entire Compute package.
/// <see cref="RegistryLookup.SuggestPackageForToken"/> still exposes the verb/namespace
/// association for diagnostic "did you mean" hints.
/// </summary>
public class RegistryLookupTests
{
    private const string PackageId = "Microsoft.Azure.Azclips.Extensions.Resources";

    private static ExtensionRegistry RegistryWith(ExtensionRegistryEntry entry, string id = PackageId)
    {
        var registry = new ExtensionRegistry();
        registry.Extensions[id] = entry;
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

        Assert.True(RegistryLookup.TryFind(registry, "MICROSOFT.AZURE.AZCLIPS.EXTENSIONS.RESOURCES", out var match));
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
    public void TryFind_ShouldNotMatchLegacyVerb()
    {
        // Verbs are sub-units of a package; matching here would let
        // `extension remove --name deployment` uninstall the whole Resources package.
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.False(RegistryLookup.TryFind(registry, "deployment", out _));
    }

    [Fact]
    public void TryFind_ShouldNotMatchNamespace()
    {
        // Same trap: namespaces are sub-units, not packages.
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.False(RegistryLookup.TryFind(registry, "group", out _));
    }

    [Fact]
    public void TryFind_ShouldNotMatchContributionVerb()
    {
        var entry = MultiNamespaceEntry();
        entry.Namespaces.Clear();
        var registry = RegistryWith(entry);

        Assert.False(RegistryLookup.TryFind(registry, "group", out _));
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

    // ── SuggestPackageForToken ──

    [Fact]
    public void SuggestPackageForToken_ShouldFindOwner_ByVerb()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.True(RegistryLookup.SuggestPackageForToken(registry, "deployment", out var owner));
        Assert.Equal(PackageId, owner);
    }

    [Fact]
    public void SuggestPackageForToken_ShouldFindOwner_ByNamespace()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.True(RegistryLookup.SuggestPackageForToken(registry, "group", out var owner));
        Assert.Equal(PackageId, owner);
    }

    [Fact]
    public void SuggestPackageForToken_ShouldReturnFalse_WhenTokenUnknown()
    {
        var registry = RegistryWith(MultiNamespaceEntry());

        Assert.False(RegistryLookup.SuggestPackageForToken(registry, "no-such-verb", out _));
    }

    [Fact]
    public void SuggestPackageForToken_ShouldReturnFalse_WhenAmbiguous()
    {
        // Two distinct packages share a contributed namespace. Without a unique winner we
        // refuse to suggest, since pointing the user at one arbitrarily would re-introduce
        // the same destructive trap we just removed.
        var registry = new ExtensionRegistry();
        registry.Extensions["Microsoft.Azure.Azclips.Extensions.Alpha"] = new()
        {
            Version = "1.0.0",
            Namespaces = { "shared" },
        };
        registry.Extensions["Microsoft.Azure.Azclips.Extensions.Beta"] = new()
        {
            Version = "1.0.0",
            Namespaces = { "shared" },
        };

        Assert.False(RegistryLookup.SuggestPackageForToken(registry, "shared", out _));
    }

    // ── ToShortName ──

    [Theory]
    [InlineData("Microsoft.Azure.Azclips.Extensions.Accounts", "Accounts")]
    [InlineData("microsoft.azure.azclips.extensions.accounts", "accounts")]
    [InlineData("Microsoft.Azure.Azclips.Extensions.Compute", "Compute")]
    [InlineData("microsoft.azure.azclips.extensions.compute", "compute")]
    [InlineData("Contoso.MyExtension", "Contoso.MyExtension")]
    [InlineData("Compute", "Compute")]
    public void ToShortName_ShouldStripKnownPrefix_CaseInsensitively(string input, string expected)
    {
        Assert.Equal(expected, RegistryLookup.ToShortName(input));
    }
}
