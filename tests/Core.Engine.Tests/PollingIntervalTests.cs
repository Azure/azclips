namespace Core.Engine.Tests;

[CollectionDefinition(nameof(PollingIntervalTests), DisableParallelization = true)]
public sealed class PollingIntervalTestsCollection { }

[Collection(nameof(PollingIntervalTests))]
public sealed class PollingIntervalTests
{
    private const string PollingIntervalVariable = "TEMP_AZCLIPS_PLAYBACK_TEST_POLLING_INTERVAL";

    [Theory]
    [InlineData(null, 15)]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("-1", -1)]
    [InlineData("2147483647", int.MaxValue)]
    public void GetPollingIntervalTemp_UsesInternalOverrideOrProductionDefault(string? value, int expected)
    {
        var original = Environment.GetEnvironmentVariable(PollingIntervalVariable);
        try
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, value);

            Assert.Equal(expected, Engine.GetPollingIntervalTemp());
        }
        finally
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, original);
        }
    }
}
