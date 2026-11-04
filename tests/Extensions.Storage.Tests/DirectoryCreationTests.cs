// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class DirectoryCreationTests
{
    [Fact]
    public async Task NativeCreationSendsSmbDefaultsAndReturnsOnlyConfirmedPropertiesWithoutARead()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("fixture.file.core.windows.net", request.RequestUri!.IdnHost);
            Assert.Equal("/share/parent/new%252F%2B.", request.RequestUri.AbsolutePath);
            Assert.Equal("directory", BlobWorkflowTests.Query(request)["restype"]);
            Assert.Equal("9", BlobWorkflowTests.Query(request)["timeout"]);
            Assert.Null(BlobWorkflowTests.Query(request)["sharesnapshot"]);
            Assert.Equal("2026-06-06", request.Headers.GetValues("x-ms-version").Single());
            Assert.Equal("backup", request.Headers.GetValues("x-ms-file-request-intent").Single());
            Assert.Equal("true", request.Headers.GetValues("x-ms-allow-trailing-dot").Single());
            Assert.Equal("inherit", request.Headers.GetValues("x-ms-file-permission").Single());
            Assert.Equal("Directory", request.Headers.GetValues("x-ms-file-attributes").Single());
            Assert.Equal("now", request.Headers.GetValues("x-ms-file-creation-time").Single());
            Assert.Equal("now", request.Headers.GetValues("x-ms-file-last-write-time").Single());
            Assert.False(request.Headers.Contains("x-ms-file-permission-key"));
            Assert.False(request.Headers.Contains("x-ms-file-permission-format"));
            Assert.False(request.Headers.Contains("x-ms-owner"));
            Assert.False(request.Headers.Contains("x-ms-group"));
            Assert.False(request.Headers.Contains("x-ms-mode"));
            Assert.Null(request.Content);
            Assert.Equal("team", request.Headers.GetValues("x-ms-meta-owner").Single());
            return Task.FromResult(Created());
        });
        var processor = Processor(fixture);
        processor.Path = "parent/new%2F+."; processor.Timeout = 9; processor.AllowTrailingDot = true;
        processor.Metadata = new Dictionary<string, string> { ["owner"] = "team" };
        await processor.ProcessAsync();
        var directory = Assert.IsType<StorageFileDirectory>(Assert.Single(fixture.Output));
        Assert.Equal("parent/new%2F+.", directory.Path);
        Assert.True(directory.IsDirectory);
        Assert.Equal("\"created\"", directory.Properties.ETag);
        Assert.Equal("directory-id", directory.Properties.FileId);
        Assert.Equal("parent-id", directory.Properties.ParentId);
        Assert.Equal("Directory", directory.Properties.Attributes);
        Assert.Equal("permission-key", directory.Properties.PermissionKey);
        Assert.NotNull(directory.Properties.CreationTime);
        Assert.NotNull(directory.Properties.LastWriteTime);
        Assert.NotNull(directory.Properties.ChangeTime);
        Assert.Null(directory.Properties.ContentLength);
        Assert.Equal("team", directory.Properties.Metadata["OWNER"]);
        Assert.Equal(1, fixture.Sends);
        Assert.Equal(new[] { "https://storage.azure.com/.default" }, Assert.Single(fixture.TokenRequests).Scopes);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task OutputFailuresAreNotMistakenForAlreadyExistsResponses()
    {
        var fixture = new Fixture(_ => Task.FromResult(Created()));
        var failure = new HttpRequestException("Output failed.", null, HttpStatusCode.Conflict);
        failure.Data["ServiceErrorCode"] = "ResourceAlreadyExists";
        int writes = 0;
        Moq.Mock.Get(fixture.Context.OutputHandler).SetupGet(handler => handler.WriteOutput)
            .Returns((object _, CancellationToken _) =>
            {
                writes++;
                throw failure;
            });
        var processor = Processor(fixture);
        Assert.Same(failure, await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync()));
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task NativeCreationPropagatesExistingDirectoryFailures()
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Conflict, "ResourceAlreadyExists")));
        var processor = Processor(fixture);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal("ResourceAlreadyExists", error.Data["ServiceErrorCode"]);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData(403, "AuthorizationPermissionMismatch")]
    [InlineData(404, "ParentNotFound")]
    [InlineData(404, "ShareNotFound")]
    [InlineData(409, "ShareBeingDeleted")]
    [InlineData(409, null)]
    [InlineData(409, "ResourceTypeMismatch")]
    [InlineData(400, "UnsupportedQueryParameter")]
    public async Task OtherFailuresRemainFailuresAndNeverCreateParents(int status, string? code)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/share/missing/new", request.RequestUri!.AbsolutePath);
            return Task.FromResult(BlobWorkflowTests.Error((HttpStatusCode)status, code));
        });
        var processor = Processor(fixture);
        processor.Path = "missing/new";
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-response", error.ToString());
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task AmbiguousAppliedCreateIsNeverReportedAsConfirmedNewCreation()
    {
        int attempts = 0;
        var fixture = new Fixture(_ => Task.FromResult(++attempts == 1
            ? BlobWorkflowTests.Error(HttpStatusCode.ServiceUnavailable, "ServerBusy")
            : BlobWorkflowTests.Error(HttpStatusCode.Conflict, "ResourceAlreadyExists")));
        var processor = Processor(fixture);
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("///")]
    [InlineData("../outside")]
    [InlineData("parent/../new")]
    public async Task MissingRootOrDotPathsFailBeforeAuthentication(string? path)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Path = path;
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotMutationFailsBeforeAuthenticationIncludingNativeParents(bool native)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        if (native) processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", Context(), "snapshot"));
        else processor.ShareSnapshot = "snapshot";
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData("1invalid", "value")]
    [InlineData("valid", "private-value\r\n")]
    [InlineData("valid", "\u00ff")]
    public async Task InvalidMetadataFailsBeforeShouldProcessAndAuthentication(string key, string value)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Metadata = new Dictionary<string, string> { [key] = value };
        processor.ShouldProcess = (_, _) => throw new InvalidOperationException("ShouldProcess must not be called.");
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task CaseAliasedMetadataKeysAreRejectedRatherThanSilentlyMerged()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["owner"] = "one", ["OWNER"] = "two" };
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task WhatIfSendsNoRequestAndDoesNotEmitOutput()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.ShouldProcess = (target, operation) =>
        {
            Assert.Equal("https://fixture.file.core.windows.net/share/new", target);
            Assert.Equal("Create directory", operation);
            return false;
        };
        await processor.ProcessAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task NativeParentCarriesProfileAndAccountWithoutMutatingInputs()
    {
        var profile = new Core.Abstractions.Profile { Name = "source", TenantId = "source-tenant" };
        var fixture = new Fixture(request =>
        {
            Assert.Equal("sourceaccount.file.core.windows.net", request.RequestUri!.IdnHost);
            Assert.Equal("/share/parent/new", request.RequestUri.AbsolutePath);
            return Task.FromResult(Created());
        });
        var processor = Processor(fixture);
        processor.Context = null; processor.ShareName = null;
        processor.DirectoryObject = new StorageFileDirectory("parent", new StorageFileShare("share", new StorageContext("sourceaccount", profile, true)));
        await processor.ProcessAsync();
        Assert.Equal(profile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.Equal("parent/new", Assert.IsType<StorageFileDirectory>(Assert.Single(fixture.Output)).Path);
        Assert.Null(processor.Context);
        Assert.Null(processor.ShareName);
        Assert.Equal("new", processor.Path);
    }

    [Fact]
    public async Task MetadataIsFrozenBeforeConfirmationAndNotExposedAsAMutableDictionary()
    {
        var original = new Dictionary<string, string> { ["owner"] = "one" };
        var fixture = new Fixture(request =>
        {
            Assert.Equal("one", request.Headers.GetValues("x-ms-meta-owner").Single());
            original["owner"] = "three";
            return Task.FromResult(Created());
        });
        var processor = Processor(fixture);
        processor.Metadata = original;
        processor.ShouldProcess = (_, _) => { original["owner"] = "two"; return true; };
        await processor.ProcessAsync();
        var metadata = Assert.IsType<StorageFileDirectory>(Assert.Single(fixture.Output)).Properties.Metadata;
        Assert.Equal("one", metadata["owner"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)metadata)["owner"] = "four");
    }

    [Theory]
    [InlineData("status")]
    [InlineData("etag")]
    [InlineData("invalid-etag")]
    [InlineData("modified")]
    [InlineData("invalid-modified")]
    [InlineData("date")]
    public async Task MalformedCreationResponsesDoNotProduceNativeSuccess(string damage)
    {
        var fixture = new Fixture(_ =>
        {
            var response = Created();
            switch (damage)
            {
                case "status": response.StatusCode = HttpStatusCode.OK; break;
                case "etag": response.Headers.ETag = null; break;
                case "invalid-etag": response.Headers.ETag = null; response.Headers.TryAddWithoutValidation("ETag", "private-value"); break;
                case "modified": response.Content.Headers.LastModified = null; break;
                case "invalid-modified": response.Content.Headers.LastModified = null; response.Content.Headers.TryAddWithoutValidation("Last-Modified", "private-value"); break;
                case "date": response.Headers.Remove("x-ms-file-creation-time"); response.Headers.Add("x-ms-file-creation-time", "private-value"); break;
            }
            return Task.FromResult(response);
        });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessAsync());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task ExplicitBackupIntentIsRequiredBeforeMutation()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Context = new StorageContext("fixture", Fixture.DefaultProfile);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrAfterCreateEmitsNoSuccess(bool after)
    {
        using var cancellation = new CancellationTokenSource();
        if (!after) cancellation.Cancel();
        var fixture = new Fixture(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(Created());
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.Equal(after ? 1 : 0, fixture.Sends);
    }

    private static StorageContext Context() => new("fixture", Fixture.DefaultProfile, enableFileBackupRequestIntent: true);
    private static NewStorageDirectoryProcessor_Orchestrator Processor(Fixture fixture)
    {
        var processor = new NewStorageDirectoryProcessor_Orchestrator { Context = Context(), ShareName = "share", Path = "new" };
        processor.Init(fixture.Context);
        return processor;
    }
    private static HttpResponseMessage Created()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created) { Content = new ByteArrayContent([]) };
        response.Headers.ETag = new EntityTagHeaderValue("\"created\"");
        response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-21T00:00:00Z");
        response.Headers.Add("x-ms-file-attributes", "Directory");
        response.Headers.Add("x-ms-file-permission-key", "permission-key");
        response.Headers.Add("x-ms-file-id", "directory-id");
        response.Headers.Add("x-ms-file-parent-id", "parent-id");
        response.Headers.Add("x-ms-file-creation-time", "2026-10-21T00:00:00Z");
        response.Headers.Add("x-ms-file-last-write-time", "2026-10-21T00:00:00Z");
        response.Headers.Add("x-ms-file-change-time", "2026-10-21T00:00:00Z");
        return response;
    }
}
