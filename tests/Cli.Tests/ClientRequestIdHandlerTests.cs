// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Core.Runtime.Handler;
using Moq;

namespace Cli.Tests;

public class ClientRequestIdHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsGuidHeader_AndPreservesItAcrossRetries()
    {
        var observedIds = new List<string>();
        var userAgentObservedIds = new List<string>();
        var sendCount = 0;
        var terminal = new SendAsyncTerminalFactory((request, _) =>
        {
            observedIds.Add(Assert.Single(request.Headers.GetValues(RequestCorrelation.ClientRequestIdHeaderName)));
            sendCount++;
            return Task.FromResult(new HttpResponseMessage(
                sendCount == 1 ? System.Net.HttpStatusCode.InternalServerError : System.Net.HttpStatusCode.OK));
        });
        var pipeline = new HttpPipeline(terminal, maxRetry: 1, maxRetryFor429: 0);
        pipeline.Prepend((request, callback, next) =>
        {
            var requestId = Assert.Single(request.Headers.GetValues(RequestCorrelation.ClientRequestIdHeaderName));
            userAgentObservedIds.Add(requestId);
            request.Headers.UserAgent.ParseAdd($"test-client/{requestId}");
            return next.SendAsync(request, callback);
        });
        pipeline.Prepend(new ClientRequestIdHandler().SendAsync);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);

        using var response = await pipeline.SendAsync(request, publisher.Object);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, observedIds.Count);
        Assert.True(Guid.TryParse(observedIds[0], out _));
        Assert.Equal(observedIds[0], observedIds[1]);
        Assert.Equal(observedIds[0], Assert.Single(userAgentObservedIds));
    }

    [Fact]
    public async Task SendAsync_PreservesExistingHeader()
    {
        const string expected = "11111111-1111-1111-1111-111111111111";
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        request.Headers.Add(RequestCorrelation.ClientRequestIdHeaderName, expected);
        var next = new Mock<ISendAsync>();
        next.Setup(sender => sender.SendAsync(request, It.IsAny<IEventPublisher>()))
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var publisher = new Mock<IEventPublisher>();

        using var response = await new ClientRequestIdHandler().SendAsync(request, publisher.Object, next.Object);

        Assert.Equal(expected, Assert.Single(request.Headers.GetValues(RequestCorrelation.ClientRequestIdHeaderName)));
    }
}
