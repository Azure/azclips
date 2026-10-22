// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using NetworkIndex = Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for the virtual network parameter surface: the compound subnet argument, tag
/// parsing, the <c>$expand</c> query option, and the long-running delete's final polling target.
/// </summary>
public class VirtualNetworkParameterTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ResourceGroupName = "my-rg";
    private const string VNetName = "my-vnet";

    private static readonly string ResourceUri =
        $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}" +
        $"/providers/Microsoft.Network/virtualNetworks/{VNetName}";

    private static void Init(IProcessor processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        pipeline.PollingInterval = 0;
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Minimal ARM reply that lets the runtime treat the PUT as a completed operation.</summary>
    private static HttpResponseMessage CreatedVNet() =>
        Json($$"""{ "name": "{{VNetName}}", "location": "eastus", "properties": { "provisioningState": "Succeeded" } }""");

    /// <summary>Runs a create and returns the JSON body of the initial PUT.</summary>
    private static async Task<JsonElement> CaptureCreateBodyAsync(Action<NewVirtualNetworkProcessor_Customization> configure)
    {
        string? body = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            body ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return CreatedVNet();
        });

        var processor = new NewVirtualNetworkProcessor_Customization
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = VNetName,
            Location = "eastus",
            AddressPrefixes = ["10.0.0.0/16"]
        };
        configure(processor);
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(body);
        return JsonDocument.Parse(body!).RootElement;
    }

    [Fact]
    public async Task Create_WithoutOptionalArguments_OmitsOptionalProperties()
    {
        var body = await CaptureCreateBodyAsync(_ => { });

        var properties = body.GetProperty("properties");
        Assert.False(body.TryGetProperty("tags", out _));
        Assert.False(body.TryGetProperty("extendedLocation", out _));
        Assert.False(properties.TryGetProperty("dhcpOptions", out _));
        Assert.False(properties.TryGetProperty("flowTimeoutInMinutes", out _));
        Assert.False(properties.TryGetProperty("encryption", out _));
        Assert.False(properties.TryGetProperty("bgpCommunities", out _));
        Assert.False(properties.TryGetProperty("enableVmProtection", out _));
        Assert.Equal("10.0.0.0/16", properties.GetProperty("addressSpace").GetProperty("addressPrefixes")[0].GetString());
    }

    [Fact]
    public async Task Create_WithoutAddressPrefixes_FallsBackToTheLegacyDefault()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.AddressPrefixes = []);

        var addressPrefixes = body.GetProperty("properties").GetProperty("addressSpace").GetProperty("addressPrefixes");
        Assert.Equal(1, addressPrefixes.GetArrayLength());
        Assert.Equal("10.0.0.0/16", addressPrefixes[0].GetString());
    }

    [Fact]
    public async Task Create_WithIpamAllocationsAndNoAddressPrefixes_DoesNotInjectTheDefault()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.AddressPrefixes = [];
            processor.IpamPoolPrefixAllocations = ["/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkManagers/nm/ipamPools/pool"];
        });

        var addressSpace = body.GetProperty("properties").GetProperty("addressSpace");
        Assert.Equal(0, addressSpace.GetProperty("addressPrefixes").GetArrayLength());
        Assert.Equal(1, addressSpace.GetProperty("ipamPoolPrefixAllocations").GetArrayLength());
    }

    [Fact]
    public async Task Create_CompoundSubnets_MapEachKeyOntoSubnetProperties()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.SubnetSpecs =
        [
            ["name=web", "address-prefixes=10.0.1.0/24", "default-outbound-access=false"],
            ["name=data", "address-prefix=10.0.2.0/24", "service-endpoints=Microsoft.Storage,Microsoft.Sql", "delegations=Microsoft.Web/serverFarms"]
        ]);

        var subnets = body.GetProperty("properties").GetProperty("subnets");
        Assert.Equal(2, subnets.GetArrayLength());

        var web = subnets[0];
        Assert.Equal("web", web.GetProperty("name").GetString());
        Assert.Equal("10.0.1.0/24", web.GetProperty("properties").GetProperty("addressPrefix").GetString());
        Assert.False(web.GetProperty("properties").GetProperty("defaultOutboundAccess").GetBoolean());

        var data = subnets[1].GetProperty("properties");
        Assert.Equal("10.0.2.0/24", data.GetProperty("addressPrefix").GetString());
        Assert.Equal(
            ["Microsoft.Storage", "Microsoft.Sql"],
            data.GetProperty("serviceEndpoints").EnumerateArray().Select(e => e.GetProperty("service").GetString()));
        Assert.Equal(
            "Microsoft.Web/serverFarms",
            data.GetProperty("delegations")[0].GetProperty("properties").GetProperty("serviceName").GetString());
    }

    /// <summary>
    /// PowerShell callers pass a hashtable whose keys are PascalCase; those must resolve to the same
    /// subnet properties as the CLI's kebab-case spelling.
    /// </summary>
    [Fact]
    public async Task Create_CompoundSubnets_AcceptPascalCaseKeys()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.SubnetSpecs =
        [
            ["Name=web", "AddressPrefix=10.0.1.0/24", "DefaultOutboundAccess=false", "ServiceEndpoint=Microsoft.Storage"]
        ]);

        var subnet = body.GetProperty("properties").GetProperty("subnets")[0];
        Assert.Equal("web", subnet.GetProperty("name").GetString());
        var properties = subnet.GetProperty("properties");
        Assert.Equal("10.0.1.0/24", properties.GetProperty("addressPrefix").GetString());
        Assert.False(properties.GetProperty("defaultOutboundAccess").GetBoolean());
        Assert.Equal("Microsoft.Storage", properties.GetProperty("serviceEndpoints")[0].GetProperty("service").GetString());
    }

    [Fact]
    public async Task Create_CompoundSubnets_UnknownKey_ListsSupportedKeys()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.SubnetSpecs = [["name=web", "bogus=1"]]));

        Assert.Contains("bogus", error.Message);
        Assert.Contains("address-prefixes", error.Message);
    }

    /// <summary>
    /// These subnet properties exist in the ARM model but on neither migrated surface
    /// (<c>az network vnet create</c> / <c>New-AzVirtualNetworkSubnetConfig</c>), so they stay unexposed.
    /// </summary>
    [Theory]
    [InlineData("service-gateway")]
    [InlineData("sharing-scope")]
    [InlineData("application-gateway-ip-configurations")]
    public async Task Create_CompoundSubnets_RejectKeysOutsideTheMigratedSurface(string key)
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.SubnetSpecs = [["name=web", $"{key}=value"]]));

        Assert.Contains(key, error.Message);
        Assert.DoesNotContain($"{key}, ", error.Message);
    }

    /// <summary>
    /// <c>ParameterMetadata.Aliases</c> only renders help; the parser resolves short flags through
    /// <c>CommandMetadata.ParameterAliases</c>, so the two must stay in sync or `-g` prints in help
    /// but fails to bind.
    /// </summary>
    [Theory]
    [InlineData("network vnet create", "g", "resource-group")]
    [InlineData("network vnet create", "n", "name")]
    [InlineData("network vnet create", "l", "location")]
    [InlineData("network vnet show", "g", "resource-group")]
    [InlineData("network vnet show", "n", "name")]
    [InlineData("network vnet list", "g", "resource-group")]
    [InlineData("network vnet delete", "g", "resource-group")]
    [InlineData("network vnet delete", "n", "name")]
    public void CliCommands_MapLegacyShortFlags_InBothHelpAndParserTables(string command, string alias, string canonical)
    {
        var metadata = new NetworkIndex().GetCommandMetadata(command, ClientType.CLI);

        Assert.True(metadata.ParameterAliases.TryGetValue(alias, out var resolved), $"'{command}' does not resolve -{alias}");
        Assert.Equal(canonical, resolved);
        Assert.Contains(alias, metadata.Parameters[canonical].Aliases);
    }

    [Fact]
    public async Task Create_CompoundSubnets_RequireName()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.SubnetSpecs = [["address-prefixes=10.0.1.0/24"]]));

        Assert.Contains("name=", error.Message);
    }

    /// <summary>Azure tag keys are case sensitive, so they must survive parsing verbatim.</summary>
    [Fact]
    public async Task Create_Tags_PreserveKeyCasing()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.TagSpecs = ["Owner=team-A", "costCenter=42"]);

        var tags = body.GetProperty("tags");
        Assert.Equal("team-A", tags.GetProperty("Owner").GetString());
        Assert.Equal("42", tags.GetProperty("costCenter").GetString());
    }

    [Fact]
    public async Task Create_Tags_AcceptBareKeys_AsEmptyValues()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.TagSpecs = ["owner", "env=dev"]);

        var tags = body.GetProperty("tags");
        Assert.Equal("", tags.GetProperty("owner").GetString());
        Assert.Equal("dev", tags.GetProperty("env").GetString());
    }

    [Fact]
    public async Task Create_Tags_RepeatedKey_KeepsTheLastValue()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.TagSpecs = ["env=dev", "env=prod"]);

        Assert.Equal("prod", body.GetProperty("tags").GetProperty("env").GetString());
    }

    [Fact]
    public async Task Create_Tags_RejectTokensWithoutKey()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.TagSpecs = ["=dev"]));

        Assert.Contains("--tags", error.Message);
    }

    /// <summary>PowerShell can hand the processor objects that are neither subnets nor KEY=VALUE tokens.</summary>
    [Fact]
    public async Task Create_SubnetInputs_RejectUnsupportedObjects()
    {
        await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.SubnetInputs = [42]));
    }

    /// <summary>
    /// Both network policies are optional in the spec; sending them unasked would override the
    /// RP default, so they stay out of the body unless a flag sets them.
    /// </summary>
    [Fact]
    public async Task Create_Subnets_OmitNetworkPoliciesUnlessRequested()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.SubnetSpecs =
        [
            ["name=web", "address-prefix=10.0.1.0/24"],
            ["name=pe", "address-prefix=10.0.2.0/24", "disable-private-endpoint-network-policies=true"]
        ]);

        var subnets = body.GetProperty("properties").GetProperty("subnets");
        var web = subnets[0].GetProperty("properties");
        Assert.False(web.TryGetProperty("privateEndpointNetworkPolicies", out _));
        Assert.False(web.TryGetProperty("privateLinkServiceNetworkPolicies", out _));
        Assert.Equal("Disabled", subnets[1].GetProperty("properties").GetProperty("privateEndpointNetworkPolicies").GetString());
    }

    [Fact]
    public async Task Create_DnsServersAndFlowTimeout_PopulateProperties()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.DnsServers = ["10.0.0.4", "10.0.0.5"];
            processor.FlowTimeoutInMinutes = 10;
        });

        var properties = body.GetProperty("properties");
        Assert.Equal(10, properties.GetProperty("flowTimeoutInMinutes").GetInt32());
        Assert.Equal(
            ["10.0.0.4", "10.0.0.5"],
            properties.GetProperty("dhcpOptions").GetProperty("dnsServers").EnumerateArray().Select(e => e.GetString()));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("subnets/ipConfigurations", true)]
    public async Task Show_AppendsExpand_OnlyWhenRequested(string expand, bool expectExpand)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json($$"""{ "name": "{{VNetName}}" }""");
        });

        var processor = new GetVirtualNetworkProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = VNetName,
            Expand = expand
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        if (expectExpand)
        {
            Assert.Contains("$expand=subnets%2FipConfigurations", requested!.ToString());
        }
        else
        {
            Assert.DoesNotContain("$expand", requested!.ToString());
        }
    }

    /// <summary>
    /// Network RP deletes answer 202 + Azure-AsyncOperation + Location: poll the status, then
    /// finish on the Location result, an empty 204, so the delete writes no output. A null final
    /// URI would skip that GET and print the operation-status JSON; the deleted resource itself
    /// must never be re-read.
    /// </summary>
    [Fact]
    public async Task Delete_FinishesOnTheLocationResult_AndWritesNothing()
    {
        const string statusUri = "https://management.azure.com/subscriptions/s/providers/Microsoft.Network/locations/eastus/operations/op1?api-version=2025-07-01";
        const string resultUri = "https://management.azure.com/subscriptions/s/providers/Microsoft.Network/locations/eastus/operationResults/op1?api-version=2025-07-01";
        var gets = new List<string>();

        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Add("Azure-AsyncOperation", statusUri);
                accepted.Headers.Add("Location", resultUri);
                return accepted;
            }
            gets.Add(request.RequestUri!.ToString());
            return gets[^1] == statusUri
                ? Json("""{ "status": "Succeeded" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var processor = new DeleteVirtualNetworkProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = VNetName
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal([statusUri, resultUri], gets);
        Assert.DoesNotContain(gets, uri => uri.StartsWith(ResourceUri, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData("New-AzVirtualNetwork", typeof(NewVirtualNetworkProcessor_Customization))]
    [InlineData("Get-AzVirtualNetwork_Get", typeof(GetVirtualNetworkProcessor))]
    [InlineData("Get-AzVirtualNetwork_List", typeof(ListVirtualNetworkProcessor))]
    [InlineData("Remove-AzVirtualNetwork", typeof(DeleteVirtualNetworkProcessor))]
    public void PowerShellCommands_RouteToTheirProcessors(string command, Type processorType)
    {
        Assert.Equal(processorType, new NetworkIndex().GetCommandMetadata(command, ClientType.PowerShell).ProcessorType);
    }
}
