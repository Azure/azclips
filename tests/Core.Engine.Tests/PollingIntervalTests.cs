namespace Core.Engine.Tests;

[CollectionDefinition(nameof(PollingIntervalTests), DisableParallelization = true)]
public sealed class PollingIntervalTestsCollection { }

[Collection(nameof(PollingIntervalTests))]
public sealed class PollingIntervalTests
{
    private const string PollingIntervalVariable = "AZCLIPS_INTERNAL_TEST_POLLING_INTERVAL_SECONDS";

    [Theory]
    [InlineData(null, 15)]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("2147483", 2147483)]
    public void GetPollingInterval_UsesInternalOverrideOrProductionDefault(string? value, int expected)
    {
        var original = Environment.GetEnvironmentVariable(PollingIntervalVariable);
        try
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, value);

            Assert.Equal(expected, Engine.GetPollingInterval());
        }
        finally
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, original);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("2147484")]
    [InlineData("2147483648")]
    public void GetPollingInterval_InvalidOverrideFailsExplicitly(string value)
    {
        var original = Environment.GetEnvironmentVariable(PollingIntervalVariable);
        try
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, value);

            var error = Assert.Throws<InvalidOperationException>(() => Engine.GetPollingInterval());

            Assert.Contains(PollingIntervalVariable, error.Message);
            Assert.Contains("0 and 2147483", error.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, original);
        }
    }
}
