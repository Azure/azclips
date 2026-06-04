using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Cirrus.Extensions.Rest.Models;
using Microsoft.Azure.Cirrus.Extensions.Rest.Processors;
using Xunit;
using GetEventData = System.Func<object>;

namespace Cli.Tests;

/// <summary>
/// Offline, deterministic tests for the security-sensitive URL resolution / validation logic in
/// <see cref="RestProcessor"/>. Exercises the processor through its public <see cref="RestProcessor.ProcessAsync"/>
/// path using hand-written fakes for the runtime context (pipeline, publisher, auth, output),
/// matching the stub-based style used elsewhere in Cli.Tests (no mocking framework).
/// </summary>
public class RestProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static RestProcessor CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        string subscriptionId = TestSubscriptionId)
    {
        var processor = new RestProcessor();
        var context = new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(new AuthContext("test-tenant", subscriptionId)),
            output);
        processor.Init(context);
        return processor;
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonHttpsAbsoluteUrl_BeforeSending()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "http://management.azure.com/subscriptions/x/resourcegroups?api-version=2021-04-01";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Contains("must use https", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonArmHost_BeforeSending()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "https://example.com/subscriptions/x/resourcegroups?api-version=2021-04-01";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Contains("not an allowed Azure Resource Manager endpoint", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_PrefixesRelativePath_WithArmEndpoint_AndSubstitutesSubscriptionId()
    {
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.NotNull(sentUri);
        Assert.Equal("https", sentUri!.Scheme);
        Assert.Equal("management.azure.com", sentUri.Host);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups?api-version=2021-04-01",
            sentUri.ToString());
    }

    [Fact]
    public async Task ProcessAsync_WritesOutput_ThenThrows_OnHttpError()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "Not Found",
            Content = new StringContent("{\"error\":\"missing\"}", Encoding.UTF8, "application/json")
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups/does-not-exist?api-version=2021-04-01";

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(1, pipeline.SendCount);

        // The full response payload must be surfaced to output BEFORE the failure is thrown.
        var written = Assert.Single(output.WrittenValues);
        var response = Assert.IsType<RestResponse>(written);
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("missing", response.Content);
    }

    // ----- Fakes -------------------------------------------------------------------------------

    private sealed class FakeOutputHandler : IOutputHandler
    {
        public List<object> WrittenValues { get; } = new();

        public OutputStreamDelegate WriteOutput { get; set; }

        public FakeOutputHandler()
        {
            WriteOutput = (value, _) =>
            {
                WrittenValues.Add(value);
                return Task.CompletedTask;
            };
        }
    }

    private sealed class FakeAuthHandler : IAuthHandler
    {
        public FakeAuthHandler(AuthContext authContext) => AuthContext = authContext;

        public AuthContext AuthContext { get; set; }

        public AcquireTokenAsyncDelegate AcquireTokenAsyncDefault { get; set; } =
            (_, _, _, _) => throw new NotSupportedException("Token acquisition is not exercised in these offline tests.");

        public AcquireTokenAsyncDelegate? AcquireTokenAsyncOverride { get; set; }

        public bool ApplyOverride { get; set; }

        public SendAsyncStep SendAsync => (request, callback, next) => next.SendAsync(request, callback);
    }

    private sealed class FakeHttpPipeline : IRuntimeHttpPipeline
    {
        private readonly Func<HttpRequestMessage, IEventPublisher, HttpResponseMessage> _responder;

        public FakeHttpPipeline(Func<HttpRequestMessage, IEventPublisher, HttpResponseMessage> responder)
            => _responder = responder;

        public int SendCount { get; private set; }

        public int PollingInterval { get; set; }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback)
        {
            SendCount++;
            return Task.FromResult(_responder(request, callback));
        }

        public IRuntimeHttpPipeline Prepend(ISendAsyncFactory item) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Append(SendAsyncStep item) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Prepend(SendAsyncStep item) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Append(IEnumerable<SendAsyncStep> items) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Prepend(IEnumerable<SendAsyncStep> items) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Append(ISendAsyncFactory item) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Prepend(IEnumerable<ISendAsyncFactory> items) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Append(IEnumerable<ISendAsyncFactory> items) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Append(SendAsyncStepExpand item) => throw new NotSupportedException();
        public IRuntimeHttpPipeline Prepend(SendAsyncStepExpand item) => throw new NotSupportedException();
    }

    private sealed class FakeEventPublisher : IEventPublisher
    {
        public CancellationToken Token => CancellationToken.None;
        public Action Cancel => () => { };

        public Task Publish(string id, CancellationToken token, GetEventData createMessage) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, string messageText) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, string messageText, HttpRequestMessage request) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, string messageText, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, HttpRequestMessage request) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, CancellationToken token, EventData message) => Task.CompletedTask;

        public Task Publish(string id, GetEventData createMessage) => Task.CompletedTask;
        public Task Publish(string id) => Task.CompletedTask;
        public Task Publish(string id, string messageText) => Task.CompletedTask;
        public Task Publish(string id, string messageText, HttpRequestMessage request) => Task.CompletedTask;
        public Task Publish(string id, string messageText, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, string messageText, double magnitude) => Task.CompletedTask;
        public Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request) => Task.CompletedTask;
        public Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, HttpRequestMessage request) => Task.CompletedTask;
        public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, HttpResponseMessage response) => Task.CompletedTask;
        public Task Publish(string id, EventData message) => Task.CompletedTask;
        public Task Publish(string id, Uri uri) => Task.CompletedTask;
    }
}
