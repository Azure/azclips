using System.Net;
using Core.Abstractions;
using Tests.Common.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public sealed class ScenarioHostTests
{
    private const string PollingIntervalVariable = "TEMP_AZCLIPS_PLAYBACK_TEST_POLLING_INTERVAL";

    [Theory]
    [InlineData(ClientType.CLI, null, false)]
    [InlineData(ClientType.PowerShell, "7", false)]
    [InlineData(ClientType.CLI, "0", true)]
    [InlineData(ClientType.PowerShell, null, true)]
    public void PollingOverride_IsScopedToOfflineHostAndRestoresEnvironment(
        ClientType surface, string? previousValue, bool useLiveAuth)
    {
        var original = Environment.GetEnvironmentVariable(PollingIntervalVariable);
        try
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, previousValue);
            var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.OK));

            using (var host = new ScenarioHost(surface, http, useLiveAuth: useLiveAuth))
            {
                Assert.Equal(useLiveAuth ? null : "0",
                    Environment.GetEnvironmentVariable(PollingIntervalVariable));
            }

            Assert.Equal(previousValue, Environment.GetEnvironmentVariable(PollingIntervalVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(PollingIntervalVariable, original);
        }
    }
}
