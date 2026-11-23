using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;
using Moq;

namespace Extensions.KeyVault.Tests;

/// <summary>
/// Helpers for Key Vault command tests. Create the pipeline with <see cref="CreatePipelineMock"/>: it is
/// <see cref="MockBehavior.Strict"/>, so any request a test did not set up throws <see cref="MockException"/>.
/// </summary>
internal static class KeyVaultCommandTestHelpers
{
    /// <summary>
    /// A strict pipeline mock with only the setup calls the Key Vault command base makes (polling interval and
    /// the vault middleware <c>Prepend</c>); requests must be set up with a <c>RespondWith</c> overload.
    /// </summary>
    internal static Mock<IRuntimeHttpPipeline> CreatePipelineMock()
    {
        var mockPipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        mockPipeline.SetupGet(p => p.PollingInterval).Returns(0);
        mockPipeline.Setup(p => p.Prepend(It.IsAny<SendAsyncStep>())).Returns(mockPipeline.Object);
        return mockPipeline;
    }

    /// <summary>Wires <paramref name="command"/> to <paramref name="mockPipeline"/> and returns the list that receives its output.</summary>
    internal static List<object> Initialize(KeyVaultDataPlaneProcessorBase command, Mock<IRuntimeHttpPipeline> mockPipeline, CancellationToken token = default)
    {
        var mockPublisher = new Mock<IEventPublisher>();
        mockPublisher.SetReturnsDefault(Task.CompletedTask);
        mockPublisher.SetupGet(value => value.Token).Returns(token);
        mockPublisher.SetupGet(value => value.Cancel).Returns(() => { });
        var output = new List<object>();
        var mockOutputHandler = new Mock<IOutputHandler>();
        mockOutputHandler.SetupGet(value => value.WriteOutput).Returns((value, _) => { output.Add(value); return Task.CompletedTask; });
        command.VaultName = "fixture";
        command.Init(new ProcessorContext(mockPipeline.Object, mockPublisher.Object, Mock.Of<IAuthHandler>(),
            mockOutputHandler.Object,
            new Profile { TenantId = "fixture-tenant" }));
        return output;
    }

    /// <summary>
    /// Answers the n-th request with the n-th of <paramref name="bodies"/> (the last body repeats) as 200 OK JSON.
    /// Returns the request bodies, read at send time because the command disposes each request after sending it.
    /// </summary>
    internal static List<string?> RespondWith(this Mock<IRuntimeHttpPipeline> mockPipeline, params string[] bodies)
    {
        var sentBodies = new List<string?>();
        mockPipeline.Setup(p => p.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Returns(async (HttpRequestMessage request, IEventPublisher _) =>
            {
                sentBodies.Add(await ReadBodyAsync(request));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bodies[Math.Min(sentBodies.Count, bodies.Length) - 1], Encoding.UTF8, "application/json"),
                    RequestMessage = request,
                };
            });
        return sentBodies;
    }

    /// <summary>
    /// Answers every request with <paramref name="send"/>. Returns the request bodies, read at send time because
    /// the command disposes each request after sending it.
    /// </summary>
    internal static List<string?> RespondWith(this Mock<IRuntimeHttpPipeline> mockPipeline, Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    {
        var sentBodies = new List<string?>();
        mockPipeline.Setup(p => p.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Returns(async (HttpRequestMessage request, IEventPublisher _) =>
            {
                sentBodies.Add(await ReadBodyAsync(request));
                return await send(request);
            });
        return sentBodies;
    }

    /// <summary>The requests sent through the pipeline, in order. Method and URI stay readable after the request is disposed.</summary>
    internal static IReadOnlyList<HttpRequestMessage> SentRequests(this Mock<IRuntimeHttpPipeline> mockPipeline) =>
        mockPipeline.Invocations
            .Where(i => i.Method.Name == nameof(IRuntimeHttpPipeline.SendAsync))
            .Select(i => (HttpRequestMessage)i.Arguments[0])
            .ToList();

    /// <summary>Each sent request as "METHOD absolute-uri" (including query), in order.</summary>
    internal static IEnumerable<string> RequestLines(this Mock<IRuntimeHttpPipeline> mockPipeline) =>
        mockPipeline.SentRequests().Select(r => $"{r.Method} {r.RequestUri!.AbsoluteUri}");

    internal static Task<HttpResponseMessage> Response(string body) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

    internal static SecureString SecureStringOf(string value)
    {
        var secure = new SecureString();
        foreach (char character in value) secure.AppendChar(character);
        return secure;
    }

    internal static X509Certificate2 CreateCertificate(RSA key)
    {
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static async Task<string?> ReadBodyAsync(HttpRequestMessage request) =>
        request.Content is null ? null : await request.Content.ReadAsStringAsync();
}