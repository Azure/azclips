// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Extensions.Storage.Tests;

public sealed class SourceIntegrationTests
{
    [Fact]
    public void WorkflowsAreLoadedFromTheProductAssemblyNotLinkedStagingSources()
    {
        var assembly = typeof(StorageContext).Assembly;
        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Storage", assembly.GetName().Name);
        Assert.NotEqual(typeof(SourceIntegrationTests).Assembly, assembly);
        Assert.Same(assembly, typeof(StartStorageBlobCopyProcessor).Assembly);
        Assert.Same(assembly, typeof(ListStorageAccountKeysProcessor).Assembly);
        Assert.Same(assembly, typeof(DownloadStorageFileProcessor).Assembly);
    }

    [Fact]
    public void DiscoveryExposesOnlyTheReviewedCliLeaves()
    {
        var contract = LeafReader.ReadContract(Path.GetDirectoryName(typeof(StorageContext).Assembly.Location)!);
        Assert.Equal(["storage"], contract.Verbs);
        Assert.Equal(
            [
                "storage account keys list", "storage blob copy start", "storage blob delete", "storage blob download",
                "storage blob download-batch", "storage blob exists", "storage blob list", "storage blob set-tier",
                "storage blob show", "storage blob upload", "storage directory create", "storage directory list",
                "storage file download", "storage file list", "storage file upload"
            ],
            contract.LeafKeys.Order(StringComparer.Ordinal));
    }
}
