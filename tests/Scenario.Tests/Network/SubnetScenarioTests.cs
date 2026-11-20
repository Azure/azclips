// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Runs the subnet commands (create, update, show, list, delete) on both client surfaces against
/// one recorded session per case. Record mode runs the CLI steps against live Azure and rewrites the
/// recording. Playback replays the CLI steps, then the PowerShell steps, against the same recording,
/// matching on method, URI and body, so the PowerShell requests must equal the recorded CLI requests.
/// <para>
/// Request expectations are checked on the requests each step sent (<see cref="RecordPlaybackInterceptor.Sent"/>),
/// so every surface is checked against its own requests. Output expectations are checked on the
/// emitted <see cref="Subnet"/> and <see cref="List{Subnet}"/> models.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class SubnetScenarioTests
{
    // Playback matches the placeholder the sanitizers wrote into the recording.
    private const string PlaybackSubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ApiVersion = "2026-03-01";
    private const string NsgName = "test-vnet-nsg";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    // T1: verifies "T1 Subnet lifecycle: create, list, show, update prefix, show missing, delete" of network-subnets-tests.
    [Fact]
    public void CreateListShowUpdateShowMissingDelete()
    {
        RunScenario(nameof(CreateListShowUpdateShowMissingDelete), "scenario-subnet-lifecycle-rg", "westus", s =>
        {
            const string vnet = "vnet1";

            // 2
            s.CreateVnet(vnet, addressPrefixes: ["10.0.0.0/16", "20.0.0.0/16"], subnetName: "default");

            // 3
            Step created = s.CreateSubnet(vnet, "subnet1", new SubnetArgs
            {
                AddressPrefixes = ["20.0.0.0/24"],
                DefaultOutboundAccess = false,
                DefaultOutboundShortOption = true,
            });
            CapturedHttpRequest put = AssertFirstRequestIsPut(s, created, vnet, "subnet1");
            JsonObject body = ParseBody(put);
            Assert.Equal("subnet1", body["name"]?.GetValue<string>());
            JsonObject props = PropertiesOf(body);
            Assert.Equal("20.0.0.0/24", props["addressPrefix"]?.GetValue<string>());
            Assert.False(props.ContainsKey("addressPrefixes"));
            Assert.Equal("Disabled", props["privateEndpointNetworkPolicies"]?.GetValue<string>());
            Assert.Equal("Enabled", props["privateLinkServiceNetworkPolicies"]?.GetValue<string>());
            foreach (string absent in new[] { "networkSecurityGroup", "routeTable", "natGateway", "serviceEndpoints", "delegations" })
            {
                Assert.False(props.ContainsKey(absent), $"The PUT body should have no properties.{absent}.");
            }
            Subnet subnet = AssertSubnet(Assert.Single(created.Outputs), "subnet1", "20.0.0.0/24");
            Assert.Null(subnet.AddressPrefixes);
            Assert.Equal("Succeeded", subnet.ProvisioningState);
            Assert.False(subnet.DefaultOutboundAccess);
            Assert.Equal("Disabled", subnet.PrivateEndpointNetworkPolicies);
            Assert.Equal("Enabled", subnet.PrivateLinkServiceNetworkPolicies);

            // 4
            AssertListedNames(s.ListSubnets(vnet), "default", "subnet1");

            // 5
            AssertSubnet(Assert.Single(s.ShowSubnet(vnet, "subnet1").Outputs), "subnet1", "20.0.0.0/24");

            // 6
            Step updated = s.UpdateSubnet(vnet, "subnet1", new SubnetArgs { AddressPrefixes = ["20.0.3.0/24"] });
            props = PropertiesOf(ParseBody(AssertGetThenPut(s, updated, vnet, "subnet1")));
            Assert.Equal("20.0.3.0/24", props["addressPrefix"]?.GetValue<string>());
            Assert.False(props.ContainsKey("addressPrefixes"));
            subnet = AssertSubnet(Assert.Single(updated.Outputs), "subnet1", "20.0.3.0/24");
            Assert.False(subnet.DefaultOutboundAccess);
            Assert.Equal("Disabled", subnet.PrivateEndpointNetworkPolicies);
            Assert.Equal("Enabled", subnet.PrivateLinkServiceNetworkPolicies);

            // 7: the call throws, so it returns no output.
            var error = Assert.IsType<HttpRequestException>(Assert.ThrowsAny<Exception>(() => s.ShowSubnet(vnet, "subnet-doesnotexist")));
            Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);

            // 8
            Assert.Empty(s.DeleteSubnet(vnet, "subnet1").Outputs);

            // 9
            List<Subnet> remaining = AssertList(s.ListSubnets(vnet));
            Assert.Contains(remaining, item => item.Name == "default");
            Assert.DoesNotContain(remaining, item => item.Name == "subnet1");
        });
    }

    // T2: verifies "T2 Update address prefix and attach or detach a network security group" of network-subnets-tests.
    [Fact]
    public void UpdatePrefixAndAttachDetachNsg()
    {
        RunScenario(nameof(UpdatePrefixAndAttachDetachNsg), "scenario-subnet-nsg-rg", "westus", s =>
        {
            const string vnet = "vnet1";

            // 2
            s.CreateVnet(vnet, addressPrefixes: ["123.0.0.0/16"], subnetName: "default", subnetPrefixes: ["123.0.0.0/24"]);

            // 3
            string nsgId = Assert.IsType<NetworkSecurityGroup>(Assert.Single(s.CreateNsg(NsgName).Outputs)).Id!;
            Assert.False(string.IsNullOrEmpty(nsgId));

            // 4: attach by name.
            Step attached = s.UpdateSubnet(vnet, "default", new SubnetArgs
            {
                AddressPrefixes = ["123.0.5.0/24"],
                NetworkSecurityGroup = NsgName,
            });
            JsonObject props = PropertiesOf(ParseBody(AssertGetThenPut(s, attached, vnet, "default")));
            Assert.DoesNotContain(attached.Requests, request => request.Uri.Contains("/networkSecurityGroups/", StringComparison.OrdinalIgnoreCase));
            AssertNsgReference(s, props, nsgId);
            Assert.Equal("123.0.5.0/24", props["addressPrefix"]?.GetValue<string>());
            Assert.False(props.ContainsKey("addressPrefixes"));
            Subnet subnet = AssertSubnet(Assert.Single(attached.Outputs), "default", "123.0.5.0/24");
            AssertNsgId(s, nsgId, subnet.NetworkSecurityGroupId);

            // 5: attach by full ID, with the --nsg alias.
            Step created = s.CreateSubnet(vnet, "subnet2", new SubnetArgs
            {
                AddressPrefixes = ["123.0.2.0/24"],
                NetworkSecurityGroup = nsgId,
                NsgShortOption = true,
                DefaultOutboundAccess = false,
            });
            props = PropertiesOf(ParseBody(AssertFirstRequestIsPut(s, created, vnet, "subnet2")));
            JsonObject reference = Assert.IsType<JsonObject>(props["networkSecurityGroup"]);
            Assert.Equal(new[] { "id" }, reference.Select(property => property.Key).ToArray());
            Assert.Equal(nsgId, reference["id"]?.GetValue<string>());
            AssertNsgId(s, nsgId, Assert.IsType<Subnet>(Assert.Single(created.Outputs)).NetworkSecurityGroupId);

            // 6: detach with an empty value, address prefix given again.
            Step detached = s.UpdateSubnet(vnet, "default", new SubnetArgs
            {
                AddressPrefixes = ["123.0.5.0/24"],
                NetworkSecurityGroup = "",
            });
            props = PropertiesOf(ParseBody(AssertGetThenPut(s, detached, vnet, "default")));
            Assert.False(props.ContainsKey("networkSecurityGroup"));
            subnet = AssertSubnet(Assert.Single(detached.Outputs), "default", "123.0.5.0/24");
            Assert.Null(subnet.NetworkSecurityGroupId);

            // 7: detach with an empty value only; the rest comes from the GET.
            detached = s.UpdateSubnet(vnet, "subnet2", new SubnetArgs { NetworkSecurityGroup = "" });
            props = PropertiesOf(ParseBody(AssertGetThenPut(s, detached, vnet, "subnet2")));
            Assert.False(props.ContainsKey("networkSecurityGroup"));
            Assert.Equal("123.0.2.0/24", props["addressPrefix"]?.GetValue<string>());
            Assert.False(props["defaultOutboundAccess"]?.GetValue<bool>());
            subnet = AssertSubnet(Assert.Single(detached.Outputs), "subnet2", "123.0.2.0/24");
            Assert.Null(subnet.NetworkSecurityGroupId);
            Assert.False(subnet.DefaultOutboundAccess);

            // 8, 9
            s.DeleteVnet(vnet);
            s.DeleteNsg(NsgName);
        });
    }

    // T3: verifies "T3 Delegations set at create and replaced on update" of network-subnets-tests.
    [Fact]
    public void DelegationsSetAtCreateAndReplacedOnUpdate()
    {
        RunScenario(nameof(DelegationsSetAtCreateAndReplacedOnUpdate), "scenario-subnet-delegation-rg", "westus", s =>
        {
            const string vnet = "vnet1";

            // 2
            s.CreateVnet(vnet, location: "westcentralus");

            // 3
            Step created = s.CreateSubnet(vnet, "subnet1", new SubnetArgs
            {
                AddressPrefixes = ["10.0.0.0/24"],
                Delegations = ["Microsoft.Web/serverFarms"],
                DefaultOutboundAccess = false,
                DefaultOutboundShortOption = true,
            });
            AssertSingleDelegationInBody(PropertiesOf(ParseBody(AssertFirstRequestIsPut(s, created, vnet, "subnet1"))), "Microsoft.Web/serverFarms");
            Subnet subnet = Assert.IsType<Subnet>(Assert.Single(created.Outputs));
            Assert.Equal("Microsoft.Web/serverFarms", Assert.Single(subnet.Delegations ?? []).ServiceName);

            // 4
            Step updated = s.UpdateSubnet(vnet, "subnet1", new SubnetArgs { Delegations = ["Microsoft.Sql/managedInstances"] });
            CapturedHttpRequest put = AssertGetThenPut(s, updated, vnet, "subnet1");
            AssertSingleDelegationInBody(PropertiesOf(ParseBody(put)), "Microsoft.Sql/managedInstances");
            Assert.DoesNotContain("Microsoft.Web/serverFarms", put.Body, StringComparison.OrdinalIgnoreCase);
            subnet = AssertSubnet(Assert.Single(updated.Outputs), "subnet1", "10.0.0.0/24");
            Assert.Equal("Microsoft.Sql/managedInstances", Assert.Single(subnet.Delegations ?? []).ServiceName);
        });
    }

    // T4: verifies "T4 Service endpoints set at create" of network-subnets-tests.
    [Fact]
    public void ServiceEndpointsSetAtCreate()
    {
        RunScenario(nameof(ServiceEndpointsSetAtCreate), "scenario-subnet-endpoint-rg", "eastus2", s =>
        {
            const string vnet = "vnet1";

            // 2
            s.CreateVnet(vnet);

            // 3
            Step created = s.CreateSubnet(vnet, "subnet1", new SubnetArgs
            {
                AddressPrefixes = ["10.0.1.0/24"],
                ServiceEndpoints = ["Microsoft.Storage"],
                DefaultOutboundAccess = false,
                DefaultOutboundShortOption = true,
            });
            JsonObject props = PropertiesOf(ParseBody(AssertFirstRequestIsPut(s, created, vnet, "subnet1")));
            Assert.Equal("[{\"service\":\"Microsoft.Storage\"}]", props["serviceEndpoints"]?.ToJsonString());
            Subnet subnet = Assert.IsType<Subnet>(Assert.Single(created.Outputs));
            Assert.Equal("Microsoft.Storage", Assert.Single(subnet.ServiceEndpoints ?? []).Service);
        });
    }

    // T5: verifies "T5 Network policies at create: given values versus defaults" of network-subnets-tests.
    [Fact]
    public void NetworkPoliciesAtCreate()
    {
        RunScenario(nameof(NetworkPoliciesAtCreate), "scenario-subnet-policies-rg", "westus", s =>
        {
            const string vnet = "MyVnet";

            // 2
            s.CreateVnet(vnet);

            // 3: private endpoint policies given, private link service policies defaulted.
            AssertCreatedWithPolicies(s, vnet, "MySubnet1", "10.0.1.0/24",
                givenPrivateEndpoint: "Disabled", givenPrivateLinkService: null,
                expectedPrivateEndpoint: "Disabled", expectedPrivateLinkService: "Enabled");

            // 4: private link service policies given, private endpoint policies defaulted.
            AssertCreatedWithPolicies(s, vnet, "MySubnet2", "10.0.2.0/24",
                givenPrivateEndpoint: null, givenPrivateLinkService: "Disabled",
                expectedPrivateEndpoint: "Disabled", expectedPrivateLinkService: "Disabled");

            // 5: both given.
            AssertCreatedWithPolicies(s, vnet, "MySubnet3", "10.0.3.0/24",
                givenPrivateEndpoint: "Disabled", givenPrivateLinkService: "Disabled",
                expectedPrivateEndpoint: "Disabled", expectedPrivateLinkService: "Disabled");
        });
    }

    // T6: verifies "T6 Default outbound access set at create and changed on update" of network-subnets-tests.
    [Fact]
    public void DefaultOutboundAccessCreateAndUpdate()
    {
        RunScenario(nameof(DefaultOutboundAccessCreateAndUpdate), "scenario-subnet-outbound-rg", "westus", s =>
        {
            const string vnet = "vnet1";

            // 2
            s.CreateVnet(vnet, location: "westcentralus");

            // 3
            Step created = s.CreateSubnet(vnet, "subnet1", new SubnetArgs
            {
                AddressPrefixes = ["10.0.3.0/24"],
                DefaultOutboundAccess = false,
            });
            JsonObject props = PropertiesOf(ParseBody(AssertFirstRequestIsPut(s, created, vnet, "subnet1")));
            Assert.False(props["defaultOutboundAccess"]?.GetValue<bool>());
            Subnet createdSubnet = Assert.IsType<Subnet>(Assert.Single(created.Outputs));
            Assert.False(createdSubnet.DefaultOutboundAccess);

            // 4: the policies are kept as the GET returned them, which is what create returned.
            Step updated = s.UpdateSubnet(vnet, "subnet1", new SubnetArgs { DefaultOutboundAccess = true });
            props = PropertiesOf(ParseBody(AssertGetThenPut(s, updated, vnet, "subnet1")));
            Assert.True(props["defaultOutboundAccess"]?.GetValue<bool>());
            Assert.Equal("10.0.3.0/24", props["addressPrefix"]?.GetValue<string>());
            Assert.Equal(createdSubnet.PrivateEndpointNetworkPolicies, props["privateEndpointNetworkPolicies"]?.GetValue<string>());
            Assert.Equal(createdSubnet.PrivateLinkServiceNetworkPolicies, props["privateLinkServiceNetworkPolicies"]?.GetValue<string>());
            Subnet subnet = AssertSubnet(Assert.Single(updated.Outputs), "subnet1", "10.0.3.0/24");
            Assert.True(subnet.DefaultOutboundAccess);

            // 5
            Assert.True(Assert.IsType<Subnet>(Assert.Single(s.ShowSubnet(vnet, "subnet1").Outputs)).DefaultOutboundAccess);
        });
    }

    // T7: verifies "T7 Subnet with several address prefixes" of network-subnets-tests.
    [Fact]
    public void SeveralAddressPrefixes()
    {
        RunScenario(nameof(SeveralAddressPrefixes), "scenario-subnet-multiprefix-rg", "westus", s =>
        {
            const string vnet = "vnet1";

            // 2
            s.CreateVnet(vnet, addressPrefixes: ["10.0.0.0/16"], subnetName: "subnet1", subnetPrefixes: ["10.0.1.0/28", "10.0.2.0/28"]);

            // 3
            Step created = s.CreateSubnet(vnet, "subnet2", new SubnetArgs
            {
                AddressPrefixes = ["10.0.3.0/28", "10.0.4.0/28"],
                DefaultOutboundAccess = false,
                DefaultOutboundShortOption = true,
            });
            JsonObject props = PropertiesOf(ParseBody(AssertFirstRequestIsPut(s, created, vnet, "subnet2")));
            Assert.Equal(new[] { "10.0.3.0/28", "10.0.4.0/28" }, StringsOf(props["addressPrefixes"]));
            Assert.False(props.ContainsKey("addressPrefix"));
            Subnet subnet = Assert.IsType<Subnet>(Assert.Single(created.Outputs));
            AssertPrefixes(subnet, "10.0.3.0/28", "10.0.4.0/28");
            Assert.Null(subnet.AddressPrefix);

            // 4
            Step updated = s.UpdateSubnet(vnet, "subnet2", new SubnetArgs { AddressPrefixes = ["10.0.5.0/28", "10.0.6.0/28"] });
            props = PropertiesOf(ParseBody(AssertGetThenPut(s, updated, vnet, "subnet2")));
            Assert.Equal(new[] { "10.0.5.0/28", "10.0.6.0/28" }, StringsOf(props["addressPrefixes"]));
            Assert.False(props.ContainsKey("addressPrefix"));
            AssertPrefixes(Assert.IsType<Subnet>(Assert.Single(updated.Outputs)), "10.0.5.0/28", "10.0.6.0/28");

            // 5
            AssertListedNames(s.ListSubnets(vnet), "subnet1", "subnet2");

            // 6
            subnet = Assert.IsType<Subnet>(Assert.Single(s.ShowSubnet(vnet, "subnet2").Outputs));
            Assert.Equal("subnet2", subnet.Name);
            AssertPrefixes(subnet, "10.0.5.0/28", "10.0.6.0/28");

            // 7
            Assert.Empty(s.DeleteSubnet(vnet, "subnet2").Outputs);

            // 8
            Assert.Equal("subnet1", Assert.Single(AssertList(s.ListSubnets(vnet))).Name);
        });
    }

    /// <summary>
    /// Opens the case's session and runs <paramref name="steps"/> after the resource group is created
    /// (step 1). Record mode runs the CLI surface only and deletes the group after the save, so the
    /// delete never lands in the recording. Playback runs the CLI surface, then the PowerShell surface.
    /// </summary>
    private static void RunScenario(string testName, string groupBaseName, string groupLocation, Action<Surface> steps)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(SubnetScenarioTests), testName, new DefaultRequestMatcher(matchBody: true));
        string groupName = TestRun.ScopedName(groupBaseName, recording.Mode);

        if (recording.Mode == RecorderMode.Record)
        {
            RecordPlaybackInterceptor interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, interceptor, useLiveAuth: true, extensions: s_extensions);
            string liveSubscriptionId = host.LiveSubscriptionId();
            var cli = new Surface(host, interceptor, ClientType.CLI, liveSubscriptionId, groupName, isPlayback: false);
            try
            {
                cli.CreateGroup(groupLocation);
                steps(cli);
                recording.Save();
            }
            finally
            {
                try
                {
                    host.RunCli("group", "delete", "--subscription-id", liveSubscriptionId, "--name", groupName);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"Failed to clean up resource group '{groupName}': {exception}");
                }
            }
            return;
        }

        RecordPlaybackInterceptor cliRequests = recording.CreateInterceptor();
        using (var cliHost = new ScenarioHost(ClientType.CLI, cliRequests, extensions: s_extensions))
        {
            var cli = new Surface(cliHost, cliRequests, ClientType.CLI, PlaybackSubscriptionId, groupName, isPlayback: true);
            cli.CreateGroup(groupLocation);
            steps(cli);
        }

        RecordPlaybackInterceptor psRequests = recording.CreateInterceptor();
        using (var psHost = new ScenarioHost(ClientType.PowerShell, psRequests, extensions: s_extensions))
        {
            var ps = new Surface(psHost, psRequests, ClientType.PowerShell, PlaybackSubscriptionId, groupName, isPlayback: true);
            ps.CreateGroup(groupLocation);
            steps(ps);
        }
    }

    private static void AssertCreatedWithPolicies(
        Surface s,
        string vnet,
        string subnetName,
        string addressPrefix,
        string? givenPrivateEndpoint,
        string? givenPrivateLinkService,
        string expectedPrivateEndpoint,
        string expectedPrivateLinkService)
    {
        Step created = s.CreateSubnet(vnet, subnetName, new SubnetArgs
        {
            AddressPrefixes = [addressPrefix],
            PrivateEndpointNetworkPolicies = givenPrivateEndpoint,
            PrivateLinkServiceNetworkPolicies = givenPrivateLinkService,
            DefaultOutboundAccess = false,
            DefaultOutboundShortOption = true,
        });
        JsonObject props = PropertiesOf(ParseBody(AssertFirstRequestIsPut(s, created, vnet, subnetName)));
        Assert.Equal(expectedPrivateEndpoint, props["privateEndpointNetworkPolicies"]?.GetValue<string>());
        Assert.Equal(expectedPrivateLinkService, props["privateLinkServiceNetworkPolicies"]?.GetValue<string>());
        Subnet subnet = AssertSubnet(Assert.Single(created.Outputs), subnetName, addressPrefix);
        Assert.Equal(expectedPrivateEndpoint, subnet.PrivateEndpointNetworkPolicies);
        Assert.Equal(expectedPrivateLinkService, subnet.PrivateLinkServiceNetworkPolicies);
    }

    private static Subnet AssertSubnet(object output, string expectedName, string expectedAddressPrefix)
    {
        var subnet = Assert.IsType<Subnet>(output);
        Assert.Equal(expectedName, subnet.Name);
        Assert.Equal(expectedAddressPrefix, subnet.AddressPrefix);
        return subnet;
    }

    private static void AssertPrefixes(Subnet subnet, params string[] expected)
    {
        Assert.NotNull(subnet.AddressPrefixes);
        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), subnet.AddressPrefixes.Order(StringComparer.Ordinal).ToArray());
    }

    private static List<Subnet> AssertList(Step step) => Assert.IsType<List<Subnet>>(Assert.Single(step.Outputs));

    private static void AssertListedNames(Step step, params string[] expected)
    {
        string[] names = AssertList(step).Select(subnet => subnet.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), names);
    }

    // Name resolution builds the ID from the subscription and resource group, which differ between the
    // live run and the sanitized playback, so the ID is compared in full only in playback. ARM can return
    // the resource group in another case, so the comparison ignores case.
    private static void AssertNsgId(Surface s, string expected, string? actual)
    {
        Assert.NotNull(actual);
        if (s.IsPlayback)
        {
            Assert.Equal(expected, actual, ignoreCase: true);
        }
        else
        {
            Assert.EndsWith($"/networkSecurityGroups/{NsgName}", actual, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNsgReference(Surface s, JsonObject properties, string expectedId)
    {
        JsonObject reference = Assert.IsType<JsonObject>(properties["networkSecurityGroup"]);
        Assert.Equal(new[] { "id" }, reference.Select(property => property.Key).ToArray());
        AssertNsgId(s, expectedId, reference["id"]?.GetValue<string>());
    }

    private static void AssertSingleDelegationInBody(JsonObject properties, string serviceName)
    {
        JsonArray delegations = Assert.IsType<JsonArray>(properties["delegations"]);
        JsonObject delegation = Assert.IsType<JsonObject>(Assert.Single(delegations));
        Assert.Equal("0", delegation["name"]?.GetValue<string>());
        Assert.Equal(serviceName, delegation["properties"]?["serviceName"]?.GetValue<string>());
    }

    /// <summary>A create step sends the subnet PUT first; LRO polls follow it.</summary>
    private static CapturedHttpRequest AssertFirstRequestIsPut(Surface s, Step step, string vnet, string subnetName)
    {
        Assert.NotEmpty(step.Requests);
        CapturedHttpRequest put = step.Requests[0];
        Assert.Equal("PUT", put.Method);
        Assert.Equal(s.SubnetPath(vnet, subnetName), new Uri(put.Uri).PathAndQuery);
        Assert.Single(step.Requests, request => request.Method == "PUT");
        return put;
    }

    /// <summary>An update step reads the subnet first, then PUTs it back to the same URI.</summary>
    private static CapturedHttpRequest AssertGetThenPut(Surface s, Step step, string vnet, string subnetName)
    {
        string path = s.SubnetPath(vnet, subnetName);
        Assert.True(step.Requests.Count >= 2, $"Expected a GET and a PUT, but the step sent {step.Requests.Count} request(s).");
        Assert.Equal("GET", step.Requests[0].Method);
        Assert.Equal(path, new Uri(step.Requests[0].Uri).PathAndQuery);
        CapturedHttpRequest put = step.Requests[1];
        Assert.Equal("PUT", put.Method);
        Assert.Equal(path, new Uri(put.Uri).PathAndQuery);
        Assert.Single(step.Requests, request => request.Method == "PUT");
        return put;
    }

    private static JsonObject ParseBody(CapturedHttpRequest request)
    {
        Assert.False(string.IsNullOrEmpty(request.Body), "The PUT has no body.");
        return Assert.IsType<JsonObject>(JsonNode.Parse(request.Body!));
    }

    private static JsonObject PropertiesOf(JsonObject body) => Assert.IsType<JsonObject>(body["properties"]);

    private static string[] StringsOf(JsonNode? node) =>
        Assert.IsType<JsonArray>(node).Select(item => item!.GetValue<string>()).ToArray();

    /// <summary>The outputs of one command and the requests it sent.</summary>
    private sealed record Step(IReadOnlyList<object> Outputs, IReadOnlyList<CapturedHttpRequest> Requests);

    /// <summary>Subnet create and update inputs. A null value is not passed.</summary>
    private sealed record SubnetArgs
    {
        public string[]? AddressPrefixes { get; init; }
        public string? NetworkSecurityGroup { get; init; }

        /// <summary>CLI only: pass the network security group with the <c>--nsg</c> alias.</summary>
        public bool NsgShortOption { get; init; }

        public string[]? ServiceEndpoints { get; init; }
        public string[]? Delegations { get; init; }
        public string? PrivateEndpointNetworkPolicies { get; init; }
        public string? PrivateLinkServiceNetworkPolicies { get; init; }
        public bool? DefaultOutboundAccess { get; init; }

        /// <summary>CLI only: pass default outbound access with the <c>--default-outbound</c> alias.</summary>
        public bool DefaultOutboundShortOption { get; init; }
    }

    /// <summary>
    /// One client surface bound to the case's subscription and resource group. Each command builds both
    /// forms, the CLI arguments and the PowerShell parameters, and runs the one its host was booted for.
    /// </summary>
    private sealed class Surface(
        ScenarioHost host,
        RecordPlaybackInterceptor interceptor,
        ClientType clientType,
        string subscriptionId,
        string groupName,
        bool isPlayback)
    {
        public bool IsPlayback => isPlayback;

        public string SubnetPath(string vnet, string subnetName) =>
            $"/subscriptions/{subscriptionId}/resourceGroups/{groupName}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnetName}?api-version={ApiVersion}";

        public Step CreateGroup(string location) => Run(
            ["group", "create", "--subscription-id", subscriptionId, "--name", groupName, "--location", location],
            "New-AzResourceGroup",
            new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = groupName,
                ["Location"] = location,
            },
            extension: "Resources");

        public Step CreateVnet(string name, string[]? addressPrefixes = null, string? subnetName = null, string[]? subnetPrefixes = null, string? location = null)
        {
            List<string> cli = ["network", "vnet", "create", "--subscription-id", subscriptionId, "--resource-group", groupName, "--name", name];
            Dictionary<string, object> ps = new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["Name"] = name,
            };
            if (location is not null)
            {
                cli.AddRange(["--location", location]);
                ps["Location"] = location;
            }
            if (addressPrefixes is not null)
            {
                cli.Add("--address-prefixes");
                cli.AddRange(addressPrefixes);
                ps["AddressPrefixes"] = addressPrefixes;
            }
            if (subnetName is not null)
            {
                cli.AddRange(["--subnet-name", subnetName]);
                ps["SubnetName"] = subnetName;
            }
            if (subnetPrefixes is not null)
            {
                cli.Add("--subnet-prefixes");
                cli.AddRange(subnetPrefixes);
                ps["SubnetPrefixes"] = subnetPrefixes;
            }
            return Run([.. cli], "New-AzVirtualNetwork", ps);
        }

        public Step DeleteVnet(string name) => Run(
            ["network", "vnet", "delete", "--subscription-id", subscriptionId, "--resource-group", groupName, "--name", name],
            "Remove-AzVirtualNetwork",
            ResourceParameters(name));

        public Step CreateNsg(string name) => Run(
            ["network", "nsg", "create", "--subscription-id", subscriptionId, "--resource-group", groupName, "--name", name],
            "New-AzNetworkSecurityGroup",
            ResourceParameters(name));

        public Step DeleteNsg(string name) => Run(
            ["network", "nsg", "delete", "--subscription-id", subscriptionId, "--resource-group", groupName, "--name", name],
            "Remove-AzNetworkSecurityGroup",
            ResourceParameters(name));

        public Step CreateSubnet(string vnet, string name, SubnetArgs args) => RunSubnetWrite("create", "New-AzVirtualNetworkSubnet", vnet, name, args);

        public Step UpdateSubnet(string vnet, string name, SubnetArgs args) => RunSubnetWrite("update", "Set-AzVirtualNetworkSubnet", vnet, name, args);

        public Step ShowSubnet(string vnet, string name) => Run(
            ["network", "vnet", "subnet", "show", "--subscription-id", subscriptionId, "--resource-group", groupName, "--vnet-name", vnet, "--name", name],
            "Get-AzVirtualNetworkSubnet_Get",
            SubnetParameters(vnet, name));

        public Step ListSubnets(string vnet) => Run(
            ["network", "vnet", "subnet", "list", "--subscription-id", subscriptionId, "--resource-group", groupName, "--vnet-name", vnet],
            "Get-AzVirtualNetworkSubnet_List",
            new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["VirtualNetworkName"] = vnet,
            });

        public Step DeleteSubnet(string vnet, string name) => Run(
            ["network", "vnet", "subnet", "delete", "--subscription-id", subscriptionId, "--resource-group", groupName, "--vnet-name", vnet, "--name", name],
            "Remove-AzVirtualNetworkSubnet",
            SubnetParameters(vnet, name));

        private Step RunSubnetWrite(string cliVerb, string psCommand, string vnet, string name, SubnetArgs args)
        {
            List<string> cli = ["network", "vnet", "subnet", cliVerb, "--subscription-id", subscriptionId, "--resource-group", groupName, "--vnet-name", vnet, "--name", name];
            Dictionary<string, object> ps = SubnetParameters(vnet, name);
            if (args.AddressPrefixes is not null)
            {
                cli.Add("--address-prefixes");
                cli.AddRange(args.AddressPrefixes);
                ps["AddressPrefixes"] = args.AddressPrefixes;
            }
            if (args.NetworkSecurityGroup is not null)
            {
                cli.AddRange([args.NsgShortOption ? "--nsg" : "--network-security-group", args.NetworkSecurityGroup]);
                ps["NetworkSecurityGroup"] = args.NetworkSecurityGroup;
            }
            if (args.ServiceEndpoints is not null)
            {
                cli.Add("--service-endpoints");
                cli.AddRange(args.ServiceEndpoints);
                ps["ServiceEndpoints"] = args.ServiceEndpoints;
            }
            if (args.Delegations is not null)
            {
                cli.Add("--delegations");
                cli.AddRange(args.Delegations);
                ps["Delegations"] = args.Delegations;
            }
            if (args.PrivateEndpointNetworkPolicies is not null)
            {
                cli.AddRange(["--private-endpoint-network-policies", args.PrivateEndpointNetworkPolicies]);
                ps["PrivateEndpointNetworkPolicies"] = args.PrivateEndpointNetworkPolicies;
            }
            if (args.PrivateLinkServiceNetworkPolicies is not null)
            {
                cli.AddRange(["--private-link-service-network-policies", args.PrivateLinkServiceNetworkPolicies]);
                ps["PrivateLinkServiceNetworkPolicies"] = args.PrivateLinkServiceNetworkPolicies;
            }
            if (args.DefaultOutboundAccess is bool defaultOutboundAccess)
            {
                cli.AddRange([args.DefaultOutboundShortOption ? "--default-outbound" : "--default-outbound-access", defaultOutboundAccess ? "true" : "false"]);
                ps["DefaultOutboundAccess"] = defaultOutboundAccess;
            }
            return Run([.. cli], psCommand, ps);
        }

        private Dictionary<string, object> ResourceParameters(string name) => new()
        {
            ["SubscriptionId"] = subscriptionId,
            ["ResourceGroupName"] = groupName,
            ["Name"] = name,
        };

        private Dictionary<string, object> SubnetParameters(string vnet, string name) => new()
        {
            ["SubscriptionId"] = subscriptionId,
            ["ResourceGroupName"] = groupName,
            ["VirtualNetworkName"] = vnet,
            ["Name"] = name,
        };

        private Step Run(string[] cliArgs, string psCommand, Dictionary<string, object> psParameters, string extension = "Network")
        {
            int from = interceptor.Sent.Count;
            IReadOnlyList<object> outputs = clientType == ClientType.CLI
                ? host.RunCli(cliArgs)
                : host.RunPowerShell(psCommand, psParameters, extension);
            return new Step(outputs, [.. interceptor.Sent.Skip(from)]);
        }
    }
}
