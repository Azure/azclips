using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;
using Moq;

namespace Extensions.KeyVault.Tests;

public class KeyVaultDataPlaneProcessorBaseTests
{
    [Fact]
    public async Task ChildSharesRuntimeAndVaultWithoutRequiringSubscription()
    {
        using var cancellation = new CancellationTokenSource();
        var publisher = CreatePublisher();
        publisher.SetupGet(value => value.Token).Returns(cancellation.Token);
        publisher.SetupGet(value => value.Cancel).Returns(new Action(cancellation.Cancel));
        var pipeline = Mock.Of<IRuntimeHttpPipeline>();
        var profile = new Profile { TenantId = "fixture-tenant" };
        var authHandler = new AuthHandler { Profile = profile };
        var output = new List<object>();
        var processor = new TestProcessor
        {
            Profile = "fixture-profile",
            VaultBaseUrl = new Uri("https://fixture.vault.azure.net/")
        };
        processor.Init(new ProcessorContext(pipeline, publisher.Object, authHandler,
            new OutputHandler { WriteOutput = (value, _) => { output.Add(value); return Task.CompletedTask; } }, profile));

        var child = processor.CreateChild();

        Assert.Same(pipeline, child.HttpPipeline);
        Assert.Same(publisher.Object, child.Publisher);
        Assert.Same(processor.JsonOptions, child.JsonOptions);
        Assert.Same(profile, child.CapturedProfile);
        Assert.Null(child.CapturedProfile!.SubscriptionId);
        Assert.Same(authHandler, child.CapturedAuthHandler);
        Assert.Equal(processor.Profile, child.Profile);
        Assert.Equal(processor.VaultBaseUrl, child.VaultBaseUrl);
        Assert.Equal(cancellation.Token, child.Token);

        var result = new TestResponse { Value = "fixture" };
        await child.EmitAsync(result);
        Assert.Same(result, Assert.Single(output));
        child.Cancel();
        Assert.True(cancellation.IsCancellationRequested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FoundationRejectsExecutionBeforeNetworkOrOutput(bool hasProfile)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        var publisher = CreatePublisher();
        var profile = hasProfile ? new Profile { TenantId = "fixture-tenant" } : null;
        var authHandler = new AuthHandler { Profile = profile };
        var output = new List<object>();
        var processor = new TestProcessor();
        processor.Init(new ProcessorContext(pipeline.Object, publisher.Object, authHandler,
            new OutputHandler { WriteOutput = (value, _) => { output.Add(value); return Task.CompletedTask; } }, profile));

        await Assert.ThrowsAsync<NotSupportedException>(() => processor.ProcessAsync());

        pipeline.VerifyNoOtherCalls();
        Assert.Empty(output);
        Assert.False(authHandler.ApplyOverride);
        Assert.Null(authHandler.AcquireTokenAsyncOverride);
        publisher.Verify(value => value.Publish(Events.ProcessorBeforeHttpRequestCreate), Times.Never);
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncEnd), Times.Once);
    }

    [Fact]
    public async Task MalformedResponseDoesNotIncludePayloadInException()
    {
        const string sensitiveValue = "fixture-private-value";
        var publisher = CreatePublisher();
        var output = new List<object>();
        var processor = CreateWithTestPipeline(
            "{\"value\":\"" + sensitiveValue + "\",\"broken\":}", publisher.Object, output);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.DoesNotContain(sensitiveValue, exception.ToString());
        Assert.Null(exception.InnerException);
        Assert.Empty(output);
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncStart), Times.Once);
        publisher.Verify(value => value.Publish(Events.ResponseInvalidJson, "Response has invalid JSON."), Times.Once);
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncEnd), Times.Once);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("{\"value\":\"fixture\"}", true)]
    public async Task LifecycleWritesOnlyNonEmptyResponses(string body, bool expectsOutput)
    {
        var publisher = CreatePublisher();
        var output = new List<object>();
        var processor = CreateWithTestPipeline(body, publisher.Object, output);

        await processor.ProcessAsync();

        if (expectsOutput)
        {
            Assert.Equal("fixture", Assert.IsType<TestResponse>(Assert.Single(output)).Value);
        }
        else
        {
            Assert.Empty(output);
        }
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncStart), Times.Once);
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncEnd), Times.Once);
    }

    private static Mock<IEventPublisher> CreatePublisher()
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return publisher;
    }

    private static TestProcessor CreateWithTestPipeline(string body, IEventPublisher publisher, List<object> output)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), publisher))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        var processor = new TestProcessor { UseTestPipeline = true };
        processor.Init(new ProcessorContext(pipeline.Object, publisher, new AuthHandler(),
            new OutputHandler { WriteOutput = (value, _) => { output.Add(value); return Task.CompletedTask; } }));
        return processor;
    }

    public sealed class TestResponse
    {
        public string? Value { get; set; }
    }

    public sealed class TestProcessor : KeyVaultDataPlaneProcessorBase<object, TestResponse>
    {
        public bool UseTestPipeline { get; set; }
        public Profile? CapturedProfile => ResolvedProfile;
        public IAuthHandler CapturedAuthHandler => AuthHandler;
        protected override HttpMethod Method => HttpMethod.Get;
        protected override string CreateRequestUri() => "https://fixture.invalid/";
        protected override Task BeforeProcessAsync() => UseTestPipeline ? Task.CompletedTask : base.BeforeProcessAsync();
        public TestProcessor CreateChild() => CreateExtendedProcessor<TestProcessor>();
        public Task EmitAsync(TestResponse response) => WriteOutput(response);
    }
}