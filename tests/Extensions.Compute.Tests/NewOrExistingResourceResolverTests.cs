// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

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
}
