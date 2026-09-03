using Core.Recording;

namespace Scenario.Tests;

/// <summary>
/// Covers the <c>AZURE_TEST_MODE</c> switch: default to playback, accept either mode name
/// case-insensitively, and reject anything else. Lives in the scenario collection because
/// <see cref="RecorderModeResolver.FromEnvironment"/> reads a process-wide variable.
/// </summary>
[Collection("Scenario Tests")]
public class RecorderModeResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_DefaultsToPlayback_WhenUnset(string? value)
    {
        Assert.Equal(RecorderMode.Playback, RecorderModeResolver.Parse(value));
    }

    [Theory]
    [InlineData("Record", RecorderMode.Record)]
    [InlineData("record", RecorderMode.Record)]
    [InlineData("RECORD", RecorderMode.Record)]
    [InlineData("  Record  ", RecorderMode.Record)]
    [InlineData("Playback", RecorderMode.Playback)]
    [InlineData("playback", RecorderMode.Playback)]
    public void Parse_MapsModeNames_CaseInsensitively(string value, RecorderMode expected)
    {
        Assert.Equal(expected, RecorderModeResolver.Parse(value));
    }

    [Theory]
    [InlineData("Recrod")]
    [InlineData("live")]
    [InlineData("1")]
    [InlineData("Record,Playback")]
    public void Parse_Throws_OnUnrecognizedValue(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => RecorderModeResolver.Parse(value));
        Assert.Contains(RecorderModeResolver.EnvironmentVariable, exception.Message);
        Assert.Contains(value, exception.Message);
    }

    [Fact]
    public void FromEnvironment_ReadsTheVariable_AndRestores()
    {
        var original = Environment.GetEnvironmentVariable(RecorderModeResolver.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(RecorderModeResolver.EnvironmentVariable, null);
            Assert.Equal(RecorderMode.Playback, RecorderModeResolver.FromEnvironment());

            Environment.SetEnvironmentVariable(RecorderModeResolver.EnvironmentVariable, "Record");
            Assert.Equal(RecorderMode.Record, RecorderModeResolver.FromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable(RecorderModeResolver.EnvironmentVariable, original);
        }
    }
}
