// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobContentPowerShellOrchestratorTests : IDisposable
{
    private readonly string directory = Path.Combine(Environment.CurrentDirectory, "blob-ps-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(directory, "source.txt");

    public BlobContentPowerShellOrchestratorTests()
    {
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(Source, "hello");
    }

    [Fact]
    public async Task ExistingDestinationRequestsConfirmationBeforeAnyMutationAndPinsTheApprovedReplacement()
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Head(length: 5)));
        var request = Upload();
        var plan = request.Prepare();
        request.Blob = "changed"; request.File = "missing";
        var command = new SetStorageBlobContentProcessor_Orchestrator { Plan = plan };
        command.Init(fixture.Context);
        var conflict = await Assert.ThrowsAsync<StorageBlobOverwriteRequiredException>(() => command.ProcessAsync());
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.EndsWith("/container/source.txt", conflict.Plan.Target);

        var retryFixture = new Fixture(request =>
        {
            Assert.Equal("\"etag\"", Assert.Single(request.Headers.IfMatch).ToString());
            Assert.Equal("/container/source.txt", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Head(length: 5));
            Assert.Equal(HttpMethod.Put, request.Method);
            return Task.FromResult(Created());
        });
        var retry = new SetStorageBlobContentProcessor_Orchestrator { Plan = conflict.Plan.ApproveOverwrite() };
        retry.Init(retryFixture.Context);
        await retry.ProcessAsync();
        Assert.Equal(2, retryFixture.Sends);
        Assert.IsType<StorageBlob>(Assert.Single(retryFixture.Output));
    }

    [Theory]
    [InlineData("Block", "BlockBlob")]
    [InlineData("Append", "AppendBlob")]
    public async Task MissingDestinationWritesCreateOnlyAndAppendNeverAppendsToAnUnconfirmedExistingBlob(string kind, string wireType)
    {
        int heads = 0;
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head && ++heads == 1)
                return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.NotFound, "BlobNotFound"));
            if (request.Method == HttpMethod.Head)
            {
                var response = BlobWorkflowTests.Head(length: 5);
                response.Headers.Remove("x-ms-blob-type");
                response.Headers.Add("x-ms-blob-type", wireType);
                return Task.FromResult(response);
            }
            if (request.Headers.Contains("x-ms-blob-type"))
            {
                Assert.Equal(wireType, Assert.Single(request.Headers.GetValues("x-ms-blob-type")));
                Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).ToString());
            }
            return Task.FromResult(Created());
        });
        var request = Upload(); request.BlobType = kind;
        var command = new SetStorageBlobContentProcessor_Orchestrator { Plan = request.Prepare() };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Equal(2, heads);
        Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "AuthorizationFailure")]
    [InlineData(HttpStatusCode.NotFound, "ContainerNotFound")]
    [InlineData(HttpStatusCode.NotFound, null)]
    [InlineData(HttpStatusCode.Conflict, "LeaseIdMissing")]
    public async Task ProbeFailuresNeverBecomeOverwriteApprovalOrSuccess(HttpStatusCode status, string? code)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(status, code)));
        var command = new SetStorageBlobContentProcessor_Orchestrator { Plan = Upload().Prepare() };
        command.Init(fixture.Context);
        await Assert.ThrowsAsync<HttpRequestException>(() => command.ProcessAsync());
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task CancellationDuringDiscoveryNeverRequestsConfirmation()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(BlobWorkflowTests.Head(length: 5));
        }, token: cancellation.Token);
        var command = new SetStorageBlobContentProcessor_Orchestrator { Plan = Upload().Prepare() };
        command.Init(fixture.Context);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ProcessAsync());
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task ForceSkipsDiscoveryAndFreezesMutableSettingsBeforeExecution()
    {
        var metadata = new Dictionary<string, string> { ["owner"] = "original" };
        var tags = new Dictionary<string, string> { ["team"] = "original" };
        var request = Upload(); request.Force = true; request.Metadata = metadata; request.Tags = tags;
        var plan = request.Prepare();
        metadata["owner"] = "changed"; tags["team"] = "changed";
        int puts = 0;
        var fixture = new Fixture(message =>
        {
            if (message.Method == HttpMethod.Head)
            {
                Assert.Equal(1, puts);
                return Task.FromResult(BlobWorkflowTests.Head(length: 5));
            }
            puts++;
            Assert.Equal("original", Assert.Single(message.Headers.GetValues("x-ms-meta-owner")));
            Assert.Equal("team=original", Assert.Single(message.Headers.GetValues("x-ms-tags")));
            Assert.Empty(message.Headers.IfNoneMatch);
            return Task.FromResult(Created());
        });
        var command = new SetStorageBlobContentProcessor_Orchestrator { Plan = plan };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Equal(2, fixture.Sends);
        Assert.Equal("original", Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Tags["team"]);
    }

    [Theory]
    [InlineData("page")]
    [InlineData("tier")]
    [InlineData("md5")]
    [InlineData("workers")]
    [InlineData("tags")]
    [InlineData("snapshot")]
    public void InvalidUploadsFailDuringLocalPreflight(string invalid)
    {
        var command = Upload();
        if (invalid == "page") command.BlobType = "Page";
        if (invalid == "tier") command.Tier = "P4";
        if (invalid == "md5") command.ContentMd5 = [1];
        if (invalid == "workers") command.Concurrency = 5;
        if (invalid == "tags") command.Tags = new Dictionary<string, string> { ["invalid!"] = "value" };
        if (invalid == "snapshot") command.Snapshot = "snapshot";
        Assert.Throws<ArgumentException>(() => command.Prepare());
    }

    [Fact]
    public void DownloadDirectoryDefaultsRetainHierarchyAndLegacySnapshotSuffixWithoutCreatingDirectories()
    {
        var command = new GetStorageBlobContentProcessor_Orchestrator
        {
            Context = BlobWorkflowTests.Context(), Container = "container", Blob = "nested/item.txt",
            Snapshot = "2026-10-22T01:02:03.1234567Z"
        };
        var plan = command.Prepare(directory);
        Assert.Equal(Path.Combine(directory, "nested", "item (2026-10-22 010203).txt"), plan.Target);
        Assert.False(Directory.Exists(Path.Combine(directory, "nested")));
    }

    [Fact]
    public async Task PageDispatchPreservesSettingsAndCreateOnlySafety()
    {
        System.IO.File.WriteAllBytes(Source, Enumerable.Repeat((byte)1, 512).ToArray());
        int heads = 0;
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(++heads == 1 ?
                    BlobWorkflowTests.Error(HttpStatusCode.NotFound, "BlobNotFound") :
                    BlobWorkflowTests.Head("PageBlob", 512));
            if (request.Headers.Contains("x-ms-blob-type"))
            {
                Assert.Equal("PageBlob", Assert.Single(request.Headers.GetValues("x-ms-blob-type")));
                Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).ToString());
                Assert.Equal("P10", Assert.Single(request.Headers.GetValues("x-ms-access-tier")));
            }
            else
            {
                Assert.Equal("page", BlobWorkflowTests.Query(request)["comp"]);
                Assert.Equal("\"etag\"", Assert.Single(request.Headers.IfMatch).ToString());
            }
            return Task.FromResult(Created());
        });
        var request = Upload(); request.BlobType = "Page"; request.Tier = "P10";
        var command = new SetStorageBlobContentProcessor_Orchestrator { Plan = request.Prepare() };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Equal(4, fixture.Sends);
        Assert.Equal("PageBlob", Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Properties.BlobType);
    }

    [Fact]
    public async Task NativeSnapshotDownloadKeepsItsSelectorAndWritesDiskNotPipelineBytes()
    {
        string snapshot = "2026-10-22T01:02:03.1234567Z";
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            Assert.Equal(snapshot, BlobWorkflowTests.Query(request)["snapshot"]);
            return Task.FromResult(BlobWorkflowTests.Head());
        });
        string destination = Path.Combine(directory, "download");
        var request = new GetStorageBlobContentProcessor_Orchestrator
        {
            InputObject = new StorageBlob("item", new("container", BlobWorkflowTests.Context()), new()) { Snapshot = snapshot },
            Destination = destination
        };
        var command = new GetStorageBlobContentProcessor_Orchestrator { Plan = request.Prepare(directory) };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.True(System.IO.File.Exists(destination));
        Assert.Empty(System.IO.File.ReadAllBytes(destination));
        Assert.Equal(snapshot, Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Snapshot);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("nested/../../outside")]
    [InlineData("/absolute")]
    [InlineData("nested//empty")]
    public void DownloadNamesCannotEscapeOrAmbiguouslyMapTheChosenDirectory(string name)
    {
        var command = new GetStorageBlobContentProcessor_Orchestrator
        {
            Context = BlobWorkflowTests.Context(), Container = "container", Blob = name
        };
        Assert.Throws<ArgumentException>(() => command.Prepare(directory));
    }

    [Fact]
    public void CopyPreflightRetainsIndependentContextsAndDefaultsDestinationToSourceName()
    {
        var sourceContext = new StorageContext("sourceaccount", Fixture.DefaultProfile);
        var source = new StorageBlob("nested/source.txt", new("source", sourceContext), new()) { VersionId = "version" };
        var command = new StartStorageBlobCopyPowerShellProcessor_Orchestrator
        {
            Request = new StartStorageBlobCopyProcessor_Orchestrator
            {
                Context = BlobWorkflowTests.Context(), Container = "container",
                SourceContext = sourceContext, SourceBlobObject = source
            }
        };
        var plan = command.Prepare(false);
        Assert.Equal("https://fixture.blob.core.windows.net/container/nested/source.txt", plan.Target);
        command.Request.SourceBlobObject = null;
        Assert.Equal("https://fixture.blob.core.windows.net/container/nested/source.txt", plan.Target);
    }

    [Fact]
    public async Task CopyProbeMustCompleteBeforeSourceAuthorizationOrCopyStart()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            Assert.Equal("fixture.blob.core.windows.net", request.RequestUri!.Host);
            return Task.FromResult(BlobWorkflowTests.Head());
        });
        var request = new StartStorageBlobCopyPowerShellProcessor_Orchestrator
        {
            Request = new StartStorageBlobCopyProcessor_Orchestrator
            {
                Context = BlobWorkflowTests.Context(), Container = "container", Blob = "target",
                SourceContainer = "source", SourceBlob = "item"
            }
        };
        var command = new StartStorageBlobCopyPowerShellProcessor_Orchestrator { Plan = request.Prepare(false) };
        command.Init(fixture.Context);
        await Assert.ThrowsAsync<StorageBlobOverwriteRequiredException>(() => command.ProcessAsync());
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("failed")]
    public async Task ForcedCopyReturnsNativeAcknowledgementWithoutPollingOrLeakingAuthorization(string status)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Contains("sig=private", Assert.Single(request.Headers.GetValues("x-ms-copy-source")));
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent([]) };
            response.Headers.ETag = new EntityTagHeaderValue("\"copy\"");
            response.Headers.Add("x-ms-copy-id", BlobCopyTests.CopyId);
            response.Headers.Add("x-ms-copy-status", status);
            response.Content.Headers.LastModified = DateTimeOffset.UtcNow;
            return Task.FromResult(response);
        });
        var request = new StartStorageBlobCopyPowerShellProcessor_Orchestrator
        {
            Request = new StartStorageBlobCopyProcessor_Orchestrator
            {
                Context = BlobWorkflowTests.Context(), Container = "container", Blob = "target",
                SourceUri = "https://sourceaccount.blob.core.windows.net/source/item?sv=2026-06-06&sp=r&sig=private"
            }
        };
        var command = new StartStorageBlobCopyPowerShellProcessor_Orchestrator { Plan = request.Prepare(true) };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
        Assert.Single(fixture.TokenRequests);
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal(status, blob.Properties.CopyStatus);
        Assert.DoesNotContain("sig=", blob.Properties.CopySource);
    }

    private SetStorageBlobContentProcessor_Orchestrator Upload() => new()
    {
        Context = BlobWorkflowTests.Context(), Container = "container", File = Source
    };

    private static HttpResponseMessage Created()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
        response.Headers.TryAddWithoutValidation("x-ms-blob-append-offset", "0");
        response.Headers.TryAddWithoutValidation("x-ms-blob-committed-block-count", "1");
        return response;
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
