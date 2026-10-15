// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Xunit;

namespace Cli.Tests;

public class HttpRuntimeLroTests
{
    private const string ResourceUri =
        "https://management.azure.com/subscriptions/s/resourceGroups/rg/providers/Microsoft.Test/resources/name?api-version=2025-01-01";

    [Theory]
    [InlineData(
        """{"status":"Failed","error":{"code":"OperationFailed","message":"The operation failed."}}""",
        "Failed",
        "OperationFailed: The operation failed.")]
    [InlineData(
        """{"properties":{"provisioningState":"Canceled","error":{"code":"OperationCanceled","message":"The operation was canceled."}}}""",
        "Canceled",
        "OperationCanceled: The operation was canceled.")]
    public async Task SendAsyncLro_TerminalFailure_UsesArmDetailAndDisposesResponse(
        string body,
        string terminalState,
        string expectedDetail)
    {
        var terminalResponse = new TrackingResponse(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        var pipeline = CreatePollingPipeline(terminalResponse);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendAsyncLro(pipeline));

        Assert.Contains($"terminal provisioningState '{terminalState}'", error.Message);
        Assert.Contains(ResourceUri, error.Message);
        Assert.Contains(expectedDetail, error.Message);
        Assert.DoesNotContain("sig=secret", error.Message);
        Assert.True(terminalResponse.WasDisposed);
    }

    private static FakeHttpPipeline CreatePollingPipeline(HttpResponseMessage terminalResponse)
    {
        int call = 0;
        return new FakeHttpPipeline((_, _) =>
        {
            call++;
            if (call == 1)
            {
                var accepted = Json(HttpStatusCode.Accepted, "{}");
                accepted.Headers.Location =
                    new Uri("https://management.azure.com/operations/123?sig=secret");
                return accepted;
            }

            return terminalResponse;
        })
        {
            PollingInterval = 0,
        };
    }

    private static Task<HttpResponseMessage> SendAsyncLro(FakeHttpPipeline pipeline) =>
        HttpRuntime.SendAsyncLRO(
            () => Task.FromResult(new HttpRequestMessage(HttpMethod.Put, ResourceUri)),
            new FakeEventPublisher(),
            pipeline,
            GetLocation,
            _ => null,
            _ => null,
            GetLocation);

    private static string? GetLocation(HttpResponseMessage response) =>
        response.Headers.Location?.AbsoluteUri;

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class TrackingResponse(HttpStatusCode statusCode) : HttpResponseMessage(statusCode)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
