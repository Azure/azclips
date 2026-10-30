// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public class BlobWorkflowTests
{
    [Fact]
    public async Task ListingDecodesWireNamesPropertiesMetadataTagsAndReplication()
    {
        var blob = BlobElement("literal%252F+space%20name", encoded: true);
        blob.Add(new XElement("Metadata", new XAttribute("Encrypted", "true"), new XElement("team", "one"), new XElement("empty", "")));
        blob.Add(new XElement("Tags", new XElement("TagSet", new XElement("Tag", new XElement("Key", "tag"), new XElement("Value", "")))));
        blob.Add(new XElement("OrMetadata", new XElement("policy_rule", "complete")));
        blob.Element("Properties")!.Add(new XElement("Content-MD5", "AQIDBA=="), new XElement("AccessTier", "Cool"),
            new XElement("ServerEncrypted", "true"), new XElement("TagCount", "1"));
        blob.Add(new XElement("VersionId", "version"), new XElement("IsCurrentVersion", "false"), new XElement("Deleted", "true"));
        var fixture = new Fixture(_ => Task.FromResult(Page(null, blob)));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container" };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        var item = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("literal%2F+space name", item.Name);
        Assert.Contains("literal%252F%2Bspace%20name", item.Uri.AbsoluteUri);
        Assert.Equal("one", item.Metadata["team"]);
        Assert.Equal("", item.Metadata["empty"]);
        Assert.Equal("", item.Tags["tag"]);
        Assert.Equal("complete", item.ObjectReplicationMetadata["policy_rule"]);
        Assert.Equal("Cool", item.Properties.AccessTier);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, item.Properties.ContentMd5);
        Assert.Equal(5L * 1024 * 1024 * 1024, item.Properties.ContentLength);
        Assert.True(item.Deleted);
        Assert.False(item.IsCurrentVersion);
        Assert.Equal("version", System.Web.HttpUtility.ParseQueryString(item.Uri.Query)["versionid"]);
    }

    [Fact]
    public async Task PagingFollowsEmptyPagesAndUsesRemainingResultLimit()
    {
        int page = 0;
        var fixture = new Fixture(request =>
        {
            var query = Query(request);
            return Task.FromResult(++page switch
            {
                1 when query["maxresults"] == "3" && query["marker"] is null => Page("one", BlobElement("a")),
                2 when query["maxresults"] == "2" && query["marker"] == "one" => Page("two"),
                3 when query["maxresults"] == "2" && query["marker"] == "two" => Page("remaining", BlobElement("b"), BlobElement("c")),
                _ => throw new InvalidOperationException("Unexpected page or limit.")
            });
        });
        var processor = new ListStorageBlobsProcessor
        {
            Context = Context(), Container = "container", NumResults = 3, ShowNextMarker = true
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(3, fixture.Sends);
        Assert.Equal(new[] { "a", "b", "c" }, fixture.Output.OfType<StorageBlob>().Select(item => item.Name));
        Assert.Equal("remaining", Assert.IsType<StorageBlobNextMarker>(fixture.Output.Last()).NextMarker);
        Assert.Equal("remaining", fixture.Output.OfType<StorageBlob>().Last().ContinuationToken!.NextMarker);
    }

    [Fact]
    public async Task StorageContextProfileWinsOverADifferentProfileName()
    {
        var profile = new Core.Abstractions.Profile { Name = "source", TenantId = "source-tenant" };
        var fixture = new Fixture(_ => Task.FromResult(Page(null, BlobElement("one"))));
        var processor = new ListStorageBlobsProcessor
        {
            Context = new StorageContext("fixture", profile), Container = "container", Profile = "other"
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(profile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
    }

    [Fact]
    public async Task PowerShellCountLimitsExaminedResultsBeforeWildcardFiltering()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page("more", BlobElement("skip"), BlobElement("match"))));
        var processor = new ListStorageBlobsProcessor
        {
            Context = Context(), Container = "container", MaxCount = 2, NameFilter = name => name == "match"
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        var item = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("match", item.Name);
        Assert.Equal("more", item.ContinuationToken!.NextMarker);
        Assert.Equal(1, fixture.Sends);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonpositivePowerShellCountRetainsUnlimitedSemantics(int maxCount)
    {
        int page = 0;
        var fixture = new Fixture(_ => Task.FromResult(++page == 1 ? Page("next", BlobElement("a")) : Page(null, BlobElement("b"))));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container", MaxCount = maxCount };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(2, fixture.Output.Count);
        Assert.Equal(2, fixture.Sends);
    }

    [Fact]
    public async Task ForeignContinuationIsRejectedBeforeAuthentication()
    {
        var fixture = new Fixture();
        var processor = new ListStorageBlobsProcessor
        {
            Context = Context(), Container = "container",
            ContinuationToken = new StorageBlobContinuationToken("marker", "other", "container", null, null)
        };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task RepeatedMarkerFailsRatherThanLooping()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page("same")));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container" };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Equal(2, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("<Blob><Name>secret</Name><Properties><Last-Modified>time</Last-Modified></Properties></Blob>")]
    [InlineData("<Blob><Name Encoded='true'>secret%ZZ</Name><Properties><Last-Modified>time</Last-Modified><Etag>etag</Etag></Properties></Blob>")]
    [InlineData("<Blob><Name>secret</Name><Properties><Last-Modified>time</Last-Modified><Etag>etag</Etag><Content-Length>bad-secret</Content-Length></Properties></Blob>")]
    public async Task MalformedListingDoesNotLeakValues(string entry)
    {
        var fixture = new Fixture(_ => Task.FromResult(Page(null, XElement.Parse(entry))));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container" };
        processor.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("secret", error.ToString());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task FlatAndHierarchyAtomicCodecsRetainApiModels()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page(null, BlobElement("one"))));
        var processor = new BlobProcessors.ListBlobsContainerProcessor { Url = Context().GetBlobUri("container").AbsoluteUri };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        var page = Assert.IsType<BlobModels.ListBlobsResponse>(Assert.Single(fixture.Output));
        Assert.Equal("one", Assert.Single(page.BlobItems).NameContent);
        using var hierarchy = Page(null, new XElement("BlobPrefix", new XElement("Name", "prefix/")));
        var parsed = await StorageWireFormat.ReadResponseAsync<BlobModels.ListBlobsHierarchicalResponse>(hierarchy, CancellationToken.None);
        Assert.Equal("prefix/", Assert.Single(parsed.HierarchicalListBlobPrefixes!).NameContent);
    }

    [Theory]
    [InlineData("BlobNotFound", true)]
    [InlineData("ContainerNotFound", true)]
    [InlineData(null, false)]
    public async Task ExistsConvertsOnlyBlobAndContainerNotFoundCodes(string? code, bool returnsFalse)
    {
        var fixture = new Fixture(_ => Task.FromResult(Error(HttpStatusCode.NotFound, code)));
        var processor = new TestStorageBlobExistsProcessor { Context = Context(), Container = "container", Blob = "blob" };
        processor.Init(fixture.Context);
        if (returnsFalse)
        {
            await processor.ProcessAsync();
            Assert.False(Assert.IsType<StorageBlobExistence>(Assert.Single(fixture.Output)).Exists);
            Assert.DoesNotContain(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
            Assert.Single(fixture.Messages, item => item.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
        }
    }

    [Fact]
    public async Task ShowReturnsNativeBlobAndFetchesTagsWithTheObservedEtag()
    {
        int calls = 0;
        var fixture = new Fixture(request =>
        {
            calls++;
            if (request.Method == HttpMethod.Head) return Task.FromResult(Head());
            Assert.Equal("\"etag\"", Assert.Single(request.Headers.GetValues("x-ms-blob-if-match")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<Tags><TagSet><Tag><Key>name</Key><Value>value</Value></Tag></TagSet></Tags>")
            });
        });
        var processor = new GetStorageBlobProcessor { Context = Context(), Container = "container", Blob = "blob", IncludeTag = true };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("value", blob.Tags["name"]);
        Assert.Equal("BlockBlob", blob.Properties.BlobType);
        Assert.Null(blob.VersionId);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteHonorsNativeInputAndPassThru(bool passThru)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("version", Query(request)["versionid"]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        var processor = new RemoveStorageBlobProcessor
        {
            InputObject = new StorageBlob("blob", new StorageBlobContainer("container", Context()), new BlobModels.BlobProperties { ETag = "etag", LastModified = "time" })
            { VersionId = "version" },
            PassThru = passThru
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(passThru ? 1 : 0, fixture.Output.Count);
        if (passThru) Assert.True(Assert.IsType<bool>(Assert.Single(fixture.Output)));
    }

    [Fact]
    public async Task WhatIfSuppressesDeleteAndTierBeforeAnyRequest()
    {
        var fixture = new Fixture();
        var delete = new RemoveStorageBlobProcessor { Context = Context(), Container = "container", Blob = "blob", ShouldProcess = (_, _) => false };
        var tier = new SetStorageBlobTierProcessor { Context = Context(), Container = "container", Blob = "blob", Tier = "Cool", ShouldProcess = (_, _) => false };
        delete.Init(fixture.Context);
        tier.Init(fixture.Context);
        await delete.ProcessAsync();
        await tier.ProcessAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task TierUsesBlobTypeValidationAndEmitsNoOutput()
    {
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(Head());
            Assert.Equal("tier", Query(request)["comp"]);
            Assert.Equal("Cool", Assert.Single(request.Headers.GetValues("x-ms-access-tier")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var processor = new SetStorageBlobTierProcessor { Context = Context(), Container = "container", Blob = "blob", Tier = "Cool" };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(2, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task ForbiddenExistsIsNotFalse()
    {
        var fixture = new Fixture(_ => Task.FromResult(Error(HttpStatusCode.Forbidden, "AuthorizationFailure")));
        var processor = new TestStorageBlobExistsProcessor { Context = Context(), Container = "container", Blob = "blob" };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("Content-Length", "-1")]
    [InlineData("Content-Length", "9223372036854775808")]
    [InlineData("TagCount", "2147483648")]
    [InlineData("ServerEncrypted", "private-value")]
    [InlineData("Content-MD5", "private-value")]
    public async Task MalformedTypedXmlPropertiesAreRejected(string field, string value)
    {
        var blob = BlobElement("blob");
        blob.Element("Properties")!.Element(field)?.Remove();
        blob.Element("Properties")!.Add(new XElement(field, value));
        using var response = Page(null, blob);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            StorageBlobXml.ReadPageAsync(response.Content, new StorageBlobContainer("container", Context()), false, CancellationToken.None));
        Assert.DoesNotContain(value, error.ToString());
    }

    [Theory]
    [InlineData("Metadata")]
    [InlineData("OrMetadata")]
    [InlineData("Tags")]
    public async Task DuplicateMapKeysAreRejectedInListingXml(string field)
    {
        var blob = BlobElement("blob");
        var map = new XElement(field);
        if (field == "Tags")
            map.Add(new XElement("TagSet", Enumerable.Range(0, 2).Select(_ =>
                new XElement("Tag", new XElement("Key", "duplicate"), new XElement("Value", "value")))));
        else
            map.Add(new XElement("duplicate", "one"), new XElement("duplicate", "two"));
        blob.Add(map);
        using var response = Page(null, blob);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StorageBlobXml.ReadPageAsync(response.Content, new StorageBlobContainer("container", Context()), false, CancellationToken.None));
    }

    [Fact]
    public async Task DtdIsRejectedWithoutExposingTheDocument()
    {
        using var content = new StringContent("<!DOCTYPE EnumerationResults [<!ENTITY private 'private-value'>]>" +
            "<EnumerationResults ServiceEndpoint='https://fixture.blob.core.windows.net/' ContainerName='container'>" +
            "<Blobs/><NextMarker>&private;</NextMarker></EnumerationResults>");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            StorageBlobXml.ReadPageAsync(content, new StorageBlobContainer("container", Context()), false, CancellationToken.None));
        Assert.DoesNotContain("private", error.ToString());
    }

    [Fact]
    public async Task ForeignContainerIsRejectedButResponseEndpointCannotRedirectCredentials()
    {
        var document = new XElement("EnumerationResults",
            new XAttribute("ServiceEndpoint", "https://untrusted.example/"), new XAttribute("ContainerName", "container"),
            new XElement("Blobs", BlobElement("one")), new XElement("NextMarker"));
        using var content = new StringContent(document.ToString());
        var page = await StorageBlobXml.ReadPageAsync(content, new StorageBlobContainer("container", Context()), false, CancellationToken.None);
        Assert.Equal("fixture.blob.core.windows.net", Assert.IsType<StorageBlob>(Assert.Single(page.Entries)).Uri.Host);
        document.SetAttributeValue("ContainerName", "another");
        using var foreign = new StringContent(document.ToString());
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StorageBlobXml.ReadPageAsync(foreign, new StorageBlobContainer("container", Context()), false, CancellationToken.None));
    }

    [Theory]
    [InlineData(false, "literal%2F+name", "literal%2F+name")]
    [InlineData(true, "literal%252F+name", "literal%2F+name")]
    [InlineData(true, "name%EF%BF%BE", "name\uFFFE")]
    [InlineData(false, "directory/", "directory/")]
    public async Task NameDecodingOccursExactlyOnceOnlyWhenFlagged(bool encoded, string wireName, string expected)
    {
        using var response = Page(null, BlobElement(wireName, encoded));
        var page = await StorageBlobXml.ReadPageAsync(response.Content, new StorageBlobContainer("container", Context()), false, CancellationToken.None);
        var blob = Assert.IsType<StorageBlob>(Assert.Single(page.Entries));
        Assert.Equal(expected, blob.Name);
    }

    [Fact]
    public async Task OversizedPageIsRejectedBeforeOutput()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page("next", BlobElement("one"), BlobElement("two"))));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container", NumResults = 1 };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task EmptyTerminalPageEmitsANullMarkerWhenRequested()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page(null)));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container", ShowNextMarker = true };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal("{\"nextMarker\":null}", JsonSerializer.Serialize(Assert.IsType<StorageBlobNextMarker>(Assert.Single(fixture.Output))));
        Assert.Equal("{\"exists\":false}", JsonSerializer.Serialize(new StorageBlobExistence(false)));
    }

    [Fact]
    public async Task PositiveResultLimitDoesNotFetchMoreThanFiveThousandPerRequest()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("5000", Query(request)["maxresults"]);
            return Task.FromResult(Page(null));
        });
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container", NumResults = long.MaxValue };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task LimitedListingWarnsWhenTheCallerDidNotRequestMarkerOutput()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page("opaque-marker", BlobElement("one"))));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container", NumResults = 1 };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Contains(fixture.Messages, message => message.Id == Core.Abstractions.Runtime.Events.Warning &&
            message.Message.Contains("opaque-marker", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CyclicContinuationIsRejected()
    {
        int calls = 0;
        var fixture = new Fixture(_ => Task.FromResult(Page(++calls switch { 1 => "a", 2 => "b", _ => "a" })));
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container" };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Equal(3, fixture.Sends);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("versionid")]
    public async Task ShowTagsPreservesSelectorsAlongsideTheOperationQuery(string selector)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("selected+/=&", Query(request)[selector]);
            if (request.Method == HttpMethod.Head) return Task.FromResult(Head());
            Assert.Equal("tags", Query(request)["comp"]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<Tags><TagSet/></Tags>") });
        });
        var processor = new GetStorageBlobProcessor
        {
            Context = Context(), Container = "container", Blob = "blob", IncludeTag = true,
            Snapshot = selector == "snapshot" ? "selected+/=&" : null,
            VersionId = selector == "versionid" ? "selected+/=&" : null
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(2, fixture.Sends);
    }

    [Fact]
    public async Task ReusedWorkflowResolvesEachNativeContainersProfileIndependently()
    {
        var fixture = new Fixture(_ => Task.FromResult(Head()));
        var processor = new GetStorageBlobProcessor { Blob = "blob" };
        processor.Init(fixture.Context);
        var first = new Core.Abstractions.Profile { Name = "first", TenantId = "first-tenant" };
        var second = new Core.Abstractions.Profile { Name = "second", TenantId = "second-tenant" };
        processor.ContainerObject = new StorageBlobContainer("container", new StorageContext("firstaccount", first));
        await processor.ProcessAsync();
        processor.ContainerObject = new StorageBlobContainer("container", new StorageContext("secondaccount", second));
        await processor.ProcessAsync();
        Assert.Null(processor.Context);
        Assert.Equal(new[] { first, second }, fixture.TokenRequests.Select(request => request.Profile));
        Assert.Equal(new[] { "firstaccount", "secondaccount" }, fixture.Output.Cast<StorageBlob>().Select(blob => blob.Context.StorageAccountName));
    }

    [Fact]
    public async Task ConflictingSelectorsAndContextFailBeforeAuthentication()
    {
        var fixture = new Fixture();
        BlobContainerWorkflowBase[] processors =
        [
            new GetStorageBlobProcessor { Context = Context(), Container = "container", Blob = "blob", Snapshot = "snapshot", VersionId = "version" },
            new GetStorageBlobProcessor { Context = Context(), Container = "container", Blob = "blob", StorageAccountName = "anotheraccount" },
            new ListStorageBlobsProcessor { Context = Context(), Container = "container", NumResults = 1, MaxCount = 1 },
            new ListStorageBlobsProcessor { Context = Context(), Container = "container", NumResults = 0 }
        ];
        foreach (var processor in processors)
        {
            processor.Init(fixture.Context);
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        }
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task WorkflowCancellationStopsBeforeAuthentication()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(token: cancellation.Token);
        var processor = new ListStorageBlobsProcessor { Context = Context(), Container = "container" };
        processor.Init(fixture.Context);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Contains(Core.Abstractions.Runtime.Events.ProcessorProcessAsyncEnd, fixture.Events);
    }

    [Theory]
    [InlineData("BlockBlob", "P4")]
    public async Task TierRejectsIncompatibleBlobTypesWithoutMutation(string blobType, string tier)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            return Task.FromResult(Head(blobType));
        });
        var processor = new SetStorageBlobTierProcessor { Context = Context(), Container = "container", Blob = "blob", Tier = tier };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task UnsupportedTierConditionsAreNotSilentlyIgnored()
    {
        var fixture = new Fixture();
        var processor = new SetStorageBlobTierProcessor { Context = Context(), Container = "container", Blob = "blob", Tier = "Cool", IfMatch = "\"etag\"" };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task MalformedHeaderChecksumFailsWithoutLeakingItsValue()
    {
        var fixture = new Fixture(_ =>
        {
            var response = Head();
            response.Content.Headers.TryAddWithoutValidation("Content-MD5", "private-value");
            return Task.FromResult(response);
        });
        var processor = new GetStorageBlobProcessor { Context = Context(), Container = "container", Blob = "blob" };
        processor.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("BlobNotFound", true)]
    [InlineData("private-message with spaces", false)]
    [InlineData("BlobNotFound\n", false)]
    [InlineData("", false)]
    public async Task SensitiveErrorsRetainOnlyValidatedCodesWithoutResponseContent(string code, bool valid)
    {
        var fixture = new Fixture(_ =>
        {
            var response = Error(HttpStatusCode.NotFound, null);
            response.Headers.TryAddWithoutValidation("x-ms-error-code", code);
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "93d70ac1-f6e6-4411-967e-18b1c71508ef");
            return Task.FromResult(response);
        });
        var processor = new GetStorageBlobProcessor { Context = Context(), Container = "container", Blob = "private-blob" };
        processor.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(valid ? code : null, error.Data["ServiceErrorCode"]);
        Assert.Equal("93d70ac1-f6e6-4411-967e-18b1c71508ef", error.Data["ServiceRequestId"]);
        Assert.DoesNotContain("private", error.ToString());
        Assert.DoesNotContain(fixture.Messages, message => message.Message.Contains("private", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateErrorHeadersCannotMaskTheHttpFailure()
    {
        var fixture = new Fixture(_ =>
        {
            var response = Error(HttpStatusCode.NotFound, null);
            response.Headers.TryAddWithoutValidation("x-ms-error-code", new[] { "BlobNotFound", "ContainerNotFound" });
            response.Headers.TryAddWithoutValidation("x-ms-request-id", new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() });
            return Task.FromResult(response);
        });
        var processor = new TestStorageBlobExistsProcessor { Context = Context(), Container = "container", Blob = "blob" };
        processor.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Null(error.Data["ServiceErrorCode"]);
        Assert.Null(error.Data["ServiceRequestId"]);
        Assert.Empty(fixture.Output);
    }

    internal static StorageContext Context() => new("fixture", Fixture.DefaultProfile);
    internal static System.Collections.Specialized.NameValueCollection Query(HttpRequestMessage request) =>
        System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
    internal static XElement BlobElement(string name, bool encoded = false) => new("Blob",
        new XElement("Name", encoded ? new XAttribute("Encoded", "true") : null, name),
        new XElement("Properties", new XElement("Last-Modified", "Tue, 20 Oct 2026 00:00:00 GMT"),
            new XElement("Etag", "\"etag\""), new XElement("Content-Length", 5L * 1024 * 1024 * 1024),
            new XElement("BlobType", "BlockBlob")));
    internal static HttpResponseMessage Page(string? marker, params XElement[] entries) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(new XElement("EnumerationResults",
            new XAttribute("ServiceEndpoint", "https://fixture.blob.core.windows.net/"), new XAttribute("ContainerName", "container"),
            new XElement("Blobs", entries), new XElement("NextMarker", marker)).ToString())
    };
    internal static HttpResponseMessage Head(string blobType = "BlockBlob", long length = 0)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
        response.Headers.Add("x-ms-blob-type", blobType);
        response.Headers.Add("x-ms-version-id", "current-version");
        response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        response.Content.Headers.ContentLength = length;
        return response;
    }
    internal static HttpResponseMessage Error(HttpStatusCode status, string? code)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("private-response") };
        if (code is not null) response.Headers.Add("x-ms-error-code", code);
        return response;
    }
}
