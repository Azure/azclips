// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Diagnostics;

namespace Core.Telemetry.Tests;

public class TelemetryLaunchEnvironmentTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void W3cParents_RoundTripWithDistinctSpanIdsAndPreserveOptOut(bool enabled)
    {
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), enabled,
            "11111111111111111111111111111111", "2222222222222222", "vm show", DataBoundary: "EU",
            CommandTraceParent: "00-11111111111111111111111111111111-2222222222222222-01",
            CommandTraceState: "vendor=value",
            RecoveryTraceParent: "00-11111111111111111111111111111111-3333333333333333-01",
            RecoveryTraceState: "vendor=value");
        var environment = new Dictionary<string, string?>();
        TelemetryLayerEnvironment.SetAgentLaunch(environment, context, context.InvocationId);

        Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(environment.GetValueOrDefault,
            out var actual, out _, out var diagnostic));
        Assert.Null(diagnostic);
        Assert.Equal(context, actual);
        Assert.True(actual!.TryGetCommandParentContext(out var command));
        Assert.True(actual.TryGetRecoveryParentContext(out var recovery));
        Assert.Equal(command.TraceId, recovery.TraceId);
        Assert.NotEqual(command.SpanId, recovery.SpanId);
        Assert.False(command.IsRemote);
        Assert.True(recovery.IsRemote);
        Assert.Equal(ActivityTraceFlags.Recorded, recovery.TraceFlags);
        Assert.Equal("vendor=value", recovery.TraceState);
        Assert.Equal(enabled, actual.Enabled);
    }

    [Theory]
    [InlineData("malformed", null)]
    [InlineData("00-00000000000000000000000000000000-3333333333333333-01", null)]
    [InlineData("00-11111111111111111111111111111111-0000000000000000-01", null)]
    [InlineData("00-11111111111111111111111111111111-3333333333333333-xx", null)]
    [InlineData("00-44444444444444444444444444444444-3333333333333333-01", null)]
    [InlineData(null, "vendor=value")]
    [InlineData("00-11111111111111111111111111111111-3333333333333333-01", "vendor=value\nbad")]
    public void InvalidRecoveryContext_IsDiagnosedWithoutReenablingTelemetry(string? parent, string? state)
    {
        var environment = new Dictionary<string, string?>();
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), false,
            "11111111111111111111111111111111", "2222222222222222",
            RecoveryTraceParent: parent, RecoveryTraceState: state);
        TelemetryLayerEnvironment.SetAgentLaunch(environment, context, context.InvocationId);

        Assert.False(TelemetryLayerEnvironment.TryReadAgentLaunch(environment.GetValueOrDefault,
            out var actual, out _, out var diagnostic));
        Assert.Null(actual);
        Assert.Equal("invalid_agent_telemetry_context", diagnostic);
        Assert.False(TelemetryLayerEnvironment.ParentAllowsTelemetry(environment.GetValueOrDefault));
    }

    [Fact]
    public void Launch_RoundTripsCorrelationAndRemovesOldParenting()
    {
        var environment = new Dictionary<string, string?>
        {
            ["AZCLIPS_RECOVERY_TRACEPARENT"] = "old",
            ["AZCLIPS_RECOVERY_HIERARCHY"] = "old",
            [TelemetryLayerEnvironment.FileId] = "inherited",
        };
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true,
            "11111111111111111111111111111111", "2222222222222222", "vm show",
            DataBoundary: "EU");
        var fileId = Guid.NewGuid().ToString("N");

        TelemetryLayerEnvironment.SetAgentLaunch(environment, context, fileId);

        Assert.False(environment.ContainsKey("AZCLIPS_RECOVERY_TRACEPARENT"));
        Assert.False(environment.ContainsKey("AZCLIPS_RECOVERY_HIERARCHY"));
        Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
            name => environment.GetValueOrDefault(name), out var actual, out var actualFile, out var diagnostic));
        Assert.Equal(context, actual);
        Assert.Equal(fileId, actualFile);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void ExplicitParentOptOut_CannotBeReenabledByTheContextPacket()
    {
        var environment = new Dictionary<string, string?>();
        TelemetryLayerEnvironment.SetAgentLaunch(environment,
            new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true), Guid.NewGuid().ToString("N"));
        environment[TelemetryLayerEnvironment.Enabled] = "false";

        Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
            name => environment.GetValueOrDefault(name), out var actual, out _, out _));

        Assert.NotNull(actual);
        Assert.False(actual.Enabled);
        Assert.False(TelemetryLayerEnvironment.ParentAllowsTelemetry(name => environment.GetValueOrDefault(name)));
    }

    [Fact]
    public void MissingLaunch_IsDistinctFromMalformedLaunch()
    {
        Assert.False(TelemetryLayerEnvironment.TryReadAgentLaunch(
            _ => null, out _, out _, out var absentDiagnostic));
        Assert.Null(absentDiagnostic);

        var environment = new Dictionary<string, string?>
        {
            [TelemetryLayerEnvironment.Layer] = "agent",
            [TelemetryLayerEnvironment.FileId] = "..\\outside",
            [TelemetryLayerEnvironment.Context] = "{}",
            [TelemetryLayerEnvironment.Enabled] = "false",
        };
        Assert.False(TelemetryLayerEnvironment.TryReadAgentLaunch(
            name => environment.GetValueOrDefault(name), out _, out _, out var invalidDiagnostic));
        Assert.NotNull(invalidDiagnostic);
        Assert.False(TelemetryLayerEnvironment.ParentAllowsTelemetry(name => environment.GetValueOrDefault(name)));
    }

    [Theory]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData("   ", "Unknown")]
    [InlineData("Unknown", "Unknown")]
    [InlineData("unknown", "unknown")]
    [InlineData("APAC", "APAC")]
    [InlineData("Global", "Global")]
    [InlineData("global", "Global")]
    [InlineData("eu", "EU")]
    public void Routing_MatchesSharedFailClosedNormalization(string? raw, string expected)
    {
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true, DataBoundary: raw);
        Assert.Equal(expected, context.EffectiveDataBoundary);
        Assert.Equal(DataBoundaryRouting.Normalize(raw), context.EffectiveDataBoundary);
    }
}
