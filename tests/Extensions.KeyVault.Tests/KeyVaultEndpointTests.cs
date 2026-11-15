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
    [InlineData("https://other.vault.azure.net/certificates/test")]
    [InlineData("https://fixture.vault.azure.net/certificates-unsupported/test")]
    public async Task CrossOriginOrUnsupportedRequestIsRejectedBeforeAuth(string requestUri)
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
    [InlineData("GET", "/certificates/test", false)]
    [InlineData("POST", "/deletedcertificates/test/recover", false)]
    [InlineData("GET", "/certificates/test", true)]
    [InlineData("POST", "/deletedcertificates/test/recover", true)]
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
    [InlineData("GET", "https://fixture.vault.azure.net/deletedcertificates/test/recover")]
    [InlineData("DELETE", "https://fixture.vault.azure.net/deletedcertificates/test/recover")]
    [InlineData("PATCH", "https://fixture.vault.azure.net/deletedcertificates/test/recover")]
    [InlineData("GET", "https://fixture.vault.azure.net/deletedcertificates")]
    [InlineData("DELETE", "https://fixture.vault.azure.net/deletedcertificates/test")]
    [InlineData("POST", "https://fixture.vault.azure.net/deletedcertificates/test")]
    [InlineData("POST", "https://fixture.vault.azure.net/deletedcertificates/test/recover/extra")]
    [InlineData("POST", "https://other.vault.azure.net/deletedcertificates/test/recover")]
    public async Task OtherDeletedResourceRequestsAreRejectedBeforeAuth(string method, string requestUri)
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

    [Theory]
    [InlineData("/secrets/test")]
    [InlineData("/certificates/test")]
    public async Task SubstitutedResponseRequestStillSuppressesSensitiveDiagnostics(string path)
    {
        using var substituted = new HttpRequestMessage(HttpMethod.Get, "https://other.example/private-fixture");
        using var supplied = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = substituted,
            Content = new StringContent("{\"value\":\"private-fixture\"}")
        };
        var terminal = new Mock<ISendAsync>();
        terminal.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>())).ReturnsAsync(supplied);
        var factory = new Mock<ISendAsyncTerminalFactory>();
        factory.Setup(value => value.Create()).Returns(terminal.Object);
        var processor = new EndpointProcessor { VaultName = "fixture", RequestUri = "https://fixture.vault.azure.net" + path };
        processor.Init(Context(new HttpPipeline(factory.Object), new AuthHandler()));

        using var response = await processor.SendCoreDirectlyAsync();

        Assert.True(response.RequestMessage.HasSensitiveDiagnostics());
        Assert.DoesNotContain("private-fixture", await response.GetLog());
        response.StatusCode = HttpStatusCode.Forbidden;
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => response.EnsureSuccessAsync(publisher.Object));
        Assert.DoesNotContain("private-fixture", error.ToString());
    }

    [Fact]
    public Task VaultNameAndVaultUrl_ThrowsBeforeNetwork() =>
        AssertVaultRejectedBeforeNetworkAsync(new EndpointProcessor { VaultName = "fixture", VaultUrl = "https://fixture.vault.azure.net/" },
            "Specify either a vault name or a vault URL, not both.");

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("fixture.vault.azure.net")]
    public Task NonAbsoluteVaultUrl_ThrowsBeforeNetwork(string url) =>
        AssertVaultRejectedBeforeNetworkAsync(new EndpointProcessor { VaultUrl = url }, "The vault URL must be an absolute HTTPS URL.");

    [Theory]
    [InlineData("http://fixture.vault.azure.net/")]
    [InlineData("https://fixture.vault.azure.net.attacker.example/")]
    [InlineData("https://fixture.vault.azure.net:444/")]
    [InlineData("https://user@fixture.vault.azure.net/")]
    [InlineData("https://fixture.vault.azure.net/secrets")]
    [InlineData("https://fixture.vault.azure.net/?query=value")]
    [InlineData("https://fixture.vault.azure.net/#fragment")]
    [InlineData("https://fixture.vault.azure.cn/")]
    public Task NonRootOrNonPublicCloudVaultUrl_ThrowsBeforeNetwork(string url) =>
        AssertVaultRejectedBeforeNetworkAsync(new EndpointProcessor { VaultUrl = url }, "Use the root HTTPS URL of an Azure public-cloud vault.");

    [Fact]
    public Task MissingVault_ThrowsBeforeNetwork() =>
        AssertVaultRejectedBeforeNetworkAsync(new EndpointProcessor(), "Use the root HTTPS URL of an Azure public-cloud vault.");

    [Theory]
    [InlineData("ab")]
    [InlineData("1abc")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("ab--c")]
    [InlineData("ab_c")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaa")]
    public Task InvalidVaultName_ThrowsBeforeNetwork(string name) =>
        AssertVaultRejectedBeforeNetworkAsync(new EndpointProcessor { VaultName = name },
            "Vault names must be 3-24 letters, digits or hyphens, start with a letter, end with a letter or digit and contain no consecutive hyphens.");

    [Theory]
    [InlineData("ab")]
    [InlineData("1abc")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("ab--c")]
    [InlineData("ab_c")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaa")]
    public Task InvalidVaultNameInVaultUrl_ThrowsBeforeNetwork(string name) =>
        AssertVaultRejectedBeforeNetworkAsync(new EndpointProcessor { VaultUrl = $"https://{name}.vault.azure.net/" },
            "Vault names must be 3-24 letters, digits or hyphens, start with a letter, end with a letter or digit and contain no consecutive hyphens.");

    [Fact]
    public Task VaultUrlWithSubdomain_ThrowsVaultNameError() =>
        AssertVaultRejectedBeforeNetworkAsync(new EndpointProcessor { VaultUrl = "https://other.fixture.vault.azure.net/" },
            "Vault names must be 3-24 letters, digits or hyphens, start with a letter, end with a letter or digit and contain no consecutive hyphens.");

    [Theory]
    [InlineData("abc")]
    [InlineData("a23456789012345678901234")]
    public async Task VaultNameAtLengthBoundary_IsAccepted(string name)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        string? host = null;
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync((HttpRequestMessage request, IEventPublisher _) =>
            {
                host = request.RequestUri!.Host;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}"), RequestMessage = request };
            });
        var processor = new EndpointProcessor { VaultName = name, RequestUri = $"https://{name}.vault.azure.net/secrets/test" };
        processor.Init(Context(pipeline.Object, new AuthHandler()));

        await processor.ProcessAsync();

        Assert.Equal($"{name}.vault.azure.net", host);
    }

    [Theory]
    [InlineData("GET", "https://other.vault.azure.net/secrets/test")]
    [InlineData("GET", "https://fixture.vault.azure.net/keys/test")]
    [InlineData("GET", "https://fixture.vault.azure.net/deletedsecrets/test/recover")]
    public async Task RequestOutsideSelectedVaultEndpoint_ThrowsBeforeAuthOrSend(string method, string requestUri)
    {
        var auth = new AuthHandler { AcquireTokenAsyncDefault = (_, _, _, _, _) => throw new Exception("Token acquisition must not occur.") };
        var terminal = new Mock<ISendAsync>(MockBehavior.Strict);
        var factory = new Mock<ISendAsyncTerminalFactory>();
        factory.Setup(value => value.Create()).Returns(terminal.Object);
        var pipeline = new HttpPipeline(factory.Object);
        pipeline.Prepend(auth.SendAsync);
        var processor = new EndpointProcessor { VaultName = "fixture", RequestUri = requestUri, RequestMethod = new HttpMethod(method) };
        processor.Init(Context(pipeline, auth));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Equal("The request must target the selected vault's secrets or certificates endpoint.", ex.Message);
    }

    private static async Task AssertVaultRejectedBeforeNetworkAsync(EndpointProcessor processor, string message)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected request."));
        processor.Init(Context(pipeline.Object, new AuthHandler()));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Equal(message, ex.Message);
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