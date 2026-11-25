// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class OsDiskStorageSkuResolverTests
{
    [Fact]
    public void Null_MeansServiceDefault()
    {
        Assert.Null(OsDiskStorageSkuResolver.Resolve(null));
    }

    [Theory]
    [InlineData("Premium_LRS", "Premium_LRS")]
    [InlineData("os=Premium_LRS", "Premium_LRS")]
    [InlineData("OS=StandardSSD_LRS", "StandardSSD_LRS")]
    [InlineData("os = Standard_LRS", "Standard_LRS")]
    public void SkuAndOsForms_ResolveToTheOsDiskSku(string input, string expected)
    {
        Assert.Equal(expected, OsDiskStorageSkuResolver.Resolve(input));
    }

    [Theory]
    [InlineData("0=Premium_LRS")]
    [InlineData("2=Standard_LRS")]
    public void DataDiskAssignment_IsRejected(string input)
    {
        var error = Assert.Throws<UsageErrorException>(() => OsDiskStorageSkuResolver.Resolve(input));

        Assert.Equal("Data-disk SKU assignments are not supported. Specify a SKU or os=SKU.", error.Message);
    }

    [Theory]
    [InlineData("data=Premium_LRS")]
    [InlineData("=Premium_LRS")]
    public void UnknownId_IsRejected(string input)
    {
        var error = Assert.Throws<UsageErrorException>(() => OsDiskStorageSkuResolver.Resolve(input));

        Assert.StartsWith("A sku ID is incorrect.", error.Message);
    }

    [Fact]
    public void OsWithoutSku_IsRejected()
    {
        var error = Assert.Throws<UsageErrorException>(() => OsDiskStorageSkuResolver.Resolve("os="));

        Assert.StartsWith("A sku's format is incorrect.", error.Message);
    }
}
