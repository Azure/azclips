// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TelemetryTargetResolverTests
{
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    internal void Resolve_ReturnsCompileDefault_WhenEnvUnset(TelemetryTarget compileDefault)
    {
        var result = TelemetryTargetResolver.Resolve(compileDefault, _ => null);

        Assert.Equal(compileDefault, result);
    }

    [Theory]
    [InlineData("local", TelemetryTarget.Local)]
    [InlineData("LOCAL", TelemetryTarget.Local)]
    [InlineData("  local  ", TelemetryTarget.Local)]
    [InlineData("remote", TelemetryTarget.Remote)]
    [InlineData("REMOTE", TelemetryTarget.Remote)]
    internal void Resolve_HonorsEnvOverride_RegardlessOfDefault(string value, TelemetryTarget expected)
    {
        // Start from the opposite compile default to prove the override always wins.
        var opposite = expected == TelemetryTarget.Local ? TelemetryTarget.Remote : TelemetryTarget.Local;

        var result = TelemetryTargetResolver.Resolve(opposite, _ => value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("locale")]
    public void Resolve_FallsBackToCompileDefault_OnUnrecognized(string value)
    {
        Assert.Equal(TelemetryTarget.Remote, TelemetryTargetResolver.Resolve(TelemetryTarget.Remote, _ => value));
        Assert.Equal(TelemetryTarget.Local, TelemetryTargetResolver.Resolve(TelemetryTarget.Local, _ => value));
    }
}
