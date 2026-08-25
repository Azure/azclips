using Live.Tool;

namespace Live.Tool.Tests;

public class TimingFormatTests
{
    [Theory]
    // The exact strings the bash timing_fmt produced, so CI output stays comparable.
    [InlineData(0, "0.0s")]
    [InlineData(8210, "8.2s")]
    [InlineData(91234, "1m31.2s")]
    [InlineData(60000, "1m0.0s")]
    [InlineData(178200, "2m58.2s")]
    // "other (untimed)" goes negative when a rebuilt table over-accounts; the bash version
    // printed a mangled "-99.-7s" until sign handling was added.
    [InlineData(-1500, "-1.5s")]
    public void Format_MatchesTheShellFormatter(long ms, string expected)
    {
        Assert.Equal(expected, Timing.Format(ms));
    }
}

/// <summary>
/// The marker format is a contract with the bash half that still runs on the VM:
/// <c>az vm run-command</c> returns nothing but the guest's stdout, so these markers are
/// the only way VM-side timings survive the trip home.
/// </summary>
public class TimingMarkerTests
{
    [Fact]
    public void RecordFromMarkers_ParsesGuestOutput()
    {
        var timing = new Timing();
        timing.RecordFromMarkers(
            """
            ==> Downloading bundle from Azure Storage
            MI_LIVE_TIMING:download bundle|523|0
            MI_LIVE_TIMING:run live tests|6100|0
            MI_LIVE_TIMING:test: ProfileLiveTests.Something|3700|1
            not a marker
            """);

        Assert.Equal(3, timing.RowCount);
        Assert.Equal(6100, timing.MsFor("run live tests"));
        Assert.True(timing.IsDetail("test: ProfileLiveTests.Something"));
        Assert.False(timing.IsDetail("download bundle"));
    }

    [Fact]
    public void RecordFromMarkers_IgnoresMalformedRows()
    {
        var timing = new Timing();
        timing.RecordFromMarkers("MI_LIVE_TIMING:broken|notanumber|0\nMI_LIVE_TIMING:ok|10|0");

        Assert.Equal(1, timing.RowCount);
        Assert.Equal(10, timing.MsFor("ok"));
    }

    [Fact]
    public void RecordFromMarkers_HandlesTrailingCarriageReturns()
    {
        // az on Windows (and via WSL interop) returns CRLF.
        var timing = new Timing();
        timing.RecordFromMarkers("MI_LIVE_TIMING:extract bundle|1700|0\r\n");

        Assert.Equal(1700, timing.MsFor("extract bundle"));
    }
}
