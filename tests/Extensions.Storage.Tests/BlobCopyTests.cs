// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using System.Xml.Linq;
using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobCopyTests
{
    internal const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Oid = "33333333-3333-3333-3333-333333333333";
    internal const string CopyId = "44444444-4444-4444-4444-444444444444";
    private const string Secret = "private-signature";
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    private static Profile SourceProfile => new() { Name = "source", TenantId = Tenant, CloudName = "AzureCloud",
        SubscriptionId = "55555555-5555-5555-5555-555555555555" };
    private static StorageContext SourceContext(bool files = false) => new("sourceaccount", SourceProfile, files);

    [Theory]
    [InlineData("blob", "eF+qsZxK4gIw5r7iQbmiyLJJBC2jw75HugCxWiz0nTo=")]
    [InlineData("snapshot", "Gc6GMzspHWU8fbpLB31LIj6G6grBpNXjOEkEjQ1I6VQ=")]
    [InlineData("version", "z0MRdwiiI0BTnxxR8kHJZ/9UIAwmWr3xBG5emvEfUj0=")]
    [InlineData("file", "jM7170OPSJQYNstoTv0+GR+lHeOSKP3mxZubn0FRP7w=")]
    public void SigningMatchesIndependentNodeHmacVectorsIncludingUnicodeAndPercentNames(string kind, string signature)
    {
        var context = SourceContext(kind == "file");
        DateTimeOffset start = DateTimeOffset.Parse("2026-10-22T00:00:00Z"), expiry = start.AddHours(1);
        string query = kind == "snapshot" ? "?snapshot=2026-10-21T00:00:00.0000000Z" :
            kind == "version" ? "?versionid=2026-10-20T00:00:00.0000000Z" : "";
        Uri resource = new((kind == "file" ? context.GetFileUri("share", "dir/\u96ea+%#") : context.GetBlobUri("container", "dir/\u96ea+%#")).AbsoluteUri + query);
        var signed = StorageCopyAuthorization.Sign(resource, context, kind == "file" ? StorageService.Files : StorageService.Blob,
            DelegationKey(kind == "file", start, expiry), start, expiry);
        var fields = HttpUtility.ParseQueryString(signed.Query);
        Assert.Equal(signature, fields["sig"]);
        Assert.Equal("r", fields["sp"]); Assert.Equal("https", fields["spr"]);
        Assert.Equal("2026-06-06", fields["sv"]);
        Assert.Equal(kind == "file" ? "f" : kind == "snapshot" ? "bs" : kind == "version" ? "bv" : "b", fields["sr"]);
        Assert.Equal(resource.AbsolutePath, signed.AbsolutePath);
        Assert.Null(fields["saoid"]); Assert.Null(fields["sduoid"]); Assert.Null(fields["srh"]);
    }

    [Theory]
    [InlineData(false, false, "pending")]
    [InlineData(false, true, "success")]
    [InlineData(false, false, "failed")]
    [InlineData(false, true, "aborted")]
    [InlineData(true, false, "pending")]
    [InlineData(true, true, "success")]
    public async Task CopyUsesIndependentSourceAuthenticationAndReturnsAcknowledgedStateWithoutPolling(bool files, bool cli, string status)
    {
        var service = new CopyService { Status = status };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture, files);
        processor.Profile = Fixture.DefaultProfile.Name;
        processor.ReturnCopyState = cli;
        await processor.ProcessAsync();
        Assert.Equal(2, fixture.Sends);
        Assert.Equal("*", service.DestinationIfNoneMatch);
        Assert.Equal(new[] { SourceProfile, Fixture.DefaultProfile }, fixture.TokenRequests.Select(item => item.Profile));
        Assert.Equal(new[] { files ? "https://management.azure.com/.default" : "https://storage.azure.com/.default" },
            fixture.TokenRequests.First().Scopes);
        Assert.Equal(new[] { "https://storage.azure.com/.default" }, fixture.TokenRequests.Last().Scopes);
        Assert.Same(fixture.Profile, fixture.Auth.Profile);
        var authorization = HttpUtility.ParseQueryString(service.AuthorizedSource!.Query);
        Assert.Equal(files ? null : "b", authorization["sks"]);
        Assert.Equal(files ? "f" : "b", authorization["sr"]);
        Assert.Equal("r", authorization["sp"]);
        Assert.Equal("https", authorization["spr"]);
        DateTimeOffset from = DateTimeOffset.Parse(authorization["st"]!), until = DateTimeOffset.Parse(authorization["se"]!);
        Assert.Equal(TimeSpan.FromMinutes(65), until - from);
        Assert.InRange(until - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromHours(1));
        if (cli)
        {
            var result = Assert.IsType<StorageBlobCopyStartResult>(Assert.Single(fixture.Output));
            Assert.Equal(CopyId, result.CopyId); Assert.Equal(status, result.CopyStatus);
            Assert.Equal("\"copy-etag\"", result.ETag);
            Assert.Equal(DateTimeOffset.Parse("2026-10-22T00:00:00Z"), result.LastModified);
        }
        else
        {
            var result = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
            Assert.Equal(CopyId, result.Properties.CopyId); Assert.Equal(status, result.Properties.CopyStatus);
            Assert.Equal("dest", result.Container.Name); Assert.Equal("target", result.Name);
            Assert.DoesNotContain("sig=", result.Properties.CopySource);
            Assert.Null(result.Properties.CopyStatusDescription);
        }
        string output = JsonSerializer.Serialize(fixture.Output);
        Assert.DoesNotContain(authorization["sig"]!, output);
        Assert.DoesNotContain(Convert.ToBase64String(Key), output);
        Assert.DoesNotContain(fixture.Messages, item => item.Message.Contains("sig=", StringComparison.Ordinal));
        Assert.Empty(fixture.Progress);
    }

    [Theory]
    [InlineData("container")]
    [InlineData("blob")]
    [InlineData("share")]
    [InlineData("directory")]
    [InlineData("root-directory")]
    [InlineData("file")]
    public async Task NativeSourceFamiliesUseSelectedParentsProfilesAndSelectorsWithoutMutation(string family)
    {
        bool files = family is "share" or "directory" or "root-directory" or "file";
        var service = new CopyService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture, files);
        processor.SourceContext = null;
        var context = SourceContext(files);
        if (files)
        {
            processor.SourceShareName = null;
            var share = new StorageFileShare("share", context, "snapshot+/=&");
            switch (family)
            {
                case "share": processor.SourceShareObject = share; break;
                case "directory":
                    processor.SourceDirectoryObject = new StorageFileDirectory("parent", share);
                    break;
                case "root-directory":
                    processor.SourceDirectoryObject = new StorageFileDirectory("", share);
                    break;
                case "file":
                    processor.SourceFileObject = new StorageFile("file", share);
                    processor.SourcePath = null;
                    break;
            }
        }
        else
        {
            processor.SourceContainer = null;
            var container = new StorageBlobContainer("container", context);
            if (family == "blob")
            {
                processor.SourceBlobObject = new StorageBlob("blob", container, new BlobModels.BlobProperties()) { Snapshot = "snapshot+/=&" };
                processor.SourceBlob = null;
            }
            else processor.SourceContainerObject = container;
        }
        await processor.ProcessAsync();
        Assert.Same(context.Profile, fixture.TokenRequests.First().Profile);
        var query = HttpUtility.ParseQueryString(service.AuthorizedSource!.Query);
        Assert.Equal(files || family == "blob" ? "snapshot+/=&" : null, query[files ? "sharesnapshot" : "snapshot"]);
        Assert.Equal(family == "directory" ? "/share/parent/file" : files ? "/share/file" : "/container/blob", service.AuthorizedSource.AbsolutePath);
        Assert.Null(processor.SourceContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedSourceSasIsConsumedOnlyByDestinationAndNeverReturned(bool files)
    {
        var service = new CopyService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.SourceContext = null; processor.SourceContainer = null; processor.SourceBlob = null;
        string source = $"https://sourceaccount.{(files ? "file" : "blob")}.core.windows.net/{(files ? "share/file" : "container/blob")}?sv=2026-06-06&sp=r&sr={(files ? "f" : "b")}&sig={Secret}";
        processor.SourceUri = source;
        await processor.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
        Assert.Same(fixture.Profile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.Equal(new Uri(source), service.AuthorizedSource);
        var output = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal(source.Split('?')[0], output.Properties.CopySource);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(output));
    }

    [Fact]
    public async Task SourcePolicySasDoesNotRequireInlinePermissionsOrSourceTokenAcquisition()
    {
        var service = new CopyService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.SourceContainer = null; processor.SourceBlob = null;
        processor.SourceUri = $"https://sourceaccount.blob.core.windows.net/container/blob?sv=2026-06-06&si=read-policy&sig={Secret}";
        await processor.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task PresignedSourceCannotSilentlyIgnoreAnAdditionalSnapshotSelection()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.SourceContainer = null; processor.SourceBlob = null;
        processor.SourceUri = $"https://sourceaccount.blob.core.windows.net/container/blob?sv=2026-06-06&sp=r&sig={Secret}";
        processor.SourceSnapshot = "selected-snapshot";
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task UnspecifiedSourceContextUsesActiveProfileNotTheNativeDestinationCredential()
    {
        var service = new CopyService();
        var fixture = new Fixture(service.SendAsync, profile: SourceProfile);
        var processor = Processor(fixture);
        processor.SourceContext = null; processor.SourceStorageAccountName = "sourceaccount";
        await processor.ProcessAsync();
        Assert.Equal(new[] { SourceProfile, Fixture.DefaultProfile }, fixture.TokenRequests.Select(item => item.Profile));
        Assert.Same(fixture.Profile, fixture.Auth.Profile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeDestinationDoesNotChangeIndependentBlobOrFileSource(bool files)
    {
        var service = new CopyService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture, files);
        processor.Context = null; processor.Container = null; processor.Blob = null;
        processor.InputObject = new StorageBlob("native-target", new StorageBlobContainer("dest", BlobWorkflowTests.Context()),
            new BlobModels.BlobProperties());
        await processor.ProcessAsync();
        Assert.Equal("native-target", Assert.IsType<StorageBlob>(Assert.Single(fixture.Output)).Name);
        Assert.Equal(SourceProfile, fixture.TokenRequests.First().Profile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubsequentShowAndListDoNotExposePersistedCopySourceCredentials(bool listing)
    {
        string source = $"https://sourceaccount.blob.core.windows.net/container/blob?snapshot=selected&sv=2026-06-06&sp=r&sig={Secret}";
        var fixture = new Fixture(_ =>
        {
            if (listing)
            {
                var entry = BlobWorkflowTests.BlobElement("blob");
                entry.Element("Properties")!.Add(new XElement("CopySource", source), new XElement("CopyStatus", "failed"),
                    new XElement("CopyStatusDescription", source));
                return Task.FromResult(BlobWorkflowTests.Page(null, entry));
            }
            var response = BlobWorkflowTests.Head();
            response.Headers.Add("x-ms-copy-source", source); response.Headers.Add("x-ms-copy-status", "failed");
            response.Headers.Add("x-ms-copy-status-description", source);
            return Task.FromResult(response);
        });
        if (listing)
        {
            var processor = new ListStorageBlobsProcessor { Context = BlobWorkflowTests.Context(), Container = "container" };
            processor.Init(fixture.Context);
            await processor.ProcessAsync();
        }
        else
        {
            var processor = new GetStorageBlobProcessor { Context = BlobWorkflowTests.Context(), Container = "container", Blob = "blob" };
            processor.Init(fixture.Context);
            await processor.ProcessAsync();
        }
        var blob = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("failed", blob.Properties.CopyStatus);
        Assert.Equal("https://sourceaccount.blob.core.windows.net/container/blob?snapshot=selected", blob.Properties.CopySource);
        Assert.NotNull(blob.Properties.CopyStatusDescription);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(blob));
    }

    [Fact]
    public async Task UnsignedUriUsesExplicitSourceProfileAndPreservesSnapshot()
    {
        var service = new CopyService();
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.SourceContainer = null; processor.SourceBlob = null;
        processor.SourceUri = "https://sourceaccount.blob.core.windows.net/container/a%2Bb%25?snapshot=selector%2B%2F%3D%26";
        await processor.ProcessAsync();
        Assert.Equal("/container/a+b%", Uri.UnescapeDataString(service.AuthorizedSource!.AbsolutePath));
        Assert.Equal("selector+/=&", HttpUtility.ParseQueryString(service.AuthorizedSource.Query)["snapshot"]);
        Assert.Equal("bs", HttpUtility.ParseQueryString(service.AuthorizedSource.Query)["sr"]);
    }

    [Fact]
    public async Task ConditionsMetadataTagsAndLeaseUseExactFrozenWireAndOutputValues()
    {
        var metadata = new Dictionary<string, string> { ["owner"] = "one" };
        var tags = new Dictionary<string, string> { ["tag"] = "value +" };
        var service = new CopyService();
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                Assert.Equal("\"dest-etag\"", request.Headers.IfMatch.Single().ToString());
                Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
                Assert.Equal("\"source-etag\"", request.Headers.GetValues("x-ms-source-if-match").Single());
                Assert.Equal("one", request.Headers.GetValues("x-ms-meta-owner").Single());
                Assert.Equal("value +", HttpUtility.ParseQueryString(request.Headers.GetValues("x-ms-tags").Single())["tag"]);
                Assert.Equal("Wed, 01 Jan 2020 00:00:00 GMT", request.Headers.GetValues("x-ms-source-if-modified-since").Single());
                Assert.Equal("7", BlobWorkflowTests.Query(request)["timeout"]);
            }
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.Metadata = metadata; processor.Tags = tags; processor.Overwrite = true;
        processor.IfMatch = "\"dest-etag\""; processor.SourceIfMatch = "\"source-etag\"";
        processor.SourceIfModifiedSince = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
        processor.LeaseId = "lease"; processor.Timeout = 7;
        processor.ShouldProcess = (_, _) => { metadata["owner"] = "two"; tags["tag"] = "other"; processor.Tags = null; return true; };
        await processor.ProcessAsync();
        var output = Assert.IsType<StorageBlob>(Assert.Single(fixture.Output));
        Assert.Equal("one", output.Metadata["owner"]); Assert.Equal("value +", output.Tags["tag"]);
    }

    [Theory]
    [InlineData("BlockBlob", "Cool", true)]
    [InlineData("PageBlob", "P10", true)]
    [InlineData("AppendBlob", "Cool", false)]
    [InlineData("BlockBlob", "P10", false)]
    [InlineData("PageBlob", "Hot", false)]
    public async Task OAuthSourceTierCompatibilityIsCheckedBeforeSigning(string type, string tier, bool valid)
    {
        var service = new CopyService { BlobType = type };
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Tier = tier;
        if (valid)
        {
            await processor.ProcessAsync();
            Assert.Equal(3, fixture.Sends);
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
            Assert.Equal(1, fixture.Sends);
            Assert.Empty(fixture.Output);
        }
    }

    [Theory]
    [InlineData("source")]
    [InlineData("family")]
    [InlineData("account")]
    [InlineData("snapshot")]
    [InlineData("destination-snapshot")]
    [InlineData("overwrite")]
    [InlineData("source-condition-file")]
    [InlineData("intent")]
    [InlineData("tier")]
    [InlineData("header")]
    [InlineData("external-uri")]
    [InlineData("http-uri")]
    [InlineData("duplicate-query")]
    [InlineData("unknown-query")]
    public async Task InvalidInputsFailBeforeAuthentication(string invalid)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        switch (invalid)
        {
            case "source": processor.SourceBlob = null; break;
            case "family": processor.SourceShareName = "share"; break;
            case "account": processor.SourceStorageAccountName = "otheraccount"; break;
            case "snapshot": processor.SourceSnapshot = "one"; processor.SourceVersionId = "two"; break;
            case "destination-snapshot": processor.Snapshot = "snapshot"; break;
            case "overwrite": processor.IfMatch = "\"etag\""; break;
            case "source-condition-file":
            case "intent":
                processor.SourceContainer = null; processor.SourceBlob = null;
                processor.SourceShareName = "share"; processor.SourcePath = "file";
                if (invalid == "source-condition-file") { processor.EnableSourceFileBackupRequestIntent = true; processor.SourceIfMatch = "\"etag\""; }
                break;
            case "tier": processor.Tier = "Smart"; break;
            case "header": processor.SourceIfMatch = "private\r\nvalue"; break;
            default:
                processor.SourceContainer = null; processor.SourceBlob = null;
                processor.SourceUri = invalid switch
                {
                    "external-uri" => "https://example.com/container/blob",
                    "http-uri" => "http://sourceaccount.blob.core.windows.net/container/blob",
                    "duplicate-query" => "https://sourceaccount.blob.core.windows.net/container/blob?snapshot=a&snapshot=b",
                    _ => "https://sourceaccount.blob.core.windows.net/container/blob?unknown=value"
                };
                break;
        }
        await Assert.ThrowsAnyAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task WhatIfNeverAcquiresDelegationMaterialOrStartsCopy()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.ShouldProcess = (_, _) => false;
        await processor.ProcessAsync();
        Assert.Empty(fixture.TokenRequests); Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("oid")]
    [InlineData("tenant")]
    [InlineData("service")]
    [InlineData("version")]
    [InlineData("expiry")]
    [InlineData("start")]
    [InlineData("value")]
    [InlineData("delegated")]
    public void InvalidDelegationKeysCannotProduceAuthorization(string damage)
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-10-22T00:00:00Z"), expiry = start.AddHours(1);
        var key = DelegationKey(false, start, expiry);
        switch (damage)
        {
            case "oid": key.SignedOid = Secret; break;
            case "tenant": key.SignedTid = Guid.NewGuid().ToString(); break;
            case "service": key.SignedService = "f"; break;
            case "version": key.SignedVersion = "2025-07-05"; break;
            case "expiry": key.SignedExpiry = StorageCopyAuthorization.Time(expiry.AddSeconds(-1)); break;
            case "start": key.SignedStart = StorageCopyAuthorization.Time(start.AddSeconds(1)); break;
            case "value": key.Value = [1]; break;
            case "delegated": key.SignedDelegatedUserTid = Tenant; break;
        }
        var context = SourceContext();
        var error = Assert.Throws<InvalidDataException>(() =>
            StorageCopyAuthorization.Sign(context.GetBlobUri("container", "blob"), context, StorageService.Blob, key, start, expiry));
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.DoesNotContain(Convert.ToBase64String(Key), error.ToString());
    }

    [Theory]
    [InlineData(false, "xml")]
    [InlineData(true, "xml")]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "service")]
    [InlineData(true, "service")]
    [InlineData(false, "value")]
    [InlineData(true, "value")]
    public async Task MalformedSourceAuthorizationResponsesFailBeforeCopyWithoutSecretDetails(bool files, string damage)
    {
        var service = new CopyService();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Method == HttpMethod.Post)
            {
                if (damage == "xml") response.Content = new StringContent("<invalid>" + Secret);
                else if (files)
                    response.Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        keys = new[] { new
                        {
                            keyName = damage == "missing" ? null : "key1",
                            permissions = damage == "service" ? Secret : "Full",
                            value = damage == "value" ? Secret : Convert.ToBase64String(Key)
                        } }
                    }));
                else
                {
                    var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (damage == "missing") document.Root!.Element("SignedOid")!.Remove();
                    if (damage == "service") document.Root!.Element("SignedService")!.Value = Secret;
                    if (damage == "value") document.Root!.Element("Value")!.Value = Secret;
                    response.Content = new StringContent(document.ToString());
                }
            }
            return response;
        });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture, files).ProcessAsync());
        Assert.Equal(1, fixture.Sends);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.DoesNotContain(fixture.Messages, item => item.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("id")]
    [InlineData("state")]
    [InlineData("etag")]
    [InlineData("modified")]
    public async Task InvalidCopyAcknowledgmentsNeverLeakAuthorizedSourceOrEmitSuccess(string damage)
    {
        var service = new CopyService();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Method == HttpMethod.Put)
            {
                switch (damage)
                {
                    case "status": response.StatusCode = HttpStatusCode.OK; break;
                    case "id": response.Headers.Remove("x-ms-copy-id"); response.Headers.Add("x-ms-copy-id", Secret); break;
                    case "state": response.Headers.Remove("x-ms-copy-status"); response.Headers.Add("x-ms-copy-status", Secret); break;
                    case "etag": response.Headers.ETag = null; break;
                    case "modified": response.Content.Headers.LastModified = null; break;
                }
            }
            return response;
        });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessAsync());
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.DoesNotContain("sig=", error.ToString());
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelegationOrCopyFailureDoesNotTriggerKeyFallbackOrCompletionPolling(bool copy)
    {
        var service = new CopyService();
        var fixture = new Fixture(request =>
            (request.Method == HttpMethod.Put) == copy
                ? Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthorizationPermissionMismatch"))
                : service.SendAsync(request));
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Equal(copy ? 2 : 1, fixture.Sends);
        Assert.Empty(fixture.Output);
        Assert.All(fixture.TokenRequests, item => Assert.DoesNotContain("https://management.azure.com/.default", item.Scopes));
    }

    [Theory]
    [InlineData("before")]
    [InlineData("delegation")]
    [InlineData("copy")]
    public async Task CancellationNeverEmitsCopyState(string boundary)
    {
        using var cancellation = new CancellationTokenSource();
        if (boundary == "before") cancellation.Cancel();
        var service = new CopyService();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (boundary == "delegation" && request.Method == HttpMethod.Post ||
                boundary == "copy" && request.Method == HttpMethod.Put) cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.Output);
        if (boundary == "before") Assert.Empty(fixture.TokenRequests);
        if (boundary == "delegation") Assert.Equal(1, fixture.Sends);
    }

    private static StartStorageBlobCopyProcessor Processor(Fixture fixture, bool files = false)
    {
        var processor = new StartStorageBlobCopyProcessor
        {
            Context = BlobWorkflowTests.Context(), Container = "dest", Blob = "target", SourceContext = SourceContext(files)
        };
        if (files)
        {
            processor.SourceShareName = "share"; processor.SourcePath = "file";
            processor.UseSourceAccountKeyLookup = true; processor.SourceKeyResourceGroup = "source-rg";
        }
        else { processor.SourceContainer = "container"; processor.SourceBlob = "blob"; }
        processor.Init(fixture.Context);
        return processor;
    }

    private static BlobModels.UserDelegationKey DelegationKey(bool files, DateTimeOffset start, DateTimeOffset expiry) => new()
    {
        SignedOid = Oid, SignedTid = Tenant, SignedService = files ? "f" : "b", SignedVersion = "2026-06-06",
        SignedStart = StorageCopyAuthorization.Time(start), SignedExpiry = StorageCopyAuthorization.Time(expiry), Value = Key.ToArray()
    };

    internal sealed class CopyService
    {
        internal string Status { get; init; } = "pending";
        internal string BlobType { get; init; } = "BlockBlob";
        internal string SourceAccount { get; init; } = "sourceaccount";
        internal Uri? AuthorizedSource { get; private set; }
        internal string? DestinationIfNoneMatch { get; private set; }
        internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            Assert.True(request.HasSensitiveDiagnostics());
            if (request.RequestUri!.IdnHost != "management.azure.com")
                Assert.Equal("2026-06-06", request.Headers.GetValues("x-ms-version").Single());
            if (request.Method == HttpMethod.Head)
                return BlobWorkflowTests.Head(blobType: BlobType);
            if (request.Method == HttpMethod.Post)
            {
                if (request.RequestUri!.IdnHost == "management.azure.com")
                {
                    Assert.Contains("/resourceGroups/source-rg/", request.RequestUri.AbsolutePath);
                    Assert.Contains("/storageAccounts/" + SourceAccount + "/listKeys", request.RequestUri.AbsolutePath);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new
                        {
                            keys = new[] { new { keyName = "key1", permissions = "Full", value = Convert.ToBase64String(Key) } }
                        }))
                    };
                }
                Assert.Equal(SourceAccount, request.RequestUri!.IdnHost.Split('.')[0]);
                Assert.Equal("/", request.RequestUri.AbsolutePath);
                Assert.Equal("userdelegationkey", BlobWorkflowTests.Query(request)["comp"]);
                var body = XDocument.Parse(await request.Content!.ReadAsStringAsync()).Root!;
                bool files = request.RequestUri.IdnHost.Contains(".file.", StringComparison.Ordinal);
                if (files) Assert.Equal("backup", request.Headers.GetValues("x-ms-file-request-intent").Single());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(new XElement("UserDelegationKey",
                        new XElement("SignedOid", Oid), new XElement("SignedTid", Tenant),
                        new XElement("SignedStart", body.Element("Start")!.Value), new XElement("SignedExpiry", body.Element("Expiry")!.Value),
                        new XElement("SignedService", files ? "f" : "b"), new XElement("SignedVersion", "2026-06-06"),
                        new XElement("Value", Convert.ToBase64String(Key))).ToString())
                };
            }
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("fixture.blob.core.windows.net", request.RequestUri!.IdnHost);
            DestinationIfNoneMatch = request.Headers.IfNoneMatch.SingleOrDefault()?.ToString();
            AuthorizedSource = new Uri(request.Headers.GetValues("x-ms-copy-source").Single());
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent([]) };
            response.Headers.ETag = new EntityTagHeaderValue("\"copy-etag\"");
            response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-22T00:00:00Z");
            response.Headers.Add("x-ms-copy-id", CopyId); response.Headers.Add("x-ms-copy-status", Status);
            response.Headers.Add("x-ms-copy-source", AuthorizedSource.AbsoluteUri);
            response.Headers.Add("x-ms-copy-status-description", AuthorizedSource.AbsoluteUri);
            return response;
        }
    }
}
