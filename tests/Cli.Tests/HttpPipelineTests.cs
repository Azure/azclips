// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Xunit;

namespace Cli.Tests;

public class HttpPipelineTests
{
    [Fact]
    public async Task SendAsync_DisableRetry_SendsStreamingRequestOnce()
    {
        int sendCount = 0;
        var terminal = new SendAsyncTerminalFactory((_, _) =>
        {
            sendCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });
        var pipeline = new HttpPipeline(terminal, maxRetry: 3, maxRetryFor429: 5);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://account.blob.core.windows.net/container/blob")
        {
            Content = new ThrowOnCopyContent()
        };
        request.Options.Set(RuntimeHttpRequestOptions.DisableRetry, true);

        using HttpResponseMessage response = await pipeline.SendAsync(request, new FakeEventPublisher());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, sendCount);
    }

    private sealed class ThrowOnCopyContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("Request content must not be cloned.");

        protected override bool TryComputeLength(out long length)
        {
            length = 3;
            return true;
        }
    }
}
