// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using System.Web;
using Core.Abstractions.Runtime;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobCopyOrchestratorTests
{
    private const string Secret = "private-source-signature";
    private const string Sas = "sv=2026-06-06&sp=r&sig=" + Secret;
    private const string BlobUri = "https://sourceaccount.blob.core.windows.net/container/blob";
    private static Profile SelectedProfile => new() { Name = "selected", TenantId = BlobCopyTests.Tenant, CloudName = "AzureCloud" };

    [Theory]
    [InlineData(false, "pending")]
    [InlineData(false, "success")]
    [InlineData(false, "failed")]
    [InlineData(false, "aborted")]
    [InlineData(true, "pending")]
    public async Task NamedSourcesUseSelectedAuthorizationAndReturnOnlyTheFourAcknowledgmentFields(bool files, string status)
    {
        var service = new BlobCopyTests.CopyService { Status = status };
        var fixture = new Fixture(service.SendAsync, profile: SelectedProfile);
        var command = Command();
        if (files)
        {
            command.SourceShareName = "share"; command.SourcePath = "dir/file";
            command.EnableSourceFileBackupRequestIntent = true;
            command.SourceAccountKey = Convert.ToBase64String(new byte[32]);
        }
        else { command.SourceContainer = "container"; command.SourceBlob = "blob"; }
        await Run(command, fixture);
        var output = Assert.IsType<JsonElement>(Assert.Single(fixture.Output));
        Assert.Equal(["copyId", "copyStatus", "etag", "lastModified"], output.EnumerateObject().Select(property => property.Name));
        Assert.Equal(BlobCopyTests.CopyId, output.GetProperty("copyId").GetString());
        Assert.Equal(status, output.GetProperty("copyStatus").GetString());
        Assert.Equal("\"copy-etag\"", output.GetProperty("etag").GetString());
        Assert.Equal("2026-10-22T00:00:00Z", output.GetProperty("lastModified").GetString());
        Assert.Null(service.DestinationIfNoneMatch);
        Assert.Equal(files ? 1 : 2, fixture.Sends);
        Assert.All(fixture.TokenRequests, request => Assert.Equal("selected", request.Profile.Name));
        Assert.All(fixture.TokenRequests, request => Assert.Equal(["https://storage.azure.com/.default"], request.Scopes));
        var query = HttpUtility.ParseQueryString(service.AuthorizedSource!.Query);
        Assert.Equal("r", query["sp"]);
        Assert.Equal(files ? null : "b", query["sks"]);
        Assert.DoesNotContain(query["sig"]!, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Messages, message => message.Message.Contains("sig=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("embedded")]
    [InlineData("separate-uri")]
    [InlineData("named-blob")]
    [InlineData("named-file")]
    [InlineData("same-account")]
    public async Task SuppliedSourceSasAvoidsSourceAuthenticationAndNeverReachesOutput(string mode)
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(service.SendAsync, profile: SelectedProfile);
        var command = Command();
        if (mode == "embedded") command.SourceUri = BlobUri + "?" + Sas;
        else
        {
            command.SourceSas = "?" + Sas;
            if (mode == "separate-uri") command.SourceUri = BlobUri;
            else if (mode == "named-file") { command.SourceShareName = "share"; command.SourcePath = "/dir/file"; }
            else { command.SourceContainer = "container"; command.SourceBlob = "blob"; }
            if (mode == "same-account") command.SourceStorageAccountName = null;
        }
        await Run(command, fixture);
        Assert.Equal(1, fixture.Sends);
        Assert.Single(fixture.TokenRequests);
        var source = service.AuthorizedSource!;
        Assert.Equal(Secret, HttpUtility.ParseQueryString(source.Query)["sig"]);
        Assert.Equal(mode == "same-account" ? "fixture" : "sourceaccount", source.Host.Split('.')[0]);
        Assert.Equal(mode == "named-file" ? "/share/dir/file" : "/container/blob", source.AbsolutePath);
        Assert.DoesNotContain(Secret, Assert.Single(fixture.Output).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Messages, message => message.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("versionid")]
    [InlineData("sharesnapshot")]
    public async Task SeparateSasPreservesOrAddsExactlyOneMatchingSelector(string selector)
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(service.SendAsync, profile: SelectedProfile);
        var command = Command();
        command.SourceSas = Sas + "&" + selector + "=selected%2B%2F%3D";
        if (selector == "sharesnapshot")
        {
            command.SourceShareName = "share"; command.SourcePath = "file";
            command.SourceShareSnapshot = "selected+/=";
        }
        else
        {
            command.SourceUri = BlobUri + "?" + selector + "=selected%2B%2F%3D";
            if (selector == "snapshot") command.SourceSnapshot = "selected+/=";
            else command.SourceVersionId = "selected+/=";
        }
        await Run(command, fixture);
        var query = HttpUtility.ParseQueryString(service.AuthorizedSource!.Query);
        Assert.Equal("selected+/=", query[selector]);
        Assert.Single(query.GetValues(selector)!);
        Assert.Equal(Secret, query["sig"]);
    }

    [Fact]
    public async Task MetadataTagsAndBothConditionFamiliesReachTheCopyRequest()
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(request =>
        {
            Assert.Equal("9", BlobWorkflowTests.Query(request)["timeout"]);
            Assert.Equal("\"dest\"", request.Headers.IfMatch!.ToString());
            Assert.Equal("\"dest-other\"", Assert.Single(request.Headers.IfNoneMatch).ToString());
            Assert.Equal("lease", Assert.Single(request.Headers.GetValues("x-ms-lease-id")));
            Assert.Equal("\"source\"", Assert.Single(request.Headers.GetValues("x-ms-source-if-match")));
            Assert.Equal("\"source-other\"", Assert.Single(request.Headers.GetValues("x-ms-source-if-none-match")));
            Assert.Equal("Tue, 20 Oct 2026 00:00:00 GMT", Assert.Single(request.Headers.GetValues("x-ms-source-if-modified-since")));
            Assert.Equal("Wed, 21 Oct 2026 00:00:00 GMT", Assert.Single(request.Headers.GetValues("x-ms-source-if-unmodified-since")));
            Assert.Equal(DateTimeOffset.Parse("2026-10-20T00:00:00Z"), request.Headers.IfModifiedSince);
            Assert.Equal(DateTimeOffset.Parse("2026-10-21T00:00:00Z"), request.Headers.IfUnmodifiedSince);
            Assert.Equal("\"a\"='b'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
            Assert.Equal("\"c\"='d'", Assert.Single(request.Headers.GetValues("x-ms-source-if-tags")));
            Assert.Equal("value=tail", Assert.Single(request.Headers.GetValues("x-ms-meta-team")));
            Assert.Equal("", Assert.Single(request.Headers.GetValues("x-ms-meta-empty")));
            var tags = HttpUtility.ParseQueryString(Assert.Single(request.Headers.GetValues("x-ms-tags")));
            Assert.Equal("", tags["bare"]);
            Assert.Equal("a=b", tags["team"]);
            return service.SendAsync(request);
        }, profile: SelectedProfile);
        var command = Signed();
        command.Timeout = 9; command.LeaseId = "lease"; command.IfMatch = "\"dest\""; command.IfNoneMatch = "\"dest-other\"";
        command.IfModifiedSinceInput = command.SourceIfModifiedSinceInput = "2026-10-20";
        command.IfUnmodifiedSinceInput = command.SourceIfUnmodifiedSinceInput = "2026-10-21T02:00:00+02:00";
        command.SourceIfMatch = "\"source\""; command.SourceIfNoneMatch = "\"source-other\"";
        command.IfTags = "\"a\"='b'"; command.SourceIfTags = "\"c\"='d'";
        command.MetadataInput = ["team=value=tail", "empty="]; command.TagsInput = ["bare", "team=a=b"];
        await Run(command, fixture);
        Assert.Single(fixture.Output);
    }

    [Fact]
    public async Task ExplicitAbsentDestinationProtectionIsNotOverwrittenByCliOverwriteDefault()
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(service.SendAsync, profile: SelectedProfile);
        var command = Signed(); command.IfNoneMatch = "*";
        command.StorageAccountName = null; command.BlobEndpoint = "https://fixture.blob.core.windows.net/";
        await Run(command, fixture);
        Assert.Equal("*", service.DestinationIfNoneMatch);
    }

    [Fact]
    public async Task OmittedNamedSourceAccountUsesTheResolvedDestinationAccountForOAuth()
    {
        var service = new BlobCopyTests.CopyService { SourceAccount = "fixture" };
        var fixture = new Fixture(service.SendAsync, profile: SelectedProfile);
        var command = Command();
        command.SourceStorageAccountName = null; command.SourceContainer = "container"; command.SourceBlob = "blob";
        command.StorageAccountName = null; command.BlobEndpoint = "https://fixture.blob.core.windows.net/";
        await Run(command, fixture);
        Assert.Equal("fixture.blob.core.windows.net", service.AuthorizedSource!.Host);
        Assert.Null(HttpUtility.ParseQueryString(service.AuthorizedSource.Query)["sig"]);
        Assert.Equal(1, fixture.Sends);
        Assert.Equal(SelectedProfile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.Single(fixture.Output);
    }

    [Fact]
    public async Task NamedSasSourcesEscapeNamesExactlyOnceWithoutLosingSignatureCharacters()
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(service.SendAsync, profile: SelectedProfile);
        var command = Command();
        command.SourceContainer = "container"; command.SourceBlob = "dir/\u96ea %2F+?#";
        command.SourceSas = "sv=2026-06-06&sp=r&sig=a%2Bb%2Fc%3D";
        await Run(command, fixture);
        Assert.Equal("/container/dir/\u96ea %2F+?#", Uri.UnescapeDataString(service.AuthorizedSource!.AbsolutePath));
        Assert.Equal("a+b/c=", HttpUtility.ParseQueryString(service.AuthorizedSource.Query)["sig"]);
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task PendingDelegationFreezesCliMetadataSourceAndDestinationControls()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                entered.SetResult();
                await release.Task;
            }
            else
            {
                Assert.Equal("one", Assert.Single(request.Headers.GetValues("x-ms-meta-team")));
                Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).ToString());
                Assert.Equal("/dest/target", request.RequestUri!.AbsolutePath);
            }
            return await service.SendAsync(request);
        }, profile: SelectedProfile);
        var command = Command();
        command.SourceContainer = "container"; command.SourceBlob = "blob";
        command.MetadataInput = ["team=one"]; command.IfNoneMatch = "*";
        var pending = Run(command, fixture);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        command.MetadataInput[0] = "team=two";
        command.SourceBlob = "other"; command.SourceStorageAccountName = "other";
        command.Blob = "other"; command.Container = "other"; command.IfNoneMatch = null;
        release.SetResult();
        await pending;
        Assert.Equal("/container/blob", service.AuthorizedSource!.AbsolutePath);
        Assert.Single(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleTierUsesTheExistingSourceTypeAndSigningWorkflow(bool files)
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(service.SendAsync, profile: SelectedProfile);
        var command = Command(); command.Tier = "Cool"; command.RehydratePriority = "High";
        if (files)
        {
            command.SourceShareName = "share"; command.SourcePath = "file"; command.EnableSourceFileBackupRequestIntent = true;
            command.SourceAccountKey = Convert.ToBase64String(new byte[32]);
        }
        else { command.SourceContainer = "container"; command.SourceBlob = "blob"; }
        await Run(command, fixture);
        Assert.Equal(files ? 1 : 3, fixture.Sends);
        Assert.Single(fixture.Output);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("families")]
    [InlineData("partial")]
    [InlineData("intent")]
    [InlineData("file-condition")]
    [InlineData("selectors")]
    [InlineData("service-selector")]
    [InlineData("source-date")]
    [InlineData("destination-date")]
    [InlineData("tier")]
    [InlineData("priority")]
    [InlineData("account")]
    [InlineData("auth")]
    [InlineData("endpoint")]
    [InlineData("timeout")]
    [InlineData("destination-selector")]
    [InlineData("header")]
    public async Task InvalidSelectionsFailBeforeAuthentication(string invalid)
    {
        var command = Signed();
        switch (invalid)
        {
            case "source": command.SourceUri = null; break;
            case "families": command.SourceContainer = "container"; command.SourceBlob = "blob"; break;
            case "partial": command.SourceUri = null; command.SourceContainer = "container"; break;
            case "intent": command.SourceUri = null; command.SourceShareName = "share"; command.SourcePath = "file"; break;
            case "file-condition":
                command.SourceUri = "https://sourceaccount.file.core.windows.net/share/file?" + Sas;
                command.SourceIfMatch = "\"etag\""; break;
            case "selectors": command.SourceSnapshot = "one"; command.SourceVersionId = "two"; break;
            case "service-selector": command.SourceShareSnapshot = "one"; break;
            case "source-date": command.SourceIfModifiedSinceInput = Secret; break;
            case "destination-date": command.IfUnmodifiedSinceInput = Secret; break;
            case "tier": command.Tier = "invalid"; break;
            case "priority": command.Tier = "P4"; command.RehydratePriority = "High"; break;
            case "account": command.SourceStorageAccountName = "other"; break;
            case "auth": command.AuthMode = "key"; break;
            case "endpoint": command.BlobEndpoint = "https://fixture.blob.core.windows.net/path"; break;
            case "timeout": command.Timeout = 0; break;
            case "destination-selector": command.Snapshot = "snapshot"; break;
            case "header": command.SourceIfMatch = "bad\r\n" + Secret; break;
        }
        var fixture = new Fixture(profile: SelectedProfile);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => Run(command, fixture));
        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
        Assert.Equal(0, fixture.Sends);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("already-authorized")]
    [InlineData("duplicate")]
    [InlineData("selector-conflict")]
    [InlineData("uri-selector-conflict")]
    [InlineData("null-name")]
    [InlineData("empty-name")]
    [InlineData("missing-sig")]
    [InlineData("no-read")]
    [InlineData("family")]
    [InlineData("service")]
    [InlineData("origin")]
    public async Task SeparateSasNeverOverridesAuthorizationOrConflictingResources(string invalid)
    {
        var command = Command(); command.SourceUri = BlobUri; command.SourceSas = Sas;
        switch (invalid)
        {
            case "empty": command.SourceSas = ""; break;
            case "already-authorized": command.SourceUri += "?" + Sas; break;
            case "duplicate": command.SourceSas += "&sig=other"; break;
            case "selector-conflict": command.SourceSas += "&snapshot=one"; command.SourceSnapshot = "two"; break;
            case "uri-selector-conflict": command.SourceUri += "?snapshot=one"; command.SourceSnapshot = "two"; break;
            case "null-name": command.SourceSas += "&" + Secret; break;
            case "empty-name": command.SourceSas += "&=value"; break;
            case "missing-sig": command.SourceSas = "sv=2026-06-06&sp=r"; break;
            case "no-read": command.SourceSas = "sv=2026-06-06&sp=w&sig=" + Secret; break;
            case "family": command.SourceBlob = "other"; break;
            case "service": command.SourceShareSnapshot = "share"; break;
            case "origin": command.SourceUri = "https://sourceaccount.blob.core.windows.net.example/container/blob"; break;
        }
        var fixture = new Fixture(profile: SelectedProfile);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => Run(command, fixture));
        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData(false, "bare")]
    [InlineData(false, "=value")]
    [InlineData(false, "bad-key=value")]
    [InlineData(true, "=value")]
    [InlineData(true, "tag=invalid!")]
    public async Task InvalidKeyValueInputDoesNotEchoValuesOrAuthenticate(bool tags, string value)
    {
        var command = Signed();
        if (tags) command.TagsInput = [value]; else command.MetadataInput = [value];
        var fixture = new Fixture(profile: SelectedProfile);
        await Assert.ThrowsAsync<ArgumentException>(() => Run(command, fixture));
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateKeysAndEmptyArraysAreRejected(bool tags)
    {
        foreach (bool empty in new[] { false, true })
        {
            var command = Signed();
            string[] values = empty ? [] : ["team=one", tags ? "team=two" : "TEAM=two"];
            if (tags) command.TagsInput = values; else command.MetadataInput = values;
            var fixture = new Fixture(profile: SelectedProfile);
            await Assert.ThrowsAsync<ArgumentException>(() => Run(command, fixture));
            Assert.Empty(fixture.TokenRequests);
        }
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("delegation")]
    [InlineData("ack")]
    public async Task FailuresProduceOneSafeDiagnosticAndNoCliResult(string boundary)
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(async request =>
        {
            if (boundary == "copy" && request.Method == HttpMethod.Put ||
                boundary == "delegation" && request.Method == HttpMethod.Post)
                return BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthorizationFailure");
            var response = await service.SendAsync(request);
            if (boundary == "ack" && request.Method == HttpMethod.Put)
            {
                response.Headers.Remove("x-ms-copy-status");
                response.Headers.Add("x-ms-copy-status", Secret);
            }
            return response;
        }, profile: SelectedProfile);
        var command = boundary == "delegation" ? Command() : Signed();
        if (boundary == "delegation") { command.SourceContainer = "container"; command.SourceBlob = "blob"; }
        var error = await Assert.ThrowsAnyAsync<Exception>(() => Run(command, fixture));
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.DoesNotContain(fixture.TokenRequests, request => request.Scopes.Contains("https://management.azure.com/.default"));
    }

    [Theory]
    [InlineData("before")]
    [InlineData("delegation")]
    [InlineData("copy")]
    public async Task CancellationNeverPublishesAnAcknowledgment(string boundary)
    {
        using var cancellation = new CancellationTokenSource();
        if (boundary == "before") cancellation.Cancel();
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (boundary == "delegation" && request.Method == HttpMethod.Post ||
                boundary == "copy" && request.Method == HttpMethod.Put) cancellation.Cancel();
            return response;
        }, profile: SelectedProfile, token: cancellation.Token);
        var command = Command(); command.SourceContainer = "container"; command.SourceBlob = "blob";
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(command, fixture));
        Assert.Empty(fixture.Output);
        if (boundary == "before") Assert.Empty(fixture.TokenRequests);
        if (boundary == "delegation") Assert.Equal(1, fixture.Sends);
    }

    private static StartStorageBlobCopyProcessor_Orchestrator Command() => new()
    {
        StorageAccountName = "fixture", Container = "dest", Blob = "target", SourceStorageAccountName = "sourceaccount"
    };

    private static StartStorageBlobCopyProcessor_Orchestrator Signed()
    {
        var command = Command(); command.SourceUri = BlobUri + "?" + Sas; return command;
    }

    private static async Task Run(StartStorageBlobCopyProcessor_Orchestrator command, Fixture fixture)
    {
        command.Init(fixture.Context);
        await command.ProcessAsync();
    }
}
