using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;

namespace Cli.Tests;

public class PaginationCompatibilityTests
{
    private const string FirstPageUri = "https://fixture.invalid/first";
    private const string NextPageUri = "https://fixture.invalid/next";

    [Theory]
    [InlineData(typeof(ComputeProcessorBase<object, object>))]
    [InlineData(typeof(ResourcesProcessorBase<object, object>))]
    public void PageReadersRequireContinuationConfiguration(Type baseType)
    {
        var methods = baseType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Where(method => method.Name == "ReadPagesAsync").ToArray();

        Assert.Equal([1, 2], methods.Select(method => method.GetParameters().Length).Order());
        Assert.All(methods, method =>
        {
            Assert.False(method.IsVirtual);
            Assert.All(method.GetParameters(), parameter => Assert.False(parameter.IsOptional));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadResponseDoesNotFollowContinuation(bool compute)
    {
        var content = new TrackedContent($$"""{"value":[{"name":"first"}],"nextLink":"{{NextPageUri}}"}""");
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        var output = new FakeOutputHandler();
        IReadProbe processor = compute ? new ComputeReadProbe() : new ResourcesReadProbe();
        processor.Init(new ProcessorContext(pipeline, new FakeEventPublisher(), new FakeAuthHandler(null), output));

        object? body = await processor.ReadOneAsync();

        Assert.NotNull(body);
        Assert.Equal(1, pipeline.SendCount);
        Assert.True(content.Disposed);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SharedPageReadersFollowConfiguredContinuationLazily(bool compute, bool tokenPaging)
    {
        var sent = new List<string>();
        var contents = new List<TrackedContent>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sent.Add(request.RequestUri!.AbsoluteUri);
            var content = new TrackedContent(sent.Count == 1
                ? $$"""{"value":[{"name":"first"}],"nextLink":"{{NextPageUri}}"}"""
                : """{"value":[{"name":"second"}]}""");
            contents.Add(content);
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = content };
        });
        var output = new FakeOutputHandler();
        IReadProbe processor = compute ? new ComputeReadProbe() : new ResourcesReadProbe();
        processor.Init(new ProcessorContext(pipeline, new FakeEventPublisher(), new FakeAuthHandler(null), output));
        IAsyncEnumerable<object> pages = processor.Pages(tokenPaging);
        await using var iterator = pages.GetAsyncEnumerator();

        Assert.Empty(sent);
        Assert.True(await iterator.MoveNextAsync());
        Assert.Single(sent);
        Assert.Equal("first", compute
            ? Assert.Single(Assert.IsType<VirtualMachineListResult>(iterator.Current).Value!).Name
            : Assert.Single(Assert.IsType<ResourceGroupListResult>(iterator.Current).Value!).Name);
        Assert.All(contents, content => Assert.True(content.Disposed));

        Assert.True(await iterator.MoveNextAsync());
        Assert.Equal([FirstPageUri, NextPageUri], sent);
        Assert.Equal("second", compute
            ? Assert.Single(Assert.IsType<VirtualMachineListResult>(iterator.Current).Value!).Name
            : Assert.Single(Assert.IsType<ResourceGroupListResult>(iterator.Current).Value!).Name);
        Assert.False(await iterator.MoveNextAsync());
        Assert.Equal(2, pipeline.SendCount);
        Assert.All(contents, content => Assert.True(content.Disposed));
        Assert.Empty(output.WrittenValues);
    }

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

    private interface IReadProbe : IProcessor
    {
        Task<object?> ReadOneAsync();
        IAsyncEnumerable<object> Pages(bool tokenPaging);
    }

    private sealed class ComputeReadProbe : ListVMProcessor, IReadProbe
    {
        private string? marker;

        protected override string CreateRequestUri() => marker ?? FirstPageUri;
        public async Task<object?> ReadOneAsync() => await ReadResponseAsync();
        public IAsyncEnumerable<object> Pages(bool tokenPaging) => tokenPaging
            ? ReadPagesAsync(page => page.NextLink, token => marker = token)
            : ReadPagesAsync(nextLink);
    }

    private sealed class ResourcesReadProbe : ListResourceGroupProcessor, IReadProbe
    {
        private string? marker;

        protected override string CreateRequestUri() => marker ?? FirstPageUri;
        public async Task<object?> ReadOneAsync() => await ReadResponseAsync();
        public IAsyncEnumerable<object> Pages(bool tokenPaging) => tokenPaging
            ? ReadPagesAsync(page => page.NextLink, token => marker = token)
            : ReadPagesAsync(nextLink);
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
