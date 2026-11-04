// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobRemovalPowerShellOrchestratorTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("include", false)]
    [InlineData("include", true)]
    [InlineData("only", false)]
    [InlineData("only", true)]
    public async Task DeleteUsesExactlyTheSelectedPolicyAndOutputsOnlyRequestedSuccess(string? mode, bool passThru)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("/container/literal%2A", request.RequestUri!.AbsolutePath);
            Assert.Equal(mode, request.Headers.TryGetValues("x-ms-delete-snapshots", out var values) ? Assert.Single(values) : null);
            Assert.Equal("\"team\"='one'", Assert.Single(request.Headers.GetValues("x-ms-if-tags")));
            Assert.Equal("9", BlobWorkflowTests.Query(request)["timeout"]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        var command = Command(); command.DeleteSnapshots = mode; command.PassThru = passThru;
        command.IfTags = "\"team\"='one'"; command.Timeout = 9; command.Blob = "literal*";
        await Run(command, fixture);
        if (passThru) Assert.True(Assert.IsType<bool>(Assert.Single(fixture.Output)));
        else Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
        Assert.Equal(["https://storage.azure.com/.default"], Assert.Single(fixture.TokenRequests).Scopes);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactSelectorsDoNotBroadenTheDeletion(bool snapshot)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(snapshot ? "snapshot" : null, BlobWorkflowTests.Query(request)["snapshot"]);
            Assert.Equal(snapshot ? null : "version", BlobWorkflowTests.Query(request)["versionid"]);
            Assert.False(request.Headers.Contains("x-ms-delete-snapshots"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        var command = Command(); command.Snapshot = snapshot ? "snapshot" : null; command.VersionId = snapshot ? null : "version";
        await Run(command, fixture);
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task NativeInputKeepsItsOwnContextAndSelector()
    {
        var context = BlobWorkflowTests.Context();
        var input = new StorageBlob("item", new("container", context), new()) { VersionId = "version" };
        var fixture = new Fixture(request =>
        {
            Assert.Equal("version", BlobWorkflowTests.Query(request)["versionid"]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        var command = new RemoveStorageBlobPowerShellProcessor_Orchestrator { InputObject = input, PassThru = true };
        Assert.Contains("versionid=version", command.GetConfirmationTarget());
        await Run(command, fixture);
        Assert.True(Assert.IsType<bool>(Assert.Single(fixture.Output)));
        Assert.Equal(context.Profile, Assert.Single(fixture.TokenRequests).Profile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyPreciseBaseSnapshotConflictBecomesAHandledControlException(bool passThru)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Conflict, "SnapshotsPresent")));
        var command = Command(); command.PassThru = passThru;
        var conflict = await Assert.ThrowsAsync<StorageBlobSnapshotsPresentException>(() => Run(command, fixture));
        Assert.Equal("item", conflict.BlobName);
        Assert.Same(command.Context, conflict.Container.Context);
        Assert.Equal("https://fixture.blob.core.windows.net/container/item", conflict.BlobUri.AbsoluteUri);
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.DoesNotContain("private-response", conflict.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, fixture.Sends);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "LeaseIdMissing")]
    [InlineData(HttpStatusCode.Conflict, null)]
    [InlineData(HttpStatusCode.Conflict, "snapshotspresent")]
    [InlineData(HttpStatusCode.Forbidden, "AuthorizationFailure")]
    [InlineData(HttpStatusCode.NotFound, "BlobNotFound")]
    [InlineData(HttpStatusCode.BadRequest, "SnapshotsPresent")]
    public async Task OtherFailuresAreNotConfirmationOrSuccess(HttpStatusCode status, string? code)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(status, code)));
        var command = Command(); command.PassThru = true;
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Run(command, fixture));
        Assert.Equal(status, error.StatusCode);
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.DoesNotContain("private-response", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("version")]
    [InlineData("include")]
    [InlineData("only")]
    public async Task ASelectedOrAlreadyBroadenedRequestCannotRequestAnotherConfirmation(string selection)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Conflict, "SnapshotsPresent")));
        var command = Command();
        if (selection == "snapshot") command.Snapshot = "snapshot";
        else if (selection == "version") command.VersionId = "version";
        else command.DeleteSnapshots = selection;
        await Assert.ThrowsAsync<HttpRequestException>(() => Run(command, fixture));
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task AmbiguousServiceErrorHeadersAreNeverAConfirmationSignal()
    {
        var fixture = new Fixture(_ =>
        {
            var response = BlobWorkflowTests.Error(HttpStatusCode.Conflict, null);
            response.Headers.TryAddWithoutValidation("x-ms-error-code", new[] { "SnapshotsPresent", "LeaseIdMissing" });
            return Task.FromResult(response);
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => Run(Command(), fixture));
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("selectors")]
    [InlineData("mode")]
    [InlineData("snapshot-mode")]
    [InlineData("context")]
    [InlineData("timeout")]
    public async Task PreflightUsesCoreValidationWithoutInitializationOrAuthentication(string invalid)
    {
        var command = Command();
        if (invalid == "selectors") { command.Snapshot = "snapshot"; command.VersionId = "version"; }
        if (invalid == "mode") command.DeleteSnapshots = "all";
        if (invalid == "snapshot-mode") { command.Snapshot = "snapshot"; command.DeleteSnapshots = "only"; }
        if (invalid == "context") command.ContainerObject = new("container", new("other", Fixture.DefaultProfile));
        if (invalid == "timeout") command.Timeout = 0;
        Assert.Throws<ArgumentException>(() => command.GetConfirmationTarget());
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => Run(command, fixture));
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public void PreflightWithoutANativeContextFailsExplicitly() =>
        Assert.Throws<ArgumentException>(() => new RemoveStorageBlobPowerShellProcessor_Orchestrator
        {
            Container = "container", Blob = "item"
        }.GetConfirmationTarget());

    [Fact]
    public async Task PendingRequestCannotChangeItsSnapshotConflictPolicy()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Fixture(async _ =>
        {
            entered.SetResult(); await release.Task;
            return BlobWorkflowTests.Error(HttpStatusCode.Conflict, "SnapshotsPresent");
        });
        var command = Command();
        var task = Run(command, fixture);
        await entered.Task;
        command.DeleteSnapshots = "include";
        command.Blob = "other";
        command.Context = new("other", Fixture.DefaultProfile);
        release.SetResult();
        var conflict = await Assert.ThrowsAsync<StorageBlobSnapshotsPresentException>(() => task);
        Assert.Equal("item", conflict.BlobName);
        Assert.Equal("fixture", conflict.Container.Context.StorageAccountName);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task CancellationAfterConflictNeverRequestsConfirmation()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Conflict, "SnapshotsPresent"));
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(Command(), fixture));
        Assert.Empty(fixture.Output);
        Assert.Equal(1, fixture.Sends);
    }

    private static RemoveStorageBlobPowerShellProcessor_Orchestrator Command() => new()
    {
        Context = BlobWorkflowTests.Context(), Container = "container", Blob = "item"
    };

    private static async Task Run(RemoveStorageBlobPowerShellProcessor_Orchestrator command, Fixture fixture)
    {
        command.Init(fixture.Context);
        await command.ProcessAsync();
    }
}
