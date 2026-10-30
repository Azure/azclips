// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class AppendBlobUploadTests : IDisposable
{
    private readonly string source = Path.GetTempFileName();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AppendOrRecreatePreservesTheSelectedOverwriteBehavior(bool exists, bool overwrite)
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        await System.IO.File.WriteAllBytesAsync(source, bytes);
        var service = new AppendService(exists ? new byte[] { 9, 8 } : null);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Overwrite = overwrite;
        processor.ChunkSize = 2;
        processor.ValidateContent = true;
        await processor.ProcessAsync();
        Assert.Equal(exists && !overwrite ? new byte[] { 9, 8, 1, 2, 3, 4, 5 } : bytes, service.Bytes);
        Assert.Equal(exists && !overwrite ? 0 : 1, service.Creates);
        Assert.Equal(3, service.AppendAttempts);
        Assert.Equal(service.Bytes.Count, Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Properties.ContentLength);
        Assert.Equal(100, fixture.Progress.Last().Value);
        Assert.DoesNotContain(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData("response")]
    [InlineData("network")]
    [InlineData("exhausted")]
    public async Task AppliedAmbiguousAppendIsVerifiedWithoutDuplicatingBytes(string failure)
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9]) { Ambiguity = failure };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.ChunkSize = 2;
        await processor.ProcessAsync();
        Assert.Equal(new byte[] { 9, 1, 2, 3 }, service.Bytes);
        Assert.Equal(1, service.RangeReads);
        Assert.Single(fixture.Output);
        Assert.Contains(fixture.Messages, item => item.Message.Contains("Verified an ambiguous append", StringComparison.Ordinal));
        Assert.All(service.States, state => Assert.True(state.Attempts >= 1));
        Assert.Contains(service.States, state => failure == "network" ? state.Attempts == 1 : state.Attempts == 2);
        Assert.DoesNotContain(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("length")]
    [InlineData("count")]
    [InlineData("range")]
    [InlineData("etag")]
    [InlineData("short-body")]
    public async Task UnverifiableAmbiguityFailsWithoutAnotherAppend(string damage)
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9]) { Ambiguity = "response", Damage = damage };
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(2, service.AppendAttempts);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task RetryOfAnUnappliedAppendUsesIdenticalPositionEtagAndBytes()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9]) { RejectFirstWith = HttpStatusCode.ServiceUnavailable };
        var fixture = new Fixture(service.SendAsync);
        await Processor(fixture).ProcessAsync();
        Assert.Equal(new byte[] { 9, 1, 2, 3 }, service.Bytes);
        Assert.Equal(2, service.AppendAttempts);
        Assert.Equal(0, service.RangeReads);
        Assert.Single(fixture.Output);
        Assert.Equal(service.RequestBodies[0], service.RequestBodies[1]);
        Assert.Equal(service.RequestPositions[0], service.RequestPositions[1]);
        Assert.Equal(service.RequestEtags[0], service.RequestEtags[1]);
        Assert.Same(service.States[0], service.States[1]);
        Assert.Equal(2, service.States[0].Attempts);
        Assert.True(service.States[0].HadAmbiguousResponse);
    }

    [Theory]
    [InlineData(HttpStatusCode.PreconditionFailed)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotImplemented)]
    [InlineData(HttpStatusCode.HttpVersionNotSupported)]
    public async Task DefiniteFailuresAreNotReconciledOrTreatedAsSuccess(HttpStatusCode status)
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9]) { RejectFirstWith = status };
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(1, service.AppendAttempts);
        Assert.Equal(1, service.Heads);
        Assert.Equal(0, service.RangeReads);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThrottleThenPreconditionFailureIsNotEvidenceOfAnAppliedWrite(bool retryAfter)
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9]) { RejectFirstWith = (HttpStatusCode)429, RejectSecond = true, UseRetryAfter = retryAfter };
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(2, service.AppendAttempts);
        Assert.False(service.States[0].HadAmbiguousResponse);
        Assert.Same(service.States[0], service.States[1]);
        Assert.Equal(2, service.States[0].Attempts);
        Assert.Equal(1, service.Heads);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task AuthenticationTransportFailureDoesNotTriggerReconciliation()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        var fixture = new Fixture();
        fixture.Auth.AcquireTokenAsyncDefault = (_, _, _, _, _) => throw new HttpRequestException("Authentication transport failed.");
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(0, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task AuthenticationFailureOnAppendDoesNotProbeForAnUnsentWrite()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        var service = new AppendService([9]);
        var fixture = new Fixture(service.SendAsync);
        var acquire = fixture.Auth.AcquireTokenAsyncDefault;
        int calls = 0;
        fixture.Auth.AcquireTokenAsyncDefault = (scopes, tenant, claims, token, profile) =>
        {
            if (++calls == 2) throw new HttpRequestException("Authentication transport failed.");
            return acquire(scopes, tenant, claims, token, profile);
        };
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(1, service.Heads);
        Assert.Equal(0, service.AppendAttempts);
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("BlobNotFound", true)]
    [InlineData("AuthorizationFailure", false)]
    public async Task OnlyMissingSelectedBlobPermitsCreation(string code, bool creates)
    {
        int createCount = 0;
        var service = new AppendService();
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head && createCount == 0)
                return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.NotFound, code));
            if (request.Method == HttpMethod.Put && BlobWorkflowTests.Query(request)["comp"] is null) createCount++;
            return service.SendAsync(request);
        });
        if (creates) await Processor(fixture).ProcessAsync();
        else await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(creates ? 1 : 0, createCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyCliUploadIsAnExplicitNoOpEvenWithOverwrite(bool overwrite)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.SkipEmptyUpload = true; processor.Overwrite = overwrite;
        await processor.ProcessAsync();
        Assert.Equal(0, fixture.Sends);
        Assert.Equal("{}", JsonSerializer.Serialize(Assert.IsType<StorageAppendUploadNoOp>(Assert.Single(fixture.Output))));
    }

    [Fact]
    public async Task NativeEmptyRecreateInitializesAnEmptyAppendBlob()
    {
        var service = new AppendService([9]);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Overwrite = true;
        await processor.ProcessAsync();
        Assert.Equal(1, service.Creates);
        Assert.Empty(service.Bytes);
        Assert.Equal(0, service.AppendAttempts);
        Assert.Single(fixture.Output);
    }

    [Fact]
    public async Task MaxSizeAndBlockCountAreCheckedBeforeMutation()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9, 8]) { Blocks = 50000 };
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<ArgumentException>(() => Processor(fixture).ProcessAsync());
        service.Blocks = 1;
        var processor = Processor(fixture);
        processor.MaxSizeCondition = 4;
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Equal(0, service.AppendAttempts);
        Assert.Equal(0, service.Creates);
    }

    [Fact]
    public async Task ExistingBlobSettingsAreNotInventedFromIgnoredCreateOptions()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        var service = new AppendService([9]);
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Tags = new Dictionary<string, string> { ["ignored"] = "value" };
        processor.ContentType = "text/plain";
        await processor.ProcessAsync();
        Assert.Empty(Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Tags);
        Assert.Equal(0, service.Creates);
        Assert.Contains(fixture.Messages, item => item.Message.Contains("retains", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhatIfDoesNotOpenSourceOrSendRequests()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.File = source + ".does-not-exist"; processor.ShouldProcess = (_, _) => false;
        await processor.ProcessAsync();
        Assert.Equal(0, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task CancellationAfterAppliedAppendDoesNotReconcileOrAppendAgain()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        var service = new AppendService([9]);
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (BlobWorkflowTests.Query(request)["comp"] == "appendblock") cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(1, service.AppendAttempts);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task CreationRaceCannotOverwriteAnotherWritersNewBlob()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.NotFound, "BlobNotFound"));
            Assert.Null(BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal("*", request.Headers.IfNoneMatch.Single().ToString());
            return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet"));
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(2, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task CreateSettingsAndInitialConditionsAreSeparatedFromGuardedAppends()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9]);
        var fixture = new Fixture(request =>
        {
            Assert.Equal("lease", Assert.Single(request.Headers.GetValues("x-ms-lease-id")));
            if (request.Method == HttpMethod.Put && BlobWorkflowTests.Query(request)["comp"] is null)
            {
                Assert.Equal("\"initial\"", request.Headers.IfMatch.Single().ToString());
                Assert.Equal("text/plain", Assert.Single(request.Headers.GetValues("x-ms-blob-content-type")));
                Assert.Equal("gzip", Assert.Single(request.Headers.GetValues("x-ms-blob-content-encoding")));
                Assert.Equal("en", Assert.Single(request.Headers.GetValues("x-ms-blob-content-language")));
                Assert.Equal("attachment", Assert.Single(request.Headers.GetValues("x-ms-blob-content-disposition")));
                Assert.Equal("no-cache", Assert.Single(request.Headers.GetValues("x-ms-blob-cache-control")));
                Assert.Equal("owner", Assert.Single(request.Headers.GetValues("x-ms-meta-owner")));
                Assert.Equal("new=value", Assert.Single(request.Headers.GetValues("x-ms-tags")));
                Assert.Equal("\"old\"='value'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
                Assert.NotNull(request.Headers.IfUnmodifiedSince);
            }
            else
            {
                Assert.False(request.Headers.Contains("x-ms-if-tags"));
                Assert.Null(request.Headers.IfUnmodifiedSince);
                if (request.Method == HttpMethod.Put)
                    Assert.Equal("8", Assert.Single(request.Headers.GetValues("x-ms-blob-condition-maxsize")));
            }
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Overwrite = true; processor.ChunkSize = 2; processor.MaxSizeCondition = 8; processor.LeaseId = "lease";
        processor.IfMatch = "\"initial\""; processor.IfUnmodifiedSince = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        processor.IfTags = "\"old\"='value'";
        processor.ContentType = "text/plain"; processor.ContentEncoding = "gzip"; processor.ContentLanguage = "en";
        processor.ContentDisposition = "attachment"; processor.CacheControl = "no-cache";
        processor.Metadata = new Dictionary<string, string> { ["owner"] = "owner" };
        processor.Tags = new Dictionary<string, string> { ["new"] = "value" };
        await processor.ProcessAsync();
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Bytes);
        Assert.Equal("value", Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Tags["new"]);
    }

    [Fact]
    public async Task DefaultAppendChunksStayWithinEightMiB()
    {
        byte[] bytes = new byte[StorageWireFormat.MaximumChunkBytes + 17];
        Random.Shared.NextBytes(bytes);
        await System.IO.File.WriteAllBytesAsync(source, bytes);
        var service = new AppendService();
        var fixture = new Fixture(service.SendAsync);
        await Processor(fixture).ProcessAsync();
        Assert.Equal(new[] { StorageWireFormat.MaximumChunkBytes, 17 }, service.RequestBodies.Select(body => body.Length));
        Assert.Equal(bytes, service.Bytes);
        Assert.Single(fixture.Output);
    }

    [Fact]
    public async Task ReconciliationReadPermissionFailureIsExplicitAndDoesNotAppendAgain()
    {
        await System.IO.File.WriteAllBytesAsync(source, [1, 2, 3]);
        var service = new AppendService([9]) { Ambiguity = "response", Damage = "forbidden" };
        var fixture = new Fixture(service.SendAsync);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal(2, service.AppendAttempts);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
        Assert.Single(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
    }

    private UploadStorageAppendBlobProcessor Processor(Fixture fixture)
    {
        var processor = new UploadStorageAppendBlobProcessor
        {
            Context = BlobWorkflowTests.Context(), Container = "container", Blob = "blob", File = source
        };
        processor.Init(fixture.Context);
        return processor;
    }

    public void Dispose() => System.IO.File.Delete(source);

    private sealed class AppendService
    {
        internal List<byte> Bytes { get; } = [];
        internal bool Exists { get; private set; }
        internal int Blocks { get; set; }
        internal int Creates { get; private set; }
        internal int Heads { get; private set; }
        internal int AppendAttempts { get; private set; }
        internal int RangeReads { get; private set; }
        internal string? Ambiguity { get; init; }
        internal string? Damage { get; init; }
        internal HttpStatusCode? RejectFirstWith { get; init; }
        internal bool RejectSecond { get; init; }
        internal bool UseRetryAfter { get; init; }
        internal List<RequestAttemptState> States { get; } = [];
        internal List<byte[]> RequestBodies { get; } = [];
        internal List<long> RequestPositions { get; } = [];
        internal List<string> RequestEtags { get; } = [];
        private int version = 1;
        private string Etag => $"\"v{version}\"";
        private bool ambiguityUsed;

        internal AppendService(byte[]? bytes = null)
        {
            Exists = bytes is not null;
            if (bytes is not null) Bytes.AddRange(bytes);
            Blocks = Bytes.Count > 0 ? 1 : 0;
        }

        internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            string? operation = BlobWorkflowTests.Query(request)["comp"];
            if (request.Method == HttpMethod.Head)
            {
                Heads++;
                if (!Exists) return BlobWorkflowTests.Error(HttpStatusCode.NotFound, "BlobNotFound");
                if (request.Headers.IfMatch.Any() && request.Headers.IfMatch.Single().ToString() != Etag)
                    return BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
                var response = BlobWorkflowTests.Head("AppendBlob", Bytes.Count);
                response.Headers.ETag = new EntityTagHeaderValue(Etag);
                response.Headers.Add("x-ms-blob-committed-block-count", Blocks.ToString(CultureInfo.InvariantCulture));
                return response;
            }
            if (request.Method == HttpMethod.Get)
            {
                RangeReads++;
                if (Damage == "forbidden") return BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthorizationFailure");
                Assert.Equal(Etag, request.Headers.IfMatch.Single().ToString());
                var range = request.Headers.Range!.Ranges.Single();
                int start = checked((int)range.From!.Value), end = checked((int)range.To!.Value);
                byte[] bytes = Bytes.Skip(start).Take(end - start + 1).ToArray();
                if (Damage == "short-body") bytes = bytes[..^1];
                var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes) };
                response.Headers.ETag = new EntityTagHeaderValue(Damage == "etag" ? "\"other\"" : Etag);
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, Damage == "range" ? Bytes.Count + 1 : Bytes.Count);
                return response;
            }
            if (operation is null)
            {
                Creates++;
                Assert.Equal("AppendBlob", Assert.Single(request.Headers.GetValues("x-ms-blob-type")));
                Assert.Equal(0, request.Content!.Headers.ContentLength);
                if (request.Headers.IfNoneMatch.Any(value => value.ToString() == "*") && Exists)
                    return BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
                Exists = true; Bytes.Clear(); Blocks = 0; version++;
                return Created();
            }
            Assert.Equal("appendblock", operation);
            AppendAttempts++;
            Assert.True(request.Options.TryGetValue(RequestAttemptState.OptionsKey, out var state));
            States.Add(state);
            byte[] body = await request.Content!.ReadAsByteArrayAsync();
            long offset = long.Parse(Assert.Single(request.Headers.GetValues("x-ms-blob-condition-appendpos")), CultureInfo.InvariantCulture);
            string etag = request.Headers.IfMatch.Single().ToString();
            RequestBodies.Add(body); RequestPositions.Add(offset); RequestEtags.Add(etag);
            if (request.Content.Headers.ContentMD5 is { } checksum) Assert.Equal(StorageWireFormat.ContentMd5(body), checksum);
            if (AppendAttempts == 1 && RejectFirstWith is { } rejected)
            {
                var response = BlobWorkflowTests.Error(rejected, "ConditionNotMet");
                if (UseRetryAfter) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return response;
            }
            if (AppendAttempts == 2 && RejectSecond)
                return BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
            if (ambiguityUsed && Ambiguity == "exhausted" && etag != Etag)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            if (etag != Etag || offset != Bytes.Count)
                return BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
            Bytes.AddRange(body); Blocks++; version++;
            if (!ambiguityUsed && Ambiguity is not null)
            {
                ambiguityUsed = true;
                if (Damage == "bytes") Bytes[^1] ^= 1;
                if (Damage == "length") Bytes.Add(42);
                if (Damage == "count") Blocks++;
                if (Ambiguity == "network") throw new HttpRequestException("Ambiguous transport failure.");
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            var appended = Created();
            appended.Headers.Add("x-ms-blob-append-offset", offset.ToString(CultureInfo.InvariantCulture));
            appended.Headers.Add("x-ms-blob-committed-block-count", Blocks.ToString(CultureInfo.InvariantCulture));
            return appended;
        }

        private HttpResponseMessage Created()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.ETag = new EntityTagHeaderValue(Etag);
            return response;
        }
    }
}
