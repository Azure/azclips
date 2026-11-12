using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;

namespace Cli.Tests;

public class PaginationCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleResponseRemainsAliveDuringOutput(bool compute)
    {
        var content = new TrackedContent("""{"name":"test"}""");
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        var publisher = new FakeEventPublisher();
        var output = new FakeOutputHandler();
        var write = output.WriteOutput;
        output.WriteOutput = (value, token) =>
        {
            Assert.False(content.Disposed);
            return write(value, token);
        };
        IProcessor processor = compute
            ? new GetVMProcessor { SubscriptionId = "sub", ResourceGroupName = "rg", Name = "test" }
            : new GetResourceGroupProcessor { SubscriptionId = "sub", Name = "test" };
        processor.Init(new ProcessorContext(pipeline, publisher, new FakeAuthHandler(null), output));

        await processor.ProcessAsync();

        Assert.Single(output.WrittenValues);
        Assert.True(content.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialJsonErrorsRetainProcessorSpecificDiagnosticsAndInnerExceptions(bool compute)
    {
        const string rawBody = "{invalid-json";
        var content = new TrackedContent(rawBody);
        var pipeline = new FakeHttpPipeline((request, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = content,
        });
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        var output = new FakeOutputHandler();
        IProcessor processor = compute
            ? new ListVMProcessor { SubscriptionId = "sub" }
            : new ListResourceGroupProcessor { SubscriptionId = "sub" };
        processor.Init(new ProcessorContext(pipeline, publisher.Object, new FakeAuthHandler(null), output));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        string model = compute ? "VirtualMachineListResult" : "ResourceGroupListResult";
        Assert.Equal($"Failed to deserialize response body into {model}. Raw body: {rawBody}", error.Message);
        if (compute) Assert.Null(error.InnerException);
        else Assert.IsType<JsonException>(error.InnerException);
        publisher.Verify(value => value.Publish(Events.ResponseInvalidJson,
            It.Is<string>(message => message.StartsWith("response has invalid JSON for ", StringComparison.Ordinal))), Times.Once);
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncEnd), Times.Once);
        Assert.Empty(output.WrittenValues);
        Assert.True(content.Disposed);
    }

    [Theory]
    [InlineData(false, """{"value":[{"name":"test"}]}""")]
    [InlineData(false, """{"value":[]}""")]
    [InlineData(false, """{}""")]
    [InlineData(false, "null")]
    [InlineData(false, " ")]
    [InlineData(true, """{"value":[{"name":"test"}]}""")]
    [InlineData(true, """{"value":[]}""")]
    [InlineData(true, """{}""")]
    [InlineData(true, "null")]
    [InlineData(true, " ")]
    public async Task SinglePageListsRetainRequestCreationHooks(bool compute, string body)
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        var output = new FakeOutputHandler();
        IProcessor processor = compute
            ? new ListVMProcessor { SubscriptionId = "sub" }
            : new ListResourceGroupProcessor { SubscriptionId = "sub" };
        processor.Init(new ProcessorContext(pipeline, publisher.Object, new FakeAuthHandler(null), output));

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Single(output.WrittenValues);
        publisher.Verify(value => value.Publish(Events.ProcessorBeforeHttpRequestCreate), Times.Exactly(2));
        publisher.Verify(value => value.Publish(Events.ProcessorAfterHttpRequestCreate), Times.Exactly(2));
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncStart), Times.Once);
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncEnd), Times.Once);
    }

    private sealed class TrackedContent(string value) : StringContent(value)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
