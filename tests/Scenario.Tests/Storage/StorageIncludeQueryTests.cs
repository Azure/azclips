using Core.Abstractions;
using Blob = Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors;
using FileService = Microsoft.Azure.AzClips.Extensions.StorageFile.Processors;

namespace Scenario.Tests;

public class StorageIncludeQueryTests
{
    public static IEnumerable<object?[]> IncludeCases()
    {
        foreach (string service in new[]
        {
            "blob", "share", "file", "containers", "hierarchy", "flat-arrow", "hierarchy-arrow",
            "tags-container", "tags-service",
        })
        {
            (string first, string second) = service switch
            {
                "file" => ("Timestamps", "Etag"),
                "containers" => ("metadata", "deleted"),
                "tags-container" or "tags-service" => ("none", "versions"),
                _ => ("metadata", "snapshots"),
            };
            yield return [service, null, null];
            yield return [service, Array.Empty<string>(), null];
            yield return [service, new[] { first }, first];
            yield return [service, new[] { first, second }, $"{first}%2C{second}"];
        }
    }

    [Theory]
    [MemberData(nameof(IncludeCases))]
    public void IncludeIsCommaDelimitedAndOmittedWhenEmpty(string service, string[]? include, string? expected)
    {
        string uri = service switch
        {
            "blob" => new BlobListProbe { Url = "https://fixture.invalid/container", Include = include }.RequestUri(),
            "share" => new ShareListProbe { Url = "https://fixture.invalid", Include = include }.RequestUri(),
            "file" => new FileListProbe { Url = "https://fixture.invalid/share", Include = include }.RequestUri(),
            "containers" => new BlobContainerListProbe { Url = "https://fixture.invalid", Include = include }.RequestUri(),
            "hierarchy" => new BlobHierarchyListProbe { Url = "https://fixture.invalid/container", Include = include }.RequestUri(),
            "flat-arrow" => new BlobFlatArrowListProbe { Url = "https://fixture.invalid/container", Include = include }.RequestUri(),
            "hierarchy-arrow" => new BlobHierarchyArrowListProbe { Url = "https://fixture.invalid/container", Include = include }.RequestUri(),
            "tags-container" => new BlobContainerTagsProbe { Url = "https://fixture.invalid/container", Include = include }.RequestUri(),
            "tags-service" => new BlobServiceTagsProbe { Url = "https://fixture.invalid", Include = include }.RequestUri(),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null),
        };
        string[] query = new Uri(uri).Query.TrimStart('?').Split('&');

        if (expected is null)
        {
            Assert.DoesNotContain(query, pair => pair.StartsWith("include=", StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal($"include={expected}", Assert.Single(query, pair => pair.StartsWith("include=", StringComparison.Ordinal)));
        }
        Assert.Contains(service.StartsWith("tags-", StringComparison.Ordinal) ? "comp=blobs" : "comp=list", query);
    }

    [Theory]
    [InlineData(ClientType.CLI, "storage blob list", "include", typeof(Blob.ListBlobsContainerProcessor))]
    [InlineData(ClientType.CLI, "storage share list", "include", typeof(FileService.ListSharesSegmentServiceProcessor))]
    [InlineData(ClientType.CLI, "storage file list", "include", typeof(FileService.ListFilesAndDirectoriesSegmentDirectoryProcessor))]
    [InlineData(ClientType.PowerShell, "Get-AzStorageBlob_List", "Include", typeof(Blob.ListBlobsContainerProcessor))]
    [InlineData(ClientType.PowerShell, "Get-AzStorageShare_List", "Include", typeof(FileService.ListSharesSegmentServiceProcessor))]
    [InlineData(ClientType.PowerShell, "Get-AzStorageFile_List", "Include", typeof(FileService.ListFilesAndDirectoriesSegmentDirectoryProcessor))]
    public void BothSurfacesUseTheCorrectedProcessors(ClientType clientType, string command, string parameter, Type processorType)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Storage.ExtensionIndex().GetCommandMetadata(command, clientType);

        Assert.Equal(processorType, metadata.ProcessorType);
        Assert.Equal("Include", metadata.Parameters[parameter].Name);
    }

    private sealed class BlobListProbe : Blob.ListBlobsContainerProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class ShareListProbe : FileService.ListSharesSegmentServiceProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class FileListProbe : FileService.ListFilesAndDirectoriesSegmentDirectoryProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class BlobContainerListProbe : Blob.ListContainersServiceProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class BlobHierarchyListProbe : Blob.ListBlobHierarchySegmentContainerProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class BlobFlatArrowListProbe : Blob.ListBlobFlatSegmentApacheArrowContainerProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class BlobHierarchyArrowListProbe : Blob.ListBlobHierarchySegmentApacheArrowContainerProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class BlobContainerTagsProbe : Blob.FindBlobsByTagsContainerProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }

    private sealed class BlobServiceTagsProbe : Blob.FindBlobsByTagsServiceProcessor
    {
        public string RequestUri() => CreateRequestUri();
    }
}
