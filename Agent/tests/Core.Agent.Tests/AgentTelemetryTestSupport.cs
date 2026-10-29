// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Telemetry;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

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
        Telemetry = new TelemetryService(_provider, Configuration, new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
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

internal sealed class AgentLayerTestHarness : IAsyncDisposable
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
        Factory = new TelemetryLayerScopeFactory(new TraceDirectory(DirectoryPath), Configuration, timeProvider: timeProvider);
        Context = context ?? new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), enabled);
    }

    internal string DirectoryPath { get; }
    internal AgentTestConfiguration Configuration { get; }
    internal TelemetryLayerScopeFactory Factory { get; }
    internal TelemetryInvocationContext Context { get; }
    internal LayerTelemetryScope? Scope { get; private set; }

    internal LayerTelemetryScope CreateScope(string? fileId = null) =>
        Scope = Factory.CreateScope("Agent", Context, fileId);

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

    public async ValueTask DisposeAsync()
    {
        if (Scope is not null)
            await Scope.DisposeAsync();
    }
}

internal sealed class AgentTestTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<Timer> _timers = [];
    private readonly DateTimeOffset _epoch = DateTimeOffset.UtcNow;
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public override DateTimeOffset GetUtcNow() => _epoch.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
            timer.Change(dueTime, period);
        }
        return timer;
    }

    internal void Advance(TimeSpan duration)
    {
        List<Timer> ready;
        lock (_gate)
        {
            _ticks += duration.Ticks;
            ready = _timers.Where(timer => !timer.Disposed && timer.Due <= _ticks).ToList();
            foreach (var timer in ready)
                timer.Due = timer.Period > 0 ? _ticks + timer.Period : long.MaxValue;
        }
        foreach (var timer in ready)
            timer.Fire();
    }

    private sealed class Timer(AgentTestTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        internal long Due { get; set; }
        internal long Period { get; private set; }
        internal bool Disposed { get; private set; }
        internal void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                if (Disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks;
                Period = period.Ticks;
                return true;
            }
        }

        public void Dispose() { lock (clock._gate) Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return default; }
    }
}
