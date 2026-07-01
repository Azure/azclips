// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.Version.Models;
using Microsoft.Azure.Azclips.Extensions.Version.Processors;
using Xunit;
using GetEventData = System.Func<object>;

namespace Cli.Tests;

/// <summary>
/// Offline, deterministic tests for <see cref="GetVersionProcessor"/>. The version command is a
/// diagnostic that must NEVER fail, so these exercise the processor through its public
/// <see cref="GetVersionProcessor.ProcessAsync"/> path using hand-written fakes for the runtime
/// context (publisher, output) — matching the stub-based style used elsewhere in Cli.Tests
/// (no mocking framework). The paths section resolves through <c>AzclipsServiceProvider</c>, which
/// is not initialized here; the processor's defensive fallbacks must keep the command succeeding.
/// </summary>
public class GetVersionProcessorTests
{
    /// <summary>
    /// Builds a <see cref="GetVersionProcessor"/> wired to inert fakes: the pipeline and auth
    /// handler throw if touched (the processor makes no network/auth calls), while the output and
    /// publisher record what the processor emits for assertions.
    /// </summary>
    private static GetVersionProcessor CreateProcessor(FakeOutputHandler output, FakeEventPublisher? publisher = null)
    {
        var processor = new GetVersionProcessor();
        var context = new ProcessorContext(
            new ThrowingHttpPipeline(),
            publisher ?? new FakeEventPublisher(),
            new StubAuthHandler(),
            output);
        processor.Init(context);
        return processor;
    }

    /// <summary>
    /// The processor writes its result as a single structured <see cref="VersionInformation"/>
    /// object (not text) — the CLI/PS hosts are responsible for formatting it. Guards against a
    /// regression that emits multiple objects or a raw string.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldWriteSingleVersionInformation()
    {
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(output);

        await processor.ProcessAsync();

        var written = Assert.Single(output.WrittenValues);
        Assert.IsType<VersionInformation>(written);
    }

    /// <summary>
    /// The CLI version string is always populated. It is read from the entry (or fallback)
    /// assembly's version attributes, so it is never null/blank even in the test host.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldPopulateCliVersion()
    {
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(output);

        await processor.ProcessAsync();

        var info = Assert.IsType<VersionInformation>(output.WrittenValues.Single());
        Assert.False(string.IsNullOrWhiteSpace(info.AzCli));
    }

    /// <summary>
    /// Runtime details come from cross-platform <c>System.Runtime.InteropServices.RuntimeInformation</c>
    /// APIs, which always return a value on every OS. Asserting all three fields are non-blank
    /// documents that the runtime section is platform-agnostic and never OS-specific.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldPopulateRuntimeInformation()
    {
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(output);

        await processor.ProcessAsync();

        var info = Assert.IsType<VersionInformation>(output.WrittenValues.Single());
        Assert.NotNull(info.Runtime);
        // RuntimeInformation APIs are cross-platform and always return a value.
        Assert.False(string.IsNullOrWhiteSpace(info.Runtime.DotNet));
        Assert.False(string.IsNullOrWhiteSpace(info.Runtime.Os));
        Assert.False(string.IsNullOrWhiteSpace(info.Runtime.Architecture));
    }

    /// <summary>
    /// Core "never fail" contract. Path resolution goes through <c>AzclipsServiceProvider</c>, which
    /// is deliberately left uninitialized here so the internal lookup throws. The processor must
    /// catch that and still emit a populated object with <c>"unknown"</c> path placeholders instead
    /// of propagating the exception — `az version` is a diagnostic that must always succeed.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldNeverFail_WhenServiceProviderUninitialized()
    {
        // AzclipsServiceProvider is not initialized in this offline test, so path resolution throws
        // internally. The processor must swallow that and still emit a populated object with safe
        // "unknown" fallbacks rather than propagating the failure.
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(output);

        await processor.ProcessAsync();

        var info = Assert.IsType<VersionInformation>(output.WrittenValues.Single());
        Assert.NotNull(info.Paths);
        Assert.Equal("unknown", info.Paths.Config);
        Assert.Equal("unknown", info.Paths.Extensions);
    }

    /// <summary>
    /// The processor brackets its work with the standard start/end lifecycle events so hosts,
    /// telemetry, and progress reporting observe a consistent processor contract. Asserts both the
    /// start and end events are published.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldPublishLifecycleEvents()
    {
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        var processor = CreateProcessor(output, publisher);

        await processor.ProcessAsync();

        Assert.Contains(Events.ProcessorProcessAsyncStart, publisher.PublishedIds);
        Assert.Contains(Events.ProcessorProcessAsyncEnd, publisher.PublishedIds);
    }

    // ----- Fakes -------------------------------------------------------------------------------
    // Hand-written test doubles (no mocking framework, matching Cli.Tests convention). Only the
    // output and publisher record anything; the auth handler and HTTP pipeline throw if used,
    // documenting that the version processor performs no authentication or network I/O.

    /// <summary>Captures every value the processor writes so tests can assert on the emitted object.</summary>
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

    /// <summary>Inert auth handler: the version processor never acquires tokens, so this throws if used.</summary>
    private sealed class StubAuthHandler : IAuthHandler
    {
        public AuthContext AuthContext { get; set; } = new("test-tenant", "test-subscription");

        public AcquireTokenAsyncDelegate AcquireTokenAsyncDefault { get; set; } =
            (_, _, _, _) => throw new NotSupportedException("Token acquisition is not exercised in version tests.");

        public AcquireTokenAsyncDelegate? AcquireTokenAsyncOverride { get; set; }

        public bool ApplyOverride { get; set; }

        public SendAsyncStep SendAsync => (request, callback, next) => next.SendAsync(request, callback);
    }

    /// <summary>Inert HTTP pipeline: the version command is fully offline, so any send is a bug and throws.</summary>
    private sealed class ThrowingHttpPipeline : IRuntimeHttpPipeline
    {
        public int PollingInterval { get; set; }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) =>
            throw new NotSupportedException("The version processor performs no HTTP calls.");

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

    /// <summary>Records published event ids (in order) so lifecycle-event assertions can inspect them.</summary>
    private sealed class FakeEventPublisher : IEventPublisher
    {
        public CancellationToken Token => CancellationToken.None;
        public Action Cancel => () => { };

        public List<string> PublishedIds { get; } = new();

        public Task Publish(string id, CancellationToken token, GetEventData createMessage) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, string messageText) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, string messageText, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, string messageText, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, CancellationToken token, EventData message) { PublishedIds.Add(id); return Task.CompletedTask; }

        public Task Publish(string id, GetEventData createMessage) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, string messageText) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, string messageText, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, string messageText, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, string messageText, double magnitude) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, HttpRequestMessage request) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, HttpResponseMessage response) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, EventData message) { PublishedIds.Add(id); return Task.CompletedTask; }
        public Task Publish(string id, Uri uri) { PublishedIds.Add(id); return Task.CompletedTask; }
    }
}
