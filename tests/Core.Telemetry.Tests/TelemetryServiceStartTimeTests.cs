// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

/// <summary>
/// Coverage for the explicit start-time overloads. Startup work finishes before the
/// telemetry stack exists, so those phases are replayed afterwards as spans carrying
/// their original timestamps. These tests pin that the timestamps survive.
/// </summary>
public class TelemetryServiceStartTimeTests : IDisposable
{
    private readonly ActivitySource _source;
    private readonly ActivityListener _listener;
    private readonly StubTelemetryProvider _provider;
    private readonly EnabledConfigurationService _config;
    private readonly TelemetryService _telemetryService;

    public TelemetryServiceStartTimeTests()
    {
        _source = new ActivitySource("azclips-starttime-test-" + Guid.NewGuid().ToString("N"));
        _listener = new ActivityListener
        {
            ShouldListenTo = src => src == _source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);

        _provider = new StubTelemetryProvider(_source);
        _config = new EnabledConfigurationService();
        _telemetryService = new TelemetryService(_provider, _config, ClientType.CLI);
    }

    public void Dispose()
    {
        _telemetryService.Dispose();
        _listener.Dispose();
        _source.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void StartActivity_WithPastStartTime_UsesThatStartTime()
    {
        var past = DateTimeOffset.UtcNow.AddMilliseconds(-500);

        using var activity = _telemetryService.StartActivity("test.op", ActivityKind.Internal, past);

        Assert.NotNull(activity);
        Assert.Equal(past.UtcDateTime, activity.StartTimeUtc);
    }

    [Fact]
    public void StartActivity_WithoutStartTime_StampsCurrentTime()
    {
        var before = DateTime.UtcNow;

        using var activity = _telemetryService.StartActivity("test.op");

        Assert.NotNull(activity);
        Assert.InRange(activity.StartTimeUtc, before, DateTime.UtcNow);
    }

    [Fact]
    public void StartActivity_PastStartTimePlusExplicitEnd_ProducesRecordedDuration()
    {
        var start = DateTimeOffset.UtcNow.AddMilliseconds(-500);
        var end = start.AddMilliseconds(300);

        using var activity = _telemetryService.StartActivity("test.op", ActivityKind.Internal, start);
        activity!.SetEndTime(end.UtcDateTime);

        Assert.Equal(TimeSpan.FromMilliseconds(300), activity.Duration);
    }

    [Fact]
    public void StartRootActivity_WithPastStartTime_BackdatesAndKeepsInvariantTags()
    {
        var past = DateTimeOffset.UtcNow.AddMilliseconds(-500);

        using var activity = _telemetryService.StartRootActivity(past);

        Assert.NotNull(activity);
        Assert.Equal(past.UtcDateTime, activity.StartTimeUtc);
        Assert.Equal("CLI", activity.GetTagItem(TelemetryTags.ClientType));
        Assert.NotNull(activity.GetTagItem(TelemetryTags.ProductVersion));
    }

    [Fact]
    public void StartRootActivity_WithCustomName_UsesItAndKeepsInvariantTags()
    {
        using var activity = _telemetryService.StartRootActivity(name: "CLI.Activity.AiRecovery");

        Assert.NotNull(activity);
        Assert.Equal("CLI.Activity.AiRecovery", activity.OperationName);
        // eventName mirrors the span name; the invariants are what make a root row queryable.
        Assert.Equal("CLI.Activity.AiRecovery", activity.GetTagItem(TelemetryTags.EventName));
        Assert.NotNull(activity.GetTagItem(TelemetryTags.InstallationId));
        Assert.NotNull(activity.GetTagItem(TelemetryTags.ProductName));
        Assert.NotNull(activity.GetTagItem(TelemetryTags.ClientType));
    }

    [Fact]
    public void StartRootActivity_WithoutName_KeepsTheClientTypeDefault()
    {
        using var activity = _telemetryService.StartRootActivity();

        Assert.NotNull(activity);
        Assert.Equal("CLI.Activity.Root", activity.OperationName);
    }

    /// <summary>Minimal ITelemetryProvider exposing a caller-supplied ActivitySource.</summary>
    private sealed class StubTelemetryProvider : ITelemetryProvider
    {
        public StubTelemetryProvider(ActivitySource source)
        {
            ActivitySource = source;
            // Dedicated TracerProvider so TelemetryService.Dispose() does not dispose
            // the shared TracerProvider.Default used by other tests in this process.
            TracerProvider = Sdk.CreateTracerProviderBuilder().Build()!;
        }

        public ActivitySource ActivitySource { get; }
        public TracerProvider TracerProvider { get; }
    }

    /// <summary>IConfigurationService stub that always reports telemetry as enabled.</summary>
    private sealed class EnabledConfigurationService : IConfigurationService
    {
        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(TelemetryOptions))
            {
                return (T)(object)new TelemetryOptions { Enabled = true };
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
