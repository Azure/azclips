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
            bool disableBodyLogging = false;
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
                request.Options.TryGetValue(RuntimeHttpRequestOptions.DisableBodyLogging, out disableBodyLogging);
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
            Assert.Equal("2026-04-06", storageVersion);
            Assert.True(DateTimeOffset.TryParse(storageDate, out _));
            Assert.NotNull(scopes);
            Assert.Equal(["https://storage.azure.com/.default"], scopes);
            Assert.True(disableRetry);
            Assert.True(disableBodyLogging);
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
    public async Task Upload_PreservesDotSegmentsInBlobName()
    {
        string directory = CreateTempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.bin");
            await File.WriteAllBytesAsync(sourcePath, [1]);
            Uri? requestUri = null;
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                requestUri = request.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
            var processor = CreateProcessor(
                new BlockBlobUploadProcessor_Orchestrator(),
                pipeline,
                new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "folder/../blob.bin";
            processor.FilePath = sourcePath;

            await processor.ProcessAsync();

            Assert.Equal(
                "https://account123.blob.core.windows.net/container/folder/../blob.bin",
                requestUri!.AbsoluteUri);
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
    public async Task UploadProcessor_MapsSwaggerRequestFields()
    {
        Guid requestId = Guid.NewGuid();
        DateTimeOffset modifiedSince = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(
                "https://account.blob.core.windows.net/container/blob?timeout=30",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("2026-04-06", request.Headers.GetValues("x-ms-version").Single());
            Assert.Equal(requestId.ToString(), request.Headers.GetValues("x-ms-client-request-id").Single());
            Assert.Equal("owner", request.Headers.GetValues("x-ms-meta-team").Single());
            Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
            Assert.Equal("Cool", request.Headers.GetValues("x-ms-access-tier").Single());
            Assert.Equal("*", request.Headers.GetValues("If-None-Match").Single());
            Assert.Equal(modifiedSince.ToString("R"), request.Headers.GetValues("If-Modified-Since").Single());
            Assert.Equal("kind=report", request.Headers.GetValues("x-ms-tags").Single());
            Assert.Equal("BlockBlob", request.Headers.GetValues("x-ms-blob-type").Single());
            Assert.Equal(3, request.Content!.Headers.ContentLength);
            Assert.Equal("application/octet-stream", request.Content.Headers.ContentType!.MediaType);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var processor = CreateProcessor(
            new BlockBlobUploadProcessor(),
            pipeline,
            new FakeOutputHandler());
        processor.Url = "https://account.blob.core.windows.net/container/blob";
        processor.ClientRequestId = requestId;
        processor.Metadata = new Dictionary<string, string> { ["team"] = "owner" };
        processor.Timeout = 30;
        processor.ContentLength = 3;
        processor.LeaseId = "lease";
        processor.Tier = "Cool";
        processor.IfNoneMatch = "*";
        processor.IfModifiedSince = modifiedSince;
        processor.BlobTagsString = "kind=report";
        processor.Body = new MemoryStream([1, 2, 3]);

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Upload_ExistingBlob_ExplainsOverwrite()
    {
        string directory = CreateTempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.bin");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            var pipeline = new FakeHttpPipeline(
                (_, _) => new HttpResponseMessage(HttpStatusCode.PreconditionFailed));
            var processor = CreateProcessor(new BlockBlobUploadProcessor_Orchestrator(), pipeline, new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = sourcePath;

            HttpRequestException exception =
                await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

            Assert.Contains("overwrite", exception.Message);
            Assert.Equal(HttpStatusCode.PreconditionFailed, exception.StatusCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_UnrelatedConflictPreservesOriginalError()
    {
        string directory = CreateTempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.bin");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            var pipeline = new FakeHttpPipeline(
                (_, _) => new HttpResponseMessage(HttpStatusCode.Conflict));
            var processor = CreateProcessor(
                new BlockBlobUploadProcessor_Orchestrator(),
                pipeline,
                new FakeOutputHandler());
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = sourcePath;

            HttpRequestException exception =
                await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

            Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
            Assert.DoesNotContain("overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            bool disableBodyLogging = false;
            var pipeline = new FakeHttpPipeline((request, _) =>
            {
                requestMethod = request.Method;
                storageVersion = request.Headers.GetValues("x-ms-version").Single();
                storageDate = request.Headers.GetValues("x-ms-date").Single();
                request.Options.TryGetValue(RuntimeHttpRequestOptions.Scopes, out scopes);
                request.Options.TryGetValue(RuntimeHttpRequestOptions.DisableBodyLogging, out disableBodyLogging);
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
            Assert.Equal("2026-04-06", storageVersion);
            Assert.True(DateTimeOffset.TryParse(storageDate, out _));
            Assert.NotNull(scopes);
            Assert.Equal(["https://storage.azure.com/.default"], scopes);
            Assert.True(disableBodyLogging);
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
    public async Task DownloadProcessor_MapsSwaggerRequestFields()
    {
        Guid requestId = Guid.NewGuid();
        DateTimeOffset unmodifiedSince = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "https://account.blob.core.windows.net/container/blob?snapshot=snapshot&versionid=version&timeout=30",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("2026-04-06", request.Headers.GetValues("x-ms-version").Single());
            Assert.Equal(requestId.ToString(), request.Headers.GetValues("x-ms-client-request-id").Single());
            Assert.Equal("bytes=0-2", request.Headers.GetValues("Range").Single());
            Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
            Assert.Equal("true", request.Headers.GetValues("x-ms-range-get-content-md5").Single());
            Assert.Equal("false", request.Headers.GetValues("x-ms-range-get-content-crc64").Single());
            Assert.Equal("\"etag\"", request.Headers.GetValues("If-Match").Single());
            Assert.Equal(unmodifiedSince.ToString("R"), request.Headers.GetValues("If-Unmodified-Since").Single());
            return new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([1, 2, 3])
            };
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(
            new BlobDownloadProcessor(),
            pipeline,
            output);
        processor.Url = "https://account.blob.core.windows.net/container/blob";
        processor.ClientRequestId = requestId;
        processor.Snapshot = "snapshot";
        processor.VersionId = "version";
        processor.Timeout = 30;
        processor.Range = "bytes=0-2";
        processor.LeaseId = "lease";
        processor.RangeGetContentMd5 = true;
        processor.RangeGetContentCrc64 = false;
        processor.IfMatch = "\"etag\"";
        processor.IfUnmodifiedSince = unmodifiedSince;

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        using Stream body = Assert.IsAssignableFrom<Stream>(Assert.Single(output.WrittenValues));
        var bytes = new byte[3];
        Assert.Equal(3, await body.ReadAsync(bytes));
        Assert.Equal([1, 2, 3], bytes);
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
            var publisher = new FakeEventPublisher();
            var processor = CreateProcessor(
                new BlobDownloadProcessor_Orchestrator(),
                pipeline,
                new FakeOutputHandler(),
                publisher: publisher);
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = destinationPath;

            HttpRequestException exception =
                await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

            Assert.IsType<IOException>(exception.InnerException);
            Assert.False(File.Exists(destinationPath));
            Assert.Empty(Directory.GetFiles(directory, "*.azclips-download-*"));
            Assert.Equal(Events.ProcessorProcessAsyncEnd, publisher.PublishedIds[^1]);
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
            var publisher = new FakeEventPublisher();
            var processor = CreateProcessor(
                new BlobDownloadProcessor_Orchestrator(),
                pipeline,
                new FakeOutputHandler(),
                publisher: publisher);
            processor.AccountName = "account123";
            processor.ContainerName = "container";
            processor.BlobName = "blob.bin";
            processor.FilePath = destinationPath;

            IOException exception = await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync());

            Assert.Contains("overwrite", exception.Message);
            Assert.Equal("original", await File.ReadAllTextAsync(destinationPath));
            Assert.Equal(0, pipeline.SendCount);
            Assert.Equal(Events.ProcessorProcessAsyncEnd, publisher.PublishedIds[^1]);
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
            bool initialRequestHadScopes = false;
            bool initialRequestDisabledAuthentication = false;
            bool initialRequestDisabledQueryLogging = false;
            Uri? initialRequestUri = null;
            var anonymousPipeline = new FakeHttpPipeline((request, _) =>
            {
                initialRequestHadScopes = request.Options.TryGetValue(
                    RuntimeHttpRequestOptions.Scopes,
                    out string[]? _);
                request.Options.TryGetValue(
                    RuntimeHttpRequestOptions.DisableProfileAuthentication,
                    out initialRequestDisabledAuthentication);
                request.Options.TryGetValue(
                    RuntimeHttpRequestOptions.DisableQueryLogging,
                    out initialRequestDisabledQueryLogging);
                initialRequestUri = request.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1])
                };
            });
            var anonymousOutput = new FakeOutputHandler();
            var anonymous = CreateProcessor(
                new BlobDownloadProcessor_Orchestrator(),
                anonymousPipeline,
                anonymousOutput);
            anonymous.AbsoluteUri =
                "https://account123.blob.core.windows.net/container/blob?sv=2026-04-06&sig=secret";
            anonymous.FilePath = Path.Combine(directory, "anonymous.bin");

            await anonymous.ProcessAsync();

            Assert.Equal([1], await File.ReadAllBytesAsync(anonymous.FilePath));
            Assert.False(initialRequestHadScopes);
            Assert.True(initialRequestDisabledAuthentication);
            Assert.True(initialRequestDisabledQueryLogging);
            Assert.Contains("sig=secret", initialRequestUri!.Query);
            BlobTransferResult result =
                Assert.IsType<BlobTransferResult>(Assert.Single(anonymousOutput.WrittenValues));
            Assert.Equal(
                "https://account123.blob.core.windows.net/container/blob",
                result.Source);

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
    public async Task ListProcessor_MapsSwaggerRequestFields()
    {
        Guid requestId = Guid.NewGuid();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "https://account.blob.core.windows.net/container?restype=container&comp=list&prefix=folder%2F&marker=next%20marker&maxresults=2&include=metadata%2Ctags&timeout=30&startFrom=start.txt",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("2026-04-06", request.Headers.GetValues("x-ms-version").Single());
            Assert.Equal(requestId.ToString(), request.Headers.GetValues("x-ms-client-request-id").Single());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<EnumerationResults><Blobs /><NextMarker /></EnumerationResults>")
            };
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(new ListStorageBlobProcessor(), pipeline, output);
        processor.Url = "https://account.blob.core.windows.net/container";
        processor.ClientRequestId = requestId;
        processor.Prefix = "folder/";
        processor.Marker = "next marker";
        processor.MaxResults = 2;
        processor.Include = ["metadata", "tags"];
        processor.Timeout = 30;
        processor.StartFrom = "start.txt";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(Assert.IsType<ListBlobsResponse>(Assert.Single(output.WrittenValues)).Blobs);
    }

    [Fact]
    public async Task GetPropertiesProcessor_MapsSwaggerRequestFields()
    {
        Guid requestId = Guid.NewGuid();
        DateTimeOffset modified = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            Assert.Equal(
                "https://account.blob.core.windows.net/container/blob?snapshot=snapshot&versionid=version&timeout=30",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("2026-04-06", request.Headers.GetValues("x-ms-version").Single());
            Assert.Equal(requestId.ToString(), request.Headers.GetValues("x-ms-client-request-id").Single());
            Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
            Assert.Equal("key", request.Headers.GetValues("x-ms-encryption-key").Single());
            Assert.Equal("hash", request.Headers.GetValues("x-ms-encryption-key-sha256").Single());
            Assert.Equal("AES256", request.Headers.GetValues("x-ms-encryption-algorithm").Single());
            Assert.Equal(modified.ToString("R"), request.Headers.GetValues("If-Modified-Since").Single());
            Assert.Equal("\"etag\"", request.Headers.GetValues("If-Match").Single());
            Assert.Equal("kind = 'report'", request.Headers.GetValues("x-ms-if-tags").Single());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var processor = CreateProcessor(
            new GetStorageBlobPropertiesProcessor(),
            pipeline,
            new FakeOutputHandler());
        processor.Url = "https://account.blob.core.windows.net/container/blob";
        processor.ClientRequestId = requestId;
        processor.Snapshot = "snapshot";
        processor.VersionId = "version";
        processor.Timeout = 30;
        processor.LeaseId = "lease";
        processor.EncryptionKey = "key";
        processor.EncryptionKeySha256 = "hash";
        processor.EncryptionAlgorithm = "AES256";
        processor.IfModifiedSince = modified;
        processor.IfMatch = "\"etag\"";
        processor.IfTags = "kind = 'report'";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task GetTagsProcessor_MapsSwaggerRequestFields()
    {
        Guid requestId = Guid.NewGuid();
        DateTimeOffset unmodified = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "https://account.blob.core.windows.net/container/blob?comp=tags&timeout=30&snapshot=snapshot&versionid=version",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("2026-04-06", request.Headers.GetValues("x-ms-version").Single());
            Assert.Equal(requestId.ToString(), request.Headers.GetValues("x-ms-client-request-id").Single());
            Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
            Assert.Equal("kind = 'report'", request.Headers.GetValues("x-ms-if-tags").Single());
            Assert.Equal(
                unmodified.ToString("R"),
                request.Headers.GetValues("x-ms-blob-if-unmodified-since").Single());
            Assert.Equal("\"etag\"", request.Headers.GetValues("x-ms-blob-if-none-match").Single());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<Tags><TagSet /></Tags>")
            };
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(
            new GetStorageBlobTagsProcessor(),
            pipeline,
            output);
        processor.Url = "https://account.blob.core.windows.net/container/blob";
        processor.ClientRequestId = requestId;
        processor.Timeout = 30;
        processor.Snapshot = "snapshot";
        processor.VersionId = "version";
        processor.LeaseId = "lease";
        processor.IfTags = "kind = 'report'";
        processor.BlobIfUnmodifiedSince = unmodified;
        processor.BlobIfNoneMatch = "\"etag\"";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(Assert.IsType<BlobTags>(Assert.Single(output.WrittenValues)).TagSet);
    }

    [Fact]
    public async Task List_FollowsMarkersAndHonorsResultLimit()
    {
        var queries = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            queries.Add(request.RequestUri!.Query);
            string xml = queries.Count == 1
                ? """
                  <EnumerationResults><Blobs><Blob><Name>one.txt</Name><Properties><Last-Modified>Mon, 24 Aug 2026 00:00:00 GMT</Last-Modified><Etag>"one"</Etag><Content-Length>1</Content-Length><Content-Type>text/plain</Content-Type><BlobType>BlockBlob</BlobType><AccessTier>Hot</AccessTier></Properties><Metadata><owner>test</owner></Metadata><BlobTags><TagSet><Tag><Key>kind</Key><Value>report</Value></Tag></TagSet></BlobTags></Blob><Blob><Name>two.txt</Name><Properties><Last-Modified>Mon, 24 Aug 2026 00:00:00 GMT</Last-Modified><Etag>"two"</Etag></Properties></Blob></Blobs><NextMarker>next marker</NextMarker></EnumerationResults>
                  """
                : """
                  <EnumerationResults><Blobs><Blob><Name>three.txt</Name><Properties><Last-Modified>Mon, 24 Aug 2026 00:00:00 GMT</Last-Modified><Etag>"three"</Etag></Properties></Blob></Blobs><NextMarker /></EnumerationResults>
                  """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) };
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(new ListStorageBlobProcessor_Orchestrator(), pipeline, output);
        processor.AccountName = "account123";
        processor.ContainerName = "container";
        processor.Prefix = "folder/";
        processor.NumResults = 3;
        processor.Include = ["metadata", "tags"];

        await processor.ProcessAsync();

        Assert.Equal(2, pipeline.SendCount);
        Assert.Contains("prefix=folder%2F", queries[0]);
        Assert.Contains("maxresults=3", queries[0]);
        Assert.Contains("include=metadata%2Ctags", queries[0]);
        Assert.Contains("marker=next%20marker", queries[1]);
        Assert.Contains("maxresults=1", queries[1]);
        StorageBlob[] blobs = Assert.IsType<StorageBlob[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(["one.txt", "two.txt", "three.txt"], blobs.Select(blob => blob.Name));
        Assert.Equal("test", blobs[0].Metadata["owner"]);
        Assert.Equal("report", blobs[0].Tags["kind"]);
        Assert.Equal(1, blobs[0].ContentLength);
    }

    [Fact]
    public async Task List_PreservesContinuationTokenWhenLimited()
    {
        var pipeline = new FakeHttpPipeline((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<EnumerationResults><Blobs><Blob><Name>one.txt</Name><Properties><Last-Modified>Mon, 24 Aug 2026 00:00:00 GMT</Last-Modified><Etag>\"one\"</Etag></Properties></Blob></Blobs><NextMarker>next marker</NextMarker></EnumerationResults>")
            });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(new ListStorageBlobProcessor_Orchestrator(), pipeline, output);
        processor.AccountName = "account123";
        processor.ContainerName = "container";
        processor.NumResults = 1;

        await processor.ProcessAsync();

        StorageBlob blob = Assert.Single(Assert.IsType<StorageBlob[]>(Assert.Single(output.WrittenValues)));
        Assert.Equal("next marker", blob.ContinuationToken);
    }

    [Fact]
    public async Task Show_ReturnsBlobProperties()
    {
        var methods = new List<HttpMethod>();
        var requests = new List<Uri>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!);
            methods.Add(request.Method);
            if (request.RequestUri!.Query == "?comp=tags")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "<Tags><TagSet><Tag><Key>kind</Key><Value>report</Value></Tag></TagSet></Tags>")
                };
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[12]),
                Headers = { ETag = new EntityTagHeaderValue("\"etag\"") },
            };
            response.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
            response.Headers.TryAddWithoutValidation("x-ms-access-tier", "Cool");
            response.Headers.TryAddWithoutValidation("x-ms-meta-owner", "test");
            return response;
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(new ShowStorageBlobProcessor_Orchestrator(), pipeline, output);
        processor.AccountName = "account123";
        processor.ContainerName = "container";
        processor.BlobName = "blob.txt";
        processor.IncludeTag = true;

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Head, HttpMethod.Get], methods);
        Assert.Equal("?comp=tags", requests[1].Query);
        StorageBlob blob = Assert.IsType<StorageBlob>(Assert.Single(output.WrittenValues));
        Assert.Equal("blob.txt", blob.Name);
        Assert.Equal("BlockBlob", blob.BlobType);
        Assert.Equal(12, blob.ContentLength);
        Assert.Equal("test", blob.Metadata["owner"]);
        Assert.Equal("report", blob.Tags["kind"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task Exists_ReturnsBoolean(HttpStatusCode status, bool expected)
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(status));
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        var processor = CreateProcessor(
            new BlobExistsProcessor_Orchestrator(),
            pipeline,
            output,
            publisher: publisher);
        processor.AccountName = "account123";
        processor.ContainerName = "container";
        processor.BlobName = "blob.txt";

        await processor.ProcessAsync();

        Assert.Equal(expected, Assert.IsType<bool>(Assert.Single(output.WrittenValues)));
        Assert.DoesNotContain(Events.ResourceOperationFailed, publisher.PublishedIds);
    }

    [Fact]
    public void ExtensionIndex_ExposesBlobCommands()
    {
        var index = new Microsoft.Azure.Azclips.Extensions.Storage.ExtensionIndex();

        IReadOnlyDictionary<string, CommandMetadata> commands = index.GetCommandTable(ClientType.CLI);

        Assert.Equal(5, commands.Count);
        Assert.IsType<BlockBlobUploadProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob upload"].ProcessorType));
        Assert.IsType<BlobDownloadProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob download"].ProcessorType));
        Assert.True(commands["storage blob upload"].Parameters["overwrite"].IsRequired is false);
        Assert.IsType<ListStorageBlobProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob list"].ProcessorType));
        Assert.IsType<ShowStorageBlobProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob show"].ProcessorType));
        Assert.IsType<BlobExistsProcessor_Orchestrator>(
            Activator.CreateInstance(commands["storage blob exists"].ProcessorType));

        IReadOnlyDictionary<string, CommandMetadata> powerShellCommands =
            index.GetCommandTable(ClientType.PowerShell);
        Assert.Equal(4, powerShellCommands.Count);
        Assert.IsType<BlockBlobUploadProcessor_Orchestrator>(
            Activator.CreateInstance(powerShellCommands["Set-AzStorageBlobContent"].ProcessorType));
        Assert.IsType<BlobDownloadProcessor_Orchestrator>(
            Activator.CreateInstance(powerShellCommands["Get-AzStorageBlobContent"].ProcessorType));
        Assert.IsType<ListStorageBlobProcessor_Orchestrator>(
            Activator.CreateInstance(powerShellCommands["Get-AzStorageBlob_List"].ProcessorType));
        Assert.IsType<ShowStorageBlobProcessor_Orchestrator>(
            Activator.CreateInstance(powerShellCommands["Get-AzStorageBlob_Get"].ProcessorType));
    }

    private static T CreateProcessor<T>(
        T processor,
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        bool authenticated = true,
        FakeEventPublisher? publisher = null)
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
            publisher ?? new FakeEventPublisher(),
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
