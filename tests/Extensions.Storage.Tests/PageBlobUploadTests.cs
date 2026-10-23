// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class PageBlobUploadTests : IDisposable
{
    private readonly string source = Path.GetTempFileName();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(512, 1)]
    [InlineData(2560, 2)]
    [InlineData(2560, 4)]
    public async Task PageUploadCreatesExactCapacityAndRoundTripsAlignedRanges(int length, int concurrency)
    {
        byte[] bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        await System.IO.File.WriteAllBytesAsync(source, bytes);
        byte[] uploaded = new byte[length];
        int creates = 0, writes = 0;
        string etag = "\"created\"";
        var offsets = new List<long>();
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                Assert.Equal(etag, request.Headers.IfMatch.Single().ToString());
                Assert.Empty(request.Headers.IfNoneMatch);
                Assert.Equal(bytes, uploaded);
                return BlobWorkflowTests.Head("PageBlob", length);
            }
            if (BlobWorkflowTests.Query(request)["comp"] is null)
            {
                creates++;
                Assert.Equal("PageBlob", Assert.Single(request.Headers.GetValues("x-ms-blob-type")));
                Assert.Equal(length.ToString(), Assert.Single(request.Headers.GetValues("x-ms-blob-content-length")));
                Assert.Equal(0, request.Content!.Headers.ContentLength);
                Assert.Empty(await request.Content.ReadAsByteArrayAsync());
                Assert.Equal("*", request.Headers.IfNoneMatch.Single().ToString());
                return Created(etag);
            }
            Assert.Equal("page", BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal(etag, request.Headers.IfMatch.Single().ToString());
            Assert.Empty(request.Headers.IfNoneMatch);
            Assert.Equal("update", Assert.Single(request.Headers.GetValues("x-ms-page-write")));
            var range = request.Headers.Range!.Ranges.Single();
            long start = range.From!.Value, end = range.To!.Value;
            Assert.Equal(0, start % 512);
            Assert.Equal(511, end % 512);
            byte[] body = await request.Content!.ReadAsByteArrayAsync();
            Assert.Equal(end - start + 1, body.Length);
            Assert.Equal(StorageWireFormat.ContentMd5(body), request.Content.Headers.ContentMD5);
            body.CopyTo(uploaded, start);
            offsets.Add(start);
            etag = $"\"page-{++writes}\"";
            return Created(etag);
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 512; processor.Concurrency = concurrency; processor.ValidateContent = true;
        await processor.ProcessAsync();
        Assert.Equal(1, creates);
        Assert.Equal(length / 512, writes);
        Assert.Equal(length / 512, offsets.Distinct().Count());
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(uploaded));
        var result = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("PageBlob", result.Properties.BlobType);
        Assert.Equal(length, result.Properties.ContentLength);
        Assert.Equal(100, fixture.Progress.Last().Value);
        Assert.All(fixture.Progress.SkipLast(1), data => Assert.InRange(data.Value, 0, 99));
    }

    [Fact]
    public async Task AtomicCreateUsesInt64CapacityButAnEmptyRequestBody()
    {
        long size = 5L * 1024 * 1024 * 1024;
        var fixture = new Fixture(async request =>
        {
            Assert.Equal(size.ToString(), Assert.Single(request.Headers.GetValues("x-ms-blob-content-length")));
            Assert.Equal(0, request.Content!.Headers.ContentLength);
            Assert.Empty(await request.Content.ReadAsByteArrayAsync());
            return Created();
        });
        var processor = new BlobProcessors.CreatePageBlobProcessor
        {
            Context = BlobWorkflowTests.Context(), Url = BlobWorkflowTests.Context().GetBlobUri("container", "blob").AbsoluteUri,
            Size = size, ContentLength = 0, BlobType = "PageBlob"
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task MaximumRangeIsFourMiBAndTheLastRangeIsExact()
    {
        int length = UploadStoragePageBlobProcessor.MaximumPageBytes + 512;
        await System.IO.File.WriteAllBytesAsync(source, new byte[length]);
        var sizes = new List<int>();
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return BlobWorkflowTests.Head("PageBlob", length);
            if (BlobWorkflowTests.Query(request)["comp"] == "page")
            {
                int size = (await request.Content!.ReadAsByteArrayAsync()).Length;
                Assert.InRange(size, 512, UploadStoragePageBlobProcessor.MaximumPageBytes);
                sizes.Add(size);
            }
            return Created();
        });
        await Processor(fixture).ProcessAsync();
        Assert.Equal(new[] { 512, UploadStoragePageBlobProcessor.MaximumPageBytes }, sizes.OrderBy(value => value));
    }

    [Fact]
    public async Task SettingsAreAppliedAtCreateAndLeaseFollowsEveryRequest()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[512]);
        byte[] checksum = new byte[16];
        var fixture = new Fixture(request =>
        {
            Assert.Equal("lease", Assert.Single(request.Headers.GetValues("x-ms-lease-id")));
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Head("PageBlob", 512));
            if (BlobWorkflowTests.Query(request)["comp"] == "page")
            {
                Assert.Equal("\"created\"", request.Headers.IfMatch.Single().ToString());
                Assert.Empty(request.Headers.IfNoneMatch);
                Assert.Null(request.Headers.IfUnmodifiedSince);
                Assert.False(request.Headers.Contains("x-ms-if-tags"));
                return Task.FromResult(Created());
            }
            Assert.Equal("\"previous\"", request.Headers.IfMatch.Single().ToString());
            Assert.Equal("P10", Assert.Single(request.Headers.GetValues("x-ms-access-tier")));
            Assert.Equal("text/plain", Assert.Single(request.Headers.GetValues("x-ms-blob-content-type")));
            Assert.Equal("gzip", Assert.Single(request.Headers.GetValues("x-ms-blob-content-encoding")));
            Assert.Equal("en", Assert.Single(request.Headers.GetValues("x-ms-blob-content-language")));
            Assert.Equal("attachment", Assert.Single(request.Headers.GetValues("x-ms-blob-content-disposition")));
            Assert.Equal("no-cache", Assert.Single(request.Headers.GetValues("x-ms-blob-cache-control")));
            Assert.Equal(Convert.ToBase64String(checksum), Assert.Single(request.Headers.GetValues("x-ms-blob-content-md5")));
            Assert.Equal("value", Assert.Single(request.Headers.GetValues("x-ms-meta-key")));
            Assert.Equal("tag=one%2Btwo", Assert.Single(request.Headers.GetValues("x-ms-tags")));
            Assert.NotNull(request.Headers.IfUnmodifiedSince);
            Assert.Equal("\"old\"='value'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
            return Task.FromResult(Created());
        });
        var processor = Processor(fixture);
        processor.Overwrite = true; processor.IfMatch = "\"previous\""; processor.LeaseId = "lease";
        processor.IfUnmodifiedSince = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        processor.IfTags = "\"old\"='value'";
        processor.Tier = "P10"; processor.ContentType = "text/plain"; processor.ContentEncoding = "gzip";
        processor.ContentLanguage = "en"; processor.ContentDisposition = "attachment"; processor.CacheControl = "no-cache";
        processor.ContentMd5 = checksum; processor.Metadata = new Dictionary<string, string> { ["key"] = "value" };
        processor.Tags = new Dictionary<string, string> { ["tag"] = "one+two" };
        await processor.ProcessAsync();
        Assert.Equal("one+two", Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Tags["tag"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(511)]
    [InlineData(513)]
    public async Task UnalignedSourceFailsBeforeCreatingTheBlob(int length)
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[length]);
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(513)]
    [InlineData(4194816)]
    public async Task InvalidChunkSizesFailBeforeAuthentication(int size)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.ChunkSize = size;
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task UnsupportedTierAndSelectorsFailBeforeAuthentication()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Tier = "Cool";
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        processor.Tier = null;
        processor.VersionId = "version";
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task WhatIfDoesNotOpenSourceOrSendRequests()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.File = source + ".does-not-exist";
        processor.ShouldProcess = (_, _) => false;
        await processor.ProcessAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task ExistingBlobWithoutOverwriteFailsAtCreate()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[512]);
        var fixture = new Fixture(request =>
        {
            Assert.Null(BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal("*", request.Headers.IfNoneMatch.Single().ToString());
            return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet"));
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task ReplacementDuringRangesFailsClosedAndDoesNotClaimCompletion()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[1536]);
        int writes = 0;
        var fixture = new Fixture(request =>
        {
            Assert.NotEqual(HttpMethod.Head, request.Method);
            if (BlobWorkflowTests.Query(request)["comp"] is null) return Task.FromResult(Created());
            writes++;
            return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet"));
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 512; processor.Concurrency = 1;
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(1, writes);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Fact]
    public async Task RetryReplaysTheSamePageRangeAndPayload()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[512]);
        int attempts = 0;
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return BlobWorkflowTests.Head("PageBlob", 512);
            if (BlobWorkflowTests.Query(request)["comp"] is null) return Created();
            Assert.Equal("bytes=0-511", request.Headers.Range!.ToString());
            Assert.Equal("\"created\"", request.Headers.IfMatch.Single().ToString());
            Assert.Equal(new byte[512], await request.Content!.ReadAsByteArrayAsync());
            return ++attempts == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Created("\"written\"");
        });
        await Processor(fixture).ProcessAsync();
        Assert.Equal(2, attempts);
        Assert.Single(fixture.Output);
    }

    [Fact]
    public async Task CancellationAfterRangePreventsFinalSuccess()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[1024]);
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(request =>
        {
            if (BlobWorkflowTests.Query(request)["comp"] is not null) cancellation.Cancel();
            return Task.FromResult(Created());
        }, token: cancellation.Token);
        var processor = Processor(fixture);
        processor.ChunkSize = 512; processor.Concurrency = 1;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Fact]
    public async Task AtomicPageRangesPreserveOffsetsBeyondInt32()
    {
        long offset = 5L * 1024 * 1024 * 1024;
        var fixture = new Fixture(request =>
        {
            Assert.Equal($"bytes={offset}-{offset + 511}", request.Headers.Range!.ToString());
            Assert.Equal(512, request.Content!.Headers.ContentLength);
            return Task.FromResult(Created());
        });
        var processor = new BlobProcessors.UploadPagesPageBlobProcessor
        {
            Context = BlobWorkflowTests.Context(), Url = BlobWorkflowTests.Context().GetBlobUri("container", "blob").AbsoluteUri,
            Body = new byte[512], ContentLength = 512, Range = $"bytes={offset}-{offset + 511}", PageWrite = "update"
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task ParallelReadAheadDoesNotIssueConcurrentConditionalWrites()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[4096]);
        int active = 0, peak = 0, writes = 0;
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return BlobWorkflowTests.Head("PageBlob", 4096);
            if (BlobWorkflowTests.Query(request)["comp"] is null) return Created();
            int current = Interlocked.Increment(ref active);
            peak = Math.Max(peak, current);
            await Task.Yield();
            Interlocked.Increment(ref writes);
            Interlocked.Decrement(ref active);
            return Created();
        });
        var processor = Processor(fixture);
        processor.Concurrency = 4; processor.ChunkSize = 512;
        await processor.ProcessAsync();
        Assert.Equal(8, writes);
        Assert.Equal(1, peak);
    }

    [Fact]
    public async Task FailedRangeStopsOtherPreparedRangesWithoutFalseSuccess()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[4096]);
        int writes = 0;
        var fixture = new Fixture(request =>
        {
            Assert.NotEqual(HttpMethod.Head, request.Method);
            if (BlobWorkflowTests.Query(request)["comp"] is null) return Task.FromResult(Created());
            Interlocked.Increment(ref writes);
            return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.BadRequest, "InvalidPageRange"));
        });
        var processor = Processor(fixture);
        processor.Concurrency = 4; processor.ChunkSize = 512;
        var error = await Record.ExceptionAsync(() => processor.ProcessAsync());
        Assert.True(error is HttpRequestException or IOException, "A failed range must surface an HTTP failure or an explicit incomplete-upload error.");
        Assert.Equal(1, writes);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Fact]
    public async Task AmbiguousAppliedRangeDoesNotRetryWithoutItsOriginalEtagGuard()
    {
        await System.IO.File.WriteAllBytesAsync(source, new byte[512]);
        int writes = 0;
        var fixture = new Fixture(request =>
        {
            Assert.NotEqual(HttpMethod.Head, request.Method);
            if (BlobWorkflowTests.Query(request)["comp"] is null) return Task.FromResult(Created());
            Assert.Equal("\"created\"", request.Headers.IfMatch.Single().ToString());
            return Task.FromResult(++writes == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet"));
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(2, writes);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Fact]
    public async Task FinalPropertiesMustHaveTheExpectedTypeAndSize()
    {
        var fixture = new Fixture(request => Task.FromResult(request.Method == HttpMethod.Head
            ? BlobWorkflowTests.Head("BlockBlob", 0) : Created()));
        await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    private UploadStoragePageBlobProcessor Processor(Fixture fixture)
    {
        var processor = new UploadStoragePageBlobProcessor
        {
            Context = BlobWorkflowTests.Context(), Container = "container", Blob = "blob", File = source
        };
        processor.Init(fixture.Context);
        return processor;
    }

    private static HttpResponseMessage Created(string etag = "\"created\"")
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.ETag = new EntityTagHeaderValue(etag);
        return response;
    }

    public void Dispose() => System.IO.File.Delete(source);
}
