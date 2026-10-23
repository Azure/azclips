// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

#pragma warning disable CA5351 // Azure Content-MD5 protocol checksums, not security-sensitive hashes.

namespace Extensions.Storage.Tests;

public sealed class BlockBlobUploadTests : IDisposable
{
    private readonly string source = Path.GetTempFileName();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    public async Task SmallAndEmptyFilesUseOneProtectedPutThenPinnedProperties(long length)
    {
        byte[] bytes = Enumerable.Range(0, (int)length).Select(index => (byte)index).ToArray();
        await System.IO.File.WriteAllBytesAsync(source, bytes);
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                Assert.Equal("\"etag\"", request.Headers.IfMatch.Single().ToString());
                Assert.Empty(request.Headers.IfNoneMatch);
                return BlobWorkflowTests.Head(length: length);
            }
            Assert.Null(BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal("*", request.Headers.IfNoneMatch.Single().ToString());
            Assert.Equal("BlockBlob", Assert.Single(request.Headers.GetValues("x-ms-blob-type")));
            Assert.Equal(length, request.Content!.Headers.ContentLength);
            Assert.Equal(bytes, await request.Content.ReadAsByteArrayAsync());
            Assert.Equal(MD5.HashData(bytes), request.Content.Headers.ContentMD5);
            return Created();
        });
        var processor = Processor(fixture);
        processor.ValidateContent = true;
        await processor.ProcessAsync();
        Assert.Equal(2, fixture.Sends);
        Assert.Equal(length, Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Properties.ContentLength);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task MultiblockUploadIsBoundedCommitsInFileOrderAndRoundTripsBytes(int concurrency)
    {
        byte[] bytes = new byte[37];
        Random.Shared.NextBytes(bytes);
        await System.IO.File.WriteAllBytesAsync(source, bytes);
        var blocks = new ConcurrentDictionary<string, byte[]>();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0, peak = 0, totalStages = 0, commits = 0;
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return BlobWorkflowTests.Head(length: bytes.Length);
            string? operation = BlobWorkflowTests.Query(request)["comp"];
            if (operation == "block")
            {
                int concurrent = Interlocked.Increment(ref active);
                int old;
                do { old = peak; } while (Interlocked.CompareExchange(ref peak, Math.Max(old, concurrent), old) != old);
                if (Interlocked.Increment(ref totalStages) == concurrency) arrived.TrySetResult();
                await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
                byte[] body = await request.Content!.ReadAsByteArrayAsync();
                Assert.InRange(body.Length, 1, 8);
                Assert.Equal(MD5.HashData(body), request.Content.Headers.ContentMD5);
                Assert.Empty(request.Headers.IfNoneMatch);
                Assert.False(request.Headers.Contains("x-ms-meta-owner"));
                blocks.AddOrUpdate(BlobWorkflowTests.Query(request)["blockid"]!, body, (_, _) => throw new InvalidOperationException("Duplicate stage."));
                Interlocked.Decrement(ref active);
                return Created();
            }
            Assert.Equal("blocklist", operation);
            Assert.Equal(0, active);
            Assert.Equal(5, blocks.Count);
            Interlocked.Increment(ref commits);
            Assert.Equal("*", request.Headers.IfNoneMatch.Single().ToString());
            var xml = XElement.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("BlockList", xml.Name.LocalName);
            Assert.All(xml.Elements(), element => Assert.Equal("Uncommitted", element.Name.LocalName));
            Assert.Equal(bytes, xml.Elements().SelectMany(element => blocks[element.Value]).ToArray());
            Assert.Single(blocks.Keys.Select(key => Convert.FromBase64String(key).Length).Distinct());
            Assert.Equal(5, blocks.Keys.Distinct().Count());
            return Created();
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 8;
        processor.Concurrency = concurrency;
        processor.ValidateContent = true;
        await processor.ProcessAsync();
        Assert.Equal(concurrency, peak);
        Assert.Equal(1, commits);
        var progress = fixture.Progress.Select(data => data.Value).ToList();
        Assert.Equal(progress.OrderBy(value => value), progress);
        Assert.All(progress.SkipLast(1), value => Assert.InRange(value, 0, 99));
        Assert.Equal(100, progress.Last());
        Assert.Single(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettingsTagsAndConditionsAreAppliedToTheWriteThatPublishesContent(bool multipart)
    {
        byte[] bytes = [1, 2, 3];
        byte[] hash = MD5.HashData(bytes);
        await System.IO.File.WriteAllBytesAsync(source, bytes);
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                Assert.Equal("\"etag\"", request.Headers.IfMatch.Single().ToString());
                return BlobWorkflowTests.Head(length: bytes.Length);
            }
            if (BlobWorkflowTests.Query(request)["comp"] == "block")
            {
                Assert.Equal("lease", Assert.Single(request.Headers.GetValues("x-ms-lease-id")));
                return Created();
            }
            Assert.Equal("\"prior\"", request.Headers.IfMatch.Single().ToString());
            Assert.Empty(request.Headers.IfNoneMatch);
            Assert.Equal("text/plain", Assert.Single(request.Headers.GetValues("x-ms-blob-content-type")));
            Assert.Equal("gzip", Assert.Single(request.Headers.GetValues("x-ms-blob-content-encoding")));
            Assert.Equal("en", Assert.Single(request.Headers.GetValues("x-ms-blob-content-language")));
            Assert.Equal("attachment", Assert.Single(request.Headers.GetValues("x-ms-blob-content-disposition")));
            Assert.Equal("no-cache", Assert.Single(request.Headers.GetValues("x-ms-blob-cache-control")));
            Assert.Equal(Convert.ToBase64String(hash), Assert.Single(request.Headers.GetValues("x-ms-blob-content-md5")));
            Assert.Equal("owner", Assert.Single(request.Headers.GetValues("x-ms-meta-owner")));
            Assert.Equal("Cool", Assert.Single(request.Headers.GetValues("x-ms-access-tier")));
            Assert.Equal("a%20b=c%2Bd%2Fe%3A%3D", Assert.Single(request.Headers.GetValues("x-ms-tags")));
            Assert.Equal("\"tag\"='value'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
            Assert.Equal("Tue, 20 Oct 2026 00:00:00 GMT", request.Headers.IfUnmodifiedSince!.Value.ToString("R"));
            if (multipart) Assert.Equal("blocklist", BlobWorkflowTests.Query(request)["comp"]);
            else Assert.Equal(bytes, await request.Content!.ReadAsByteArrayAsync());
            return Created();
        });
        var processor = Processor(fixture);
        processor.ChunkSize = multipart ? 2 : 8;
        processor.Overwrite = true; processor.IfMatch = "\"prior\""; processor.LeaseId = "lease";
        processor.IfTags = "\"tag\"='value'"; processor.IfUnmodifiedSince = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        processor.ContentType = "text/plain"; processor.ContentEncoding = "gzip"; processor.ContentLanguage = "en";
        processor.ContentDisposition = "attachment"; processor.CacheControl = "no-cache"; processor.ContentMd5 = hash;
        processor.Metadata = new Dictionary<string, string> { ["owner"] = "owner" };
        processor.Tags = new Dictionary<string, string> { ["a b"] = "c+d/e:=" }; processor.Tier = "Cool";
        await processor.ProcessAsync();
        Assert.Equal("c+d/e:=", Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Tags["a b"]);
    }

    [Fact]
    public async Task RuntimeRetryReplaysTheSameBlockIdAndBytes()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var attempts = new ConcurrentDictionary<string, int>();
        var bodies = new ConcurrentDictionary<string, byte[]>();
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return BlobWorkflowTests.Head(length: 3);
            if (BlobWorkflowTests.Query(request)["comp"] != "block") return Created();
            string id = BlobWorkflowTests.Query(request)["blockid"]!;
            byte[] bytes = await request.Content!.ReadAsByteArrayAsync();
            if (bodies.TryGetValue(id, out byte[]? previous)) Assert.Equal(previous, bytes);
            else bodies[id] = bytes;
            int attempt = attempts.AddOrUpdate(id, 1, (_, count) => count + 1);
            return attempt == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Created();
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 2;
        await processor.ProcessAsync();
        Assert.Equal(2, attempts.Count);
        Assert.All(attempts.Values, count => Assert.Equal(2, count));
        Assert.Single(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedStageOrCommitNeverProducesSuccess(bool commitFailure)
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        int commits = 0;
        var fixture = new Fixture(request =>
        {
            Assert.NotEqual(HttpMethod.Head, request.Method);
            bool commit = BlobWorkflowTests.Query(request)["comp"] == "blocklist";
            if (commit) Interlocked.Increment(ref commits);
            return Task.FromResult(commit == commitFailure
                ? BlobWorkflowTests.Error(HttpStatusCode.BadRequest, "InvalidOperation") : Created());
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 2;
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(commitFailure ? 1 : 0, commits);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Fact]
    public async Task CancellationAfterStagesPreventsCommit()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(request =>
        {
            Assert.Equal("block", BlobWorkflowTests.Query(request)["comp"]);
            cancellation.Cancel();
            return Task.FromResult(Created());
        }, token: cancellation.Token);
        var processor = Processor(fixture);
        processor.ChunkSize = 2;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Fact]
    public async Task WhatIfDoesNotOpenTheSourceOrAuthenticate()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.File = source + ".does-not-exist";
        processor.ShouldProcess = (_, _) => false;
        await processor.ProcessAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("workers")]
    [InlineData("chunk")]
    [InlineData("hash")]
    [InlineData("metadata")]
    [InlineData("tags")]
    [InlineData("tier")]
    [InlineData("header")]
    [InlineData("snapshot")]
    [InlineData("overwrite-condition")]
    [InlineData("blocks")]
    public async Task InvalidInputsAreRejectedBeforeNetwork(string invalid)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        switch (invalid)
        {
            case "workers": processor.Concurrency = 5; break;
            case "chunk": processor.ChunkSize = StorageWireFormat.MaximumChunkBytes + 1; break;
            case "hash": processor.ContentMd5 = [1]; break;
            case "metadata": processor.Metadata = new Dictionary<string, string> { ["bad-key"] = "value" }; break;
            case "tags": processor.Tags = new Dictionary<string, string> { ["key"] = "bad&value" }; break;
            case "tier": processor.Tier = "P4"; break;
            case "header": processor.ContentType = "text/plain\r\nbad"; break;
            case "snapshot": processor.Snapshot = "snapshot"; break;
            case "overwrite-condition": processor.IfNoneMatch = "\"etag\""; break;
            case "blocks":
                using (var stream = System.IO.File.OpenWrite(source)) stream.SetLength(50001);
                processor.ChunkSize = 1;
                break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task ConcurrentReplacementIsNotReturnedAsTheUploadedObject()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var fixture = new Fixture(request => Task.FromResult(request.Method == HttpMethod.Head
            ? BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet") : Created()));
        var processor = Processor(fixture);
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task MissingNameDefaultsToFileNameWithoutMutatingPipelineInputs()
    {
        var fixture = new Fixture(request =>
        {
            Assert.EndsWith("/" + Path.GetFileName(source), request.RequestUri!.AbsolutePath);
            return Task.FromResult(request.Method == HttpMethod.Head ? BlobWorkflowTests.Head() : Created());
        });
        var processor = Processor(fixture);
        processor.Blob = null;
        await processor.ProcessAsync();
        Assert.Null(processor.Blob);
        Assert.Equal(Path.GetFileName(source), Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Name);
    }

    [Fact]
    public async Task DefaultChunksNeverExceedEightMiB()
    {
        byte[] bytes = new byte[StorageWireFormat.MaximumChunkBytes + 17];
        Random.Shared.NextBytes(bytes);
        await System.IO.File.WriteAllBytesAsync(source, bytes);
        var blocks = new ConcurrentDictionary<string, byte[]>();
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head) return BlobWorkflowTests.Head(length: bytes.Length);
            if (BlobWorkflowTests.Query(request)["comp"] == "block")
            {
                byte[] body = await request.Content!.ReadAsByteArrayAsync();
                Assert.InRange(body.Length, 1, StorageWireFormat.MaximumChunkBytes);
                blocks[BlobWorkflowTests.Query(request)["blockid"]!] = body;
            }
            else
            {
                var xml = XElement.Parse(await request.Content!.ReadAsStringAsync());
                byte[] uploaded = xml.Elements().SelectMany(element => blocks[element.Value]).ToArray();
                Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(uploaded));
            }
            return Created();
        });
        await Processor(fixture).ProcessAsync();
        Assert.Equal(new[] { 17, StorageWireFormat.MaximumChunkBytes }, blocks.Values.Select(value => value.Length).OrderBy(value => value));
    }

    [Fact]
    public async Task NoOverwriteConditionIsEnforcedAtCommitNotByARacyPrecheck()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var fixture = new Fixture(request =>
        {
            Assert.NotEqual(HttpMethod.Head, request.Method);
            if (BlobWorkflowTests.Query(request)["comp"] == "block") return Task.FromResult(Created());
            Assert.Equal("*", request.Headers.IfNoneMatch.Single().ToString());
            return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet"));
        });
        var processor = Processor(fixture);
        processor.ChunkSize = 2;
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Fact]
    public async Task IndependentUploadsUseDifferentBlockIdNamespaces()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var ids = new List<string>();
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Head(length: 3));
            if (BlobWorkflowTests.Query(request)["comp"] == "block")
                ids.Add(BlobWorkflowTests.Query(request)["blockid"]!);
            return Task.FromResult(Created());
        });
        for (int upload = 0; upload < 2; upload++)
        {
            var processor = Processor(fixture);
            processor.ChunkSize = 2;
            processor.Concurrency = 1;
            await processor.ProcessAsync();
        }
        Assert.Equal(4, ids.Distinct().Count());
    }

    [Fact]
    public async Task MissingSourceIsAnExplicitLocalErrorBeforeAuthentication()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.File = source + ".does-not-exist";
        await Assert.ThrowsAsync<FileNotFoundException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task CancellationDuringFinalVerificationDoesNotReportCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(request =>
        {
            if (request.Method != HttpMethod.Head) return Task.FromResult(Created());
            cancellation.Cancel();
            return Task.FromResult(BlobWorkflowTests.Head());
        }, token: cancellation.Token);
        var processor = Processor(fixture);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingWriteEtagOrIncorrectCommittedLengthCannotProduceSuccess(bool missingEtag)
    {
        var fixture = new Fixture(request => Task.FromResult(request.Method == HttpMethod.Head
            ? BlobWorkflowTests.Head(length: 1)
            : missingEtag ? new HttpResponseMessage(HttpStatusCode.Created) : Created()));
        var processor = Processor(fixture);
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, data => data.Value == 100);
    }

    private UploadStorageBlockBlobProcessor Processor(Fixture fixture)
    {
        var processor = new UploadStorageBlockBlobProcessor
        {
            Context = BlobWorkflowTests.Context(), Container = "container", Blob = "blob", File = source
        };
        processor.Init(fixture.Context);
        return processor;
    }

    private static HttpResponseMessage Created()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag\"");
        return response;
    }

    public void Dispose() => System.IO.File.Delete(source);
}
