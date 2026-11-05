// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

/// <summary>
/// Regression coverage for the telemetry.enabled runtime gate: the config value
/// must take effect on the very next Start*Activity call, without requiring a
/// module reload. The check lives in TelemetryService and consults
/// IConfigurationService on every call — so the ITelemetryProvider singleton
/// no longer freezes the decision at ctor time.
/// </summary>
public class TelemetryServiceEnabledGateTests : IDisposable
{
    private readonly ActivitySource _source;
    private readonly ActivityListener _listener;
    private readonly StubTelemetryProvider _provider;
    private readonly ToggleableConfigurationService _config;
    private readonly TelemetryService _telemetryService;

    public TelemetryServiceEnabledGateTests()
    {
        // Unique source name per fixture so parallel tests stay isolated.
        _source = new ActivitySource("azclips-gate-test-" + Guid.NewGuid().ToString("N"));
        _listener = new ActivityListener
        {
            ShouldListenTo = src => src == _source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);

        _provider = new StubTelemetryProvider(_source);
        _config = new ToggleableConfigurationService(enabled: true);
        _telemetryService = new TelemetryService(
            _provider,
            _config,
            new ClientMetadata(ClientType.PowerShell, "00000000-0000-0000-0000-000000000000"),
            new AssemblyVersionProvider());
    }

    public void Dispose()
    {
        _telemetryService.Dispose();
        _listener.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void StartActivity_WhenEnabled_ReturnsActivity()
    {
        _config.Enabled = true;

        using var activity = _telemetryService.StartActivity("test.op");

        Assert.NotNull(activity);
    }

    [Fact]
    public void StartActivity_WhenDisabled_ReturnsNull()
    {
        _config.Enabled = false;

        using var activity = _telemetryService.StartActivity("test.op");

        Assert.Null(activity);
    }

    [Fact]
    public void StartRootActivity_WhenEnabled_ReturnsActivity()
    {
        _config.Enabled = true;

        using var activity = _telemetryService.StartRootActivity();

        Assert.NotNull(activity);
    }

    [Fact]
    public void StartRootActivity_WhenDisabled_ReturnsNull()
    {
        _config.Enabled = false;

        using var activity = _telemetryService.StartRootActivity();

        Assert.Null(activity);
    }

    [Fact]
    public void ToggleFromEnabledToDisabled_TakesEffectImmediately_OnNextCall()
    {
        // Simulates Teresa's scenario: cmdlet runs while enabled, then Set-AzConfig
        // flips telemetry.enabled=false, then another cmdlet runs.
        _config.Enabled = true;
        using (var first = _telemetryService.StartRootActivity())
        {
            Assert.NotNull(first);
        }

        _config.Enabled = false;

        using var second = _telemetryService.StartRootActivity();
        Assert.Null(second);
    }

    [Fact]
    public void ToggleFromDisabledToEnabled_TakesEffectImmediately_OnNextCall()
    {
        // Mirror scenario: session starts with enabled=false (ctor gate would
        // have short-circuited the pipeline). After moving the check into the
        // service, flipping to true must work without a session restart.
        _config.Enabled = false;
        using (var first = _telemetryService.StartRootActivity())
        {
            Assert.Null(first);
        }

        _config.Enabled = true;

        using var second = _telemetryService.StartRootActivity();
        Assert.NotNull(second);
    }

    [Fact]
    public void IndependentRoot_DoesNotInheritLiveAmbient()
    {
        using var ambient = new Activity("unrelated").Start();
        using (var scope = _telemetryService.StartIndependentRootActivity("Agent.Session"))
        {
            var root = scope?.Activity;
            Assert.NotNull(root);
            Assert.Equal(default, root.ParentSpanId);
            Assert.Null(root.Parent);
            Assert.NotEqual(ambient.TraceId, root.TraceId);
            Assert.Same(root, Activity.Current);
            Assert.Equal("PowerShell", root.GetTagItem(TelemetryTags.ProductName));
        }
        Assert.Same(ambient, Activity.Current);
    }

    [Fact]
    public void DetachedRoot_PreservesCliParentForSubsequentWork()
    {
        using var ambient = new Activity("command").Start();
        using (var scope = _telemetryService.StartIndependentRootActivity(
            "tool.find_resources", ActivityKind.Internal, activate: false))
        {
            var tool = scope?.Activity;
            Assert.NotNull(tool);
            Assert.Equal(default, tool.ParentSpanId);
            Assert.Null(tool.ParentId);
            Assert.Same(ambient, Activity.Current);
        }
        Assert.Same(ambient, Activity.Current);
        using var commandWork = _telemetryService.StartActivity("auth");
        Assert.NotNull(commandWork);
        Assert.Equal(ambient.SpanId, commandWork.ParentSpanId);
    }

    [Fact]
    public void ExplicitDefaultParent_StartsRootRatherThanInheritingAmbient()
    {
        using var ambient = new Activity("unrelated").Start();
        using var scope = _telemetryService.StartActivityWithParent("Agent.Create", default);
        var root = scope?.Activity;
        Assert.NotNull(root);
        Assert.Equal(default, root.ParentSpanId);
        Assert.NotEqual(ambient.TraceId, root.TraceId);
    }

    [Fact]
    public void NewCreationMethods_RespectLiveOptOut()
    {
        _config.Enabled = false;
        Assert.Null(_telemetryService.StartIndependentRootActivity("Agent.Session"));
        Assert.Null(_telemetryService.StartActivityWithParent("Agent.Send", default));
        _config.Enabled = true;
        using var root = _telemetryService.StartIndependentRootActivity("Agent.Session");
        Assert.NotNull(root);
    }

    [Fact]
    public void LegacyRootMethod_PreservesAmbientSemantics()
    {
        using var ambient = new Activity("existing").Start();
        using var root = _telemetryService.StartRootActivity();
        Assert.NotNull(root);
        Assert.Equal(ambient.SpanId, root.ParentSpanId);
    }

    /// <summary>Minimal ITelemetryProvider exposing a caller-supplied ActivitySource.</summary>
    private sealed class StubTelemetryProvider : ITelemetryProvider
    {
        public StubTelemetryProvider(ActivitySource source)
        {
            ActivitySource = source;
            // Build a dedicated TracerProvider so TelemetryService.Dispose() does
            // not dispose the shared TracerProvider.Default instance — that would
            // affect other tests running in the same process.
            TracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(source.Name)
                .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
                .Build()!;
        }

        public ActivitySource ActivitySource { get; }
        public TracerProvider TracerProvider { get; }
    }

    /// <summary>
    /// IConfigurationService stub whose GetConfiguration&lt;TelemetryOptions&gt;()
    /// reads the current value of a mutable flag on every call.
    /// </summary>
    private sealed class ToggleableConfigurationService : IConfigurationService
    {
        public ToggleableConfigurationService(bool enabled) => Enabled = enabled;

        public bool Enabled { get; set; }

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(TelemetryOptions))
            {
                return (T)(object)new TelemetryOptions { Enabled = Enabled };
            }
            return defaultValue ?? new T();
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
