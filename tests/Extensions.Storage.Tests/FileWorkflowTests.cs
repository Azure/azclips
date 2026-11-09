// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class FileWorkflowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BlobListingIncludesAlsoUseCommaSeparatedWireValues(bool hierarchy, bool empty)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(empty ? null : "metadata,tags", BlobWorkflowTests.Query(request)["include"]);
            return Task.FromResult(BlobWorkflowTests.Page(null));
        });
        var processor = new ListStorageBlobsProcessor_Orchestrator
        {
            Context = BlobWorkflowTests.Context(), Container = "container",
            Delimiter = hierarchy ? "/" : null, Datasets = empty ? [] : ["metadata", "tags"]
        };
        processor.Init(fixture.Context);
        await processor.ProcessNativeAsync();
        Assert.Single(fixture.TokenRequests);
    }

    [Fact]
    public async Task MixedListingPreservesWireOrderAndExtendedPropertiesWithoutInventingDirectoryLength()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page(null, FileElement("first"), DirectoryElement("middle"), FileElement("last"))));
        await Listing(fixture).ProcessNativeAsync();
        Assert.Equal(new[] { "first", "middle", "last" }, fixture.Output.Cast<StorageFileEntry>().Select(item => item.Name));
        Assert.Equal(new[] { false, true, false }, fixture.Output.Cast<StorageFileEntry>().Select(item => item.IsDirectory));
        var file = Assert.IsType<StorageFile>(fixture.Output.First());
        Assert.Equal(5L * 1024 * 1024 * 1024, file.Properties.ContentLength);
        Assert.Equal("file-id", file.Properties.FileId);
        Assert.Equal("parent-id", file.Properties.ParentId);
        Assert.Equal("Archive", file.Properties.Attributes);
        Assert.Equal("permission-key", file.Properties.PermissionKey);
        Assert.Equal("\"etag\"", file.Properties.ETag);
        Assert.NotNull(file.Properties.CreationTime);
        Assert.NotNull(file.Properties.LastAccessTime);
        Assert.NotNull(file.Properties.LastWriteTime);
        Assert.NotNull(file.Properties.ChangeTime);
        Assert.NotNull(file.Properties.LastModified);
        var directory = Assert.IsType<StorageFileDirectory>(fixture.Output.ElementAt(1));
        Assert.Null(directory.Properties.ContentLength);
        Assert.NotNull(directory.Properties.CreationTime);
        Assert.Equal("directory-id", directory.Properties.FileId);
        Assert.Equal("fixture.file.core.windows.net", file.Uri.IdnHost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExtendedInfoAndTrailingDotControlsAreExplicit(bool exclude)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("directory", BlobWorkflowTests.Query(request)["restype"]);
            Assert.Equal("list", BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal("5000", BlobWorkflowTests.Query(request)["maxresults"]);
            Assert.Equal(exclude ? null : "Timestamps,Etag,Attributes,PermissionKey", BlobWorkflowTests.Query(request)["include"]);
            Assert.Equal(exclude ? "false" : "true", request.Headers.GetValues("x-ms-file-extended-info").Single());
            Assert.Equal("true", request.Headers.GetValues("x-ms-allow-trailing-dot").Single());
            Assert.Equal("backup", request.Headers.GetValues("x-ms-file-request-intent").Single());
            Assert.Equal("2026-06-06", request.Headers.GetValues("x-ms-version").Single());
            Assert.Equal("7", BlobWorkflowTests.Query(request)["timeout"]);
            return Task.FromResult(Page(null, FileElement("file.")));
        });
        var processor = Listing(fixture);
        processor.ExcludeExtendedInfo = exclude; processor.AllowTrailingDot = true; processor.Timeout = 7;
        await processor.ProcessNativeAsync();
        Assert.Equal("file.", Assert.IsType<StorageFile>(Assert.Single(fixture.Output)).Name);
        Assert.All(fixture.TokenRequests, request => Assert.Equal(new[] { "https://storage.azure.com/.default" }, request.Scopes));
    }

    [Fact]
    public async Task EmptyPagesWithContinuationsAreDrainedAndDirectoryModeFiltersOnlyAfterPaging()
    {
        int calls = 0;
        var fixture = new Fixture(request =>
        {
            Assert.Equal(calls == 0 ? null : calls == 1 ? "empty" : "last", BlobWorkflowTests.Query(request)["marker"]);
            return Task.FromResult(++calls switch
            {
                1 => Page("empty", FileElement("file")),
                2 => Page("last"),
                _ => Page(null, DirectoryElement("directory"))
            });
        });
        var processor = Listing(fixture);
        processor.FilterDirectoriesOnly = true;
        await processor.ProcessNativeAsync();
        Assert.Equal(3, calls);
        Assert.Equal("directory", Assert.IsType<StorageFileDirectory>(Assert.Single(fixture.Output)).Name);
    }

    [Fact]
    public async Task LimitStopsAtTheServiceBoundaryAndReturnsBoundContinuation()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("2", BlobWorkflowTests.Query(request)["maxresults"]);
            return Task.FromResult(Page("opaque+/=", FileElement("file"), DirectoryElement("directory")));
        });
        var processor = Listing(fixture);
        processor.ResultLimit = 2; processor.ShowNextMarker = true;
        await processor.ProcessNativeAsync();
        Assert.Equal(1, fixture.Sends);
        Assert.Equal("opaque+/=", Assert.IsType<StorageFileNextMarker>(fixture.Output.Last()).NextMarker);
        var token = Assert.IsType<StorageFile>(fixture.Output.First()).ContinuationToken;
        Assert.Equal(new StorageFileContinuationToken("opaque+/=", "fixture", "share", "", null, null), token);
        Assert.DoesNotContain(fixture.Messages, item => item.Id == Events.Warning);
    }

    [Fact]
    public async Task LimitWithoutMarkerOutputWarnsAndTerminalMarkerSerializesNull()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page("next", FileElement("file"))));
        var processor = Listing(fixture);
        processor.ResultLimit = 1;
        await processor.ProcessNativeAsync();
        Assert.Contains(fixture.Messages, item => item.Id == Events.Warning && item.Message.Contains("next", StringComparison.Ordinal));
        var empty = new Fixture(_ => Task.FromResult(Page(null)));
        var terminal = Listing(empty);
        terminal.ShowNextMarker = true;
        await terminal.ProcessNativeAsync();
        Assert.Equal("{\"nextMarker\":null}", JsonSerializer.Serialize(Assert.Single(empty.Output)));
    }

    [Fact]
    public async Task ContinuationResumesTheSameSnapshotDirectoryPrefixAndEscapesMarkerOnce()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("/share/parent/child", request.RequestUri!.AbsolutePath);
            Assert.Equal("snapshot+/=", BlobWorkflowTests.Query(request)["sharesnapshot"]);
            Assert.Equal("marker+/=", BlobWorkflowTests.Query(request)["marker"]);
            Assert.Equal("prefix", BlobWorkflowTests.Query(request)["prefix"]);
            return Task.FromResult(PageFor("parent/child", "snapshot+/=", null, FileElement("file")));
        });
        var processor = Listing(fixture);
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", Context(), "snapshot+/="));
        processor.Path = "child"; processor.Prefix = "prefix";
        processor.ContinuationToken = new StorageFileContinuationToken("marker+/=", "fixture", "share", "parent/child", "snapshot+/=", "prefix");
        await processor.ProcessNativeAsync();
        var output = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal("parent/child/file", output.Path);
        Assert.Equal("snapshot+/=", output.Share.ShareSnapshot);
        Assert.Null(processor.ShareSnapshot);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("share")]
    [InlineData("path")]
    [InlineData("snapshot")]
    [InlineData("prefix")]
    [InlineData("empty")]
    [InlineData("both")]
    public async Task WrongContinuationIsRejectedBeforeAuthentication(string mismatch)
    {
        var fixture = new Fixture();
        var processor = Listing(fixture);
        var token = new StorageFileContinuationToken("next", "fixture", "share", "", null, null);
        processor.ContinuationToken = mismatch switch
        {
            "account" => token with { StorageAccountName = "another" },
            "share" => token with { Share = "another" },
            "path" => token with { Path = "another" },
            "snapshot" => token with { ShareSnapshot = "another" },
            "prefix" => token with { Prefix = "another" },
            "empty" => token with { NextMarker = "" },
            _ => token
        };
        if (mismatch == "both") processor.Marker = "another";
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessNativeAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task RepeatedMarkersAndOversizedPagesFailExplicitly()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page("next")));
        await Assert.ThrowsAsync<InvalidDataException>(() => Listing(fixture).ProcessNativeAsync());
        Assert.Equal(2, fixture.Sends);
        var oversized = new Fixture(_ => Task.FromResult(Page(null, FileElement("one"), FileElement("two"))));
        var processor = Listing(oversized);
        processor.ResultLimit = 1;
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessNativeAsync());
        Assert.Empty(oversized.Output);
    }

    [Theory]
    [InlineData(true, "literal%252F+name", "literal%2F+name")]
    [InlineData(true, "name%EF%BF%BE", "name\uFFFE")]
    public async Task FlaggedNamesAreDecodedExactlyOnceAndPlusIsNotASpace(bool encoded, string wireName, string expected)
    {
        var file = FileElement(wireName);
        if (encoded) file.Element("Name")!.SetAttributeValue("Encoded", "true");
        var fixture = new Fixture(_ => Task.FromResult(Page(null, file)));
        await Listing(fixture).ProcessNativeAsync();
        Assert.Equal(expected, Assert.IsType<StorageFile>(Assert.Single(fixture.Output)).Name);
    }

    [Fact]
    public async Task EncodedDirectoryPathIsBoundToTheInputAndReturnedEndpointCannotRedirectCredentials()
    {
        var xml = Document("literal%252F+", null, null, FileElement("file"));
        xml.SetAttributeValue("Encoded", "true");
        xml.SetAttributeValue("ServiceEndpoint", "https://untrusted.example/");
        var fixture = new Fixture(request =>
        {
            Assert.Equal("fixture.file.core.windows.net", request.RequestUri!.IdnHost);
            return Task.FromResult(Response(xml));
        });
        var processor = Listing(fixture);
        processor.Path = "literal%2F+";
        await processor.ProcessNativeAsync();
        var file = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal("literal%2F+/file", file.Path);
        Assert.Equal("fixture.file.core.windows.net", file.Uri.IdnHost);
    }

    [Theory]
    [InlineData("foreign-share")]
    [InlineData("foreign-path")]
    [InlineData("foreign-snapshot")]
    [InlineData("missing-entries")]
    [InlineData("missing-marker")]
    [InlineData("duplicate-name")]
    [InlineData("duplicate-length")]
    [InlineData("missing-length")]
    [InlineData("negative-length")]
    [InlineData("overflow-length")]
    [InlineData("nested-name")]
    [InlineData("traversal-name")]
    [InlineData("bad-percent")]
    [InlineData("bad-flag")]
    [InlineData("bad-date")]
    [InlineData("unknown-entry")]
    public async Task MalformedListingsFailWithoutReturningRawPayloads(string damage)
    {
        var file = FileElement("file");
        var xml = Document("", null, null, file);
        switch (damage)
        {
            case "foreign-share": xml.SetAttributeValue("ShareName", "another"); break;
            case "foreign-path": xml.SetAttributeValue("DirectoryPath", "another"); break;
            case "foreign-snapshot": xml.SetAttributeValue("ShareSnapshot", "another"); break;
            case "missing-entries": xml.Element("Entries")!.Remove(); break;
            case "missing-marker": xml.Element("NextMarker")!.Remove(); break;
            case "duplicate-name": file.Add(new XElement("Name", "private-value")); break;
            case "duplicate-length": file.Element("Properties")!.Add(new XElement("Content-Length", "1")); break;
            case "missing-length": file.Element("Properties")!.Element("Content-Length")!.Remove(); break;
            case "negative-length": file.Element("Properties")!.Element("Content-Length")!.Value = "-1"; break;
            case "overflow-length": file.Element("Properties")!.Element("Content-Length")!.Value = "9223372036854775808"; break;
            case "nested-name": file.Element("Name")!.Value = "another/file"; break;
            case "traversal-name": file.Element("Name")!.Value = ".."; break;
            case "bad-percent": file.Element("Name")!.Value = "private-value%"; file.Element("Name")!.SetAttributeValue("Encoded", "true"); break;
            case "bad-flag": file.Element("Name")!.SetAttributeValue("Encoded", "private-value"); break;
            case "bad-date": file.Element("Properties")!.Element("CreationTime")!.Value = "private-value"; break;
            case "unknown-entry": file.Name = "Other"; break;
        }
        var fixture = new Fixture(_ => Task.FromResult(Response(xml)));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Listing(fixture).ProcessNativeAsync());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task DtdAndMalformedXmlAreRejectedWithoutPayloadDetails()
    {
        foreach (string xml in new[] { "<!DOCTYPE EnumerationResults [<!ENTITY x 'private-value'>]><EnumerationResults/>", "<private-value>" })
        {
            var fixture = new Fixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) }));
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => Listing(fixture).ProcessNativeAsync());
            Assert.DoesNotContain("private-value", error.ToString());
            Assert.Empty(fixture.Output);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingIntentFailsBeforeAuthUnlessExplicitlyAuthorizedByTheCommand(bool commandIntent)
    {
        var fixture = new Fixture(_ => Task.FromResult(Page(null)));
        var context = new StorageContext("fixture", Fixture.DefaultProfile);
        var processor = Listing(fixture);
        processor.Context = context; processor.EnableFileBackupRequestIntent = commandIntent;
        if (commandIntent)
        {
            await processor.ProcessNativeAsync();
            Assert.Single(fixture.TokenRequests);
            Assert.False(context.EnableFileBackupRequestIntent);
            Assert.Same(context, processor.Context);
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessNativeAsync());
            Assert.Empty(fixture.TokenRequests);
        }
    }

    [Fact]
    public async Task NamedAccountCanUseExplicitCommandIntentWithoutCreatingAnArmDependency()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page(null)));
        var processor = Listing(fixture);
        processor.Context = null; processor.StorageAccountName = "fixture"; processor.EnableFileBackupRequestIntent = true;
        await processor.ProcessNativeAsync();
        Assert.Single(fixture.TokenRequests);
        Assert.Null(processor.Context);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("share")]
    [InlineData("snapshot")]
    [InlineData("empty-snapshot")]
    [InlineData("path")]
    [InlineData("timeout")]
    [InlineData("count")]
    public async Task ConflictingOrInvalidInputsFailBeforeAuthentication(string mismatch)
    {
        var fixture = new Fixture();
        var processor = Listing(fixture);
        processor.ShareObject = new StorageFileShare("share", Context(), "snapshot");
        switch (mismatch)
        {
            case "account": processor.StorageAccountName = "another"; break;
            case "share": processor.ShareName = "another"; break;
            case "snapshot": processor.ShareSnapshot = "another"; break;
            case "empty-snapshot": processor.ShareSnapshot = ""; break;
            case "path": processor.Path = "../outside"; break;
            case "timeout": processor.Timeout = 0; break;
            case "count": processor.ResultLimit = 0; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessNativeAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task NativeDirectorySelectsItsAccountAndProfileWithoutMutatingNamedInputs()
    {
        var profile = new Core.Abstractions.Profile { Name = "source", TenantId = "source-tenant" };
        var fixture = new Fixture(request =>
        {
            Assert.Equal("sourceaccount.file.core.windows.net", request.RequestUri!.IdnHost);
            return Task.FromResult(PageFor("parent", null, null, FileElement("file")));
        });
        var processor = Listing(fixture);
        processor.Context = null; processor.ShareName = null;
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", new StorageContext("sourceaccount", profile, true)));
        await processor.ProcessNativeAsync();
        Assert.Equal(profile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.Null(processor.Context);
        Assert.Null(processor.ShareName);
        Assert.Null(processor.Path);
    }

    [Fact]
    public async Task StorageContextProfileWinsOverADifferentProfileName()
    {
        var profile = new Core.Abstractions.Profile { Name = "source", TenantId = "source-tenant" };
        var fixture = new Fixture(_ => Task.FromResult(PageFor("parent", null, null, FileElement("file"))));
        var processor = Listing(fixture);
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", new StorageContext("fixture", profile, true)));
        processor.Context = null; processor.ShareName = null; processor.Profile = "other";
        await processor.ProcessNativeAsync();
        Assert.Equal(profile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
    }

    [Fact]
    public async Task EquivalentNativeShareAndDirectoryContextsAreCompatible()
    {
        var fixture = new Fixture(_ => Task.FromResult(PageFor("parent", null, null, DirectoryElement("child"))));
        var processor = Listing(fixture);
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", Context()));
        processor.ShareObject = new StorageFileShare("share", Context());
        await processor.ProcessNativeAsync();
        Assert.IsType<StorageFileDirectory>(Assert.Single(fixture.Output));
    }

    [Fact]
    public async Task DirectoryWithoutExtendedPropertiesRetainsUnknownRatherThanZeroValues()
    {
        var directory = DirectoryElement("directory");
        directory.Element("Properties")!.Remove();
        var fixture = new Fixture(_ => Task.FromResult(Page(null, directory)));
        await Listing(fixture).ProcessNativeAsync();
        var output = Assert.IsType<StorageFileDirectory>(Assert.Single(fixture.Output));
        Assert.Null(output.Properties.ContentLength);
        Assert.Null(output.Properties.ETag);
        Assert.Null(output.Properties.CreationTime);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    public async Task DirectoryProbeFallbackCanReturnANativeFileWithoutFalseErrors(int status)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("/share/parent/file%252F%2B", request.RequestUri!.AbsolutePath);
            Assert.Equal("snapshot+/=", BlobWorkflowTests.Query(request)["sharesnapshot"]);
            Assert.Equal("true", request.Headers.GetValues("x-ms-allow-trailing-dot").Single());
            Assert.Equal("backup", request.Headers.GetValues("x-ms-file-request-intent").Single());
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("directory", BlobWorkflowTests.Query(request)["restype"]);
                return Task.FromResult(BlobWorkflowTests.Error((HttpStatusCode)status, "DirectoryProbeFailed"));
            }
            Assert.Equal(HttpMethod.Head, request.Method);
            Assert.Null(BlobWorkflowTests.Query(request)["restype"]);
            return Task.FromResult(FileHead());
        });
        var processor = Lookup(fixture);
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", Context(), "snapshot+/="));
        processor.Path = "file%2F+"; processor.AllowTrailingDot = true;
        await processor.ProcessNativeAsync();
        var file = Assert.IsType<StorageFile>(Assert.Single(fixture.Output));
        Assert.Equal("parent/file%2F+", file.Path);
        Assert.Equal(5L * 1024 * 1024 * 1024, file.Properties.ContentLength);
        Assert.Equal("team", file.Properties.Metadata["owner"]);
        Assert.Equal("file-id", file.Properties.FileId);
        Assert.Equal("snapshot+/=", file.Share.ShareSnapshot);
        Assert.Equal(2, fixture.Sends);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task DirectoryLookupUsesGetAndDoesNotProbeAFileOrListItsChildren()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("directory", BlobWorkflowTests.Query(request)["restype"]);
            Assert.Null(BlobWorkflowTests.Query(request)["comp"]);
            return Task.FromResult(FileHead());
        });
        var processor = Lookup(fixture);
        processor.Path = "directory";
        await processor.ProcessNativeAsync();
        var directory = Assert.IsType<StorageFileDirectory>(Assert.Single(fixture.Output));
        Assert.Equal("directory", directory.Path);
        Assert.Null(directory.Properties.ContentLength);
        Assert.Single(fixture.TokenRequests);
    }

    [Fact]
    public async Task LookupWithoutPathListsNativeDirectoryChildrenAndForwardsOptions()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal("list", BlobWorkflowTests.Query(request)["comp"]);
            Assert.Equal("false", request.Headers.GetValues("x-ms-file-extended-info").Single());
            Assert.Equal("false", request.Headers.GetValues("x-ms-allow-trailing-dot").Single());
            return Task.FromResult(PageFor("parent", null, null, FileElement("file"), DirectoryElement("directory")));
        });
        var processor = Lookup(fixture);
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", Context()));
        processor.ExcludeExtendedInfo = true; processor.AllowTrailingDot = false;
        await processor.ProcessNativeAsync();
        Assert.Equal(new[] { "parent/file", "parent/directory" }, fixture.Output.Cast<StorageFileEntry>().Select(item => item.Path));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(409)]
    public async Task OtherDirectoryProbeFailuresDoNotFallBackAndAreReportedExactlyOnce(int status)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error((HttpStatusCode)status, "OriginalError")));
        var processor = Lookup(fixture);
        processor.Path = "file";
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessNativeAsync());
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData(403, 403)]
    [InlineData(403, 404)]
    [InlineData(404, 403)]
    [InlineData(404, 404)]
    public async Task TwoFailedProbesPropagateTheFileErrorRatherThanEmptySuccess(int first, int second)
    {
        var fixture = new Fixture(request => Task.FromResult(BlobWorkflowTests.Error(
            (HttpStatusCode)(request.Method == HttpMethod.Get ? first : second),
            request.Method == HttpMethod.Get ? "DirectoryError" : "FileError")));
        var processor = Lookup(fixture);
        processor.Path = "file";
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessNativeAsync());
        Assert.Equal((HttpStatusCode)second, error.StatusCode);
        Assert.Equal("FileError", error.Data["ServiceErrorCode"]);
        Assert.DoesNotContain("private-response", error.ToString());
        Assert.Equal(2, fixture.Sends);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData("etag")]
    [InlineData("modified")]
    [InlineData("type")]
    [InlineData("md5")]
    [InlineData("md5-length")]
    [InlineData("date")]
    public async Task MalformedFilePropertiesNeverProduceNativeSuccess(string damage)
    {
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Get) return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.NotFound, "ResourceNotFound"));
            var head = FileHead();
            switch (damage)
            {
                case "etag": head.Headers.ETag = null; break;
                case "modified": head.Content.Headers.LastModified = null; break;
                case "type": head.Headers.Remove("x-ms-type"); break;
                case "md5": head.Content.Headers.TryAddWithoutValidation("Content-MD5", "private-value"); break;
                case "md5-length": head.Content.Headers.ContentMD5 = [1]; break;
                case "date": head.Headers.Add("x-ms-file-creation-time", "private-value"); break;
            }
            return Task.FromResult(head);
        });
        var processor = Lookup(fixture);
        processor.Path = "file";
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessNativeAsync());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task UnsupportedProtocolErrorsAreNotRetriedAsASuccessShapedListing()
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.BadRequest, "UnsupportedQueryParameter")));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Listing(fixture).ProcessNativeAsync());
        Assert.Equal("UnsupportedQueryParameter", error.Data["ServiceErrorCode"]);
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task CancellationBeforeAndDuringListingProducesNoOutput()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(Page(null, FileElement("file")));
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Listing(fixture).ProcessNativeAsync());
        Assert.Empty(fixture.Output);
        var before = new Fixture(token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Listing(before).ProcessNativeAsync());
        Assert.Empty(before.TokenRequests);
    }

    private static StorageContext Context() => new("fixture", Fixture.DefaultProfile, enableFileBackupRequestIntent: true);
    private static ListStorageFilesProcessor_Orchestrator Listing(Fixture fixture)
    {
        var processor = new ListStorageFilesProcessor_Orchestrator { Context = Context(), ShareName = "share" };
        processor.Init(fixture.Context);
        return processor;
    }
    private static GetStorageFileProcessor_Orchestrator Lookup(Fixture fixture)
    {
        var processor = new GetStorageFileProcessor_Orchestrator { Context = Context(), ShareName = "share" };
        processor.Init(fixture.Context);
        return processor;
    }
    private static XElement FileElement(string name) => new("File",
        new XElement("Name", name), new XElement("FileId", "file-id"), Properties(length: true),
        new XElement("Attributes", "Archive"), new XElement("PermissionKey", "permission-key"));
    private static XElement DirectoryElement(string name) => new("Directory",
        new XElement("Name", name), new XElement("FileId", "directory-id"), Properties(length: false));
    private static XElement Properties(bool length) => new("Properties",
        length ? new XElement("Content-Length", 5L * 1024 * 1024 * 1024) : null,
        new XElement("CreationTime", "2026-10-20T00:00:00Z"), new XElement("LastAccessTime", "2026-10-20T00:00:00Z"),
        new XElement("LastWriteTime", "2026-10-20T00:00:00Z"), new XElement("ChangeTime", "2026-10-20T00:00:00Z"),
        new XElement("Last-Modified", "Tue, 20 Oct 2026 00:00:00 GMT"), new XElement("Etag", "\"etag\""));
    private static XElement Document(string path, string? snapshot, string? marker, params XElement[] entries) =>
        new("EnumerationResults", new XAttribute("ServiceEndpoint", "https://fixture.file.core.windows.net/"),
            new XAttribute("ShareName", "share"), new XAttribute("DirectoryPath", path),
            snapshot is null ? null : new XAttribute("ShareSnapshot", snapshot), new XElement("DirectoryId", "parent-id"),
            new XElement("Entries", entries), new XElement("NextMarker", marker));
    private static HttpResponseMessage Response(XElement xml) => new(HttpStatusCode.OK) { Content = new StringContent(xml.ToString()) };
    private static HttpResponseMessage Page(string? marker, params XElement[] entries) => PageFor("", null, marker, entries);
    private static HttpResponseMessage PageFor(string path, string? snapshot, string? marker, params XElement[] entries) =>
        Response(Document(path, snapshot, marker, entries));
    private static HttpResponseMessage FileHead()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
        response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        response.Content.Headers.ContentLength = 5L * 1024 * 1024 * 1024;
        response.Headers.Add("x-ms-type", "File");
        response.Headers.Add("x-ms-file-id", "file-id");
        response.Headers.Add("x-ms-meta-owner", "team");
        return response;
    }
}
