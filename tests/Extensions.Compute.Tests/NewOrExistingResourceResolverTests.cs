// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Processors;
using Core.Abstractions;

namespace Extensions.Compute.Tests;

/// <summary>
/// Unit tests for <c>NewOrExistingResourceResolver</c>, which maps vm create's <c>--nsg</c> and
/// <c>--public-ip-address</c> values to an existing resource id or a name to create.
/// </summary>
public class NewOrExistingResourceResolverTests
{
    [Fact]
    public async Task Whitespace_IsLookedUpAsAName()
    {
        var lookups = new List<string>();

        var (existingId, nameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            " ", "vmNSG", "sub", "rg", NewOrExistingResourceResolver.NetworkSecurityGroup,
            (_, _, name) =>
            {
                lookups.Add(name);
                return Task.FromResult<string?>(null);
            });

        // As in legacy CLI (nsg == ''), only "" means none; whitespace is a name, which ARM rejects.
        Assert.Equal([" "], lookups);
        Assert.Null(existingId);
        Assert.Equal(" ", nameToCreate);
    }

    [Fact]
    public async Task MissingResourceId_IsUsageError()
    {
        var error = await Assert.ThrowsAsync<UsageErrorException>(() =>
            NewOrExistingResourceResolver.ResolveAsync(
                "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/publicIPAddresses/missing",
                "vmPublicIP", "sub", "rg", NewOrExistingResourceResolver.PublicIpAddress,
                (_, _, _) => Task.FromResult<string?>(null)));

        Assert.Equal("UsageError", error.Code);
        Assert.Contains("Public IP address", error.Message);
        Assert.Contains("was not found", error.Message);
    }
}
