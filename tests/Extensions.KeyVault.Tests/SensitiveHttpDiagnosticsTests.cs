using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Extensions.KeyVault.Tests;

public class SensitiveHttpDiagnosticsTests
{
    [Theory]
    [InlineData("request", null)]
    [InlineData("request", " ")]
    [InlineData("request", "{invalid")]
    [InlineData("response", null)]
    [InlineData("response", " ")]
    [InlineData("response", "{invalid")]
    [InlineData("none", null)]
    [InlineData("none", " ")]
    [InlineData("none", "{invalid")]
    public async Task PaginationDiagnosticsRespectSensitiveMarkers(string marker, string? body)
    {
        const string sentinel = "private-vault-and-secret";
        const string nextLink = "https://fixture.invalid/" + sentinel;
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        using var responseRequest = new HttpRequestMessage(HttpMethod.Get, nextLink);
        if (marker == "response") responseRequest.MarkSensitive();
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), publisher.Object))
            .Returns((HttpRequestMessage request, IEventPublisher _) =>
            {
                if (marker == "request") request.MarkSensitive();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = responseRequest,
                    Content = body is null ? null : new StringContent(body)
                });
            });
        var page = new PaginationPage(["first"], nextLink);
        var collecting = page.CollectWithPaginationAsync(value => value.Items, value => value.NextLink,
            () => Task.FromResult(new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/first")),
            publisher.Object, pipeline.Object, new System.Text.Json.JsonSerializerOptions());

        if (body == "{invalid")
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => collecting);
            if (marker != "none") Assert.DoesNotContain(body, error.ToString());
        }
        else
        {
            Assert.Equal(new[] { "first" }, await collecting);
        }

        var messages = string.Join("\n", publisher.Invocations.SelectMany(invocation => invocation.Arguments.OfType<string>()));
        if (marker == "none") Assert.Contains(sentinel, messages);
        else Assert.DoesNotContain(sentinel, messages);
    }

    private sealed record PaginationPage(string[] Items, string? NextLink);

    [Fact]
    public async Task BothTransportsUseSharedHostConfiguration()
    {
        var handlers = new List<HttpClientHandler>();
        using var interceptor = new CapturingHandler();
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=transport-fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var logs = new TransportLogProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        Core.Engine.DIRegisterExtensions.RegisterEngineServices(services);
        services.ConfigureHttpClientDefaults(builder => builder
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(7);
                client.DefaultRequestHeaders.Add("host-setting", "shared");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    UseProxy = false,
                    Proxy = new WebProxy("http://127.0.0.1:8888"),
                    SslProtocols = System.Security.Authentication.SslProtocols.Tls12
                };
                handler.ClientCertificates.Add(certificate);
                handlers.Add(handler);
                return handler;
            })
            .AddHttpMessageHandler(() => new HostConfiguredHandler(interceptor)));
        using var provider = services.BuildServiceProvider();
        var ordinary = provider.GetRequiredService<HttpClient>();
        var sensitive = provider.GetRequiredService<Core.Runtime.SensitiveHttpClient>();

        Assert.Equal(2, handlers.Count);
        Assert.True(handlers[0].AllowAutoRedirect);
        Assert.False(handlers[1].AllowAutoRedirect);
        Assert.All(handlers, handler =>
        {
            Assert.False(handler.UseProxy);
            Assert.NotNull(handler.Proxy);
            Assert.Equal(System.Security.Authentication.SslProtocols.Tls12, handler.SslProtocols);
            Assert.Contains(certificate, handler.ClientCertificates.Cast<X509Certificate2>());
        });
        Assert.Equal(TimeSpan.FromSeconds(7), ordinary.Timeout);
        Assert.Equal(ordinary.Timeout, sensitive.Timeout);
        var transport = new Core.Runtime.HttpClientFactory(ordinary, sensitive);
        using var ordinaryRequest = new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/ordinary");
        using var sensitiveRequest = new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/private-secret-uri");
        sensitiveRequest.MarkSensitive();
        using var ordinaryResponse = await transport.SendAsync(ordinaryRequest, Mock.Of<IEventPublisher>());
        using var sensitiveResponse = await transport.SendAsync(sensitiveRequest, Mock.Of<IEventPublisher>());

        Assert.Equal(2, interceptor.Calls);
        Assert.Equal("shared", interceptor.HostSetting);
        Assert.Contains(logs.Messages, message => message.Contains("https://fixture.invalid/ordinary"));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-secret-uri"));
    }

    [Fact]
    public void SensitiveFactoryRejectsUnknownPrimaryTransport()
    {
        using var handler = new CapturingHandler();
        var services = new ServiceCollection();
        Core.Engine.DIRegisterExtensions.RegisterEngineServices(services);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<Core.Runtime.SensitiveHttpClient>());

        Assert.Contains("redirects can be disabled", error.Message);
        Assert.Equal(0, handler.Calls);
    }

    private sealed class TransportLogProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new TransportLogger(Messages);
        public void Dispose() { }

        private sealed class TransportLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Add(formatter(state, exception));
        }
    }

    private sealed class HostConfiguredHandler(CapturingHandler interceptor) : DelegatingHandler
    {
        private readonly HttpMessageInvoker invoker = new(interceptor, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            invoker.SendAsync(request, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing) invoker.Dispose();
            base.Dispose(disposing);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportUsesTheConfiguredClient(bool sensitive)
    {
        var ordinaryHandler = new CapturingHandler();
        var sensitiveHandler = new CapturingHandler();
        using var ordinary = new HttpClient(ordinaryHandler);
        using var protectedClient = new Core.Runtime.SensitiveHttpClient(sensitiveHandler);
        ordinary.DefaultRequestHeaders.Add("host-setting", "ordinary");
        protectedClient.DefaultRequestHeaders.Add("host-setting", "sensitive");
        protectedClient.Timeout = TimeSpan.FromSeconds(7);
        var factory = new Core.Runtime.HttpClientFactory(ordinary, protectedClient);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/");
        if (sensitive) request.MarkSensitive();

        using var response = await factory.SendAsync(request, Mock.Of<IEventPublisher>());

        Assert.Equal(sensitive ? 0 : 1, ordinaryHandler.Calls);
        Assert.Equal(sensitive ? 1 : 0, sensitiveHandler.Calls);
        Assert.Equal(sensitive ? "sensitive" : "ordinary", (sensitive ? sensitiveHandler : ordinaryHandler).HostSetting);
        Assert.Equal(TimeSpan.FromSeconds(7), protectedClient.Timeout);
    }

    [Fact]
    public async Task SensitiveRequestWithoutHostConfigurationFailsBeforeNetwork()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var factory = new Core.Runtime.HttpClientFactory(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/");
        request.MarkSensitive();
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.SendAsync(request, Mock.Of<IEventPublisher>()));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task SensitiveTransportHonorsInvocationCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new CapturingHandler { WaitForCancellation = true };
        using var client = new HttpClient();
        using var sensitive = new Core.Runtime.SensitiveHttpClient(handler);
        var factory = new Core.Runtime.HttpClientFactory(client, sensitive);
        var publisher = new Mock<IEventPublisher>();
        publisher.SetupGet(value => value.Token).Returns(cancellation.Token);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/");
        request.MarkSensitive();
        Task<HttpResponseMessage> sending = factory.SendAsync(request, publisher.Object);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? HostSetting { get; private set; }
        public bool WaitForCancellation { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            if (request.Headers.TryGetValues("host-setting", out var values)) HostSetting = values.Single();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
        }
    }

    [Theory]
    [InlineData("{\"value\":\"private-fixture\",\"pwd\":\"private-fixture\"}")]
    [InlineData("{broken private-fixture")]
    public async Task MarkedTrafficIsSanitizedBeforeItEntersARecording(string body)
    {
        var record = new global::Tests.Recording.SessionRecord();
        var recorder = new global::Tests.Recording.RecordPlaybackInterceptor(global::Tests.Recording.RecorderMode.Record, record);
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://fixture.vault.azure.net/secrets/test")
        {
            Content = new StringContent(body)
        };
        request.Headers.Add("fixture-header", "private-fixture");
        request.MarkSensitive();
        var next = new Mock<ISendAsync>();
        next.Setup(value => value.SendAsync(request, publisher.Object)).ReturnsAsync(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body), RequestMessage = request
            };
            response.Headers.Add("fixture-header", "private-fixture");
            return response;
        });
        using var response = await recorder.SendAsync(request, publisher.Object, next.Object);
        Assert.DoesNotContain("private-fixture", System.Text.Json.JsonSerializer.Serialize(record));
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
        Assert.Single(record.Entries);
    }

    [Fact]
    public async Task SensitiveTransportDoesNotFollowRedirects()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var publisher = new Mock<IEventPublisher>();
        publisher.SetupGet(value => value.Token).Returns(cancellation.Token);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"http://127.0.0.1:{port}/original")
        {
            Content = new StringContent("private-fixture")
        };
        request.MarkSensitive();
        using var ordinaryClient = new HttpClient();
        var services = new ServiceCollection();
        Core.Engine.DIRegisterExtensions.RegisterEngineServices(services);
        using var provider = services.BuildServiceProvider();
        var transport = new Core.Runtime.HttpClientFactory(ordinaryClient, provider.GetRequiredService<Core.Runtime.SensitiveHttpClient>());
        Task<HttpResponseMessage> sending = transport.SendAsync(request, publisher.Object);
        using var connection = await listener.AcceptTcpClientAsync(cancellation.Token);
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellation.Token))) { }
        byte[] reply = System.Text.Encoding.ASCII.GetBytes(
            $"HTTP/1.1 307 Temporary Redirect\r\nLocation: http://127.0.0.1:{port}/redirected\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(reply, cancellation.Token);
        using var response = await sending;
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.False(listener.Pending());
    }

    [Fact]
    public async Task SensitiveDetailsAreOmittedAfterRetryClone()
    {
        const string sentinel = "private-fixture-value";
        using var request = new HttpRequestMessage(HttpMethod.Put, $"https://fixture.invalid/{sentinel}")
        {
            Content = new StringContent($"{{\"value\":\"{sentinel}\"}}")
        };
        request.Headers.Add("fixture-header", sentinel);
        request.MarkSensitive();
        using var clone = await request.CloneWithContent();
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = clone,
            Content = new StringContent(sentinel),
            ReasonPhrase = sentinel
        };
        response.Headers.Add("fixture-header", sentinel);
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);

        Assert.True(clone.HasSensitiveDiagnostics());
        Assert.DoesNotContain(sentinel, await clone.GetLog());
        Assert.DoesNotContain(sentinel, await response.GetLog());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => response.EnsureSuccessAsync(publisher.Object));
        Assert.DoesNotContain(sentinel, error.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        publisher.Verify(value => value.Publish(Events.ResourceOperationFailed,
            It.Is<string>(message => !message.Contains(sentinel))), Times.Once);
        Assert.Contains(sentinel, await clone.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnmarkedRequestsKeepExistingDiagnostics()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.invalid/")
        {
            Content = new StringContent("ordinary-fixture")
        };
        Assert.Contains("ordinary-fixture", await request.GetLog());
    }
}