// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry.Tests;

public class TelemetryLaunchEnvironmentTests
{
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
    [InlineData(null, "Global")]
    [InlineData("unknown", "Global")]
    [InlineData("Global", "Global")]
    [InlineData("eu", "EU")]
    public void Routing_UsesTheExistingFallback(string? raw, string expected)
    {
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true, DataBoundary: raw);
        Assert.Equal(expected, context.EffectiveDataBoundary);
    }
}
