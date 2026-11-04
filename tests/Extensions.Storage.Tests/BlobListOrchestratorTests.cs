// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobListOrchestratorTests
{
    [Fact]
    public async Task NativeListingUsesTypedOptionsWithoutParsingCliSwitches()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("1", BlobWorkflowTests.Query(request)["maxresults"]);
            Assert.Equal("metadata", BlobWorkflowTests.Query(request)["include"]);
            return Task.FromResult(BlobWorkflowTests.Page(null, BlobWorkflowTests.BlobElement("one")));
        });
        var orchestrator = new ListStorageBlobsProcessor_Orchestrator
        {
            Context = BlobWorkflowTests.Context(), Container = "container",
            AuthMode = "invalid", NumResults = "invalid", Include = "invalid",
            ResultLimit = 1, Datasets = ["metadata"]
        };
        orchestrator.Init(fixture.Context);
        await orchestrator.ProcessNativeAsync();
        Assert.Equal("one", Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Name);

        fixture.Output.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyListingIsOneArrayAndOptionalMarkerIsInsideIt(bool showMarker)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("5000", BlobWorkflowTests.Query(request)["maxresults"]);
            Assert.Null(BlobWorkflowTests.Query(request)["include"]);
            return Task.FromResult(BlobWorkflowTests.Page(null));
        });
        await Run(Command(showMarker: showMarker), fixture);
        var output = Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output));
        if (showMarker)
        {
            Assert.Equal(JsonValueKind.Null, Assert.Single(output).GetProperty("nextMarker").ValueKind);
            Assert.Single(output[0].EnumerateObject());
        }
        else Assert.Empty(output);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.Warning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultLimitStopsAtExactly5000AndReturnsOrWarnsTheMarker(bool showMarker)
    {
        var entries = Enumerable.Range(0, 5000).Select(index => BlobWorkflowTests.BlobElement($"item{index}")).ToArray();
        var fixture = new Fixture(request =>
        {
            Assert.Equal("5000", BlobWorkflowTests.Query(request)["maxresults"]);
            return Task.FromResult(BlobWorkflowTests.Page("opaque+/=&", entries));
        });
        await Run(Command(showMarker: showMarker), fixture);
        var output = Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output));
        Assert.Equal(showMarker ? 5001 : 5000, output.Length);
        Assert.Equal("item0", output[0].GetProperty("name").GetString());
        Assert.Equal("item4999", output[4999].GetProperty("name").GetString());
        Assert.Equal(1, fixture.Sends);
        if (showMarker)
        {
            Assert.Equal("opaque+/=&", output[^1].GetProperty("nextMarker").GetString());
            Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.Warning);
        }
        else
            Assert.Equal("Next Marker: opaque+/=&", Assert.Single(fixture.Messages, message => message.Id == Events.Warning).Message);
    }

    [Fact]
    public async Task LargerLimitUsesBoundedServicePagesAndRemainingCount()
    {
        int page = 0;
        var entries = Enumerable.Range(0, 5000).Select(index => BlobWorkflowTests.BlobElement($"item{index}")).ToArray();
        var fixture = new Fixture(request =>
        {
            var query = BlobWorkflowTests.Query(request);
            if (++page == 1)
            {
                Assert.Equal("5000", query["maxresults"]);
                return Task.FromResult(BlobWorkflowTests.Page("next", entries));
            }
            Assert.Equal("1", query["maxresults"]);
            Assert.Equal("next", query["marker"]);
            return Task.FromResult(BlobWorkflowTests.Page("remaining", BlobWorkflowTests.BlobElement("last")));
        });
        await Run(Command(limit: "5001", showMarker: true), fixture);
        var output = Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output));
        Assert.Equal(5002, output.Length);
        Assert.Equal("last", output[^2].GetProperty("name").GetString());
        Assert.Equal("remaining", output[^1].GetProperty("nextMarker").GetString());
        Assert.Equal(2, fixture.Sends);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("9223372036854775807")]
    public async Task AllAndInt64LimitsDrainEmptyPagesAndPreserveOrder(string limit)
    {
        int page = 0;
        var fixture = new Fixture(request =>
        {
            Assert.Equal("5000", BlobWorkflowTests.Query(request)["maxresults"]);
            return Task.FromResult(++page switch
            {
                1 => BlobWorkflowTests.Page("one", BlobWorkflowTests.BlobElement("first")),
                2 => BlobWorkflowTests.Page("two"),
                _ => BlobWorkflowTests.Page(null, BlobWorkflowTests.BlobElement("last"))
            });
        });
        await Run(Command(limit: limit), fixture);
        var output = Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output));
        Assert.Equal(["first", "last"], output.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal(3, fixture.Sends);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.Warning);
    }

    [Fact]
    public async Task HierarchyAndEncodedNamesUseCliEntriesWithoutNativeEnvelopes()
    {
        var blob = BlobWorkflowTests.BlobElement("literal%252F%20space", encoded: true);
        blob.Add(new XElement("VersionId", "version"), new XElement("IsCurrentVersion", "false"),
            new XElement("Deleted", "true"), new XElement("HasVersionsOnly", "true"));
        var fixture = new Fixture(request =>
        {
            var query = BlobWorkflowTests.Query(request);
            Assert.Equal("/", query["delimiter"]);
            Assert.Equal("dir/", query["prefix"]);
            Assert.Equal("opaque+/=&", query["marker"]);
            Assert.Equal("9", query["timeout"]);
            return Task.FromResult(BlobWorkflowTests.Page(null, blob,
                new XElement("BlobPrefix", new XElement("Name", "dir/"))));
        });
        var command = Command();
        command.Delimiter = "/"; command.Prefix = "dir/"; command.Marker = "opaque+/=&"; command.Timeout = 9;
        await Run(command, fixture);
        var output = Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output));
        Assert.Equal("literal%2F space", output[0].GetProperty("name").GetString());
        Assert.Equal("version", output[0].GetProperty("versionId").GetString());
        Assert.False(output[0].GetProperty("isCurrentVersion").GetBoolean());
        Assert.True(output[0].GetProperty("deleted").GetBoolean());
        Assert.True(output[0].GetProperty("hasVersionsOnly").GetBoolean());
        Assert.Equal(5L * 1024 * 1024 * 1024, output[0].GetProperty("properties").GetProperty("contentLength").GetInt64());
        Assert.False(output[0].TryGetProperty("context", out _));
        Assert.False(output[0].TryGetProperty("continuationToken", out _));
        Assert.Equal("dir/", output[1].GetProperty("name").GetString());
        Assert.Single(output[1].EnumerateObject());
    }

    [Fact]
    public async Task IncludesAreDeduplicatedAndMapLegacyFlagsToPinnedWireValues()
    {
        var blob = BlobWorkflowTests.BlobElement("item");
        blob.Add(new XElement("Metadata", new XElement("team", "one")),
            new XElement("Tags", new XElement("TagSet", new XElement("Tag", new XElement("Key", "team"), new XElement("Value", "two")))));
        blob.Element("Properties")!.Add(new XElement("CopyId", "copy"), new XElement("CopyStatus", "pending"),
            new XElement("CopySource", "https://source.blob.core.windows.net/container/item?sig=private-source&sv=2026-06-06"),
            new XElement("CopyStatusDescription", "private-description"));
        var fixture = new Fixture(request =>
        {
            Assert.Equal("snapshots,metadata,copy,deleted,versions,tags", BlobWorkflowTests.Query(request)["include"]);
            return Task.FromResult(BlobWorkflowTests.Page(null, blob));
        });
        var command = Command();
        command.Include = "ttvvddccmmss";
        await Run(command, fixture);
        var output = Assert.Single(Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output)));
        Assert.Equal("one", output.GetProperty("metadata").GetProperty("team").GetString());
        Assert.Equal("two", output.GetProperty("tags").GetProperty("team").GetString());
        Assert.DoesNotContain("private-", output.GetRawText());
        Assert.DoesNotContain("sig=", output.GetRawText());
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task EmptyDelimiterUsesFlatListingAndEndpointOnlySelectsTheStorageProfile()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("fixture.blob.core.windows.net", request.RequestUri!.Host);
            Assert.Null(BlobWorkflowTests.Query(request)["delimiter"]);
            Assert.Equal("2026-06-06", Assert.Single(request.Headers.GetValues("x-ms-version")));
            return Task.FromResult(BlobWorkflowTests.Page(null));
        });
        var command = Command();
        command.StorageAccountName = null;
        command.BlobEndpoint = "https://fixture.blob.core.windows.net/";
        command.Delimiter = "";
        await Run(command, fixture);
        var token = Assert.Single(fixture.TokenRequests);
        Assert.Equal(["https://storage.azure.com/.default"], token.Scopes);
        Assert.Same(fixture.Profile, token.Profile);
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("path")]
    [InlineData("sas")]
    [InlineData("conflict")]
    [InlineData("context")]
    [InlineData("no-account")]
    public async Task InvalidAddressingCannotAuthenticate(string invalid)
    {
        var command = Command();
        switch (invalid)
        {
            case "auth": command.AuthMode = "key"; break;
            case "path": command.BlobEndpoint = "https://fixture.blob.core.windows.net/container"; break;
            case "sas": command.BlobEndpoint = "https://fixture.blob.core.windows.net/?sig=private-sas"; break;
            case "conflict": command.BlobEndpoint = "https://other.blob.core.windows.net/"; break;
            case "context":
                command.StorageAccountName = null;
                command.BlobEndpoint = "https://fixture.blob.core.windows.net/";
                command.Context = new StorageContext("other", Fixture.DefaultProfile);
                break;
            case "no-account": command.StorageAccountName = null; break;
        }
        var fixture = new Fixture();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => Run(command, fixture));
        Assert.True(exception is ArgumentException or NotSupportedException);
        Assert.DoesNotContain("private-", exception.Message);
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task ListingControlsAreFrozenBeforeTheFirstRequest()
    {
        var command = Command(limit: "2");
        command.Include = "m"; command.Prefix = "original/";
        int page = 0;
        var fixture = new Fixture(request =>
        {
            var query = BlobWorkflowTests.Query(request);
            Assert.Equal("original/", query["prefix"]);
            Assert.Equal("metadata", query["include"]);
            Assert.Null(query["delimiter"]);
            if (++page == 1)
            {
                command.NumResults = "*"; command.Include = "t"; command.Prefix = "changed/"; command.Delimiter = "/";
                return Task.FromResult(BlobWorkflowTests.Page("next", BlobWorkflowTests.BlobElement("first")));
            }
            Assert.Equal("1", query["maxresults"]);
            return Task.FromResult(BlobWorkflowTests.Page(null, BlobWorkflowTests.BlobElement("last")));
        });
        await Run(command, fixture);
        Assert.Equal(2, Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output)).Length);
        Assert.Equal(2, fixture.Sends);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    public async Task InvalidLimitsFailBeforeAuthentication(string limit)
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => Run(Command(limit), fixture));
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("u")]
    [InlineData("metadata")]
    [InlineData("T")]
    public async Task UnsupportedIncludeFlagsAreNotSilentlyDropped(string include)
    {
        var fixture = new Fixture();
        var command = Command();
        command.Include = include;
        await Assert.ThrowsAsync<ArgumentException>(() => Run(command, fixture));
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData("cycle")]
    [InlineData("xml")]
    [InlineData("date")]
    [InlineData("http")]
    public async Task LaterFailuresReturnNoPartialSuccessArrayAndOneDiagnostic(string failure)
    {
        int page = 0;
        var fixture = new Fixture(_ =>
        {
            if (++page == 1) return Task.FromResult(BlobWorkflowTests.Page("next", BlobWorkflowTests.BlobElement("first")));
            if (failure == "http") return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthorizationFailure"));
            if (failure == "xml") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<private-invalid") });
            var blob = BlobWorkflowTests.BlobElement("last");
            if (failure == "date") blob.Element("Properties")!.Element("Last-Modified")!.Value = "private-date";
            return Task.FromResult(BlobWorkflowTests.Page(failure == "cycle" ? "next" : null, blob));
        });
        var error = await Assert.ThrowsAnyAsync<Exception>(() => Run(Command(limit: "*", showMarker: true), fixture));
        Assert.True(error is HttpRequestException or InvalidDataException);
        Assert.DoesNotContain("private-", error.Message);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task OversizedPageDoesNotReturnAClippedSuccessArray()
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Page(null,
            BlobWorkflowTests.BlobElement("first"), BlobWorkflowTests.BlobElement("last"))));
        await Assert.ThrowsAsync<InvalidDataException>(() => Run(Command(limit: "1"), fixture));
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrBetweenPagesReturnsNoSuccessArray(bool betweenPages)
    {
        using var cancellation = new CancellationTokenSource();
        int page = 0;
        var fixture = new Fixture(_ =>
        {
            if (++page == 1) return Task.FromResult(BlobWorkflowTests.Page("next", BlobWorkflowTests.BlobElement("first")));
            cancellation.Cancel();
            return Task.FromResult(BlobWorkflowTests.Page(null, BlobWorkflowTests.BlobElement("last")));
        }, token: cancellation.Token);
        if (!betweenPages) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(Command(limit: "*"), fixture));
        Assert.Empty(fixture.Output);
        Assert.Equal(betweenPages ? 2 : 0, fixture.Sends);
    }

    private static ListStorageBlobsProcessor_Orchestrator Command(string limit = "5000", bool showMarker = false) =>
        new() { StorageAccountName = "fixture", Container = "container", NumResults = limit, ShowNextMarker = showMarker };

    private static Task Run(ListStorageBlobsProcessor_Orchestrator command, Fixture fixture)
    {
        command.Init(fixture.Context);
        return command.ProcessAsync();
    }
}
