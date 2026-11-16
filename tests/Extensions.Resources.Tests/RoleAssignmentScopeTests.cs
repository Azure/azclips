// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Extensions.Resources.Tests;

/// <summary>Direct tests for the shared role assignment scope resolution in <see cref="RoleAssignmentScope"/>.</summary>
public class RoleAssignmentScopeTests
{
    private const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Theory]
    [InlineData("/subscriptions/x", "test-rg")]
    [InlineData("", "")]
    public void Resolve_ScopeAndResourceGroup_ThrowsUsageError(string scope, string resourceGroupName)
    {
        Exception? exception = Record.Exception(() => RoleAssignmentScope.Resolve(SubscriptionId, scope, resourceGroupName));

        Assert.Equal("Specify either a scope or a resource group, not both.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankScope_ThrowsUsageError(string scope)
    {
        Exception? exception = Record.Exception(() => RoleAssignmentScope.Resolve(SubscriptionId, scope, null));

        Assert.Equal("Scope must not be empty.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankResourceGroup_ThrowsUsageError(string resourceGroupName)
    {
        Exception? exception = Record.Exception(() => RoleAssignmentScope.Resolve(SubscriptionId, null, resourceGroupName));

        Assert.Equal("Resource group must not be empty.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ResourceGroupWithoutSubscription_ThrowsMissingRequiredArgument(string? subscriptionId)
    {
        Exception? exception = Record.Exception(() => RoleAssignmentScope.Resolve(subscriptionId, null, "test-rg"));

        Assert.Equal(
            "A subscription is required to resolve a resource group scope. Specify a subscription, or set an active subscription.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NoScopeResourceGroupOrSubscription_ThrowsMissingRequiredArgument(string? subscriptionId)
    {
        Exception? exception = Record.Exception(() => RoleAssignmentScope.Resolve(subscriptionId, null, null));

        Assert.Equal(
            "A scope is required. Specify a scope or a resource group, or set an active subscription.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("//")]
    [InlineData(" //subscriptions/x ")]
    public void Resolve_ScopeWithMultipleLeadingSlashes_ThrowsUsageError(string scope)
    {
        Exception? exception = Record.Exception(() => RoleAssignmentScope.Resolve(SubscriptionId, scope, null));

        Assert.Equal($"The scope '{scope.Trim()}' must not start with multiple slashes.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    // "/" is the tenant-root scope, which normalizes to an empty string.
    [InlineData(" subscriptions/x/ ", null, "/subscriptions/x")]
    [InlineData("/", null, "")]
    [InlineData(null, " test-rg ", $"/subscriptions/{SubscriptionId}/resourceGroups/test-rg")]
    [InlineData(null, null, $"/subscriptions/{SubscriptionId}")]
    public void Resolve_ValidInputs_ReturnsScope(string? scope, string? resourceGroupName, string expected)
    {
        string resolvedScope = RoleAssignmentScope.Resolve(SubscriptionId, scope, resourceGroupName);

        Assert.Equal(expected, resolvedScope);
    }
}
