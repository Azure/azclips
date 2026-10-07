using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FoundationRejectsExecutionBeforeNetworkOrOutput(bool hasProfile, bool skipBaseHook)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        var publisher = CreatePublisher();
        var profile = hasProfile ? new Profile { TenantId = "fixture-tenant" } : null;
        var authHandler = new AuthHandler { Profile = profile };
        var output = new List<object>();
        var processor = new TestProcessor { SkipBaseHook = skipBaseHook };
        processor.Init(new ProcessorContext(pipeline.Object, publisher.Object, authHandler,
            new OutputHandler { WriteOutput = (value, _) => { output.Add(value); return Task.CompletedTask; } }, profile));

        await Assert.ThrowsAsync<NotSupportedException>(() => processor.ProcessAsync());

        pipeline.VerifyNoOtherCalls();
        Assert.Empty(output);
        Assert.False(authHandler.ApplyOverride);
        Assert.Null(authHandler.AcquireTokenAsyncOverride);
        Assert.False(processor.BeforeProcessCalled);
        Assert.False(processor.SendCoreCalled);
        Assert.False(processor.RequestCreated);
        publisher.Verify(value => value.Publish(Events.ProcessorBeforeHttpRequestCreate), Times.Never);
        publisher.Verify(value => value.Publish(Events.ProcessorProcessAsyncEnd), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FoundationRejectsDirectAndChildSendsBeforeCustomization(bool useChild)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        var publisher = CreatePublisher();
        var processor = new TestProcessor { SkipBaseHook = true };
        processor.Init(new ProcessorContext(pipeline.Object, publisher.Object, new AuthHandler(),
            new OutputHandler { WriteOutput = (_, _) => throw new InvalidOperationException("Unexpected output.") }));
        var target = useChild ? processor.CreateChild() : processor;
        var send = typeof(KeyVaultDataPlaneProcessorBase).GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.False(send.IsVirtual);
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            try
            {
                using var response = await (Task<HttpResponseMessage>)send.Invoke(target, null)!;
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            }
        });

        pipeline.VerifyNoOtherCalls();
        Assert.False(target.BeforeProcessCalled);
        Assert.False(target.SendCoreCalled);
        Assert.False(target.RequestCreated);
        publisher.Verify(value => value.Publish(Events.ProcessorBeforeHttpRequestCreate), Times.Never);
        publisher.Verify(value => value.Publish(Events.TracingHttpResponseMessage, It.IsAny<HttpResponseMessage>()), Times.Never);
    }

    [Fact]
    public async Task MalformedResponseDoesNotIncludePayloadInException()
    {
        const string sensitiveValue = "fixture-private-value";
        var publisher = CreatePublisher();
        var output = new List<object>();
        var processor = CreateForResponseProcessing(publisher.Object, output);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"value\":\"" + sensitiveValue + "\",\"broken\":}")
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessResponseForTestAsync(response));

        Assert.DoesNotContain(sensitiveValue, exception.ToString());
        Assert.Null(exception.InnerException);
        Assert.Empty(output);
        publisher.Verify(value => value.Publish(Events.ResponseInvalidJson, "Response has invalid JSON."), Times.Once);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("{\"value\":\"fixture\"}", true)]
    public async Task ResponseProcessingWritesOnlyNonEmptyResponses(string body, bool expectsOutput)
    {
        var publisher = CreatePublisher();
        var output = new List<object>();
        var processor = CreateForResponseProcessing(publisher.Object, output);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

        await processor.ProcessResponseForTestAsync(response);

        if (expectsOutput)
        {
            Assert.Equal("fixture", Assert.IsType<TestResponse>(Assert.Single(output)).Value);
        }
        else
        {
            Assert.Empty(output);
        }
        publisher.Verify(value => value.Publish(Events.ResponseEmptyBody, "Response has an empty body."),
            expectsOutput ? Times.Never() : Times.Once());
    }

    private static Mock<IEventPublisher> CreatePublisher()
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        return publisher;
    }

    private static TestProcessor CreateForResponseProcessing(IEventPublisher publisher, List<object> output)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>(MockBehavior.Strict);
        var processor = new TestProcessor();
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
        public bool SkipBaseHook { get; set; }
        public bool BeforeProcessCalled { get; private set; }
        public bool SendCoreCalled { get; private set; }
        public bool RequestCreated { get; private set; }
        public Profile? CapturedProfile => ResolvedProfile;
        public IAuthHandler CapturedAuthHandler => AuthHandler;
        protected override HttpMethod Method => HttpMethod.Get;
        protected override string CreateRequestUri() => "https://fixture.invalid/";
        protected override Task BeforeProcessAsync()
        {
            BeforeProcessCalled = true;
            return SkipBaseHook ? Task.CompletedTask : base.BeforeProcessAsync();
        }
        protected override Task<HttpResponseMessage> SendCoreAsync()
        {
            SendCoreCalled = true;
            return base.SendCoreAsync();
        }
        protected override Task<HttpRequestMessage> CreateRequestMessage()
        {
            RequestCreated = true;
            return base.CreateRequestMessage();
        }
        public TestProcessor CreateChild() => CreateExtendedProcessor<TestProcessor>();
        public Task EmitAsync(TestResponse response) => WriteOutput(response);
        public Task ProcessResponseForTestAsync(HttpResponseMessage response) => ProcessResponseAsync(response);
    }
}