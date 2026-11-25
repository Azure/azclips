using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>One surface run of a public IP address case. <see cref="SubscriptionId"/> is known only in playback.</summary>
internal sealed record PublicIpAddressRun(ScenarioHost Host, RecordPlaybackInterceptor Traffic, string ResourceGroup, string? SubscriptionId);

/// <summary>
/// Shared steps for the public IP address scenarios (<c>network public-ip create/show/list/delete</c> and
/// <c>New-AzPublicIpAddress</c>, <c>Get-AzPublicIpAddress_Get</c>, <c>Get-AzPublicIpAddress_List</c>,
/// <c>Remove-AzPublicIpAddress</c>). Test spec: <c>network-public-ip-addresses-tests</c>.
/// </summary>
internal static class PublicIpAddressScenario
{
    public const string PublicIpAddressesPath = "/providers/Microsoft.Network/publicIPAddresses";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    /// <summary>
    /// Record: runs the CLI steps once against live Azure with a run-scoped group name, then saves.
    /// Playback: replays the recording through the CLI steps, then through the PowerShell steps,
    /// which send the same requests.
    /// </summary>
    public static void RunCase(Type testClass, string testName, string resourceGroup,
        Action<PublicIpAddressRun> cli, Action<PublicIpAddressRun> powerShell, RecordingSanitizers? sanitizers = null)
    {
        var recording = SessionRecordFiles.OpenSession(testClass, testName, sanitizers: sanitizers);

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(resourceGroup, recording.Mode);
            var traffic = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, traffic, useLiveAuth: true, extensions: s_extensions);
            try
            {
                cli(new PublicIpAddressRun(host, traffic, groupName, null));
                recording.Save();
            }
            finally
            {
                // After the save, so this safety delete never lands in the recording.
                try { host.RunCli("group", "delete", "--name", groupName); } catch { }
            }
            return;
        }

        var cliTraffic = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.CLI, cliTraffic, extensions: s_extensions))
        {
            cli(new PublicIpAddressRun(host, cliTraffic, resourceGroup, RecordingSanitizers.PlaceholderGuid));
        }

        var psTraffic = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.PowerShell, psTraffic, extensions: s_extensions))
        {
            powerShell(new PublicIpAddressRun(host, psTraffic, resourceGroup, RecordingSanitizers.PlaceholderGuid));
        }
    }

    public static IReadOnlyList<object> Cli(PublicIpAddressRun run, params string[] args) =>
        run.Host.RunCli(["network", "public-ip", .. args]);

    public static IReadOnlyList<object> Ps(PublicIpAddressRun run, string command, Dictionary<string, object> parameters) =>
        run.Host.RunPowerShell(command, parameters, extension: "Network");

    public static void CliGroupCreate(PublicIpAddressRun run, string location) =>
        run.Host.RunCli("group", "create", "--name", run.ResourceGroup, "--location", location);

    public static void CliGroupDelete(PublicIpAddressRun run) => run.Host.RunCli("group", "delete", "--name", run.ResourceGroup);

    public static void PsGroupCreate(PublicIpAddressRun run, string location) =>
        run.Host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup, ["Location"] = location }, extension: "Resources");

    public static void PsGroupDelete(PublicIpAddressRun run) =>
        run.Host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup }, extension: "Resources");

    public static PublicIPAddress PsShow(PublicIpAddressRun run, string name) =>
        Single(Ps(run, "Get-AzPublicIpAddress_Get", new() { ["ResourceGroupName"] = run.ResourceGroup, ["Name"] = name }));

    public static PublicIPAddress CliShow(PublicIpAddressRun run, string name) =>
        Single(Cli(run, "show", "--resource-group", run.ResourceGroup, "--name", name));

    public static PublicIPAddress Single(IReadOnlyList<object> outputs) => Assert.IsType<PublicIPAddress>(Assert.Single(outputs));

    /// <summary>List output arrives either item by item or as one page collection; flatten both.</summary>
    public static List<PublicIPAddress> Items(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => output switch
        {
            PublicIPAddress ip => [ip],
            IEnumerable<PublicIPAddress> page => page,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected list output {output.GetType()}."),
        })];

    /// <summary>Body of the PUT to the named public IP address that this step sent.</summary>
    public static JsonNode PutBody(PublicIpAddressRun run, int sentBefore, string name)
    {
        var put = run.Traffic.Sent.Skip(sentBefore).FirstOrDefault(request => request.Method == "PUT"
            && new Uri(request.Uri).AbsolutePath.EndsWith($"{PublicIpAddressesPath}/{name}", StringComparison.OrdinalIgnoreCase));
        Assert.True(put is not null, $"No PUT was sent to public IP address '{name}'.");
        return JsonNode.Parse(put!.Body!)!;
    }

    /// <summary>The path of the first GET this step sent; later GETs are next-page links.</summary>
    public static string FirstGetPath(PublicIpAddressRun run, int sentBefore) =>
        new Uri(run.Traffic.Sent.Skip(sentBefore).First(request => request.Method == "GET").Uri).AbsolutePath;
}
