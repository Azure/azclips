using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Core.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Moq;

namespace Extensions.KeyVault.Tests;

public class KeyVaultEndpointTests
{
    [Theory]
    [InlineData("http://fixture.vault.azure.net/")]
    [InlineData("https://fixture.vault.azure.net.attacker.example/")]
    [InlineData("https://fixture.vault.azure.net:444/")]
    [InlineData("https://user@fixture.vault.azure.net/")]
    [InlineData("https://fixture.vault.azure.net/secrets")]
    [InlineData("https://fixture.vault.azure.net/?query=value")]
    [InlineData("https://fixture.vault.azure.net/#fragment")]
    [InlineData("https://other.fixture.vault.azure.net/")]
    public async Task InvalidVaultNeverAcquiresToken(string url)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        var processor = new EndpointProcessor { VaultUrl = url };
        processor.Init(Context(pipeline.Object, new AuthHandler()));
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        pipeline.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("https://attacker.example/secrets/test")]
    [InlineData("https://other.vault.azure.net/secrets/test")]
    [InlineData("https://fixture.vault.azure.net/other")]
    public async Task CrossOriginOrNonSecretRequestIsRejectedBeforeAuth(string requestUri)
    {
        var auth = new AuthHandler { AcquireTokenAsyncDefault = (_, _, _, _, _) => throw new Exception("Token acquisition must not occur.") };
        var terminal = new Mock<ISendAsyncTerminalFactory>(MockBehavior.Strict);
        var pipeline = new HttpPipeline(terminal.Object);
        terminal.Setup(value => value.Create()).Returns(Mock.Of<ISendAsync>());
        pipeline.Prepend(auth.SendAsync);
        var processor = new EndpointProcessor { VaultName = "fixture", RequestUri = requestUri };
        processor.Init(Context(pipeline, auth));
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
    }

    [Theory]
    [InlineData("GET", "/secrets/test", false)]
    [InlineData("POST", "/deletedsecrets/test/recover", false)]
    [InlineData("GET", "/secrets/test", true)]
    [InlineData("POST", "/deletedsecrets/test/recover", true)]
    public async Task ValidRequestUsesVaultAudienceAndRestoresAuthOverride(string method, string path, bool directCoreSend)
    {
        var profile = new Profile { TenantId = "fixture-tenant", CloudName = "AzureCloud" };
        string[]? capturedScopes = null;
        Profile? capturedProfile = null;
        var auth = new AuthHandler
        {
            Profile = profile,
            AcquireTokenAsyncDefault = (scopes, _, _, _, suppliedProfile) =>
            {
                capturedScopes = scopes;
                capturedProfile = suppliedProfile;
                return Task.FromResult(new AuthResult
                {
                    AccessToken = "fixture-token", ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5),
                    Scopes = scopes ?? [], TenantId = profile.TenantId
                });
            }
        };
        var terminal = new Mock<ISendAsync>();
        terminal.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync((HttpRequestMessage request, IEventPublisher _) =>
            {
                Assert.True(request.HasSensitiveDiagnostics());
                Assert.Equal("fixture-token", request.Headers.Authorization!.Parameter);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}"), RequestMessage = request };
            });
        var factory = new Mock<ISendAsyncTerminalFactory>();
        factory.Setup(value => value.Create()).Returns(terminal.Object);
        var pipeline = new HttpPipeline(factory.Object);
        pipeline.Prepend(auth.SendAsync);
        var processor = new EndpointProcessor
        {
            VaultName = "fixture", RequestMethod = new HttpMethod(method),
            RequestUri = "https://fixture.vault.azure.net" + path
        };
        processor.Init(Context(pipeline, auth, profile));
        if (directCoreSend)
        {
            using var response = await processor.SendCoreDirectlyAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            await processor.ProcessAsync();
        }
        Assert.NotNull(capturedScopes);
        Assert.Equal(["https://vault.azure.net/.default"], capturedScopes);
        Assert.Same(profile, capturedProfile);
        Assert.False(auth.ApplyOverride);
        Assert.Null(auth.AcquireTokenAsyncOverride);
    }

    [Theory]
    [InlineData("GET", "https://fixture.vault.azure.net/deletedsecrets/test/recover")]
    [InlineData("DELETE", "https://fixture.vault.azure.net/deletedsecrets/test/recover")]
    [InlineData("PATCH", "https://fixture.vault.azure.net/deletedsecrets/test/recover")]
    [InlineData("GET", "https://fixture.vault.azure.net/deletedsecrets")]
    [InlineData("DELETE", "https://fixture.vault.azure.net/deletedsecrets/test")]
    [InlineData("POST", "https://fixture.vault.azure.net/deletedsecrets/test")]
    [InlineData("POST", "https://fixture.vault.azure.net/deletedsecrets/test/recover/extra")]
    [InlineData("POST", "https://other.vault.azure.net/deletedsecrets/test/recover")]
    public async Task OtherDeletedSecretRequestsAreRejectedBeforeAuth(string method, string requestUri)
    {
        var auth = new AuthHandler { AcquireTokenAsyncDefault = (_, _, _, _, _) => throw new Exception("Token acquisition must not occur.") };
        var terminal = new Mock<ISendAsync>(MockBehavior.Strict);
        var factory = new Mock<ISendAsyncTerminalFactory>();
        factory.Setup(value => value.Create()).Returns(terminal.Object);
        var pipeline = new HttpPipeline(factory.Object);
        pipeline.Prepend(auth.SendAsync);
        var processor = new EndpointProcessor
        {
            VaultName = "fixture", RequestUri = requestUri, RequestMethod = new HttpMethod(method)
        };
        processor.Init(Context(pipeline, auth));

        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        terminal.VerifyNoOtherCalls();
    }

    private static ProcessorContext Context(IRuntimeHttpPipeline pipeline, AuthHandler auth, Profile? profile = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return new ProcessorContext(pipeline, publisher.Object, auth,
            new OutputHandler { WriteOutput = (_, _) => Task.CompletedTask }, profile ?? new Profile { TenantId = "fixture-tenant" });
    }

    private sealed class EndpointProcessor : KeyVaultDataPlaneProcessorBase<object, object>
    {
        public string RequestUri { get; set; } = "https://fixture.vault.azure.net/secrets/test";
        public HttpMethod RequestMethod { get; set; } = HttpMethod.Get;
        protected override HttpMethod Method => RequestMethod;
        protected override string CreateRequestUri() => RequestUri;
        public Task<HttpResponseMessage> SendCoreDirectlyAsync() => base.SendCoreAsync();
    }
}