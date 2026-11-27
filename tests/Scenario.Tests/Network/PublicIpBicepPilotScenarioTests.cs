using System.Collections;
using System.Net;
using System.Text.Json;
using Azclips.Tests.Fixtures.PublicIpBicepPilot;
using Cli.Tests.Fixtures;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Microsoft.Extensions.DependencyInjection;
using Tests.Recording;
using NetworkIndex = Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex;
using PilotIndex = Azclips.Tests.Fixtures.PublicIpBicepPilot.ExtensionIndex;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class PublicIpBicepPilotScenarioTests
{
    private static ExtensionUnderTest Pilot(PublicIpPilotOptions? options = null) => ExtensionUnderTest.Network with
    {
        Assembly = typeof(PilotIndex).Assembly,
        RegisterServices = services => services.AddSingleton(options ?? new PublicIpPilotOptions("azclips-public-ip-offline-proof")),
    };

    [Theory]
    [InlineData(ClientType.CLI, "network public-ip create")]
    [InlineData(ClientType.PowerShell, "New-AzPublicIpAddress")]
    public void OptIn_PreservesPublicMetadataAndOtherProcessors(ClientType surface, string name)
    {
        var original = new NetworkIndex();
        var pilot = new PilotIndex();
        CommandMetadata canonical = original.GetCommandMetadata(name, surface);
        CommandMetadata selected = pilot.GetCommandMetadata(name, surface);
        Assert.Equal(typeof(PublicIpBicepCreateProcessor), selected.ProcessorType);
        Assert.Equal(canonical, selected with { ProcessorType = canonical.ProcessorType });
        Assert.Equal(original.GetCommandTable(surface), pilot.GetCommandTable(surface));
        foreach (var command in original.GetCommandTable(surface))
            if (command.Value.ProcessorType != canonical.ProcessorType)
                Assert.Equal(command.Value, pilot.GetCommandMetadata(command.Key, surface));
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Explicit")]
    public void DedicatedDeploymentRecording_ReplaysThroughCliAndRealPowerShell(string scenario)
    {
        var record = new SessionRecord();
        using (var backend = new FakePublicIpDeploymentBackend(scenario))
        using (var cli = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record),
            backend, extensions: [Pilot(), ExtensionUnderTest.Resources]))
        {
            AssertResource(cli.RunCli(CreateArgs(scenario)), scenario);
            Assert.Equal(1, backend.SubmissionCount);
            Assert.Equal(2, backend.ResourceGetCount);
        }
        Assert.Equal(new[] { "GET", "PUT", "GET", "GET", "GET" }, record.Entries.Select(entry => entry.Request.Method));
        Assert.Contains("/publicIPAddresses/", record.Entries[0].Request.Uri);
        Assert.Contains("api-version=2026-03-01", record.Entries[0].Request.Uri);
        Assert.Contains("/Microsoft.Resources/deployments/", record.Entries[1].Request.Uri);
        Assert.Contains("api-version=2025-04-01", record.Entries[1].Request.Uri);
        Assert.Contains("/operations/", record.Entries[2].Request.Uri);
        Assert.Contains("/Microsoft.Resources/deployments/", record.Entries[3].Request.Uri);
        Assert.Contains("/publicIPAddresses/", record.Entries[4].Request.Uri);
        Assert.DoesNotContain(record.Entries, entry => entry.Request.Method == "PUT" && entry.Request.Uri.Contains("/publicIPAddresses/"));

        using (var cli = new ScenarioHost(ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true)),
            extensions: [Pilot(), ExtensionUnderTest.Resources]))
            AssertResource(cli.RunCli(CreateArgs(scenario)), scenario);
        using (var ps = new ScenarioHost(ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true)),
            extensions: [Pilot(), ExtensionUnderTest.Resources]))
            AssertResource(ps.RunPowerShell("New-AzPublicIpAddress", PsParameters(scenario), extension: "Network"), scenario);
        using (var ps = new ScenarioHost(ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true)),
            extensions: [Pilot(), ExtensionUnderTest.Resources]))
        {
            var parameters = PsParameters(scenario);
            if (scenario == "Explicit") parameters["Tag"] = new Hashtable { ["Owner"] = "team-A" };
            AssertResource(ps.RunPowerShellCmdlet<NewPublicIpAddressCmdlet>(parameters), scenario);
        }
    }

    [Theory]
    [InlineData("--zone", "1")]
    [InlineData("--sku", "Basic")]
    [InlineData("--ip-tags", "FirstPartyUsage=/Sql")]
    public void UnsupportedInputs_FailBeforeAnyHttp(string flag, string value)
    {
        var record = new SessionRecord();
        using var backend = new FakePublicIpDeploymentBackend();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record),
            backend, extensions: [Pilot(), ExtensionUnderTest.Resources]);
        Assert.Throws<InvalidInputException>(() => host.RunCli([.. CreateArgs("Default"), flag, value]));
        Assert.Empty(backend.Requests);
        Assert.Empty(record.Entries);
    }

    [Fact]
    public void MissingAsset_FailsBeforePreflightWithoutAnotherBackend()
    {
        var record = new SessionRecord();
        using var backend = new FakePublicIpDeploymentBackend();
        var options = new PublicIpPilotOptions("azclips-public-ip-offline-proof")
        {
            OpenResource = name => name == "PublicIpBicepCreate.TagsAbsent.json" ? null : PublicIpBicepAssets.OpenResource(name),
        };
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record),
            backend, extensions: [Pilot(options), ExtensionUnderTest.Resources]);
        var error = Assert.Throws<InvalidOperationException>(() => host.RunCli(CreateArgs("Default")));
        Assert.Contains("PublicIpBicepCreate.TagsAbsent.json", error.Message);
        Assert.Empty(backend.Requests);
        Assert.Empty(record.Entries);
        Assert.Empty(host.Outputs);
    }

    [Theory]
    [InlineData("Existing", 0, 1)]
    [InlineData("PreflightForbidden", 0, 1)]
    [InlineData("SubmissionForbidden", 1, 1)]
    [InlineData("DeploymentFailed", 1, 1)]
    [InlineData("DeploymentCanceled", 1, 1)]
    [InlineData("FinalDeploymentFailed", 1, 1)]
    [InlineData("DeploymentMissingState", 1, 1)]
    [InlineData("FinalGetFailed", 1, 2)]
    [InlineData("FinalGetEmpty", 1, 2)]
    public void FailurePaths_DoNotProduceResourceOrRetryDirectCreate(string fault, int submissions, int resourceGets)
    {
        var record = new SessionRecord();
        using var backend = new FakePublicIpDeploymentBackend(fault: fault);
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record),
            backend, extensions: [Pilot(), ExtensionUnderTest.Resources]);
        Exception error = Assert.ThrowsAny<Exception>(() => host.RunCli(CreateArgs("Default")));
        if (fault == "DeploymentCanceled") Assert.IsAssignableFrom<OperationCanceledException>(error);
        if (fault == "Existing") Assert.IsType<InvalidInputException>(error);
        if (fault is "PreflightForbidden" or "SubmissionForbidden" or "FinalGetFailed")
            Assert.Equal(HttpStatusCode.Forbidden, Assert.IsType<HttpRequestException>(error).StatusCode);
        if (fault == "DeploymentFailed") Assert.Contains("Long running operation failed", error.Message);
        if (fault is "FinalDeploymentFailed" or "DeploymentMissingState")
            Assert.Contains("did not succeed", Assert.IsType<InvalidOperationException>(error).Message);
        if (fault == "FinalGetEmpty")
            Assert.Contains("incomplete or different", Assert.IsType<InvalidOperationException>(error).Message);
        Assert.Equal(submissions, backend.SubmissionCount);
        Assert.Equal(resourceGets, backend.ResourceGetCount);
        Assert.DoesNotContain(record.Entries, entry => entry.Request.Method == "PUT" && entry.Request.Uri.Contains("/publicIPAddresses/"));
        Assert.DoesNotContain(backend.Requests, request => request.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.Empty(host.Outputs);
    }

    [Fact]
    public void ClientCancellation_StopsPollingWithoutFinalResourceOutput()
    {
        using var cancellation = new CancellationTokenSource();
        var record = new SessionRecord();
        using var backend = new FakePublicIpDeploymentBackend(onPoll: cancellation.Cancel);
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record),
            backend, extensions: [Pilot(), ExtensionUnderTest.Resources]);
        Assert.ThrowsAny<OperationCanceledException>(() => host.RunCli(cancellation.Token, CreateArgs("Default")));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(1, backend.SubmissionCount);
        Assert.Equal(1, backend.ResourceGetCount);
        Assert.Empty(host.Outputs);
    }

    [Fact]
    public void EscapedResourceGroup_IsComparedWithReturnedArmIdentity()
    {
        var record = new SessionRecord();
        using var backend = new FakePublicIpDeploymentBackend();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record),
            backend, extensions: [Pilot(), ExtensionUnderTest.Resources]);
        var resource = Assert.IsType<PublicIPAddress>(Assert.Single(host.RunCli(CreateArgs("Default", "my-rg(1)"))));
        Assert.Contains("/resourceGroups/my-rg(1)/", resource.Id);
        Assert.Contains("my-rg%281%29", backend.Requests[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, backend.SubmissionCount);
        Assert.Equal(2, backend.ResourceGetCount);
    }

    private static string[] CreateArgs(string scenario, string resourceGroup = "my-rg") =>
    [
        "network", "public-ip", "create", "--subscription-id", "00000000-0000-0000-0000-000000000000",
        "--resource-group", resourceGroup, "--name", "my-pip", "--location", "eastus",
        .. scenario == "Explicit" ? new[] { "--tier", "Regional", "--tags", "Owner=team-A", "--idle-timeout", "30" } : [],
    ];

    private static Dictionary<string, object> PsParameters(string scenario)
    {
        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"] = "00000000-0000-0000-0000-000000000000",
            ["ResourceGroupName"] = "my-rg", ["Name"] = "my-pip", ["Location"] = "eastus",
        };
        if (scenario == "Explicit")
        {
            parameters["Tier"] = "Regional";
            parameters["Tag"] = new List<string> { "Owner=team-A" };
            parameters["IdleTimeoutInMinutes"] = 30;
        }
        return parameters;
    }

    private static void AssertResource(IReadOnlyList<object> output, string scenario)
    {
        PublicIPAddress resource = Assert.IsType<PublicIPAddress>(Assert.Single(output));
        JsonElement expected = FakePublicIpDeploymentBackend.ReadScenario(scenario).GetProperty("snapshot").GetProperty("predictedResources")[0];
        Assert.Equal("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Network/publicIPAddresses/my-pip", resource.Id);
        Assert.Equal(expected.GetProperty("name").GetString(), resource.Name);
        Assert.Equal(expected.GetProperty("location").GetString(), resource.Location);
        Assert.Equal("Succeeded", resource.ProvisioningState);
        Assert.Equal("20.0.0.1", resource.IpAddress);
        Assert.Equal("Standard", resource.SkuName);
        Assert.Equal("Static", resource.PublicIPAllocationMethod);
        Assert.Equal("IPv4", resource.PublicIPAddressVersion);
        Assert.Equal(scenario == "Explicit" ? 30 : 4, resource.IdleTimeoutInMinutes);
        Assert.Equal(scenario == "Explicit" ? "Regional" : null, resource.SkuTier);
        if (scenario == "Explicit") Assert.Equal("team-A", resource.Tags?["Owner"]);
        else Assert.Null(resource.Tags);
    }
}
