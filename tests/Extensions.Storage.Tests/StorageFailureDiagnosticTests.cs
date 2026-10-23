// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class StorageFailureDiagnosticTests
{
    [Theory]
    [InlineData("AuthenticationFailed", "403", true, true)]
    [InlineData("sig=private-secret", "403", false, true)]
    [InlineData("AuthenticationFailed", "private-secret", true, false)]
    [InlineData("AuthenticationFailed", "999", true, false)]
    public async Task DeferredFailureValidatesCopySourceDiagnostics(string code, string status, bool codeDisplayed, bool statusDisplayed)
    {
        var fixture = new Fixture(_ =>
        {
            var response = BlobWorkflowTests.Error(HttpStatusCode.BadRequest, "CannotVerifyCopySource");
            response.Headers.Add("x-ms-copy-source-error-code", code);
            response.Headers.Add("x-ms-copy-source-status-code", status);
            response.Headers.Add("x-ms-copy-source-error-message", "sig=private-secret");
            return Task.FromResult(response);
        });
        var command = new ShowStorageBlobCommand { StorageAccountName = "fixture", Container = "container", Blob = "blob" };
        command.Init(fixture.Context);
        await Assert.ThrowsAsync<HttpRequestException>(() => command.ProcessAsync());
        string message = Assert.Single(fixture.Messages, item => item.Id == Events.ResourceOperationFailed).Message;
        Assert.Contains("Storage error code: CannotVerifyCopySource.", message);
        Assert.Equal(codeDisplayed, message.Contains("Copy source error code: AuthenticationFailed.", StringComparison.Ordinal));
        Assert.Equal(statusDisplayed, message.Contains("Copy source HTTP status: 403.", StringComparison.Ordinal));
        Assert.DoesNotContain("private", message, StringComparison.Ordinal);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("InvalidHeaderValue", true)]
    [InlineData("sig=private-secret", false)]
    public async Task DeferredFailureShowsOnlyValidatedServiceCodeAndRequestId(string code, bool displayed)
    {
        const string id = "11111111-2222-3333-4444-555555555555";
        var fixture = new Fixture(_ =>
        {
            var response = BlobWorkflowTests.Error(HttpStatusCode.BadRequest, code);
            response.Headers.Add("x-ms-request-id", id);
            return Task.FromResult(response);
        });
        var command = new ShowStorageBlobCommand { StorageAccountName = "fixture", Container = "container", Blob = "blob" };
        command.Init(fixture.Context);
        await Assert.ThrowsAsync<HttpRequestException>(() => command.ProcessAsync());
        string message = Assert.Single(fixture.Messages, item => item.Id == Events.ResourceOperationFailed).Message;
        Assert.Contains(id, message);
        Assert.Equal(displayed, message.Contains("Storage error code: InvalidHeaderValue.", StringComparison.Ordinal));
        Assert.DoesNotContain("private", message, StringComparison.Ordinal);
        Assert.Empty(fixture.Output);
    }
}
