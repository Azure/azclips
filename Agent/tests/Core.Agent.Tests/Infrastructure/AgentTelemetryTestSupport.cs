// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.AssemblyLoading;
using Core.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Trace;
using Tests.Common.Utilities;

namespace Core.Agent.Tests;

internal sealed class AgentTestConfiguration(bool enabled = true) : IConfigurationService
{
    internal bool Enabled { get; set; } = enabled;
    internal AgentOptions Options { get; } = new();

    public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new() =>
        typeof(T) == typeof(TelemetryOptions) ? (T)(object)new TelemetryOptions { Enabled = Enabled } :
        typeof(T) == typeof(AgentOptions) ? (T)(object)Options : defaultValue ?? new T();

    public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public string GetConfigFilePath() => throw new NotSupportedException();
    public IConfigurationSchema GetSchema() => throw new NotSupportedException();
}

internal sealed class AgentTraceCapture : IDisposable
{
    private sealed class Provider(string name) : ITelemetryProvider
    {
        public ActivitySource ActivitySource { get; } = new(name);
        public TracerProvider TracerProvider => TracerProvider.Default;
    }

    private readonly Provider _provider;
    private readonly ActivityListener? _listener;
    internal ConcurrentQueue<Activity> Stopped { get; } = new();
    internal ITelemetryService Telemetry { get; }
    internal ActivitySource Source => _provider.ActivitySource;
    internal AgentTestConfiguration Configuration { get; }

    internal AgentTraceCapture(bool enabled = true, bool listen = true, string sourceName = "Agent")
    {
        _provider = new Provider(sourceName);
        Configuration = new AgentTestConfiguration(enabled);
        Telemetry = new TelemetryService(_provider, Configuration,
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"),
            new AssemblyLoadingService(new DefaultAssemblyLoadingProvider(), new DefaultAssemblyVersionProvider()));
        if (listen)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => ReferenceEquals(source, Source),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Stopped.Enqueue,
            };
            ActivitySource.AddActivityListener(_listener);
        }
    }

    internal IDisposable? StartToolActivity(string name) => Telemetry.StartActivity(name);

    internal static void AssertParent(Activity activity, Activity? parent)
    {
        Assert.Equal(parent?.SpanId ?? default, activity.ParentSpanId);
        if (parent is not null)
            Assert.Equal(parent.TraceId, activity.TraceId);
        Assert.DoesNotContain(activity.TagObjects, tag =>
            tag.Key.StartsWith("properties.hierarchy.", StringComparison.Ordinal));
    }

    internal static Task<object?> InvokeCapturedAsync(
        ExecutionContext captured, AIFunction function, AIFunctionArguments arguments)
    {
        Task<object?>? invocation = null;
        ExecutionContext.Run(captured.CreateCopy(), _ => invocation = InvokeAndCheckAsync(), null);
        return invocation!;

        async Task<object?> InvokeAndCheckAsync()
        {
            var previous = Activity.Current;
            try
            {
                return await function.InvokeAsync(arguments);
            }
            finally
            {
                Assert.Same(previous, Activity.Current);
            }
        }
    }

    public void Dispose()
    {
        _listener?.Dispose();
        Source.Dispose();
    }
}

internal sealed class AgentLayerTestHarness
{
    private sealed class TraceDirectory(string path) : ITraceDirectoryProvider
    {
        public string GetTraceDirectory() => path;
    }

    internal AgentLayerTestHarness(
        bool enabled = true, TimeProvider? timeProvider = null, TelemetryInvocationContext? context = null)
    {
        DirectoryPath = Path.Combine(AppContext.BaseDirectory, "TestArtifacts", "AgentLayers", Guid.NewGuid().ToString("N"));
        Configuration = new AgentTestConfiguration(enabled);
        Clock = timeProvider ?? new FakeTimeProvider();
        Factory = new TelemetryLayerScopeFactory(new TraceDirectory(DirectoryPath), Configuration,
            new AssemblyLoadingService(new DefaultAssemblyLoadingProvider(), new DefaultAssemblyVersionProvider()),
            timeProvider: Clock);
        Context = context ?? new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), enabled);
    }

    internal string DirectoryPath { get; }

    /// <summary>The clock for every product budget in the test; it advances only when the test advances it.</summary>
    internal TimeProvider Clock { get; }
    internal AgentTestConfiguration Configuration { get; }
    internal TelemetryLayerScopeFactory Factory { get; }
    internal TelemetryInvocationContext Context { get; }
    internal LayerTelemetryScope? Scope { get; private set; }
    private readonly List<LayerTelemetryScope> _owned = [];

    /// <summary>Creates the harness scope. Use <see cref="Own"/> for any further scope.</summary>
    internal LayerTelemetryScope CreateScope(string? fileId = null)
    {
        Scope = Own(Factory.CreateScope("Agent", Context, fileId));
        return Scope;
    }

    /// <summary>Adds a scope created from <see cref="Factory"/> outside <see cref="CreateScope"/> to disposal.</summary>
    internal LayerTelemetryScope Own(LayerTelemetryScope scope)
    {
        _owned.Add(scope);
        return scope;
    }

    /// <summary>Disposes the scope, then waits until its provider releases the layer source.</summary>
    internal static async Task ReleaseAsync(LayerTelemetryScope scope)
    {
        await scope.DisposeAsync();
        if (!await scope.ProviderDisposalCompletion.WaitAsync(AgentTestTimeouts.HangGuard))
            throw new InvalidOperationException("The Agent layer scope did not release its telemetry provider.");
    }

    internal JsonElement[] ReadEntries() => ReadEntries(Scope!);

    internal static Dictionary<string, JsonElement> Tags(JsonElement entry) =>
        entry.GetProperty("tags").EnumerateArray().ToDictionary(
            tag => tag.GetProperty("Key").GetString()!, tag => tag.GetProperty("Value").Clone());

    internal static JsonElement[] ReadEntries(LayerTelemetryScope scope)
    {
        var path = scope.FileSession.PublishedPath;
        Assert.NotNull(path);
        return File.ReadAllText(path).Split("--- Batch at ")
            .Where(part => part.Contains('['))
            .SelectMany(part =>
            {
                using var json = JsonDocument.Parse(part[part.IndexOf('[')..(part.LastIndexOf(']') + 1)]);
                return json.RootElement.EnumerateArray().Select(entry => entry.Clone()).ToArray();
            }).ToArray();
    }

    /// <summary>
    /// Runs <paramref name="body"/> with console output captured, then releases every owned scope.
    /// Callers are in the non-parallel "AgentTelemetry" collection, so swapping the console is safe.
    /// </summary>
    internal static Task RunAsync(AgentLayerTestHarness harness, Func<AgentLayerTestHarness, Task> body) =>
        WithCapturedConsoleAsync(async () =>
        {
            try
            {
                await body(harness);
            }
            finally
            {
                foreach (var scope in harness._owned)
                    await ReleaseAsync(scope);
                harness._owned.Clear();
            }
        });

    /// <summary>
    /// Runs <paramref name="body"/> with <see cref="Console.Out"/> and <see cref="Console.Error"/>
    /// redirected to a buffer, so simulated failures do not reach the test log.
    /// </summary>
    internal static async Task<T> WithCapturedConsoleAsync<T>(Func<Task<T>> body)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        Console.SetError(captured);
        try
        {
            return await body();
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    internal static Task WithCapturedConsoleAsync(Func<Task> body) =>
        WithCapturedConsoleAsync(async () => { await body(); return true; });
}
