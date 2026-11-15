// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Accounts;

namespace Extensions.Accounts.Tests;

/// <summary>
/// Unit tests for <see cref="TokenScopeResolver"/>. Verifies the resource / resource-type / scope
/// resolution rules, mutual exclusivity, the ARM default, and the <c>/.default</c> suffix behavior
/// (which mirrors Azure CLI's <c>resource_to_scopes</c>: the trailing slash of the resource is
/// preserved, so ARM resolves to a double-slash scope).
/// </summary>
public class TokenScopeResolverTests
{
    [Fact]
    public void Resolve_NoInput_DefaultsToArmScope()
    {
        var scopes = TokenScopeResolver.Resolve(resource: null, resourceTypeName: null, scopes: null);
        Assert.Equal(["https://management.core.windows.net//.default"], scopes);
    }

    [Fact]
    public void Resolve_Resource_AppendsDefaultSuffixPreservingTrailingSlash()
    {
        var scopes = TokenScopeResolver.Resolve("https://management.core.windows.net/", null, null);
        Assert.Equal(["https://management.core.windows.net//.default"], scopes);
    }

    [Fact]
    public void Resolve_ResourceWithoutTrailingSlash_AppendsSingleSlashDefault()
    {
        var scopes = TokenScopeResolver.Resolve("https://vault.azure.net", null, null);
        Assert.Equal(["https://vault.azure.net/.default"], scopes);
    }

    [Fact]
    public void Resolve_ResourceType_ResolvesViaWellKnownMapThenDefaultScope()
    {
        var scopes = TokenScopeResolver.Resolve(null, "key-vault", null);
        Assert.Equal(["https://vault.azure.net/.default"], scopes);
    }

    [Fact]
    public void Resolve_ScopeList_PassedThroughUnchanged()
    {
        string[] input = ["https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/Mail.Read"];
        var scopes = TokenScopeResolver.Resolve(null, null, input);
        Assert.Equal(input, scopes);
    }

    [Fact]
    public void Resolve_EmptyScopeList_ThrowsInsteadOfDefaultingToArm()
    {
        // An explicitly supplied but empty scope list (e.g. `--scope` with no values, or `-Scope @()`)
        // is an invalid required target, not "no target" - it must not silently fall back to ARM.
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve(null, null, Array.Empty<string>()));
        Assert.Equal("At least one scope must be supplied with --scope/-Scope.", ex.Message);
    }

    [Fact]
    public void Resolve_ScopeAlreadyDefault_NotDoubled()
    {
        var scopes = TokenScopeResolver.Resolve("https://vault.azure.net/.default", null, null);
        Assert.Equal(["https://vault.azure.net/.default"], scopes);
    }

    private const string MutuallyExclusiveMessage =
        "Specify at most one of a resource, a resource type, or a scope list - these inputs are mutually exclusive.";

    [Fact]
    public void Resolve_ResourceAndScope_ThrowsMutualExclusivity()
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve("https://vault.azure.net", null, ["https://graph.microsoft.com/.default"]));
        Assert.Equal(MutuallyExclusiveMessage, ex.Message);
        Assert.Equal(2, ex.ExitCode);
    }

    public static TheoryData<string?, string?, string[]?> MutuallyExclusiveCombinations() => new()
    {
        { "https://vault.azure.net", "key-vault", null },
        { null, "key-vault", ["https://graph.microsoft.com/.default"] },
        // Exclusivity is checked on presence (non-null), before any value validation.
        { "", "", [] },
    };

    [Theory]
    [MemberData(nameof(MutuallyExclusiveCombinations))]
    public void Resolve_MoreThanOneTarget_ThrowsUsageError(
        string? resource, string? resourceType, string[]? scopes)
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve(resource, resourceType, scopes));
        Assert.Equal(MutuallyExclusiveMessage, ex.Message);
    }

    [Fact]
    public void Resolve_UnknownResourceType_Throws()
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve(null, "not-a-real-type", null));
        Assert.Equal(
            $"Unknown resource type 'not-a-real-type'. Known resource types: {string.Join(", ", WellKnownResourceTypes.Names)}.",
            ex.Message);
    }

    [Fact]
    public void Resolve_EmptyScopeValue_Throws()
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve(null, null, ["  "]));
        Assert.Equal("Scope values must not be empty.", ex.Message);
    }

    [Fact]
    public void Resolve_EmptyStringScopeValue_ThrowsUsageError()
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve(null, null, [""]));
        Assert.Equal("Scope values must not be empty.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyResource_ThrowsInsteadOfDefaultingToArm(string resource)
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve(resource, null, null));
        Assert.Equal("Resource must not be empty.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyResourceType_ThrowsInsteadOfDefaultingToArm(string resourceType)
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            TokenScopeResolver.Resolve(null, resourceType, null));
        Assert.Equal("Resource type must not be empty.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDefaultScope_EmptyResource_ThrowsInvalidInput(string? resource)
    {
        // Same message as Resolve's empty-resource check, but raised as InvalidInputException rather than UsageErrorException.
        var ex = Assert.Throws<InvalidInputException>(() => TokenScopeResolver.ToDefaultScope(resource!));
        Assert.Equal("Resource must not be empty.", ex.Message);
    }
}
