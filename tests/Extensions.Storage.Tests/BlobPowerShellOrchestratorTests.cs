// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Xml.Linq;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobPowerShellOrchestratorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactLookupReturnsNativePropertiesAndOptionalGuardedTags(bool tags)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("9", BlobWorkflowTests.Query(request)["timeout"]);
            Assert.Equal("version", BlobWorkflowTests.Query(request)["versionid"]);
            if (request.Method == HttpMethod.Head)
            {
                Assert.Equal("\"team\"='one'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
                return Task.FromResult(BlobWorkflowTests.Head(length: 12));
            }
            Assert.True(tags);
            Assert.Equal("tags", BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal("\"etag\"", Assert.Single(request.Headers.GetValues("x-ms-blob-if-match")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<Tags><TagSet><Tag><Key>team</Key><Value>one</Value></Tag></TagSet></Tags>")
            });
        });
        var command = Command();
        command.Blob = "item"; command.VersionId = "version"; command.IncludeTag = tags;
        command.IfTags = "\"team\"='one'"; command.Timeout = 9;
        await Run(command, fixture);
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("item", blob.Name);
        Assert.Equal("version", blob.VersionId);
        Assert.Equal(12, blob.Properties.ContentLength);
        Assert.Equal(tags ? 2 : 1, fixture.Sends);
        if (tags) Assert.Equal("one", blob.Tags["team"]);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.Warning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeBlobInputRetainsSnapshotOrVersionAndItsOwnContext(bool snapshot)
    {
        var context = BlobWorkflowTests.Context();
        var input = new StorageBlob("item", new("container", context), new())
        {
            Snapshot = snapshot ? "snapshot" : null, VersionId = snapshot ? null : "version"
        };
        var fixture = new Fixture(request =>
        {
            Assert.Equal(snapshot ? "snapshot" : null, BlobWorkflowTests.Query(request)["snapshot"]);
            Assert.Equal(snapshot ? null : "version", BlobWorkflowTests.Query(request)["versionid"]);
            return Task.FromResult(BlobWorkflowTests.Head());
        });
        var command = new GetStorageBlobsProcessor_Orchestrator { InputObject = input };
        await Run(command, fixture);
        var output = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Same(context, output.Context);
        Assert.Equal(input.Snapshot, output.Snapshot);
        if (!snapshot) Assert.Equal(input.VersionId, output.VersionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListingIncludesNativeDetailsAndCarriesContinuationWithoutWarning(bool optionalIncludes)
    {
        var entry = BlobWorkflowTests.BlobElement("item");
        entry.Add(new XElement("Snapshot", "snapshot"), new XElement("Deleted", "true"),
            new XElement("VersionId", "version"), new XElement("Metadata", new XElement("team", "one")),
            new XElement("Tags", new XElement("TagSet", new XElement("Tag", new XElement("Key", "team"), new XElement("Value", "two")))));
        var fixture = new Fixture(request =>
        {
            Assert.Equal(optionalIncludes ? "metadata,copy,snapshots,deleted,versions,tags" : "metadata,copy,snapshots",
                BlobWorkflowTests.Query(request)["include"]);
            Assert.Equal("dir/", BlobWorkflowTests.Query(request)["prefix"]);
            Assert.Null(BlobWorkflowTests.Query(request)["delimiter"]);
            Assert.Equal("1", BlobWorkflowTests.Query(request)["maxresults"]);
            return Task.FromResult(BlobWorkflowTests.Page("remaining", entry));
        });
        var command = Listing();
        command.Prefix = "dir/"; command.MaxCount = 1;
        command.IncludeDeleted = command.IncludeVersion = command.IncludeTag = optionalIncludes;
        await Run(command, fixture);
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("snapshot", blob.Snapshot);
        Assert.Equal("version", blob.VersionId);
        Assert.True(blob.Deleted);
        Assert.Equal("one", blob.Metadata["team"]);
        Assert.Equal("two", blob.Tags["team"]);
        Assert.Equal(new StorageBlobContinuationToken("remaining", "fixture", "container", "dir/", null), blob.ContinuationToken);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.Warning);
    }

    [Fact]
    public async Task MaxCountCountsScannedEntriesBeforeFilteringAcrossPages()
    {
        int page = 0;
        var fixture = new Fixture(request =>
        {
            Assert.Equal(++page == 1 ? "3" : "1", BlobWorkflowTests.Query(request)["maxresults"]);
            return Task.FromResult(page == 1
                ? BlobWorkflowTests.Page("next", BlobWorkflowTests.BlobElement("skip"), BlobWorkflowTests.BlobElement("pick-one"))
                : BlobWorkflowTests.Page("remaining", BlobWorkflowTests.BlobElement("pick-two")));
        });
        var command = Listing();
        command.MaxCount = 3;
        command.NameFilter = name => name.StartsWith("pick", StringComparison.Ordinal);
        await Run(command, fixture);
        Assert.Equal(["pick-one", "pick-two"], fixture.Output.Cast<StorageBlob>().Select(blob => blob.Name));
        Assert.Equal(["next", "remaining"], fixture.Output.Cast<StorageBlob>().Select(blob => blob.ContinuationToken!.NextMarker));
        Assert.Equal(2, fixture.Sends);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.Warning);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task NonpositiveLimitsDrainAllPagesIncludingEmptyPages(int limit)
    {
        int page = 0;
        var fixture = new Fixture(request =>
        {
            Assert.Equal("5000", BlobWorkflowTests.Query(request)["maxresults"]);
            return Task.FromResult(++page == 1 ? BlobWorkflowTests.Page("next") :
                BlobWorkflowTests.Page(null, BlobWorkflowTests.BlobElement("item")));
        });
        var command = Listing(); command.MaxCount = limit;
        await Run(command, fixture);
        Assert.Null(Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).ContinuationToken);
        Assert.Equal(2, fixture.Sends);
    }

    [Fact]
    public async Task ContinuationResumesMatchingContainerAndPrefix()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("opaque+/=", BlobWorkflowTests.Query(request)["marker"]);
            return Task.FromResult(BlobWorkflowTests.Page(null));
        });
        var command = Listing();
        command.Prefix = "dir/";
        command.ContinuationToken = new("opaque+/=", "fixture", "container", "dir/", null);
        await Run(command, fixture);
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("container")]
    [InlineData("prefix")]
    [InlineData("delimiter")]
    public async Task MismatchedContinuationFailsBeforeAuthentication(string mismatch)
    {
        var fixture = new Fixture();
        var command = Listing();
        command.ContinuationToken = new("next", mismatch == "account" ? "other" : "fixture",
            mismatch == "container" ? "other" : "container", mismatch == "prefix" ? "other" : null,
            mismatch == "delimiter" ? "/" : null);
        await Assert.ThrowsAsync<ArgumentException>(() => Run(command, fixture));
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("selected")]
    [InlineData("condition")]
    [InlineData("input")]
    [InlineData("exact-prefix")]
    [InlineData("exact-deleted")]
    [InlineData("exact-version-include")]
    [InlineData("exact-count")]
    public async Task InvalidModesFailExplicitlyBeforeAuthentication(string invalid)
    {
        var command = Listing();
        if (invalid == "selected") command.Snapshot = "snapshot";
        if (invalid == "condition") command.IfTags = "condition";
        if (invalid == "input") command.InputObject = new StorageBlob("item", new("container", command.Context!), new());
        if (invalid.StartsWith("exact", StringComparison.Ordinal))
        {
            command.ListMode = false; command.Blob = "item";
            if (invalid == "exact-prefix") command.Prefix = "prefix";
            if (invalid == "exact-deleted") command.IncludeDeleted = true;
            if (invalid == "exact-version-include") command.IncludeVersion = true;
            if (invalid == "exact-count") command.MaxCount = 1;
        }
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => Run(command, fixture));
        Assert.Empty(fixture.Output);
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task ContextConflictsFailBeforeAuthentication()
    {
        var fixture = new Fixture();
        var command = Command();
        command.InputObject = new StorageBlob("item", new("container", new StorageContext("other", Fixture.DefaultProfile)), new());
        await Assert.ThrowsAsync<ArgumentException>(() => Run(command, fixture));
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactMissingOrDeniedRequestsRemainErrorsWithOneDiagnostic(bool denied)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(
            denied ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound, denied ? "AuthorizationFailure" : "BlobNotFound")));
        var command = Command(); command.Blob = "missing";
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Run(command, fixture));
        Assert.Equal(denied ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound, error.StatusCode);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task LaterPageFailurePreservesOnlyAlreadyStreamedNativeBlobsAndOneError()
    {
        int page = 0;
        var fixture = new Fixture(_ => Task.FromResult(++page == 1
            ? BlobWorkflowTests.Page("next", BlobWorkflowTests.BlobElement("item"))
            : BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthorizationFailure")));
        await Assert.ThrowsAsync<HttpRequestException>(() => Run(Listing(), fixture));
        Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task CancellationNeverReturnsAnExactBlobOrStartsAnotherPage()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(BlobWorkflowTests.Page("next", BlobWorkflowTests.BlobElement("item")));
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(Listing(), fixture));
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
    }

    private static GetStorageBlobsProcessor_Orchestrator Command() => new()
    {
        Context = BlobWorkflowTests.Context(), Container = "container"
    };

    private static GetStorageBlobsProcessor_Orchestrator Listing()
    {
        var command = Command(); command.ListMode = true; return command;
    }

    private static async Task Run(GetStorageBlobsProcessor_Orchestrator command, Fixture fixture)
    {
        command.Init(fixture.Context);
        await command.ProcessAsync();
    }
}
