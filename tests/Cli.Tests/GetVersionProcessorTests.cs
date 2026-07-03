// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Version.Models;
using Microsoft.Azure.Azclips.Extensions.Version.Processors;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GetEventData = System.Func<object>;

namespace Cli.Tests;

/// <summary>
/// Offline, deterministic tests for <see cref="GetVersionProcessor"/>, exercised through its public
/// <see cref="GetVersionProcessor.ProcessAsync"/> path using hand-written fakes for the runtime
/// context (publisher, output) — matching the stub-based style used elsewhere in Cli.Tests (no
/// mocking framework). Path resolution pulls an <see cref="IConfigurationService"/> from
/// <c>AzclipsServiceProvider</c>, and the processor deliberately does NOT swallow failures, so the
/// happy-path tests initialize the container with a fake configuration service (mirroring CLI
/// startup) while a dedicated test asserts that an uninitialized container surfaces the error.
/// Because the provider is process-wide static state, this class joins the serialized
/// service-provider collection and resets the provider around every test.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class GetVersionProcessorTests : IDisposable
{
    // The processor derives Paths.Config from the directory of the configuration file path, so use a
    // deterministic file path to assert the exact value the processor reports.
    private static readonly string ConfigFilePath =
        Path.Combine(Path.GetTempPath(), "azclips-version-tests", Guid.NewGuid().ToString("N"), "config.json");

    // xUnit builds a fresh instance per test; start and end from a disposed provider so the static
    // container state never leaks between tests regardless of execution order.
    public GetVersionProcessorTests() => AzclipsServiceProvider.Dispose();

    public void Dispose() => AzclipsServiceProvider.Dispose();

    /// <summary>
    /// Initializes <c>AzclipsServiceProvider</c> with a fake <see cref="IConfigurationService"/> so
    /// the processor can resolve configuration paths, mirroring how <c>az version</c> runs after the
    /// CLI has started the DI container.
    /// </summary>
    private static void InitializeServiceProvider()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
            services.AddSingleton<IConfigurationService>(new FakeConfigurationService(ConfigFilePath)));
        AzclipsServiceProvider.Initialize(ClientType.CLI);
    }

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
        InitializeServiceProvider();
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
        InitializeServiceProvider();
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
        InitializeServiceProvider();
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
    /// Path resolution goes through <c>AzclipsServiceProvider</c>, which is deliberately left
    /// uninitialized here (the constructor disposed it). The processor intentionally does NOT swallow
    /// the resulting failure — section gathering is undefended so an initialization/configuration
    /// problem surfaces cleanly instead of being masked by an "unknown" placeholder. Asserts the
    /// exception propagates out of <see cref="GetVersionProcessor.ProcessAsync"/> and that no partial
    /// result is written.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldThrow_WhenServiceProviderUninitialized()
    {
        // The provider is disposed (constructor), so IConfigurationService cannot be resolved and the
        // undefended path gathering must bubble the initialization error up through ProcessAsync.
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(output);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Empty(output.WrittenValues);
    }

    /// <summary>
    /// With the container initialized, the processor reports the configuration directory derived from
    /// <see cref="IConfigurationService.GetConfigFilePath"/> — matching the config-driven paths the
    /// CLI actually uses. Asserts the reported <c>Config</c> path is the directory of that file.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldReportConfigDirectory_FromConfigurationService()
    {
        InitializeServiceProvider();
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(output);

        await processor.ProcessAsync();

        var info = Assert.IsType<VersionInformation>(output.WrittenValues.Single());
        Assert.NotNull(info.Paths);
        Assert.Equal(Path.GetDirectoryName(ConfigFilePath), info.Paths.ConfigLocation);
    }

    /// <summary>
    /// The processor brackets its work with the standard start/end lifecycle events so hosts,
    /// telemetry, and progress reporting observe a consistent processor contract. Asserts both the
    /// start and end events are published.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldPublishLifecycleEvents()
    {
        InitializeServiceProvider();
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        var processor = CreateProcessor(output, publisher);

        await processor.ProcessAsync();

        Assert.Contains(Events.ProcessorProcessAsyncStart, publisher.PublishedIds);
        Assert.Contains(Events.ProcessorProcessAsyncEnd, publisher.PublishedIds);
    }

    // ----- Fakes -------------------------------------------------------------------------------
    // Hand-written test doubles (no mocking framework, matching Cli.Tests convention). Only the
    // output, publisher, and configuration service record/return anything; the auth handler and HTTP
    // pipeline throw if used, documenting that the version processor performs no authentication or
    // network I/O.

    /// <summary>
    /// Minimal <see cref="IConfigurationService"/> returning a fixed config file path. The version
    /// processor only calls <see cref="GetConfigFilePath"/>; every other member is unreachable in
    /// these tests and throws to document that.
    /// </summary>
    private sealed class FakeConfigurationService(string configFilePath) : IConfigurationService
    {
        public string GetConfigFilePath() => configFilePath;

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
            => throw new NotSupportedException("The version processor does not read configuration values.");

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }

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
        // The version processor performs no authentication, so no profile is required (null mirrors the
        // "no profile available" state the interface documents).
        public Profile? Profile { get; set; }

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
