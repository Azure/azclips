// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using Core.Abstractions;
using Core.Cli.Agent;
using Xunit;

namespace Cli.Tests.Agent;

/// <summary>
/// The exclusion set gates whether a failing command's exception bypasses the
/// agent fallback. It is empty by design today (every exception still reaches the
/// agent). These tests pin that contract so a future addition is a deliberate,
/// test-visible change rather than a silent behavior shift.
/// </summary>
public class AgentFallbackExclusionsTests
{
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(HttpRequestExceptionShim))]
    [InlineData(typeof(Exception))]
    public void IsExcluded_IsAlwaysFalse_WhileExclusionSetIsEmpty(Type exType)
    {
        var ex = (Exception)Activator.CreateInstance(exType)!;
        Assert.False(AgentFallbackExclusions.IsExcluded(ex));
    }

    [Fact]
    public void IsExcluded_NullSafe()
    {
        Assert.False(AgentFallbackExclusions.IsExcluded(null!));
    }

    // Stand-in so the theory can name a distinct concrete type without pulling in
    // System.Net.Http just for construction.
    private sealed class HttpRequestExceptionShim : Exception { }
}

/// <summary>
/// AgentFallbackTrace owns the session trace file on the parent side: when tracing
/// is enabled it writes the triggering exception as the first JSONL line and
/// returns the path (to hand to the child). When disabled it writes nothing and
/// returns null.
/// </summary>
public class AgentFallbackTraceTests
{
    [Fact]
    public void BeginSession_Disabled_WritesNothing_ReturnsNull()
    {
        var dir = NewTempDir();
        var trace = new TraceOptions { Enabled = false, Path = dir };

        var path = AgentFallbackTrace.BeginSession(
            trace, new InvalidOperationException("boom"), "Exception", excluded: false, willSpawn: true);

        Assert.Null(path);
        Assert.Empty(Directory.GetFiles(dir));
    }

    [Fact]
    public void BeginSession_Enabled_WritesExceptionRecord_AsFirstLine()
    {
        var dir = NewTempDir();
        var trace = new TraceOptions { Enabled = true, Path = dir };

        var path = AgentFallbackTrace.BeginSession(
            trace,
            new InvalidOperationException("the message"),
            source: "Exception",
            excluded: false,
            willSpawn: true);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));

        var lines = File.ReadAllLines(path!);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.Equal("fallback_exception", root.GetProperty("kind").GetString());
        var data = root.GetProperty("data");
        Assert.Equal(typeof(InvalidOperationException).FullName, data.GetProperty("exceptionType").GetString());
        Assert.Equal("the message", data.GetProperty("message").GetString());
        Assert.Equal("Exception", data.GetProperty("source").GetString());
        Assert.False(data.GetProperty("excluded").GetBoolean());
        Assert.True(data.GetProperty("willSpawn").GetBoolean());
    }

    [Fact]
    public void BeginSession_RecordsExcludedAndWillSpawnFlags()
    {
        var dir = NewTempDir();
        var trace = new TraceOptions { Enabled = true, Path = dir };

        var path = AgentFallbackTrace.BeginSession(
            trace, new ArgumentException("x"), "HttpRequestException", excluded: true, willSpawn: false);

        using var doc = JsonDocument.Parse(File.ReadAllLines(path!)[0]);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("excluded").GetBoolean());
        Assert.False(data.GetProperty("willSpawn").GetBoolean());
        Assert.Equal("HttpRequestException", data.GetProperty("source").GetString());
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "azclips-trace-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
