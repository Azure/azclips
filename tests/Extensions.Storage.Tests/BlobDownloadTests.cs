// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobDownloadTests : IDisposable
{
    private readonly string destination = Path.GetTempFileName();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(37)]
    public async Task EmptyAndMultirangeDownloadsReturnNativeMetadataAndExactBytes(int length)
    {
        byte[] bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        var service = new DownloadService(bytes);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ChunkSize = 8;
        await processor.ProcessAsync();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal(length, blob.Properties.ContentLength);
        Assert.Equal("\"etag\"", blob.Properties.ETag);
        Assert.Equal(length == 0 ? 0 : (length - 1) / 8 + 1, service.Ranges.Count);
        Assert.Equal(100, fixture.Progress.Last().Value);
        Assert.All(fixture.Progress.SkipLast(1), item => Assert.InRange(item.Value, 0, 99));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task BoundedConcurrentRequestsAreWrittenInBlobOrder(int workers)
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
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
        Assert.Equal(fixture.Progress.Select(item => item.Value).OrderBy(value => value), fixture.Progress.Select(item => item.Value));
    }

    [Fact]
    public async Task DefaultChunksStayWithinEightMiB()
    {
        byte[] bytes = new byte[StorageWireFormat.MaximumChunkBytes + 17];
        Random.Shared.NextBytes(bytes);
        var service = new DownloadService(bytes);
        var fixture = new Fixture(service.SendAsync);
        await Processor(fixture).ProcessAsync();
        Assert.Equal(new long[] { 17, StorageWireFormat.MaximumChunkBytes },
            service.Ranges.Select(range => range.To!.Value - range.From!.Value + 1).OrderBy(value => value));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task ChecksumValidationUsesRangesOfAtMostFourMiB()
    {
        byte[] bytes = new byte[4 * 1024 * 1024 + 17];
        Random.Shared.NextBytes(bytes);
        var service = new DownloadService(bytes);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.CheckMd5 = true;
        await processor.ProcessAsync();
        Assert.Equal(2, service.Ranges.Count);
        Assert.All(service.Ranges, range => Assert.InRange(range.To!.Value - range.From!.Value + 1, 1, 4 * 1024 * 1024));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
    }

    [Theory]
    [InlineData("etag")]
    [InlineData("status")]
    [InlineData("range")]
    [InlineData("total")]
    [InlineData("unit")]
    [InlineData("length")]
    [InlineData("short")]
    [InlineData("oversized")]
    [InlineData("hash")]
    [InlineData("missing-hash")]
    [InlineData("invalid-hash")]
    public async Task InconsistentOrCorruptRangesFailWithoutSuccess(string damage)
    {
        var service = new DownloadService([1, 2, 3]) { Damage = damage };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ValidateContent = true;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task StoredWholeBlobChecksumIsCheckedAfterOrderedWrites()
    {
        var service = new DownloadService([1, 2, 3]) { BadStoredHash = true };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.CheckMd5 = true; processor.ChunkSize = 1;
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task ExistingDestinationIsNotTouchedWithoutOverwrite()
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
    public async Task DestinationCreationRaceUsesCreateNewNotARacyOverwrite()
    {
        File.Delete(destination);
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(async request =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            await File.WriteAllBytesAsync(destination, [9, 8, 7]);
            return await service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Overwrite = false;
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task WhatIfDoesNotAuthenticateOrOpenTheDestination()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Overwrite = false;
        processor.ShouldProcess = (_, _) => false;
        await processor.ProcessAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Equal(0, new FileInfo(destination).Length);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("workers")]
    [InlineData("chunk")]
    [InlineData("negative")]
    [InlineData("end-only")]
    [InlineData("reversed")]
    [InlineData("directory")]
    [InlineData("missing-destination")]
    [InlineData("selectors")]
    public async Task InvalidInputsFailBeforeAuthentication(string invalid)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        switch (invalid)
        {
            case "workers": processor.Concurrency = 5; break;
            case "chunk": processor.ChunkSize = StorageWireFormat.MaximumChunkBytes + 1; break;
            case "negative": processor.StartRange = -1; break;
            case "end-only": processor.EndRange = 1; break;
            case "reversed": processor.StartRange = 2; processor.EndRange = 1; break;
            case "directory": processor.Destination = Path.GetDirectoryName(destination); break;
            case "missing-destination": processor.Destination = null; break;
            case "selectors": processor.Snapshot = "snapshot"; processor.VersionId = "version"; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task OutOfBoundsRangeFailsBeforeOpeningDestination()
    {
        await File.WriteAllBytesAsync(destination, [9]);
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.StartRange = 3;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task UriInputPreservesNamesSelectorsAndProfileWithoutMutatingNamedInputs()
    {
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(request =>
        {
            Assert.Equal("/container/literal%252F%2Bname/", request.RequestUri!.AbsolutePath);
            Assert.Equal("version+/=&", BlobWorkflowTests.Query(request)["versionid"]);
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Context = null; processor.Container = null; processor.Blob = null;
        processor.BlobUri = "https://fixture.blob.core.windows.net/container/literal%252F%2Bname/?versionid=version%2B%2F%3D%26";
        await processor.ProcessAsync();
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("literal%2F+name/", blob.Name);
        Assert.Equal("version+/=&", blob.VersionId);
        Assert.Null(processor.Context);
        Assert.Null(processor.Container);
        Assert.Null(processor.Blob);
        Assert.All(fixture.TokenRequests, item => Assert.Same(Fixture.DefaultProfile, item.Profile));
    }

    [Theory]
    [InlineData("https://other.blob.core.windows.net/container/blob")]
    [InlineData("https://fixture.blob.core.windows.net/other/blob")]
    [InlineData("https://fixture.blob.core.windows.net/container/other")]
    [InlineData("https://fixture.blob.core.windows.net/container/blob?snapshot=one&snapshot=two")]
    [InlineData("https://fixture.blob.core.windows.net/container/blob?snapshot=one&versionid=two")]
    [InlineData("https://fixture.blob.core.windows.net/container/blob?timeout=1")]
    public async Task ConflictingUriInputsAreRejectedBeforeAuthentication(string uri)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.BlobUri = uri;
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task LargeOffsetsRemainInt64WithoutAllocatingAMultiGiBBlob()
    {
        long offset = 5L * 1024 * 1024 * 1024;
        long size = offset + 512;
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Head(length: size));
            Assert.Equal($"bytes={offset}-{offset + 511}", request.Headers.Range!.ToString());
            return Task.FromResult(RangeResponse(new byte[512], offset, offset + 511, size));
        });
        var processor = Processor(fixture);
        processor.StartRange = offset;
        await processor.ProcessAsync();
        Assert.Equal(512, new FileInfo(destination).Length);
        Assert.Equal(size, Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Properties.ContentLength);
    }

    [Fact]
    public async Task RuntimeRetryKeepsTheSamePinnedRange()
    {
        var service = new DownloadService([1, 2, 3]);
        int attempts = 0;
        var fixture = new Fixture(request =>
        {
            if (request.Method != HttpMethod.Head && ++attempts == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return service.SendAsync(request);
        });
        await Processor(fixture).ProcessAsync();
        Assert.Equal(2, attempts);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task SourceReplacementIsNotDownloadedAsAMixedBlob()
    {
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(request => request.Method == HttpMethod.Head
            ? service.SendAsync(request)
            : Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet")));
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task CancellationBeforeWritingRangePreventsSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Method != HttpMethod.Head) cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeInputsSelectorsLeaseAndConditionsFlowIntoPinnedRequests(bool version)
    {
        var service = new DownloadService([1, 2, 3]);
        var fixture = new Fixture(request =>
        {
            Assert.Equal("selector+/=&", BlobWorkflowTests.Query(request)[version ? "versionid" : "snapshot"]);
            Assert.Equal("lease", Assert.Single(request.Headers.GetValues("x-ms-lease-id")));
            Assert.Equal("\"old\"", request.Headers.IfNoneMatch.Single().ToString());
            Assert.Equal("\"tag\"='value'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
            Assert.NotNull(request.Headers.IfModifiedSince);
            Assert.NotNull(request.Headers.IfUnmodifiedSince);
            Assert.Equal(request.Method == HttpMethod.Head ? "*" : "\"etag\"", request.Headers.IfMatch.Single().ToString());
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Context = null; processor.Container = null; processor.Blob = null;
        processor.InputObject = new StorageBlob("blob", new StorageBlobContainer("container", BlobWorkflowTests.Context()),
            new BlobModels.BlobProperties { ETag = "unused", LastModified = "unused" })
        {
            Snapshot = version ? null : "selector+/=&", VersionId = version ? "selector+/=&" : null
        };
        processor.LeaseId = "lease"; processor.IfMatch = "*"; processor.IfNoneMatch = "\"old\"";
        processor.IfModifiedSince = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
        processor.IfUnmodifiedSince = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        processor.IfTags = "\"tag\"='value'";
        await processor.ProcessAsync();
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal(version ? "selector+/=&" : null, blob.VersionId);
        Assert.Equal(version ? null : "selector+/=&", blob.Snapshot);
    }

    [Fact]
    public async Task FailureCanLeavePartialLegacyDestinationButNeverReportsCompletion()
    {
        var service = new DownloadService([1, 2, 3, 4]);
        int ranges = 0;
        var fixture = new Fixture(request =>
        {
            if (request.Method != HttpMethod.Head && ++ranges == 2)
                return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthorizationFailure"));
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Concurrency = 1; processor.ChunkSize = 2;
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task InvalidStoredChecksumLengthFailsBeforeOpeningDestination()
    {
        await File.WriteAllBytesAsync(destination, [9]);
        var fixture = new Fixture(_ =>
        {
            var response = BlobWorkflowTests.Head(length: 3);
            response.Content.Headers.ContentMD5 = [1];
            return Task.FromResult(response);
        });
        var processor = Processor(fixture);
        processor.CheckMd5 = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(destination));
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task MissingParentDirectoryIsNotCreatedImplicitly()
    {
        string parent = Path.Combine(Path.GetTempPath(), "azclips-missing-" + Guid.NewGuid().ToString("N"));
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Destination = Path.Combine(parent, "blob.bin");
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => processor.ProcessAsync());
        Assert.False(Directory.Exists(parent));
        Assert.Empty(fixture.TokenRequests);
    }

    private DownloadStorageBlobWorkflow Processor(Fixture fixture)
    {
        var processor = new DownloadStorageBlobWorkflow
        {
            Context = BlobWorkflowTests.Context(), Container = "container", Blob = "blob", Destination = destination, Overwrite = true
        };
        processor.Init(fixture.Context);
        return processor;
    }

    public void Dispose() => File.Delete(destination);

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
        internal ConcurrentQueue<RangeItemHeaderValue> Ranges { get; } = new();

        internal Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            if (request.Method == HttpMethod.Head)
            {
                var head = BlobWorkflowTests.Head(length: bytes.LongLength);
                head.Content.Headers.ContentMD5 = BadStoredHash ? new byte[16] : StorageWireFormat.ContentMd5(bytes);
                return Task.FromResult(head);
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("\"etag\"", request.Headers.IfMatch.Single().ToString());
            var range = request.Headers.Range!.Ranges.Single();
            Ranges.Enqueue(range);
            int start = checked((int)range.From!.Value), end = checked((int)range.To!.Value);
            byte[] body = bytes[start..(end + 1)];
            if (Damage == "short") body = body[..^1];
            if (Damage == "oversized") body = [.. body, 255];
            var response = RangeResponse(body, start, end, bytes.Length);
            if (request.Headers.Contains("x-ms-range-get-content-md5") && Damage != "missing-hash")
                response.Content.Headers.ContentMD5 = Damage == "hash" ? new byte[16] : StorageWireFormat.ContentMd5(body);
            switch (Damage)
            {
                case "etag": response.Headers.ETag = new EntityTagHeaderValue("\"other\""); break;
                case "status": response.StatusCode = HttpStatusCode.OK; break;
                case "range": response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start + 1, end + 1, bytes.Length + 1); break;
                case "total": response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, bytes.Length + 1); break;
                case "unit": response.Content.Headers.ContentRange!.Unit = "items"; break;
                case "length": response.Content.Headers.ContentLength = body.Length + 1; break;
                case "short" or "oversized": response.Content.Headers.ContentLength = end - start + 1; break;
                case "invalid-hash":
                    response.Content.Headers.Remove("Content-MD5");
                    response.Content.Headers.TryAddWithoutValidation("Content-MD5", "private-value");
                    break;
            }
            return Task.FromResult(response);
        }
    }
}
