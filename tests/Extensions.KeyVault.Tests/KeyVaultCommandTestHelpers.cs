using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;
using Moq;

namespace Extensions.KeyVault.Tests;

internal static class KeyVaultCommandTestHelpers
{
    internal static Task AssertRejectedBeforeNetworkAsync(KeyVaultDataPlaneProcessorBase command, string message) =>
        AssertRejectedBeforeNetworkAsync<ArgumentException>(command, message);

    internal static async Task AssertRejectedBeforeNetworkAsync<TException>(KeyVaultDataPlaneProcessorBase command, string message)
        where TException : Exception
    {
        Initialize(command, _ => throw new InvalidOperationException("Unexpected request."));

        var ex = await Assert.ThrowsAsync<TException>(() => command.ProcessAsync());

        Assert.Equal(message, ex.Message);
    }

    internal static Task<HttpResponseMessage> Response(string body) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

    internal static List<object> Initialize(KeyVaultDataPlaneProcessorBase command, string body, Func<HttpRequestMessage, Task> inspect)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), publisher.Object))
            .Returns(async (HttpRequestMessage request, IEventPublisher _) =>
            {
                await inspect(request);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body), RequestMessage = request };
            });
        var output = new List<object>();
        command.VaultName = "fixture";
        command.Init(new ProcessorContext(pipeline.Object, publisher.Object, new AuthHandler(),
            new OutputHandler { WriteOutput = (value, _) => { output.Add(value); return Task.CompletedTask; } },
            new Profile { TenantId = "fixture-tenant" }));
        return output;
    }

    internal static List<object> Initialize(KeyVaultDataPlaneProcessorBase command, Func<HttpRequestMessage, Task<HttpResponseMessage>> send, CancellationToken token = default)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        publisher.SetupGet(value => value.Token).Returns(token);
        publisher.SetupGet(value => value.Cancel).Returns(() => { });
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.SetupGet(value => value.PollingInterval).Returns(0);
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Returns((HttpRequestMessage request, IEventPublisher _) => send(request));
        var output = new List<object>();
        var outputHandler = new Mock<IOutputHandler>();
        outputHandler.SetupGet(value => value.WriteOutput).Returns((value, _) => { output.Add(value); return Task.CompletedTask; });
        command.VaultName = "fixture";
        command.Init(new ProcessorContext(pipeline.Object, publisher.Object, Mock.Of<IAuthHandler>(),
            outputHandler.Object,
            new Profile { TenantId = "fixture-tenant" }));
        return output;
    }
}
