// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class FileDownloadTests : IDisposable
{
    private readonly string destination = System.IO.Path.GetTempFileName();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(37)]
    public async Task EmptyAndMultirangeDownloadsWriteExactBytesWithNoDefaultOutput(int length)
    {
        byte[] bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        var service = new DownloadService(bytes);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ChunkSize = 8; processor.CheckMd5 = true;
        await processor.ProcessAsync();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
        Assert.Empty(fixture.Output);
        Assert.Equal(1, service.Heads);
        Assert.Equal(length == 0 ? 0 : (length - 1) / 8 + 1, service.Ranges.Count);
        Assert.Equal(100, fixture.Progress.Last().Value);
        Assert.All(fixture.Progress.SkipLast(1), item => Assert.InRange(item.Value, 0, 99));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task ConcurrentRangesAreBoundedAndWrittenInSourceOrder(int workers)
    {
        byte[] bytes = Enumerable.Range(0, 37).Select(index => (byte)index).ToArray();
        var service = new DownloadService(bytes);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0, active = 0, peak = 0;
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return await service.SendAsync(request);
            int current = Interlocked.Increment(ref active);
            int old;
            do { old = peak; } while (Interlocked.CompareExchange(ref peak, Math.Max(old, current), old) != old);
            if (Interlocked.Increment(ref requests) == workers) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Yield();
            var response = await service.SendAsync(request);
            Interlocked.Decrement(ref active);
            return response;
        });
        var processor = Processor(fixture);
        processor.Concurrency = workers; processor.ChunkSize = 8; processor.CheckMd5 = true;
        await processor.ProcessAsync();
        Assert.Equal(workers, peak);
        Assert.Equal(0, active);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
        Assert.Equal(fixture.Progress.Select(item => item.Value).OrderBy(value => value), fixture.Progress.Select(item => item.Value));
    }

    [Fact]
    public async Task ForcedOutOfOrderCompletionStillWritesAndHashesInSourceOrder()
    {
        byte[] bytes = Enumerable.Range(0, 17).Select(index => (byte)index).ToArray();
        var service = new DownloadService(bytes);
        var laterCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return await service.SendAsync(request);
            long offset = request.Headers.Range!.Ranges.Single().From!.Value;
            if (offset == 0) await laterCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var response = await service.SendAsync(request);
            if (offset == 8) laterCompleted.TrySetResult();
            return response;
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 8; processor.CheckMd5 = true;
        await processor.ProcessAsync();
        Assert.Equal(8, service.Ranges.First().From);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualFourMiBBoundaryHasExactBytesAndSha256(bool validate)
    {
        byte[] bytes = new byte[4 * 1024 * 1024 + 17];
        Random.Shared.NextBytes(bytes);
        var service = new DownloadService(bytes);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ChunkSize = validate ? StorageWireFormat.MaximumChunkBytes : 4 * 1024 * 1024;
        processor.ValidateContent = validate; processor.CheckMd5 = validate;
        await processor.ProcessAsync();
        Assert.Equal(new long[] { 17, 4 * 1024 * 1024 },
            service.Ranges.Select(range => range.To!.Value - range.From!.Value + 1).OrderBy(value => value));
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(await File.ReadAllBytesAsync(destination)));
    }

    [Fact]
    public async Task NonChecksumChunksCanUseTheExactEightMiBBoundary()
    {
        byte[] bytes = new byte[StorageWireFormat.MaximumChunkBytes + 17];
        Random.Shared.NextBytes(bytes);
        var service = new DownloadService(bytes);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ChunkSize = StorageWireFormat.MaximumChunkBytes;
        await processor.ProcessAsync();
        Assert.Equal(new long[] { 17, StorageWireFormat.MaximumChunkBytes },
            service.Ranges.Select(range => range.To!.Value - range.From!.Value + 1).OrderBy(value => value));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
    }

    [Theory]
    [InlineData(3L, 7L, 5)]
    [InlineData(3L, null, 7)]
    [InlineData(3L, 100L, 7)]
    [InlineData(0L, 0L, 1)]
    public async Task InclusiveAndOpenEndedRangesUseExactOffsets(long start, long? end, int expected)
    {
        byte[] bytes = Enumerable.Range(0, 10).Select(index => (byte)index).ToArray();
        var service = new DownloadService(bytes) { BadStoredHash = true };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.StartRange = start; processor.EndRange = end; processor.ChunkSize = 2;
        processor.CheckMd5 = true; processor.PassThru = true;
        await processor.ProcessAsync();
        Assert.Equal(bytes.Skip((int)start).Take(expected), await File.ReadAllBytesAsync(destination));
        Assert.Equal(start, service.Ranges.First().From);
        Assert.Equal(10, Assert.IsType<StorageFile>(Assert.Single(fixture.Output)).Properties.ContentLength);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("etag")]
    [InlineData("missing-etag")]
    [InlineData("range")]
    [InlineData("missing-range")]
    [InlineData("total")]
    [InlineData("unit")]
    [InlineData("length")]
    [InlineData("short")]
    [InlineData("oversized")]
    [InlineData("hash")]
    [InlineData("missing-hash")]
    [InlineData("invalid-hash")]
    [InlineData("short-hash")]
    public async Task CorruptRangeAcknowledgmentsNeverProduceSuccess(string damage)
    {
        var service = new DownloadService([1, 2, 3]) { Damage = damage };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ValidateContent = true; processor.PassThru = true;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Equal(1, service.Heads);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
        Assert.Contains(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WholeFileChecksumFailureDoesNotClaimSuccess(bool empty)
    {
        var service = new DownloadService(empty ? [] : [1, 2, 3]) { BadStoredHash = true };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.CheckMd5 = true; processor.ChunkSize = 1;
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task AbsentStoredChecksumStillAllowsTransactionalValidation()
    {
        byte[] bytes = [1, 2, 3];
        var service = new DownloadService(bytes) { OmitStoredHash = true };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.CheckMd5 = true;
        await processor.ProcessAsync();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PassThruAndCliPropertiesEmitNativeMetadataAfterFinalVerification(bool cli)
    {
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head && service.Heads == 1)
            {
                using var readable = File.OpenRead(destination);
                Assert.Equal(3, readable.Length);
            }
            return await service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.PassThru = !cli; processor.ReturnProperties = cli;
        await processor.ProcessAsync();
        var file = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal("folder/file", file.Path);
        Assert.Equal("\"etag\"", file.Properties.ETag);
        Assert.Equal("file-id", file.Properties.FileId);
        Assert.Equal("one", file.Properties.Metadata["owner"]);
        Assert.Equal(2, service.Heads);
    }

    [Theory]
    [InlineData("etag")]
    [InlineData("length")]
    [InlineData("identity")]
    [InlineData("hash")]
    [InlineData("missing")]
    public async Task FinalPropertiesChangesOrFailuresPreventNativeSuccess(string damage)
    {
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head && service.Heads == 1 && damage == "missing")
                return BlobWorkflowTests.Error(HttpStatusCode.NotFound, "ResourceNotFound");
            var response = await service.SendAsync(request);
            if (request.Method == HttpMethod.Head && service.Heads == 2)
            {
                switch (damage)
                {
                    case "etag": response.Headers.ETag = new EntityTagHeaderValue("\"changed\""); break;
                    case "length": response.Content.Headers.ContentLength = 4; break;
                    case "identity": response.Headers.Remove("x-ms-file-id"); break;
                    case "hash": response.Content.Headers.ContentMD5 = new byte[16]; break;
                }
            }
            return response;
        });
        var processor = Processor(fixture);
        processor.PassThru = true;
        if (damage == "missing") await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        else await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("etag")]
    [InlineData("modified")]
    [InlineData("type")]
    [InlineData("hash")]
    public async Task InvalidInitialPropertiesDoNotTruncateDestination(string damage)
    {
        await File.WriteAllBytesAsync(destination, [9, 8, 7]);
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            switch (damage)
            {
                case "status": response.StatusCode = HttpStatusCode.NoContent; break;
                case "etag": response.Headers.ETag = null; break;
                case "modified": response.Content.Headers.LastModified = null; break;
                case "type": response.Headers.Remove("x-ms-type"); break;
                case "hash": response.Content.Headers.ContentMD5 = [1]; break;
            }
            return response;
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(service.Ranges);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("range")]
    public async Task DeniedReadNeverEmitsSuccess(string operation)
    {
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(request =>
            (request.Method == HttpMethod.Head ? "head" : "range") == operation
                ? Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "PermissionDenied"))
                : service.SendAsync(request));
        var processor = Processor(fixture);
        processor.PassThru = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task FailedWindowsDrainAllRequestsBeforeReturning()
    {
        var service = new DownloadService([1, 2, 3, 4]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return await service.SendAsync(request);
            if (request.Headers.Range!.Ranges.Single().From == 0)
                return BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "PermissionDenied");
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return await service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 2; processor.PassThru = true;
        Task download = processor.ProcessAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(download.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<HttpRequestException>(() => download);
        Assert.Empty(await File.ReadAllBytesAsync(destination));
        Assert.Equal(1, service.Heads);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task RetriedRangesKeepExactOffsetsAndBytesWithoutAppendingDuplicates()
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        var service = new DownloadService(bytes);
        int firstAttempts = 0;
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Get && request.Headers.Range!.Ranges.Single().From == 0 &&
                Interlocked.Increment(ref firstAttempts) == 1)
                return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.ServiceUnavailable, "ServerBusy"));
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 2;
        await processor.ProcessAsync();
        Assert.Equal(2, firstAttempts);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
        Assert.Equal(3, service.Ranges.Count);
    }

    [Fact]
    public async Task FailureMayLeavePartialLegacyContentButNeverReportsCompletion()
    {
        var service = new DownloadService([1, 2, 3, 4]);
        var fixture = new Fixture(request =>
            request.Method == HttpMethod.Get && request.Headers.Range!.Ranges.Single().From == 2
                ? Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "PermissionDenied"))
                : service.SendAsync(request));
        var processor = Processor(fixture);
        processor.Concurrency = 1; processor.ChunkSize = 2;
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task ExistingDestinationIsUntouchedWithoutOverwrite()
    {
        await File.WriteAllBytesAsync(destination, [9, 8, 7]);
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Overwrite = false;
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task CreationRaceUsesCreateNewInsteadOfOverwritingNewLocalContent()
    {
        File.Delete(destination);
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(async request =>
        {
            await File.WriteAllBytesAsync(destination, [9, 8, 7]);
            return await service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Overwrite = false;
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(service.Ranges);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmationCanDeclineOrAuthorizeExistingDestination(bool accept)
    {
        await File.WriteAllBytesAsync(destination, [9, 8, 7, 6]);
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Overwrite = false;
        int prompts = 0;
        processor.ConfirmOverwrite = (path, token) =>
        {
            Assert.Equal(destination, path); Assert.Equal(processor.Token, token);
            prompts++;
            return Task.FromResult(accept);
        };
        await processor.ProcessAsync();
        Assert.Equal(1, prompts);
        Assert.Equal(accept ? new byte[] { 1, 2, 3 } : [9, 8, 7, 6], await File.ReadAllBytesAsync(destination));
        Assert.Equal(accept ? 1 : 0, service.Heads);
        Assert.Empty(fixture.Output);
        if (!accept) Assert.Empty(fixture.Progress);
    }

    [Fact]
    public async Task AppendModeUsesOneWorkerAndChecksOnlyDownloadedBytes()
    {
        await File.WriteAllBytesAsync(destination, [9, 8]);
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Append = true; processor.Overwrite = false; processor.Concurrency = 1; processor.ChunkSize = 2; processor.CheckMd5 = true;
        await processor.ProcessAsync();
        Assert.Equal(new byte[] { 9, 8, 1, 2, 3 }, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task WhatIfDoesNotOpenDestinationOrAuthenticate()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.ShouldProcess = (path, _) => { Assert.Equal(destination, path); return false; };
        using var locked = File.Open(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await processor.ProcessAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
        Assert.Empty(fixture.Progress);
    }

    [Theory]
    [InlineData("workers")]
    [InlineData("chunk")]
    [InlineData("negative-start")]
    [InlineData("end-only")]
    [InlineData("reverse")]
    [InlineData("root")]
    [InlineData("path")]
    [InlineData("destination")]
    [InlineData("lease")]
    [InlineData("intent")]
    [InlineData("output")]
    [InlineData("append-workers")]
    [InlineData("append-overwrite")]
    public async Task InvalidInputsFailBeforeAuthentication(string invalid)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        switch (invalid)
        {
            case "workers": processor.Concurrency = 5; break;
            case "chunk": processor.ChunkSize = StorageWireFormat.MaximumChunkBytes + 1; break;
            case "negative-start": processor.StartRange = -1; break;
            case "end-only": processor.EndRange = 1; break;
            case "reverse": processor.StartRange = 2; processor.EndRange = 1; break;
            case "root": processor.Path = "/"; break;
            case "path": processor.Path = null; break;
            case "destination": processor.Destination = null; break;
            case "lease": processor.LeaseId = "private\r\nvalue"; break;
            case "intent": processor.Context = new StorageContext("fixture", Fixture.DefaultProfile); break;
            case "output": processor.PassThru = true; processor.ReturnProperties = true; break;
            case "append-workers": processor.Append = true; processor.Overwrite = false; break;
            case "append-overwrite": processor.Append = true; processor.Concurrency = 1; break;
        }
        await Assert.ThrowsAnyAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData(0)]
    public async Task StartOutsideSourceDoesNotTruncateDestination(int length)
    {
        await File.WriteAllBytesAsync(destination, [9]);
        var service = new DownloadService(new byte[length]);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.StartRange = length;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(service.Ranges);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFileOrDirectoryPropagatesSelectedProfileSnapshotAndWireOptions(bool nativeFile)
    {
        var selected = new Profile { Name = "selected", TenantId = "other-tenant", CloudName = "AzureCloud" };
        var context = new StorageContext("sourceaccount", selected);
        var share = new StorageFileShare("share", context, "selector+/=&");
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(request =>
        {
            Assert.Equal("sourceaccount.file.core.windows.net", request.RequestUri!.IdnHost);
            Assert.Equal("/share/parent/file+%", Uri.UnescapeDataString(request.RequestUri.AbsolutePath));
            Assert.Equal("selector+/=&", BlobWorkflowTests.Query(request)["sharesnapshot"]);
            Assert.Equal("7", BlobWorkflowTests.Query(request)["timeout"]);
            Assert.Equal("true", request.Headers.GetValues("x-ms-allow-trailing-dot").Single());
            Assert.Equal("backup", request.Headers.GetValues("x-ms-file-request-intent").Single());
            Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
            Assert.Equal("2026-06-06", request.Headers.GetValues("x-ms-version").Single());
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Context = null; processor.ShareName = null;
        if (nativeFile)
        {
            processor.FileObject = new StorageFile("parent/file+%", share) { Properties = new() { ContentLength = 99, ETag = "\"stale\"" } };
            processor.Path = null;
        }
        else
        {
            processor.DirectoryObject = new StorageFileDirectory("parent", share);
            processor.Path = "file+%";
        }
        processor.EnableFileBackupRequestIntent = true; processor.LeaseId = "lease";
        processor.Timeout = 7; processor.AllowTrailingDot = true; processor.PassThru = true;
        await processor.ProcessAsync();
        var file = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal("parent/file+%", file.Path);
        Assert.Equal(3, file.Properties.ContentLength);
        Assert.False(context.EnableFileBackupRequestIntent);
        Assert.True(file.Context.EnableFileBackupRequestIntent);
        Assert.Equal(nativeFile ? null : "file+%", processor.Path);
        Assert.All(fixture.TokenRequests, request =>
        {
            Assert.Same(selected, request.Profile);
            Assert.Equal(new[] { "https://storage.azure.com/.default" }, request.Scopes);
        });
    }

    [Theory]
    [InlineData("path")]
    [InlineData("share")]
    [InlineData("snapshot")]
    [InlineData("account")]
    [InlineData("directory")]
    public async Task ConflictingNativeInputsFailBeforeAuthentication(string conflict)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.FileObject = new StorageFile("folder/file", new StorageFileShare("share", Context(), "snapshot"));
        switch (conflict)
        {
            case "path": processor.Path = "other"; break;
            case "share": processor.ShareObject = new StorageFileShare("other", Context()); break;
            case "snapshot": processor.ShareSnapshot = "other"; break;
            case "account": processor.StorageAccountName = "otheraccount"; break;
            case "directory": processor.DirectoryObject = new StorageFileDirectory("folder", processor.FileObject.Share); break;
        }
        await Assert.ThrowsAnyAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("head")]
    [InlineData("range")]
    [InlineData("verify")]
    public async Task CancellationAtEachBoundaryNeverPublishesCompletion(string boundary)
    {
        await File.WriteAllBytesAsync(destination, [9, 8, 7]);
        using var cancellation = new CancellationTokenSource();
        var service = new DownloadService([1, 2, 3]);
        if (boundary == "before") cancellation.Cancel();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (boundary == "head" && request.Method == HttpMethod.Head ||
                boundary == "range" && request.Method == HttpMethod.Get ||
                boundary == "verify" && service.Heads == 2)
                cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        var processor = Processor(fixture);
        processor.PassThru = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
        if (boundary == "before") Assert.Empty(fixture.TokenRequests);
        if (boundary is "before" or "head")
            Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task MidTransferCancellationLeavesOnlyPreviouslyCompletedWindows()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new DownloadService([1, 2, 3, 4]);
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Method == HttpMethod.Get && request.Headers.Range!.Ranges.Single().From == 2)
                cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        var processor = Processor(fixture);
        processor.Concurrency = 1; processor.ChunkSize = 2; processor.PassThru = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(destination));
        Assert.Equal(1, service.Heads);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task Int64OffsetsBeyondFiveGiBDoNotRequireLargeBuffers()
    {
        const long size = 5L * 1024 * 1024 * 1024 + 17;
        long start = size - 3;
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(Properties(size));
            Assert.Equal($"bytes={start}-{size - 1}", request.Headers.Range!.ToString());
            return Task.FromResult(RangeResponse([1, 2, 3], start, size - 1, size));
        });
        var processor = Processor(fixture);
        processor.StartRange = start;
        await processor.ProcessAsync();
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(destination));
        Assert.Equal(2, fixture.Sends);
    }

    private DownloadStorageFileProcessor Processor(Fixture fixture)
    {
        var processor = new DownloadStorageFileProcessor
        {
            Context = Context(), ShareName = "share", Path = "folder/file", Destination = destination, Overwrite = true
        };
        processor.Init(fixture.Context);
        return processor;
    }

    private static StorageContext Context() => new("fixture", Fixture.DefaultProfile, enableFileBackupRequestIntent: true);
    public void Dispose() => File.Delete(destination);

    private static HttpResponseMessage Properties(long size)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
        response.Headers.Add("x-ms-type", "File");
        response.Headers.Add("x-ms-file-id", "file-id");
        response.Headers.Add("x-ms-meta-owner", "one");
        response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        response.Content.Headers.ContentLength = size;
        return response;
    }

    private static HttpResponseMessage RangeResponse(byte[] bytes, long start, long end, long size)
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes) };
        response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, size);
        return response;
    }

    private sealed class DownloadService(byte[] bytes)
    {
        internal string? Damage { get; init; }
        internal bool BadStoredHash { get; init; }
        internal bool OmitStoredHash { get; init; }
        internal ConcurrentQueue<RangeItemHeaderValue> Ranges { get; } = new();
        private int heads;
        internal int Heads => heads;

        internal Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            Assert.Empty(request.Headers.IfMatch);
            Assert.Empty(request.Headers.IfNoneMatch);
            Assert.False(request.Headers.Contains("x-ms-structured-body"));
            if (request.Method == HttpMethod.Head)
            {
                Interlocked.Increment(ref heads);
                var response = Properties(bytes.LongLength);
                if (!OmitStoredHash)
                    response.Content.Headers.ContentMD5 = BadStoredHash ? new byte[16] : StorageWireFormat.ContentMd5(bytes);
                return Task.FromResult(response);
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            var range = request.Headers.Range!.Ranges.Single();
            Ranges.Enqueue(range);
            int start = checked((int)range.From!.Value), end = checked((int)range.To!.Value);
            byte[] body = bytes[start..(end + 1)];
            if (Damage == "short") body = body[..^1];
            if (Damage == "oversized") body = [.. body, 255];
            var result = RangeResponse(body, start, end, bytes.Length);
            if (request.Headers.Contains("x-ms-range-get-content-md5"))
            {
                Assert.Equal("true", request.Headers.GetValues("x-ms-range-get-content-md5").Single());
                Assert.InRange(body.Length, 1, 4 * 1024 * 1024 + 1);
                if (Damage != "missing-hash")
                    result.Content.Headers.ContentMD5 = Damage == "hash" ? new byte[16] : StorageWireFormat.ContentMd5(body);
            }
            switch (Damage)
            {
                case "status": result.StatusCode = HttpStatusCode.OK; break;
                case "etag": result.Headers.ETag = new EntityTagHeaderValue("\"changed\""); break;
                case "missing-etag": result.Headers.ETag = null; break;
                case "range": result.Content.Headers.ContentRange = new ContentRangeHeaderValue(start + 1, end + 1, bytes.Length + 1); break;
                case "missing-range": result.Content.Headers.ContentRange = null; break;
                case "total": result.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, bytes.Length + 1); break;
                case "unit": result.Content.Headers.ContentRange!.Unit = "items"; break;
                case "length": result.Content.Headers.ContentLength = body.Length + 1; break;
                case "short" or "oversized": result.Content.Headers.ContentLength = end - start + 1; break;
                case "short-hash": result.Content.Headers.ContentMD5 = [1]; break;
                case "invalid-hash":
                    result.Content.Headers.Remove("Content-MD5");
                    result.Content.Headers.TryAddWithoutValidation("Content-MD5", "private-value");
                    break;
            }
            return Task.FromResult(result);
        }
    }
}
