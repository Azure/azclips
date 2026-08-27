// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Xunit;

namespace Cli.Tests;

public class StorageBlobProcessorTests
{
    [Fact]
    public async Task Upload_StreamsBlockBlobWithStorageScopeAndOverwriteProtection()
    {
        string directory = CreateTempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.bin");
            byte[] expected = [0, 1, 2, 3, 255];
            await File.WriteAllBytesAsync(sourcePath, expected);
            byte[]? uploaded = null;
            Uri? requestUri = null;
            HttpMethod? requestMethod = null;
            string? blobType = null;
            string? ifNoneMatch = null;
            string? storageVersion = null;
            string? storageDate = null;
            string[]? scopes = null;
            bool disableRetry = false;
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                requestUri = request.RequestUri;
                requestMethod = request.Method;
                uploaded = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                blobType = request.Headers.GetValues("x-ms-blob-type").Single();
                ifNoneMatch = request.Headers.GetValues("If-None-Match").Single();
                storageVersion = request.Headers.GetValues("x-ms-version").Single();
                storageDate = request.Headers.GetValues("x-ms-date").Single();
                request.Options.TryGetValue(RuntimeHttpRequestOptions.Scopes, out scopes);
                request.Options.TryGetValue(RuntimeHttpRequestOptions.DisableRetry, out disableRetry);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Headers =
                    {
                        ETag = new EntityTagHeaderValue("\"etag\""),
                    }
                };
            });
            var output = new FakeOutputHandler();
            var processor = CreateProcessor(new BlockBlobUploadProcessor_Orchestrator(), pipeline, output);
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "folder/blob name.bin";
            processor.FilePath = sourcePath;

            await processor.ProcessAsync();

            Assert.Equal(expected, uploaded);
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("BlockBlob", blobType);
            Assert.Equal("*", ifNoneMatch);
            Assert.Equal("2023-11-03", storageVersion);
            Assert.True(DateTimeOffset.TryParse(storageDate, out _));
            Assert.NotNull(scopes);
            Assert.Equal(["https://storage.azure.com/.default"], scopes);
            Assert.True(disableRetry);
            Assert.Equal(
                "https://account123.blob.core.windows.net/container/folder/blob%20name.bin",
                requestUri!.AbsoluteUri);
            var result = Assert.IsType<BlobTransferResult>(Assert.Single(output.WrittenValues));
            Assert.Equal("upload", result.Operation);
            Assert.Equal(sourcePath, result.Source);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_Overwrite_DoesNotSendIfNoneMatch()
    {
        string directory = CreateTempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.bin");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            bool sentIfNoneMatch = false;
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                sentIfNoneMatch = request.Headers.Contains("If-None-Match");
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
            var processor = CreateProcessor(new BlockBlobUploadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = sourcePath;
            processor.Overwrite = true;

            await processor.ProcessAsync();

            Assert.False(sentIfNoneMatch);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_AppliesMetadataTagsPropertiesAndTier()
    {
        string directory = CreateTempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.bin");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            HttpRequestMessage? sent = null;
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                sent = request;
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
            var processor = CreateProcessor(new BlockBlobUploadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = sourcePath;
            processor.Metadata = ["owner=test"];
            processor.Tags = ["kind=report"];
            processor.Properties = ["ContentType=text/plain"];
            processor.StandardBlobTier = "Cool";
            processor.EncryptionScope = "scope1";

            await processor.ProcessAsync();

            Assert.Equal("test", sent!.Headers.GetValues("x-ms-meta-owner").Single());
            Assert.Equal("kind=report", sent.Headers.GetValues("x-ms-tags").Single());
            Assert.Equal("text/plain", sent.Headers.GetValues("x-ms-blob-content-type").Single());
            Assert.Equal("Cool", sent.Headers.GetValues("x-ms-access-tier").Single());
            Assert.Equal("scope1", sent.Headers.GetValues("x-ms-encryption-scope").Single());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_PageAndAppendBlobsUseTheirRequiredRequests()
    {
        string directory = CreateTempDirectory();
        try
        {
            string pagePath = Path.Combine(directory, "page.bin");
            await File.WriteAllBytesAsync(pagePath, new byte[512]);
            var pageRequests = new List<(string? Type, string Query, string? Range)>();
            var pagePipeline = new FakeHttpPipeline((request, _) =>
            {
                pageRequests.Add((
                    request.Headers.TryGetValues("x-ms-blob-type", out var types) ? types.Single() : null,
                    request.RequestUri!.Query,
                    request.Headers.TryGetValues("x-ms-range", out var ranges) ? ranges.Single() : null));
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
            var page = CreateProcessor(new BlockBlobUploadProcessor_Orchestrator(), pagePipeline, new FakeOutputHandler());
            page.AccountName = "account123";
            page.ContainerName = "container";
            page.BlobName = "page.bin";
            page.FilePath = pagePath;
            page.BlobType = "Page";

            await page.ProcessAsync();

            Assert.Equal(("PageBlob", "", (string?)null), pageRequests[0]);
            Assert.Equal((null, "?comp=page", "bytes=0-511"), pageRequests[1]);

            string appendPath = Path.Combine(directory, "append.bin");
            await File.WriteAllBytesAsync(appendPath, [1, 2, 3]);
            var appendQueries = new List<string>();
            var appendPipeline = new FakeHttpPipeline((request, _) =>
            {
                appendQueries.Add(request.RequestUri!.Query);
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
            var append = CreateProcessor(new BlockBlobUploadProcessor_Orchestrator(), appendPipeline, new FakeOutputHandler());
            append.AccountName = "account123";
            append.ContainerName = "container";
            append.BlobName = "append.bin";
            append.FilePath = appendPath;
            append.BlobType = "Append";

            await append.ProcessAsync();

            Assert.Equal(["", "?comp=appendblock"], appendQueries);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.PreconditionFailed)]
    public async Task Upload_ExistingBlob_ExplainsOverwrite(HttpStatusCode statusCode)
    {
        string directory = CreateTempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.bin");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(statusCode));
            var processor = CreateProcessor(new BlockBlobUploadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = sourcePath;

            HttpRequestException exception =
                await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

            Assert.Contains("--overwrite", exception.Message);
            Assert.Equal(statusCode, exception.StatusCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Download_StreamsToDestinationAndWritesResult()
    {
        string directory = CreateTempDirectory();
        try
        {
            string destinationPath = Path.Combine(directory, "download.bin");
            byte[] expected = [9, 8, 7, 0, 255];
            HttpMethod? requestMethod = null;
            string? storageVersion = null;
            string? storageDate = null;
            string[]? scopes = null;
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                requestMethod = request.Method;
                storageVersion = request.Headers.GetValues("x-ms-version").Single();
                storageDate = request.Headers.GetValues("x-ms-date").Single();
                request.Options.TryGetValue(RuntimeHttpRequestOptions.Scopes, out scopes);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(expected)
                };
            });
            var output = new FakeOutputHandler();
            var processor = CreateProcessor(new BlobDownloadProcessor_Orchestrator(), pipeline, output);
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = destinationPath;

            await processor.ProcessAsync();

            Assert.Equal(expected, await File.ReadAllBytesAsync(destinationPath));
            Assert.Equal(HttpMethod.Get, requestMethod);
            Assert.Equal("2023-11-03", storageVersion);
            Assert.True(DateTimeOffset.TryParse(storageDate, out _));
            Assert.NotNull(scopes);
            Assert.Equal(["https://storage.azure.com/.default"], scopes);
            var result = Assert.IsType<BlobTransferResult>(Assert.Single(output.WrittenValues));
            Assert.Equal("download", result.Operation);
            Assert.Equal(destinationPath, result.Destination);
            Assert.Empty(Directory.GetFiles(directory, "*.azclips-download-*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Download_FailedCopy_RemovesTemporaryFile()
    {
        string directory = CreateTempDirectory();
        try
        {
            string destinationPath = Path.Combine(directory, "download.bin");
            var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ThrowingDownloadContent()
            });
            var processor = CreateProcessor(new BlobDownloadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = destinationPath;

            HttpRequestException exception =
                await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

            Assert.IsType<IOException>(exception.InnerException);
            Assert.False(File.Exists(destinationPath));
            Assert.Empty(Directory.GetFiles(directory, "*.azclips-download-*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Download_DoesNotReplaceExistingFileWithoutOverwrite()
    {
        string directory = CreateTempDirectory();
        try
        {
            string destinationPath = Path.Combine(directory, "download.bin");
            await File.WriteAllTextAsync(destinationPath, "original");
            var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("replacement", Encoding.UTF8)
            });
            var processor = CreateProcessor(new BlobDownloadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = destinationPath;

            IOException exception = await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync());

            Assert.Contains("--overwrite", exception.Message);
            Assert.Equal("original", await File.ReadAllTextAsync(destinationPath));
            Assert.Equal(0, pipeline.SendCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Download_DirectoryDestinationUsesBlobNameAndChecksMd5()
    {
        string directory = CreateTempDirectory();
        try
        {
            byte[] expected = [9, 8, 7];
            Uri? requestUri = null;
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                requestUri = request.RequestUri;
                var content = new ByteArrayContent(expected);
                content.Headers.ContentMD5 = Convert.FromBase64String("DI6D171OTV6cFwkySCwyZA==");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });
            var processor = CreateProcessor(new BlobDownloadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "folder/report.bin";
            processor.FilePath = directory;
            processor.SnapshotTime = "2026-08-24T00:00:00.0000000Z";
            processor.CheckMd5 = true;

            await processor.ProcessAsync();

            Assert.Contains("snapshot=", requestUri!.Query);
            string downloaded = Assert.Single(Directory.GetFiles(directory, "report.bin.*"));
            Assert.Equal(expected, await File.ReadAllBytesAsync(downloaded));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Download_RejectsNonAzureAbsoluteUri()
    {
        string directory = CreateTempDirectory();
        try
        {
            var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
            var processor = CreateProcessor(new BlobDownloadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AbsoluteUri = "https://example.com/blob";
            processor.FilePath = Path.Combine(directory, "blob");

            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

            Assert.Equal(0, pipeline.SendCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Download_DirectUriSupportsAnonymousAndManagedDiskAudienceRetry()
    {
        string directory = CreateTempDirectory();
        try
        {
            var anonymousPipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1])
            });
            var anonymous = CreateProcessor(
                new BlobDownloadProcessor_Orchestrator(),
                anonymousPipeline,
                new FakeOutputHandler(),
                authenticated: false);
            anonymous.AbsoluteUri = "https://account123.blob.core.windows.net/container/blob";
            anonymous.FilePath = Path.Combine(directory, "anonymous.bin");

            await anonymous.ProcessAsync();

            Assert.Equal([1], await File.ReadAllBytesAsync(anonymous.FilePath));

            string[]? retryScopes = null;
            var managedDiskPipeline = new FakeHttpPipeline((request, _) =>
            {
                if (!request.Options.TryGetValue(RuntimeHttpRequestOptions.Scopes, out retryScopes)
                    || retryScopes.SequenceEqual(["https://storage.azure.com/.default"]))
                {
                    var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    unauthorized.Headers.TryAddWithoutValidation(
                        "WWW-Authenticate",
                        "Bearer resource_id=\"https://disk.azure.com\"");
                    return unauthorized;
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([2]) };
            });
            var managedDisk = CreateProcessor(
                new BlobDownloadProcessor_Orchestrator(),
                managedDiskPipeline,
                new FakeOutputHandler());
            managedDisk.AbsoluteUri = "https://md-account.blob.core.windows.net/container/blob";
            managedDisk.FilePath = Path.Combine(directory, "managed.bin");

            await managedDisk.ProcessAsync();

            Assert.NotNull(retryScopes);
            Assert.Equal(["https://disk.azure.com/.default"], retryScopes!);
            Assert.Equal([2], await File.ReadAllBytesAsync(managedDisk.FilePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExtensionIndex_ExposesUploadAndDownloadCommands()
    {
        var index = new Microsoft.Azure.Azclips.Extensions.Storage.ExtensionIndex();

        IReadOnlyDictionary<string, CommandMetadata> commands = index.GetCommandTable(ClientType.CLI);

        Assert.Equal(4, commands.Count);
        Assert.IsType<BlockBlobUploadProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob upload"].ProcessorType));
        Assert.IsType<BlobDownloadProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob download"].ProcessorType));
        Assert.IsType<BlobUploadBatchProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob upload-batch"].ProcessorType));
        Assert.IsType<BlobDownloadBatchProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob download-batch"].ProcessorType));
        Assert.True(commands["storage blob upload"].Parameters["overwrite"].IsRequired is false);

        IReadOnlyDictionary<string, CommandMetadata> powerShellCommands =
            index.GetCommandTable(ClientType.PowerShell);
        Assert.Equal(2, powerShellCommands.Count);
        Assert.IsType<BlockBlobUploadProcessor_Orchestrator>(
            Activator.CreateInstance(powerShellCommands["Set-AzStorageBlobContent"].ProcessorType));
        Assert.IsType<BlobDownloadProcessor_Orchestrator>(
            Activator.CreateInstance(powerShellCommands["Get-AzStorageBlobContent"].ProcessorType));
    }

    [Fact]
    public async Task BatchTransfersProcessOnlyMatchingFiles()
    {
        string directory = CreateTempDirectory();
        try
        {
            string source = Path.Combine(directory, "source");
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            await File.WriteAllTextAsync(Path.Combine(source, "nested", "report.csv"), "upload");
            await File.WriteAllTextAsync(Path.Combine(source, "skip.txt"), "skip");
            var uploads = new List<(string Uri, string Content)>();
            var uploadPipeline = new FakeHttpPipeline((request, _) =>
            {
                uploads.Add((
                    request.RequestUri!.AbsoluteUri,
                    request.Content!.ReadAsStringAsync().GetAwaiter().GetResult()));
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
            var uploadOutput = new FakeOutputHandler();
            var upload = CreateProcessor(
                new BlobUploadBatchProcessor_Orchestrator(),
                uploadPipeline,
                uploadOutput);
            upload.AccountName = "account123";
            upload.ContainerName = "container";
            upload.SourceDirectory = source;
            upload.Pattern = "*.csv";
            upload.DestinationPath = "archive";

            await upload.ProcessAsync();

            (string uploadedUri, string uploadedContent) = Assert.Single(uploads);
            Assert.EndsWith("/container/archive/nested/report.csv", uploadedUri);
            Assert.Equal("upload", uploadedContent);
            BlobTransferResult uploadResult =
                Assert.IsType<BlobTransferResult>(Assert.Single(uploadOutput.WrittenValues));
            Assert.Equal("upload", uploadResult.Operation);
            Assert.EndsWith("nested/report.csv", uploadResult.Destination);

            const string listing = """
                <EnumerationResults>
                  <Blobs>
                    <Blob><Name>nested/report.csv</Name></Blob>
                    <Blob><Name>skip.txt</Name></Blob>
                  </Blobs>
                  <NextMarker />
                </EnumerationResults>
                """;
            var downloadPipeline = new FakeHttpPipeline((request, _) =>
                request.RequestUri!.Query.Contains("comp=list", StringComparison.Ordinal)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(listing, Encoding.UTF8, "application/xml")
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("download", Encoding.UTF8)
                    });
            string destination = Path.Combine(directory, "destination");
            var downloadOutput = new FakeOutputHandler();
            var download = CreateProcessor(
                new BlobDownloadBatchProcessor_Orchestrator(),
                downloadPipeline,
                downloadOutput);
            download.AccountName = "account123";
            download.ContainerName = "container";
            download.DestinationDirectory = destination;
            download.Pattern = "*.csv";

            await download.ProcessAsync();

            Assert.Equal(
                "download",
                await File.ReadAllTextAsync(Path.Combine(destination, "nested", "report.csv")));
            Assert.False(File.Exists(Path.Combine(destination, "skip.txt")));
            Assert.Equal(2, downloadPipeline.SendCount);
            BlobTransferResult downloadResult =
                Assert.IsType<BlobTransferResult>(Assert.Single(downloadOutput.WrittenValues));
            Assert.Equal("download", downloadResult.Operation);
            Assert.EndsWith(Path.Combine("nested", "report.csv"), downloadResult.Destination);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BatchDownloadFollowsMarkersAndOverwrites()
    {
        string directory = CreateTempDirectory();
        try
        {
            string destinationPath = Path.Combine(directory, "report.csv");
            await File.WriteAllTextAsync(destinationPath, "old");
            var listQueries = new List<string>();
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                if (!request.RequestUri!.Query.Contains("comp=list", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("new", Encoding.UTF8)
                    };
                }

                listQueries.Add(request.RequestUri.Query);
                string listing = listQueries.Count == 1
                    ? "<EnumerationResults><Blobs /><NextMarker>next marker</NextMarker></EnumerationResults>"
                    : "<EnumerationResults><Blobs><Blob><Name>report.csv</Name></Blob></Blobs><NextMarker /></EnumerationResults>";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(listing, Encoding.UTF8, "application/xml")
                };
            });
            var processor = CreateProcessor(
                new BlobDownloadBatchProcessor_Orchestrator(),
                pipeline,
                new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.DestinationDirectory = directory;
            processor.Overwrite = true;

            await processor.ProcessAsync();

            Assert.Equal(2, listQueries.Count);
            Assert.DoesNotContain("marker=", listQueries[0]);
            Assert.Contains("marker=next%20marker", listQueries[1]);
            Assert.Equal("new", await File.ReadAllTextAsync(destinationPath));
            Assert.Equal(3, pipeline.SendCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("../evil.csv")]
    [InlineData("../../evil.csv")]
    [InlineData("sub/../../evil.csv")]
    [InlineData("sub/sub2/../../../evil.csv")]
    public async Task BatchDownloadRejectsPathTraversal(string blobName)
    {
        string directory = CreateTempDirectory();
        try
        {
            string listing =
                $"<EnumerationResults><Blobs><Blob><Name>{blobName}</Name></Blob></Blobs><NextMarker /></EnumerationResults>";
            var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(listing, Encoding.UTF8, "application/xml")
            });
            var processor = CreateProcessor(
                new BlobDownloadBatchProcessor_Orchestrator(),
                pipeline,
                new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.DestinationDirectory = Path.Combine(directory, "destination");

            InvalidDataException exception =
                await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());

            Assert.Contains("outside the destination directory", exception.Message);
            Assert.Equal(1, pipeline.SendCount);
            Assert.False(File.Exists(Path.Combine(directory, "evil.csv")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static T CreateProcessor<T>(
        T processor,
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        bool authenticated = true)
        where T : StorageBlobProcessorBase
    {
        var profile = new Profile
        {
            TenantId = "tenant",
            SubscriptionId = "subscription",
            CloudName = "AzureCloud",
        };
        Profile? resolvedProfile = authenticated ? profile : null;
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(resolvedProfile),
            output,
            resolvedProfile));
        return processor;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"azclips-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ThrowingDownloadContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(new byte[] { 1, 2, 3 });
            throw new IOException("Simulated download interruption.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 6;
            return true;
        }
    }
}
