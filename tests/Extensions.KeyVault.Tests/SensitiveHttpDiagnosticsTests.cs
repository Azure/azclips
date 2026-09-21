using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Moq;

namespace Extensions.KeyVault.Tests;

public class SensitiveHttpDiagnosticsTests
{
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
        var transport = new Core.Runtime.HttpClientFactory(ordinaryClient);
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