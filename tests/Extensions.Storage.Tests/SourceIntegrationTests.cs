// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Extensions.Storage.Tests;

public sealed class SourceIntegrationTests
{
    [Fact]
    public void OrchestratorsAndHelpersAreLoadedFromTheProductAssemblyNotLinkedStagingSources()
    {
        var assembly = typeof(StorageContext).Assembly;
        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Storage", assembly.GetName().Name);
        Assert.NotEqual(typeof(SourceIntegrationTests).Assembly, assembly);
        Assert.Same(assembly, typeof(StartStorageBlobCopyProcessor_Orchestrator).Assembly);
        Assert.Same(assembly, typeof(ListStorageAccountKeysWorkflow).Assembly);
        Assert.Same(assembly, typeof(DownloadStorageFileProcessor_Orchestrator).Assembly);
    }

    [Fact]
    public void OnlySharedTransferAndAuthorizationWorkflowsRemain()
    {
        string[] expected =
        [
            nameof(DownloadStorageBlobWorkflow),
            nameof(ListStorageAccountKeysWorkflow),
            nameof(UploadStorageAppendBlobWorkflow),
            nameof(UploadStorageBlockBlobWorkflow),
            nameof(UploadStoragePageBlobWorkflow)
        ];
        var workflows = typeof(StorageContext).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(StorageContext).Namespace &&
                type.Name.EndsWith("Workflow", StringComparison.Ordinal))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected.Order(StringComparer.Ordinal), workflows);
    }

    [Fact]
    public void DiscoveryExposesOnlyTheReviewedCliLeaves()
    {
        var contract = LeafReader.ReadContract(Path.GetDirectoryName(typeof(StorageContext).Assembly.Location)!);
        Assert.Equal(["storage"], contract.Verbs);
        Assert.Equal(
            [
                "storage account create", "storage account delete", "storage account list",
                "storage account show", "storage blob copy start", "storage blob delete",
                "storage blob download-batch", "storage blob exists", "storage blob list", "storage blob set-tier",
                "storage blob show", "storage blob upload", "storage directory list",
                "storage file download", "storage file list", "storage file upload"
            ],
            contract.LeafKeys.Order(StringComparer.Ordinal));
    }
}
