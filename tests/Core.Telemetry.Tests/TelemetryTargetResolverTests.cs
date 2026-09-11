// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TelemetryTargetResolverTests
{
    private static IReadOnlyList<TelemetryTarget> LocalOnly => [TelemetryTarget.Local];
    private static IReadOnlyList<TelemetryTarget> RemoteOnly => [TelemetryTarget.Remote];
    private static IReadOnlyList<TelemetryTarget> Both => [TelemetryTarget.Local, TelemetryTarget.Remote];

    [Fact]
    public void Resolve_ReturnsCompileDefault_WhenEnvUnset()
    {
        Assert.Equal(Both, TelemetryTargetResolver.Resolve(Both, _ => null));
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.Resolve(RemoteOnly, _ => null));
        Assert.Equal(LocalOnly, TelemetryTargetResolver.Resolve(LocalOnly, _ => null));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("LOCAL")]
    [InlineData("  local  ")]
    [InlineData("LoCaL")]
    public void Resolve_LocalToken_ReturnsLocalOnly(string value)
    {
        // Start from a different default to prove the override wins.
        Assert.Equal(LocalOnly, TelemetryTargetResolver.Resolve(Both, _ => value));
    }

    [Theory]
    [InlineData("remote")]
    [InlineData("REMOTE")]
    [InlineData("  remote  ")]
    public void Resolve_RemoteToken_ReturnsRemoteOnly(string value)
    {
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.Resolve(Both, _ => value));
    }

    [Theory]
    [InlineData("both")]
    [InlineData("BOTH")]
    [InlineData("  both  ")]
    public void Resolve_BothToken_ReturnsLocalAndRemote(string value)
    {
        Assert.Equal(Both, TelemetryTargetResolver.Resolve(RemoteOnly, _ => value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("locale")]
    [InlineData("none")]
    [InlineData("local,remote")]
    public void Resolve_FallsBackToCompileDefault_OnUnrecognized(string value)
    {
        // "none" is not a token: telemetry.enabled=false is the only opt-out.
        // "local,remote" is not a token: the accepted spelling is "both".
        Assert.Equal(Both, TelemetryTargetResolver.Resolve(Both, _ => value));
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.Resolve(RemoteOnly, _ => value));
        Assert.Equal(LocalOnly, TelemetryTargetResolver.Resolve(LocalOnly, _ => value));
    }

    [Fact]
    public void Resolve_NeverReturnsAnEmptySet()
    {
        foreach (var value in new[] { "local", "remote", "both", "bogus", "", "   " })
        {
            Assert.NotEmpty(TelemetryTargetResolver.Resolve(Both, _ => value));
        }

        Assert.NotEmpty(TelemetryTargetResolver.Resolve(Both, _ => null));
    }

    [Fact]
    public void CompileTimeDefault_MatchesBuildConfiguration()
    {
#if DEBUG
        Assert.Equal(Both, TelemetryTargetResolver.CompileTimeDefault);
#else
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.CompileTimeDefault);
#endif
    }

    [Fact]
    public void EnvironmentReader_IsLiveInDebug_AndInertInRelease()
    {
#if DEBUG
        // Uniquely named so it never collides with a real setting.
        const string ProbeVariable = "AZCLIPS_TELEMETRY_TARGET_READER_PROBE";
        try
        {
            Environment.SetEnvironmentVariable(ProbeVariable, "probe-value");
            Assert.Equal("probe-value", TelemetryTargetResolver.EnvironmentReader(ProbeVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProbeVariable, null);
        }
#else
        // PATH is always set, so a null result proves the reader is inert.
        Assert.Null(TelemetryTargetResolver.EnvironmentReader("PATH"));
        Assert.Equal(RemoteOnly, TelemetryTargetResolver.Resolve());
#endif
    }
}
