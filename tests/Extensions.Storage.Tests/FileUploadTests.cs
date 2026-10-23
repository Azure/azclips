// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class FileUploadTests : IDisposable
{
    private readonly string source = System.IO.Path.GetTempFileName();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(37)]
    public async Task EmptySmallAndMultirangeUploadsHaveExactBytesAndDefaultToNoOutput(int size)
    {
        byte[] bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        await File.WriteAllBytesAsync(source, bytes);
        var service = new FileService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ChunkSize = 8;
        await processor.ProcessAsync();
        Assert.Equal(bytes, service.Data);
        Assert.Equal(1, service.Creates);
        Assert.Equal(size == 0 ? 0 : (size - 1) / 8 + 1, service.Ranges.Count);
        Assert.Equal(0, service.Heads);
        Assert.Equal(0, service.Sets);
        Assert.Empty(fixture.Output);
        Assert.Equal(100, fixture.Progress.Last().Value);
        Assert.All(fixture.Progress.SkipLast(1), item => Assert.InRange(item.Value, 0, 99));
        Assert.Equal(fixture.Progress.Select(item => item.Value).OrderBy(value => value), fixture.Progress.Select(item => item.Value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task RequestsStayBoundedAndRangeWritesCanCompleteOutOfOrder(int workers)
    {
        byte[] bytes = Enumerable.Range(0, 37).Select(index => (byte)index).ToArray();
        await File.WriteAllBytesAsync(source, bytes);
        var service = new FileService();
        int active = 0, peak = 0, entered = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Fixture(async request =>
        {
            if (BlobWorkflowTests.Query(request)["comp"] != "range") return await service.SendAsync(request);
            int current = Interlocked.Increment(ref active);
            int old;
            do { old = peak; } while (Interlocked.CompareExchange(ref peak, Math.Max(old, current), old) != old);
            if (Interlocked.Increment(ref entered) == workers) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Yield();
            var response = await service.SendAsync(request);
            Interlocked.Decrement(ref active);
            return response;
        });
        var processor = Processor(fixture);
        processor.Concurrency = workers; processor.ChunkSize = 8; processor.ValidateContent = true;
        await processor.ProcessAsync();
        Assert.Equal(workers, peak);
        Assert.Equal(bytes, service.Data);
        Assert.All(service.Ranges, range => Assert.Equal(StorageWireFormat.ContentMd5(range.Bytes), range.Md5));
    }

    [Fact]
    public async Task ActualFourMiBBoundaryAndWholeHashUseBoundedPayloads()
    {
        byte[] bytes = new byte[UploadStorageFileProcessor.MaximumRangeBytes + 17];
        Random.Shared.NextBytes(bytes);
        await File.WriteAllBytesAsync(source, bytes);
        var service = new FileService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.StoreContentMd5 = true; processor.ValidateContent = true; processor.PassThru = true;
        await processor.ProcessAsync();
        Assert.Equal(new[] { 17, UploadStorageFileProcessor.MaximumRangeBytes }, service.Ranges.Select(range => range.Bytes.Length).OrderBy(length => length));
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(service.Data));
        Assert.Equal(StorageWireFormat.ContentMd5(bytes), service.StoredMd5);
        Assert.Equal(1, service.Sets);
        Assert.Equal(1, service.Heads);
        var file = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal(bytes.Length, file.Properties.ContentLength);
        Assert.Equal(service.StoredMd5, file.Properties.ContentMd5);
    }

    [Fact]
    public async Task WholeChecksumUsesSourceOrderEvenWhenRangesCompleteOutOfOrder()
    {
        byte[] bytes = Enumerable.Range(0, 17).Select(value => (byte)value).ToArray();
        await File.WriteAllBytesAsync(source, bytes);
        var service = new FileService();
        var laterCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Fixture(async request =>
        {
            if (BlobWorkflowTests.Query(request)["comp"] != "range") return await service.SendAsync(request);
            long offset = request.Headers.Range!.Ranges.Single().From!.Value;
            if (offset == 0) await laterCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var response = await service.SendAsync(request);
            if (offset == 8) laterCompleted.TrySetResult();
            return response;
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 8; processor.Concurrency = 2; processor.StoreContentMd5 = true; processor.PassThru = true;
        await processor.ProcessAsync();
        Assert.Equal(8, service.Ranges.First().Offset);
        Assert.Equal(bytes, service.Data);
        Assert.Equal(StorageWireFormat.ContentMd5(bytes), service.StoredMd5);
    }

    [Fact]
    public async Task AFailedWindowDrainsOutstandingRequestsBeforeReturningAndNeverFinalizes()
    {
        await File.WriteAllBytesAsync(source, new byte[17]);
        var service = new FileService();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Fixture(async request =>
        {
            if (BlobWorkflowTests.Query(request)["comp"] != "range") return await service.SendAsync(request);
            if (request.Headers.Range!.Ranges.Single().From == 0)
                return BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "PermissionDenied");
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return await service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 8; processor.Concurrency = 2; processor.StoreContentMd5 = true; processor.PassThru = true;
        Task upload = processor.ProcessAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(upload.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<HttpRequestException>(() => upload);
        Assert.Equal(0, service.Sets);
        Assert.Equal(0, service.Heads);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task SettingsIntentAndLeaseReachEveryRelevantOperationWithoutSettingAnEarlyWholeHash()
    {
        byte[] bytes = [0, 255, 13, 10, 42];
        await File.WriteAllBytesAsync(source, bytes);
        var service = new FileService();
        var fixture = new Fixture(request =>
        {
            Assert.Equal("fixture.file.core.windows.net", request.RequestUri!.IdnHost);
            Assert.Equal("/share/folder/file%252F%2B.", request.RequestUri.AbsolutePath);
            Assert.Equal("backup", request.Headers.GetValues("x-ms-file-request-intent").Single());
            Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
            Assert.Equal("true", request.Headers.GetValues("x-ms-allow-trailing-dot").Single());
            Assert.Equal("7", BlobWorkflowTests.Query(request)["timeout"]);
            string? comp = BlobWorkflowTests.Query(request)["comp"];
            if (request.Method == HttpMethod.Put && comp != "range")
            {
                Assert.Equal("text/plain", request.Headers.GetValues("x-ms-content-type").Single());
                Assert.Equal("gzip", request.Headers.GetValues("x-ms-content-encoding").Single());
                Assert.Equal("en", request.Headers.GetValues("x-ms-content-language").Single());
                Assert.Equal("attachment", request.Headers.GetValues("x-ms-content-disposition").Single());
                Assert.Equal("no-cache", request.Headers.GetValues("x-ms-cache-control").Single());
                if (comp is null)
                {
                    Assert.Equal("file", request.Headers.GetValues("x-ms-type").Single());
                    Assert.Equal("inherit", request.Headers.GetValues("x-ms-file-permission").Single());
                    Assert.Equal("Archive", request.Headers.GetValues("x-ms-file-attributes").Single());
                    Assert.Equal("now", request.Headers.GetValues("x-ms-file-creation-time").Single());
                    Assert.Equal("now", request.Headers.GetValues("x-ms-file-last-write-time").Single());
                    Assert.Equal("team", request.Headers.GetValues("x-ms-meta-owner").Single());
                    Assert.False(request.Headers.Contains("x-ms-content-md5"));
                    Assert.Equal(0, request.Content!.Headers.ContentLength);
                }
                else
                {
                    Assert.Equal("preserve", request.Headers.GetValues("x-ms-file-permission").Single());
                    Assert.Equal("preserve", request.Headers.GetValues("x-ms-file-attributes").Single());
                    Assert.Equal("preserve", request.Headers.GetValues("x-ms-file-creation-time").Single());
                    Assert.Equal("preserve", request.Headers.GetValues("x-ms-file-last-write-time").Single());
                    Assert.False(request.Headers.Contains("x-ms-content-length"));
                }
            }
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Path = "folder/file%2F+."; processor.AllowTrailingDot = true; processor.LeaseId = "lease"; processor.Timeout = 7;
        processor.ContentType = "text/plain"; processor.ContentEncoding = "gzip"; processor.ContentLanguage = "en";
        processor.ContentDisposition = "attachment"; processor.CacheControl = "no-cache";
        processor.Metadata = new Dictionary<string, string> { ["owner"] = "team" };
        processor.ContentMd5 = StorageWireFormat.ContentMd5(bytes); processor.PassThru = true;
        await processor.ProcessAsync();
        var file = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal("text/plain", file.Properties.ContentType);
        Assert.Equal("team", file.Properties.Metadata["owner"]);
        Assert.Equal(4, fixture.Sends);
        Assert.All(fixture.TokenRequests, item => Assert.Equal(new[] { "https://storage.azure.com/.default" }, item.Scopes));
    }

    [Fact]
    public async Task CliResponseMetadataDoesNotRequireFileReadPermission()
    {
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new FileService();
        var fixture = new Fixture(request => request.Method == HttpMethod.Head
            ? throw new InvalidOperationException("CLI upload must not require a HEAD.")
            : service.SendAsync(request));
        var processor = Processor(fixture);
        processor.ReturnResponseMetadata = true;
        await processor.ProcessAsync();
        var result = Assert.IsType<StorageFileUploadResult>(Assert.Single(fixture.Output));
        Assert.NotEmpty(result.ETag);
        Assert.True(result.IsServerEncrypted);
        Assert.Null(result.ContentMd5);
        Assert.Equal(0, service.Heads);
    }

    [Fact]
    public async Task MetadataAndContentSettingsAreFrozenBeforeConfirmation()
    {
        byte[] bytes = [1, 2, 3];
        await File.WriteAllBytesAsync(source, bytes);
        byte[] md5 = StorageWireFormat.ContentMd5(bytes);
        var metadata = new Dictionary<string, string> { ["owner"] = "one" };
        var service = new FileService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Metadata = metadata; processor.ContentMd5 = md5; processor.ContentType = "text/plain"; processor.PassThru = true;
        processor.ShouldProcess = (_, _) =>
        {
            metadata["owner"] = "two"; md5[0] ^= 255; processor.ContentType = "text/html";
            return true;
        };
        await processor.ProcessAsync();
        var file = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal("one", file.Properties.Metadata["owner"]);
        Assert.Equal("text/plain", file.Properties.ContentType);
        Assert.Equal(StorageWireFormat.ContentMd5(bytes), file.Properties.ContentMd5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public async Task IncrementalWholeChecksumIsStoredOnlyAfterAllRangesIncludingEmptyFiles(int length)
    {
        byte[] bytes = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
        await File.WriteAllBytesAsync(source, bytes);
        var service = new FileService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ChunkSize = 2; processor.StoreContentMd5 = true; processor.ReturnResponseMetadata = true;
        await processor.ProcessAsync();
        Assert.Equal(StorageWireFormat.ContentMd5(bytes), Assert.IsType<StorageFileUploadResult>(Assert.Single(fixture.Output)).ContentMd5);
        Assert.Equal(StorageWireFormat.ContentMd5(bytes), service.StoredMd5);
        Assert.Equal(1, service.Sets);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(412)]
    public async Task RangeFailureGatesChecksumFinalReadAndSuccess(int status)
    {
        await File.WriteAllBytesAsync(source, new byte[17]);
        var service = new FileService();
        var fixture = new Fixture(request => BlobWorkflowTests.Query(request)["comp"] == "range"
            ? Task.FromResult(BlobWorkflowTests.Error((HttpStatusCode)status, "RangeFailed"))
            : service.SendAsync(request));
        var processor = Processor(fixture);
        processor.ChunkSize = 8; processor.StoreContentMd5 = true; processor.PassThru = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(1, service.Creates);
        Assert.Equal(0, service.Sets);
        Assert.Equal(0, service.Heads);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task AppliedRangeRetryReplaysTheSameOffsetAndBytesWithoutAppending()
    {
        byte[] bytes = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(source, bytes);
        var service = new FileService();
        int ranges = 0;
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (BlobWorkflowTests.Query(request)["comp"] == "range" && ++ranges == 1)
            {
                response.Dispose();
                return BlobWorkflowTests.Error(HttpStatusCode.ServiceUnavailable, "ServerBusy");
            }
            return response;
        });
        await Processor(fixture).ProcessAsync();
        Assert.Equal(bytes, service.Data);
        Assert.Equal(2, service.Ranges.Count);
        Assert.All(service.Ranges, range => { Assert.Equal(0, range.Offset); Assert.Equal(bytes, range.Bytes); });
    }

    [Theory]
    [InlineData("create")]
    [InlineData("set")]
    [InlineData("head")]
    public async Task FailureAtAnyBoundaryDoesNotEmitSuccess(string operation)
    {
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new FileService();
        var fixture = new Fixture(request =>
        {
            string? comp = BlobWorkflowTests.Query(request)["comp"];
            bool fail = operation == "create" && request.Method == HttpMethod.Put && comp is null ||
                operation == "set" && comp == "properties" || operation == "head" && request.Method == HttpMethod.Head;
            return fail ? Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "PermissionDenied")) : service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.StoreContentMd5 = true; processor.PassThru = true;
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-response", error.ToString());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("etag")]
    [InlineData("modified")]
    [InlineData("encryption")]
    [InlineData("duplicate-encryption")]
    public async Task InvalidRangeAcknowledgmentsFailWithoutFinalizingOrLeakingHeaders(string damage)
    {
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new FileService();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (BlobWorkflowTests.Query(request)["comp"] == "range")
            {
                switch (damage)
                {
                    case "status": response.StatusCode = HttpStatusCode.OK; break;
                    case "etag": response.Headers.ETag = null; break;
                    case "modified": response.Content.Headers.LastModified = null; break;
                    case "encryption":
                        response.Headers.Remove("x-ms-request-server-encrypted");
                        response.Headers.TryAddWithoutValidation("x-ms-request-server-encrypted", "private-value");
                        break;
                    case "duplicate-encryption": response.Headers.Add("x-ms-request-server-encrypted", "true"); break;
                }
            }
            return response;
        });
        var processor = Processor(fixture);
        processor.StoreContentMd5 = true; processor.PassThru = true;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Equal(0, service.Sets);
        Assert.Equal(0, service.Heads);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Theory]
    [InlineData("size")]
    [InlineData("identity")]
    [InlineData("etag")]
    [InlineData("hash")]
    public async Task PassthruDoesNotReturnInconsistentOrReplacedFileProperties(string damage)
    {
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new FileService();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Method == HttpMethod.Head)
            {
                switch (damage)
                {
                    case "size": response.Content.Headers.ContentLength = 99; break;
                    case "identity": response.Headers.Remove("x-ms-file-id"); response.Headers.Add("x-ms-file-id", "replacement"); break;
                    case "etag": response.Headers.ETag = new EntityTagHeaderValue("\"replacement\""); break;
                    case "hash": response.Content.Headers.ContentMD5 = new byte[16]; break;
                }
            }
            return response;
        });
        var processor = Processor(fixture);
        processor.StoreContentMd5 = true; processor.PassThru = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverwriteConfirmationIsOptionalAndDecliningDoesNotCreate(bool confirm)
    {
        var service = new FileService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        int prompts = 0;
        processor.ConfirmOverwrite = (_, _) => { prompts++; return Task.FromResult(confirm); };
        await processor.ProcessAsync();
        Assert.Equal(1, prompts);
        Assert.Equal(confirm ? 1 : 0, service.Creates);
        Assert.Empty(fixture.Output);
        if (!confirm) Assert.Empty(fixture.Progress);
    }

    [Theory]
    [InlineData(404, "ResourceNotFound", true)]
    [InlineData(404, "ShareNotFound", false)]
    [InlineData(403, "PermissionDenied", false)]
    public async Task OnlyThePreciseMissingFileProbeAllowsCreateWithoutConfirmation(int status, string code, bool allowed)
    {
        var service = new FileService();
        var fixture = new Fixture(request => request.Method == HttpMethod.Head
            ? Task.FromResult(BlobWorkflowTests.Error((HttpStatusCode)status, code)) : service.SendAsync(request));
        var processor = Processor(fixture);
        processor.ConfirmOverwrite = (_, _) => throw new InvalidOperationException("Missing file must not prompt.");
        if (allowed)
        {
            await processor.ProcessAsync();
            Assert.DoesNotContain(fixture.Messages, item => item.Id == Events.ResourceOperationFailed);
        }
        else await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(allowed ? 1 : 0, service.Creates);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(5, 8)]
    [InlineData(2, 0)]
    [InlineData(2, 4194305)]
    public async Task InvalidTransferSettingsFailBeforeAuthentication(int workers, int chunk)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Concurrency = workers; processor.ChunkSize = chunk;
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task InvalidHashHeadersMetadataIntentAndSnapshotsFailBeforeAuthentication()
    {
        foreach (var configure in new Action<UploadStorageFileProcessor>[]
        {
            item => item.ContentMd5 = [1], item => item.ContentType = "private-value\r\n",
            item => item.Metadata = new Dictionary<string, string> { ["1invalid"] = "value" },
            item => item.ShareSnapshot = "snapshot",
            item => item.Context = new StorageContext("fixture", Fixture.DefaultProfile),
            item => { item.PassThru = true; item.ReturnResponseMetadata = true; }
        })
        {
            var fixture = new Fixture();
            var processor = Processor(fixture);
            configure(processor);
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
            Assert.Empty(fixture.TokenRequests);
        }
    }

    [Fact]
    public async Task WhatIfDoesNotOpenTheSourceOrSendRequests()
    {
        using var exclusive = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.ShouldProcess = (_, _) => false;
        await processor.ProcessAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task MissingSourceFailsBeforeRemoteMutation()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Source = source + ".missing";
        await Assert.ThrowsAsync<FileNotFoundException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("child/")]
    public async Task NativeParentAndFilenameDefaultsPreserveSelectedProfile(string? path)
    {
        var profile = new Core.Abstractions.Profile { Name = "source", TenantId = "source-tenant" };
        var service = new FileService();
        var fixture = new Fixture(request =>
        {
            Assert.Equal("sourceaccount.file.core.windows.net", request.RequestUri!.IdnHost);
            string expected = "/share/parent/" + (path ?? "") + System.IO.Path.GetFileName(source);
            Assert.Equal(expected, Uri.UnescapeDataString(request.RequestUri.AbsolutePath));
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Context = null; processor.ShareName = null; processor.Path = path;
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", new StorageContext("sourceaccount", profile, true)));
        await processor.ProcessAsync();
        Assert.Equal(profile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.Null(processor.Context);
        Assert.Null(processor.ShareName);
        Assert.Equal(path, processor.Path);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("range")]
    [InlineData("set")]
    [InlineData("head")]
    public async Task CancellationAtAnyTransferBoundarySuppressesSuccess(string operation)
    {
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        var service = new FileService();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            string? comp = BlobWorkflowTests.Query(request)["comp"];
            if (operation == "create" && request.Method == HttpMethod.Put && comp is null ||
                operation == "range" && comp == "range" || operation == "set" && comp == "properties" ||
                operation == "head" && request.Method == HttpMethod.Head) cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        var processor = Processor(fixture);
        processor.StoreContentMd5 = true; processor.PassThru = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task CapacityAndOffsetWireValuesRemainInt64WithoutLargeAllocation()
    {
        long length = 5L * 1024 * 1024 * 1024;
        UploadStorageFileProcessor.ValidateLength(length);
        UploadStorageFileProcessor.ValidateLength(UploadStorageFileProcessor.MaximumFileBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => UploadStorageFileProcessor.ValidateLength(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => UploadStorageFileProcessor.ValidateLength(UploadStorageFileProcessor.MaximumFileBytes + 1));
        var fixture = new Fixture(request =>
        {
            if (BlobWorkflowTests.Query(request)["comp"] == "range")
                Assert.Equal($"bytes={length}-{length}", request.Headers.Range!.ToString());
            else Assert.Equal(length.ToString(System.Globalization.CultureInfo.InvariantCulture), request.Headers.GetValues("x-ms-content-length").Single());
            return Task.FromResult(FileService.Response(HttpStatusCode.Created, 1));
        });
        var create = new FileProcessors.CreateFileProcessor
        {
            Context = Context(), Url = "https://fixture.file.core.windows.net/share/file", FileType = "file",
            FileContentLength = length, Body = [], ContentLength = 0
        };
        create.Init(fixture.Context);
        using var created = await create.SendAsync();
        var range = new FileProcessors.UploadRangeFileProcessor
        {
            Context = Context(), Url = create.Url, Range = $"bytes={length}-{length}", Body = [1], ContentLength = 1, FileRangeWrite = "update"
        };
        range.Init(fixture.Context);
        using var written = await range.SendAsync();
    }

    private UploadStorageFileProcessor Processor(Fixture fixture)
    {
        var processor = new UploadStorageFileProcessor { Context = Context(), ShareName = "share", Path = "file", Source = source };
        processor.Init(fixture.Context);
        return processor;
    }
    private static StorageContext Context() => new("fixture", Fixture.DefaultProfile, enableFileBackupRequestIntent: true);
    public void Dispose() => File.Delete(source);

    private sealed class FileService
    {
        internal byte[] Data { get; private set; } = [];
        internal byte[]? StoredMd5 { get; private set; }
        internal int Creates { get; private set; }
        internal int Sets { get; private set; }
        internal int Heads { get; private set; }
        internal ConcurrentQueue<(long Offset, byte[] Bytes, byte[]? Md5)> Ranges { get; } = new();
        private int version;
        private string contentType = "application/octet-stream";
        private string? metadata;

        internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            string? comp = BlobWorkflowTests.Query(request)["comp"];
            if (request.Method == HttpMethod.Head)
            {
                Heads++;
                var response = Response(HttpStatusCode.OK, version);
                response.Headers.Add("x-ms-type", "File");
                response.Content.Headers.ContentLength = Data.Length;
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                if (StoredMd5 is not null) response.Content.Headers.ContentMD5 = StoredMd5;
                if (metadata is not null) response.Headers.Add("x-ms-meta-owner", metadata);
                return response;
            }
            if (comp == "range")
            {
                byte[] bytes = await request.Content!.ReadAsByteArrayAsync();
                var range = request.Headers.Range!.Ranges.Single();
                long offset = range.From!.Value;
                Assert.Equal(bytes.Length, range.To!.Value - offset + 1);
                Assert.Equal(bytes.Length, request.Content.Headers.ContentLength);
                Assert.Equal("update", request.Headers.GetValues("x-ms-write").Single());
                Ranges.Enqueue((offset, bytes, request.Content.Headers.ContentMD5));
                bytes.CopyTo(Data, checked((int)offset));
                return Response(HttpStatusCode.Created, Interlocked.Increment(ref version));
            }
            if (comp == "properties")
            {
                Sets++;
                StoredMd5 = Convert.FromBase64String(request.Headers.GetValues("x-ms-content-md5").Single());
                long covered = 0;
                foreach (var range in Ranges.DistinctBy(item => item.Offset).OrderBy(item => item.Offset))
                {
                    Assert.Equal(covered, range.Offset);
                    covered += range.Bytes.LongLength;
                }
                Assert.Equal(Data.LongLength, covered);
                Assert.Equal(StorageWireFormat.ContentMd5(Data), StoredMd5);
                return Response(HttpStatusCode.OK, Interlocked.Increment(ref version));
            }
            Creates++;
            Data = new byte[int.Parse(request.Headers.GetValues("x-ms-content-length").Single(), System.Globalization.CultureInfo.InvariantCulture)];
            Assert.Empty(await request.Content!.ReadAsByteArrayAsync());
            contentType = request.Headers.GetValues("x-ms-content-type").Single();
            metadata = request.Headers.TryGetValues("x-ms-meta-owner", out var values) ? values.Single() : null;
            return Response(HttpStatusCode.Created, Interlocked.Increment(ref version));
        }

        internal static HttpResponseMessage Response(HttpStatusCode status, int version)
        {
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent([]) };
            response.Headers.ETag = new EntityTagHeaderValue($"\"version-{version}\"");
            response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-21T00:00:00Z");
            response.Headers.Add("x-ms-file-id", "file-id");
            response.Headers.Add("x-ms-request-server-encrypted", "true");
            return response;
        }
    }
}
