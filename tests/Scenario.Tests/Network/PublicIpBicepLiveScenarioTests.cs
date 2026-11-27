using System.Net;
using Azclips.Tests.Fixtures.PublicIpBicepPilot;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common.Recording;
using Tests.Common.Stubs;
using Tests.Recording;
using PilotIndex = Azclips.Tests.Fixtures.PublicIpBicepPilot.ExtensionIndex;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class PublicIpBicepLiveScenarioTests
{
    [Fact]
    public void CreatesReadsDeletesOwnedPublicIpFromCli() => Lifecycle(ClientType.CLI, nameof(CreatesReadsDeletesOwnedPublicIpFromCli));

    [Fact]
    public void CreatesReadsDeletesOwnedPublicIpFromPowerShell() => Lifecycle(ClientType.PowerShell, nameof(CreatesReadsDeletesOwnedPublicIpFromPowerShell));

    private static void Lifecycle(ClientType surface, string recordingName)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(PublicIpBicepLiveScenarioTests), recordingName,
            new DefaultRequestMatcher(matchBody: true));
        bool live = recording.Mode == RecorderMode.Record;
        if (live && Environment.GetEnvironmentVariable("AZCLIPS_BICEP_LIVE_AUTHORIZED") != "1")
            throw new InvalidOperationException("Live Bicep tests require explicit scoped provisioning/cleanup authorization.");
        string subscription = live ? Required("AZCLIPS_BICEP_LIVE_SUBSCRIPTION") : RecordingSanitizers.PlaceholderGuid;
        string group = TestRun.ScopedName("scenario-public-ip-bicep-rg", recording.Mode);
        if (live && group != Required("AZCLIPS_BICEP_LIVE_RESOURCE_GROUP"))
            throw new InvalidOperationException("The live resource group does not match the authorized test-run scope.");
        string location = "australiaeast";
        string name = TestRun.ScopedName($"scenario-public-ip-bicep-{(surface == ClientType.CLI ? "cli" : "ps")}", recording.Mode);
        string deployment = TestRun.ScopedName($"scenario-public-ip-bicep-deployment-{(surface == ClientType.CLI ? "cli" : "ps")}", recording.Mode);
        var extension = ExtensionUnderTest.Network with
        {
            Assembly = typeof(PilotIndex).Assembly,
            RegisterServices = services =>
            {
                services.AddSingleton(new PublicIpPilotOptions(deployment));
                if (live)
                {
                    services.AddSingleton<IAuthService, AzureCliLiveAuth>();
                    services.AddSingleton<IProfileService>(new StubProfileService());
                }
            },
        };
        using var host = new ScenarioHost(surface, recording.CreateInterceptor(), useLiveAuth: live,
            extensions: [extension, ExtensionUnderTest.Resources, ExtensionUnderTest.ResourceGroupDeployments]);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        Dictionary<string, object> identity = new()
        {
            ["SubscriptionId"] = subscription, ["ResourceGroupName"] = group, ["Name"] = name,
        };
        var failures = new List<Exception>();
        try
        {
            IReadOnlyList<object> created = surface == ClientType.CLI
                ? host.RunCli(deadline.Token, "network", "public-ip", "create", "--subscription-id", subscription,
                    "--resource-group", group, "--name", name, "--location", location)
                : host.RunPowerShell(deadline.Token, "New-AzPublicIpAddress",
                    new Dictionary<string, object>(identity) { ["Location"] = location, ["Tier"] = "Regional",
                        ["Tag"] = new List<string> { "purpose=bicep-pilot" }, ["IdleTimeoutInMinutes"] = 30 }, "Network");
            var resource = Assert.IsType<PublicIPAddress>(Assert.Single(created));
            Assert.Equal(name, resource.Name);
            Assert.Equal(location, resource.Location);
            Assert.Equal("Succeeded", resource.ProvisioningState);
            Assert.Equal("Standard", resource.SkuName);
            Assert.Equal("Regional", resource.SkuTier);
            Assert.Equal("Static", resource.PublicIPAllocationMethod);
            Assert.Equal("IPv4", resource.PublicIPAddressVersion);
            Assert.Equal(surface == ClientType.CLI ? 4 : 30, resource.IdleTimeoutInMinutes);
            if (surface == ClientType.PowerShell)
                Assert.Equal("bicep-pilot", Assert.IsAssignableFrom<IDictionary<string, string>>(resource.Tags)["purpose"]);
            else
                Assert.True(resource.Tags is null || resource.Tags.Count == 0);
            Assert.False(string.IsNullOrWhiteSpace(resource.IpAddress));
            IReadOnlyList<object> shown = surface == ClientType.CLI
                ? host.RunCli(deadline.Token, "network", "public-ip", "show", "--subscription-id", subscription,
                    "--resource-group", group, "--name", name)
                : host.RunPowerShell(deadline.Token, "Get-AzPublicIpAddress_Get", identity, "Network");
            Assert.Equal(resource.Id, Assert.IsType<PublicIPAddress>(Assert.Single(shown)).Id);
            Assert.Throws<InvalidInputException>(() =>
            {
                if (surface == ClientType.CLI)
                    host.RunCli(deadline.Token, "network", "public-ip", "create", "--subscription-id", subscription,
                        "--resource-group", group, "--name", name, "--location", location);
                else
                    host.RunPowerShell(deadline.Token, "New-AzPublicIpAddress",
                        new Dictionary<string, object>(identity) { ["Location"] = location }, "Network");
            });
        }
        catch (Exception error)
        {
            failures.Add(error);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                if (surface == ClientType.CLI)
                    host.RunCli(cleanup.Token, "network", "public-ip", "delete", "--subscription-id", subscription,
                        "--resource-group", group, "--name", name);
                else host.RunPowerShell(cleanup.Token, "Remove-AzPublicIpAddress", identity, "Network");
            }
            catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound) { }
            catch (Exception error) { failures.Add(error); }
            try
            {
                if (surface == ClientType.CLI)
                    host.RunCli(cleanup.Token, "deployment", "group", "delete", "--subscription-id", subscription,
                        "--resource-group", group, "--name", deployment);
                else
                    host.RunPowerShell(cleanup.Token, "Remove-AzResourceGroupDeployment",
                        new Dictionary<string, object> { ["SubscriptionId"] = subscription, ["ResourceGroupName"] = group,
                            ["Name"] = deployment }, "Resources");
            }
            catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound) { }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count > 0) throw new AggregateException("Public IP Bicep live lifecycle or owned cleanup failed.", failures);
        recording.Save();
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required authorized live-test input '{name}' is missing.");

    private sealed class AzureCliLiveAuth : StubAuthService
    {
        public AzureCliLiveAuth() { }

        protected override Task<AuthResult> AcquireTokenAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AuthResult { AccessToken = Required("AZCLIPS_BICEP_LIVE_TOKEN"),
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30), Scopes = scopes ?? [],
                TenantId = tenantId ?? throw new InvalidOperationException("The authorized live auth context has no tenant.") });
        }
    }
}
