// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
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
            var processor = CreateProcessor(new UploadBlobProcessor(), pipeline, output);
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
            var processor = CreateProcessor(new UploadBlobProcessor(), pipeline, new FakeOutputHandler());
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
            var processor = CreateProcessor(new UploadBlobProcessor(), pipeline, new FakeOutputHandler());
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
            var processor = CreateProcessor(new DownloadBlobProcessor(), pipeline, output);
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
            var processor = CreateProcessor(new DownloadBlobProcessor(), pipeline, new FakeOutputHandler());
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
            var processor = CreateProcessor(new DownloadBlobProcessor(), pipeline, new FakeOutputHandler());
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
    public void ExtensionIndex_ExposesUploadAndDownloadCommands()
    {
        var index = new Microsoft.Azure.Azclips.Extensions.Storage.ExtensionIndex();

        IReadOnlyDictionary<string, CommandMetadata> commands = index.GetCommandTable(ClientType.CLI);

        Assert.Equal(2, commands.Count);
        Assert.IsType<UploadBlobProcessor>(Activator.CreateInstance(commands["storage blob upload"].ProcessorType));
        Assert.IsType<DownloadBlobProcessor>(Activator.CreateInstance(commands["storage blob download"].ProcessorType));
        Assert.True(commands["storage blob upload"].Parameters["overwrite"].IsRequired is false);
    }

    private static T CreateProcessor<T>(T processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
        where T : StorageBlobProcessorBase
    {
        var profile = new Profile
        {
            TenantId = "tenant",
            SubscriptionId = "subscription",
            CloudName = "AzureCloud",
        };
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            profile));
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
