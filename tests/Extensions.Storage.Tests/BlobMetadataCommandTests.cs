// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobMetadataCommandTests
{
    [Fact]
    public async Task ShowProducesCliShapeWithoutContextAndPreservesInt64AndCopyRedaction()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            var response = BlobWorkflowTests.Head(length: 5L * 1024 * 1024 * 1024);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            response.Content.Headers.ContentMD5 = Enumerable.Range(0, 16).Select(number => (byte)number).ToArray();
            foreach (var header in new Dictionary<string, string>
            {
                ["x-ms-meta-team"] = "one", ["x-ms-access-tier"] = "Cool", ["x-ms-access-tier-inferred"] = "false",
                ["x-ms-server-encrypted"] = "true", ["x-ms-creation-time"] = "Tue, 20 Oct 2026 00:00:00 GMT",
                ["x-ms-access-tier-change-time"] = "Tue, 20 Oct 2026 01:00:00 GMT",
                ["x-ms-blob-sequence-number"] = "2147483648", ["x-ms-blob-committed-block-count"] = "2",
                ["x-ms-copy-id"] = "copy-id", ["x-ms-copy-status"] = "pending",
                ["x-ms-copy-source"] = "https://source.blob.core.windows.net/container/item?sig=private-sas&sv=2026-06-06",
                ["x-ms-copy-status-description"] = "private-copy-description", ["x-ms-lease-status"] = "unlocked"
            }) response.Headers.Add(header.Key, header.Value);
            return Task.FromResult(response);
        });
        await Run(Configure(new ShowStorageBlobCommand()), fixture);
        var json = Assert.IsType<JsonElement>(Assert.Single(fixture.Output));
        Assert.Equal(["content", "deleted", "metadata", "name", "properties", "snapshot", "tags", "versionId"],
            json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("item", json.GetProperty("name").GetString());
        Assert.Equal("", json.GetProperty("content").GetString());
        Assert.Equal("one", json.GetProperty("metadata").GetProperty("team").GetString());
        Assert.False(json.TryGetProperty("context", out _));
        Assert.False(json.TryGetProperty("container", out _));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("tags").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("versionId").ValueKind);
        var properties = json.GetProperty("properties");
        Assert.Equal(5L * 1024 * 1024 * 1024, properties.GetProperty("contentLength").GetInt64());
        Assert.Equal(2147483648L, properties.GetProperty("pageBlobSequenceNumber").GetInt64());
        Assert.Equal(2, properties.GetProperty("appendBlobCommittedBlockCount").GetInt32());
        Assert.Equal("\"etag\"", properties.GetProperty("etag").GetString());
        Assert.Equal("Cool", properties.GetProperty("blobTier").GetString());
        Assert.False(properties.GetProperty("blobTierInferred").GetBoolean());
        Assert.True(properties.GetProperty("serverEncrypted").GetBoolean());
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T00:00:00Z"), properties.GetProperty("creationTime").GetDateTimeOffset());
        Assert.Equal("application/octet-stream", properties.GetProperty("contentSettings").GetProperty("contentType").GetString());
        Assert.Equal("AAECAwQFBgcICQoLDA0ODw==", properties.GetProperty("contentSettings").GetProperty("contentMd5").GetString());
        Assert.Equal("unlocked", properties.GetProperty("lease").GetProperty("status").GetString());
        Assert.DoesNotContain("private-", json.GetRawText());
        Assert.DoesNotContain("sig=", json.GetRawText());
        Assert.Single(fixture.TokenRequests);
    }

    [Fact]
    public async Task TagsAreFetchedOnlyWhenRequestedWithTheObservedEtag()
    {
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Head());
            Assert.Equal("tags", BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal("\"etag\"", Assert.Single(request.Headers.GetValues("x-ms-blob-if-match")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<Tags><TagSet><Tag><Key>team</Key><Value>one</Value></Tag></TagSet></Tags>")
            });
        });
        await Run(Configure(new ShowStorageBlobCommand { IncludeTags = true }), fixture);
        var json = Assert.IsType<JsonElement>(Assert.Single(fixture.Output));
        Assert.Equal("one", json.GetProperty("tags").GetProperty("team").GetString());
        Assert.Equal(2, fixture.Sends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndpointOnlyResolutionAndSelectorsConditionsUseTheStorageAudience(bool version)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("fixture.blob.core.windows.net", request.RequestUri!.Host);
            Assert.Equal("/container/a%20b%25", request.RequestUri.AbsolutePath);
            Assert.Equal(version ? null : "snapshot", BlobWorkflowTests.Query(request)["snapshot"]);
            Assert.Equal(version ? "version" : null, BlobWorkflowTests.Query(request)["versionid"]);
            Assert.Equal("17", BlobWorkflowTests.Query(request)["timeout"]);
            Assert.Equal("\"wanted\"", Assert.Single(request.Headers.GetValues("If-Match")));
            Assert.Equal("lease", Assert.Single(request.Headers.GetValues("x-ms-lease-id")));
            Assert.Equal("\"tag\"='one'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
            Assert.Equal("Tue, 20 Oct 2026 00:00:00 GMT", Assert.Single(request.Headers.GetValues("If-Modified-Since")));
            return Task.FromResult(BlobWorkflowTests.Head());
        });
        var command = Configure(new ShowStorageBlobCommand());
        command.StorageAccountName = null;
        command.BlobEndpoint = "https://fixture.blob.core.windows.net/";
        command.Blob = "a b%";
        command.Snapshot = version ? null : "snapshot";
        command.VersionId = version ? "version" : null;
        command.Timeout = 17;
        command.IfMatch = "\"wanted\""; command.LeaseId = "lease"; command.IfTags = "\"tag\"='one'";
        command.IfModifiedSinceInput = "2026-10-20T02:00:00+02:00";
        await Run(command, fixture);
        var token = Assert.Single(fixture.TokenRequests);
        Assert.Equal(["https://storage.azure.com/.default"], token.Scopes);
    }

    [Theory]
    [InlineData("endpoint")]
    [InlineData("endpoint-path")]
    [InlineData("endpoint-query")]
    [InlineData("endpoint-sas")]
    [InlineData("endpoint-conflict")]
    [InlineData("auth")]
    [InlineData("no-account")]
    [InlineData("selectors")]
    [InlineData("date")]
    [InlineData("timeout")]
    public async Task InvalidAddressingAndModesCannotAuthenticate(string invalid)
    {
        var command = Configure(new ShowStorageBlobCommand());
        switch (invalid)
        {
            case "endpoint": command.BlobEndpoint = "http://localhost/"; break;
            case "endpoint-path": command.BlobEndpoint = "https://fixture.blob.core.windows.net/container"; break;
            case "endpoint-query": command.BlobEndpoint = "https://fixture.blob.core.windows.net/?other=value"; break;
            case "endpoint-sas": command.BlobEndpoint = "https://fixture.blob.core.windows.net/?sig=private-sas"; break;
            case "endpoint-conflict": command.BlobEndpoint = "https://other.blob.core.windows.net/"; break;
            case "auth": command.AuthMode = "key"; break;
            case "no-account": command.StorageAccountName = null; break;
            case "selectors": command.Snapshot = "snapshot"; command.VersionId = "version"; break;
            case "date": command.IfModifiedSinceInput = "private-invalid-date"; break;
            case "timeout": command.Timeout = 0; break;
        }
        var fixture = new Fixture();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => Run(command, fixture));
        Assert.True(exception is ArgumentException or NotSupportedException);
        Assert.DoesNotContain("private-", exception.Message);
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("BlobNotFound", false)]
    [InlineData("ContainerNotFound", true)]
    [InlineData("AuthorizationFailure", true)]
    public async Task ExistsOnlyHandlesPreciseBlobAbsence(string code, bool error)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(
            code == "AuthorizationFailure" ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound, code)));
        var command = Configure(new TestStorageBlobExistsCommand());
        if (error)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => Run(command, fixture));
            Assert.Empty(fixture.Output);
            Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        }
        else
        {
            await Run(command, fixture);
            Assert.False(Assert.IsType<StorageBlobExistence>(Assert.Single(fixture.Output)).Exists);
            Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        }
    }

    [Fact]
    public async Task DeleteForwardsSnapshotPolicyAndEmitsNoSuccessObject()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("include", Assert.Single(request.Headers.GetValues("x-ms-delete-snapshots")));
            Assert.Equal("\"wanted\"", Assert.Single(request.Headers.GetValues("If-Match")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        var command = Configure(new RemoveStorageBlobCommand { DeleteSnapshots = "include", IfMatch = "\"wanted\"" });
        await Run(command, fixture);
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
    }

    [Theory]
    [InlineData("block", "Cool", "BlockBlob")]
    [InlineData("page", "P4", "PageBlob")]
    public async Task TierValidatesTypeAndEmitsNoSuccessObject(string type, string tier, string actualType)
    {
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Head(actualType));
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(tier, Assert.Single(request.Headers.GetValues("x-ms-access-tier")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        await Run(Configure(new SetStorageBlobTierCommand { BlobType = type, Tier = tier }), fixture);
        Assert.Empty(fixture.Output);
        Assert.Equal(2, fixture.Sends);
    }

    [Fact]
    public async Task TierDoesNotMutateAResourceOfTheWrongSelectedType()
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Head("PageBlob")));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(Configure(new SetStorageBlobTierCommand { Tier = "Hot" }), fixture));
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("block", "P4", null)]
    [InlineData("page", "Hot", null)]
    [InlineData("append", "Hot", null)]
    [InlineData("page", "P4", "High")]
    [InlineData("block", "Hot", "invalid")]
    public async Task InvalidTierCombinationsCannotAuthenticate(string type, string tier, string? priority)
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => Run(
            Configure(new SetStorageBlobTierCommand { BlobType = type, Tier = tier, RehydratePriority = priority }), fixture));
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData("x-ms-server-encrypted", "private-invalid")]
    [InlineData("x-ms-blob-sequence-number", "-1")]
    [InlineData("x-ms-blob-committed-block-count", "2147483648")]
    [InlineData("x-ms-creation-time", "private-invalid")]
    public async Task MalformedShowPropertiesFailOnceWithoutPartialOrSensitiveOutput(string header, string value)
    {
        var fixture = new Fixture(_ =>
        {
            var response = BlobWorkflowTests.Head();
            response.Headers.TryAddWithoutValidation(header, value);
            return Task.FromResult(response);
        });
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => Run(Configure(new ShowStorageBlobCommand()), fixture));
        Assert.DoesNotContain("private-", exception.Message);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task CancellationPreventsAuthentication()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var fixture = new Fixture(token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(Configure(new ShowStorageBlobCommand()), fixture));
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    private static T Configure<T>(T command) where T : BlobCliCommandBase
    {
        command.StorageAccountName = "fixture"; command.Container = "container"; command.Blob = "item";
        return command;
    }

    private static Task Run(BlobCliCommandBase command, Fixture fixture)
    {
        command.Init(fixture.Context);
        return command.ProcessAsync();
    }
}
