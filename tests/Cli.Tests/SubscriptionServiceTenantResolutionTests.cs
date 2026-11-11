using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Authentication;
using Moq;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Unit tests for <see cref="SubscriptionService.ResolveSubscriptionTenantAsync"/>, the tenant
/// resolution that keeps <c>az login --subscription-id</c> from persisting the wrong (home) tenant
/// for a subscription homed in a different tenant.
/// </summary>
public class SubscriptionServiceTenantResolutionTests
{
    private const string SubscriptionId = "a1b2c3d4-5e6f-7a8b-9c0d-1e2f3a4b5c6d";
    private const string HomeTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
    private const string OwningTenantId = "11111111-2222-3333-4444-555555555555";

    private static readonly ILoggingService Logger = Mock.Of<ILoggingService>();

    private static SubscriptionService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new HttpClient(new StubHandler(responder)), Logger);

    [Fact]
    public async Task ResolveSubscriptionTenant_CrossTenant_UsesAuthorizationUriFromChallenge()
    {
        // ARM answers a cross-tenant subscription request with a 401 challenge naming the real tenant.
        var service = CreateService(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"https://management.azure.com/subscriptions/{SubscriptionId}?api-version=2022-12-01",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("home-token", request.Headers.Authorization?.Parameter);
            Assert.True(Guid.TryParse(
                Assert.Single(request.Headers.GetValues(RequestCorrelation.ClientRequestIdHeaderName)), out _));
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Headers.TryAddWithoutValidation(
                "WWW-Authenticate",
                $"Bearer authorization_uri=\"https://login.microsoftonline.com/{OwningTenantId}\", error=\"invalid_token\"");
            return response;
        });

        var tenant = await service.ResolveSubscriptionTenantAsync("home-token", SubscriptionId);

        Assert.Equal(OwningTenantId, tenant);
    }

    [Fact]
    public async Task ResolveSubscriptionTenant_SameTenant_ReadsTenantFromPayload()
    {
        // The token already has access, so ARM returns the subscription with its owning tenant.
        var service = CreateService(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("home-token", request.Headers.Authorization?.Parameter);
            Assert.True(Guid.TryParse(
                Assert.Single(request.Headers.GetValues(RequestCorrelation.ClientRequestIdHeaderName)), out _));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"subscriptionId\":\"{SubscriptionId}\",\"displayName\":\"Sub\",\"state\":\"Enabled\",\"tenantId\":\"{HomeTenantId}\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var tenant = await service.ResolveSubscriptionTenantAsync("home-token", SubscriptionId);

        Assert.Equal(HomeTenantId, tenant);
    }

    [Fact]
    public async Task ResolveSubscriptionTenant_ChallengeWithoutAuthorizationUri_Throws()
    {
        var service = CreateService(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", "Bearer error=\"invalid_token\"");
            return response;
        });

        // A 401/403 with no usable authorization_uri is a genuine lookup failure, not a definitive
        // "home tenant owns it". It must surface so the caller warns rather than silently persisting
        // an unverified tenant.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.ResolveSubscriptionTenantAsync("home-token", SubscriptionId));
    }

    [Fact]
    public async Task ResolveSubscriptionTenant_ServerError_Throws()
    {
        // A transient/server-side failure must not collapse into a silent home-tenant fallback.
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.ResolveSubscriptionTenantAsync("home-token", SubscriptionId));
    }

    [Fact]
    public async Task ResolveSubscriptionTenant_EmptySubscriptionId_ReturnsNullWithoutCallingArm()
    {
        var called = false;
        var service = CreateService(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var tenant = await service.ResolveSubscriptionTenantAsync("home-token", string.Empty);

        Assert.Null(tenant);
        Assert.False(called);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
