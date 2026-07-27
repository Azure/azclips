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
        _telemetryService = new TelemetryService(_provider, _config, ClientType.PowerShell);
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

    /// <summary>Minimal ITelemetryProvider exposing a caller-supplied ActivitySource.</summary>
    private sealed class StubTelemetryProvider : ITelemetryProvider
    {
        public StubTelemetryProvider(ActivitySource source)
        {
            ActivitySource = source;
            // Build a dedicated TracerProvider so TelemetryService.Dispose() does
            // not dispose the shared TracerProvider.Default instance — that would
            // affect other tests running in the same process.
            TracerProvider = Sdk.CreateTracerProviderBuilder().Build()!;
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
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
