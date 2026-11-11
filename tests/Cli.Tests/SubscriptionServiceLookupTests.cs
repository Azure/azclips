// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Authentication;
using Moq;

namespace Cli.Tests;

public class SubscriptionServiceLookupTests
{
    private const string SubscriptionId = "aaaaaaaa-1111-1111-1111-111111111111";

    [Fact]
    public async Task GetSubscriptionByIdAsync_SendsBearerTokenAndReturnsSubscription()
    {
        var requests = 0;
        using var http = new HttpClient(new StubHandler(request =>
        {
            requests++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"https://management.azure.com/subscriptions/{SubscriptionId}?api-version=2022-12-01",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("unit-test-token", request.Headers.Authorization?.Parameter);
            Assert.True(Guid.TryParse(
                Assert.Single(request.Headers.GetValues(RequestCorrelation.ClientRequestIdHeaderName)), out _));
            return JsonResponse($$"""
                {"subscriptionId":"{{SubscriptionId}}","displayName":"Test subscription","state":"Enabled"}
                """);
        }));
        var service = new SubscriptionService(http, Mock.Of<ILoggingService>());

        var result = await service.GetSubscriptionByIdAsync("unit-test-token", SubscriptionId);

        Assert.Equal(new SubscriptionInfo(SubscriptionId, "Test subscription", "Enabled"), result);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task ListSubscriptionsAsync_Pagination_SendsDistinctClientRequestIds()
    {
        const string nextLink = "https://management.azure.com/subscriptions?api-version=2022-12-01&$skiptoken=next";
        var requestIds = new List<string>();
        using var http = new HttpClient(new StubHandler(request =>
        {
            var requestId = Assert.Single(request.Headers.GetValues(RequestCorrelation.ClientRequestIdHeaderName));
            Assert.True(Guid.TryParse(requestId, out _));
            requestIds.Add(requestId);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("unit-test-token", request.Headers.Authorization?.Parameter);
            Assert.InRange(requestIds.Count, 1, 2);
            Assert.Equal(
                requestIds.Count == 1
                    ? "https://management.azure.com/subscriptions?api-version=2022-12-01"
                    : nextLink,
                request.RequestUri!.AbsoluteUri);
            return JsonResponse(requestIds.Count == 1
                ? $$"""{"value":[{"subscriptionId":"{{SubscriptionId}}","displayName":"First","state":"Enabled"}],"nextLink":"{{nextLink}}"}"""
                : """{"value":[{"subscriptionId":"bbbbbbbb-2222-2222-2222-222222222222","displayName":"Second","state":"Enabled"}]}""");
        }));
        var service = new SubscriptionService(http, Mock.Of<ILoggingService>());

        var result = await service.ListSubscriptionsAsync("unit-test-token");

        Assert.Equal(
            [
                new SubscriptionInfo(SubscriptionId, "First", "Enabled"),
                new SubscriptionInfo("bbbbbbbb-2222-2222-2222-222222222222", "Second", "Enabled"),
            ],
            result);
        Assert.Equal(2, requestIds.Count);
        Assert.NotEqual(requestIds[0], requestIds[1]);
    }

    [Fact]
    public async Task GetSubscriptionByIdAsync_OptionalFieldsMissing_ReturnsEmptyStrings()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            JsonResponse($$"""{"subscriptionId":"{{SubscriptionId}}"}""")));
        var service = new SubscriptionService(http, Mock.Of<ILoggingService>());

        var result = await service.GetSubscriptionByIdAsync("unit-test-token", SubscriptionId);

        Assert.Equal(new SubscriptionInfo(SubscriptionId, "", ""), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetSubscriptionByIdAsync_MissingId_RejectsBeforeSendingRequest(string? subscriptionId)
    {
        using var http = new HttpClient(new StubHandler(_ =>
            throw new InvalidOperationException("No request expected.")));
        var service = new SubscriptionService(http, Mock.Of<ILoggingService>());

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetSubscriptionByIdAsync("unit-test-token", subscriptionId!));

        Assert.Equal("subscriptionId", error.ParamName);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetSubscriptionByIdAsync_HttpFailure_PropagatesStatus(HttpStatusCode status)
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(status)));
        var service = new SubscriptionService(http, Mock.Of<ILoggingService>());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetSubscriptionByIdAsync("unit-test-token", SubscriptionId));

        Assert.Equal(status, error.StatusCode);
    }

    [Theory]
    [InlineData("null", "Failed to deserialize subscription.")]
    [InlineData("{}", "Subscription ID is missing.")]
    public async Task GetSubscriptionByIdAsync_IncompleteResponse_ReportsError(string body, string expectedMessage)
    {
        using var http = new HttpClient(new StubHandler(_ => JsonResponse(body)));
        var service = new SubscriptionService(http, Mock.Of<ILoggingService>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetSubscriptionByIdAsync("unit-test-token", SubscriptionId));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task GetSubscriptionByIdAsync_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var http = new HttpClient(new StubHandler(_ => JsonResponse("{}")));
        var service = new SubscriptionService(http, Mock.Of<ILoggingService>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetSubscriptionByIdAsync("unit-test-token", SubscriptionId, cancellation.Token));
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
