// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using NetworkIndex = Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for the network interface parameter surface: the legacy IP configuration
/// defaults, the name-or-id subnet / public IP / network security group arguments, the opt-in
/// body fields, the <c>$expand</c> query option, the subscription- vs resource-group-scoped list
/// URIs, and the long-running delete's polling target.
/// </summary>
public class NetworkInterfaceParameterTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ResourceGroupName = "my-rg";
    private const string NicName = "my-nic";

    private static readonly string ProviderPrefix =
        $"/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network";

    private static readonly string SubnetId = $"{ProviderPrefix}/virtualNetworks/my-vnet/subnets/default";

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
    private static HttpResponseMessage CreatedNic() =>
        Json($$"""{ "name": "{{NicName}}", "location": "eastus", "properties": { "provisioningState": "Succeeded" } }""");

    /// <summary>Runs a create and returns the JSON body of the initial PUT.</summary>
    private static async Task<JsonElement> CaptureCreateBodyAsync(Action<NewNetworkInterfaceProcessor_Customization> configure)
    {
        string? body = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            body ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return CreatedNic();
        });

        var processor = new NewNetworkInterfaceProcessor_Customization
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = NicName,
            Location = "eastus",
            Subnet = SubnetId
        };
        configure(processor);
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(body);
        return JsonDocument.Parse(body!).RootElement;
    }

    private static JsonElement IpConfiguration(JsonElement body)
    {
        var ipConfigurations = body.GetProperty("properties").GetProperty("ipConfigurations");
        Assert.Equal(1, ipConfigurations.GetArrayLength());
        return ipConfigurations[0];
    }

    /// <summary>
    /// `az network nic create` with nothing but name/group/location/subnet sends one `ipconfig1`
    /// configuration with dynamic allocation. Anything the caller did not ask for must stay out of
    /// the body, including the `false` booleans.
    /// </summary>
    [Fact]
    public async Task Create_WithoutOptionalArguments_AppliesTheLegacyDefaultsOnly()
    {
        var body = await CaptureCreateBodyAsync(_ => { });

        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.False(body.TryGetProperty("tags", out _));
        Assert.False(body.TryGetProperty("extendedLocation", out _));

        var ipConfiguration = IpConfiguration(body);
        Assert.Equal("ipconfig1", ipConfiguration.GetProperty("name").GetString());
        var ipProperties = ipConfiguration.GetProperty("properties");
        Assert.Equal(SubnetId, ipProperties.GetProperty("subnet").GetProperty("id").GetString());
        // A reference goes out as a bare id; the members only `$expand` fills never reach the wire.
        Assert.Equal(["id"], ipProperties.GetProperty("subnet").EnumerateObject().Select(member => member.Name));
        Assert.Equal("Dynamic", ipProperties.GetProperty("privateIPAllocationMethod").GetString());
        Assert.False(ipProperties.TryGetProperty("publicIPAddress", out _));
        Assert.False(ipProperties.TryGetProperty("privateIPAddress", out _));
        Assert.False(ipProperties.TryGetProperty("privateIPAddressVersion", out _));
        Assert.False(ipProperties.TryGetProperty("primary", out _));

        var properties = body.GetProperty("properties");
        Assert.False(properties.TryGetProperty("networkSecurityGroup", out _));
        Assert.False(properties.TryGetProperty("dnsSettings", out _));
        Assert.False(properties.TryGetProperty("enableAcceleratedNetworking", out _));
        Assert.False(properties.TryGetProperty("enableIPForwarding", out _));
    }

    /// <summary>Both legacy surfaces pin a supplied private IP instead of letting ARM assign one.</summary>
    [Fact]
    public async Task Create_PrivateIpAddress_SwitchesTheAllocationToStatic()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.PrivateIpAddress = "10.0.0.10";
            processor.PrivateIpAddressVersion = "IPv4";
        });

        var ipProperties = IpConfiguration(body).GetProperty("properties");
        Assert.Equal("10.0.0.10", ipProperties.GetProperty("privateIPAddress").GetString());
        Assert.Equal("Static", ipProperties.GetProperty("privateIPAllocationMethod").GetString());
        Assert.Equal("IPv4", ipProperties.GetProperty("privateIPAddressVersion").GetString());
    }

    /// <summary>The legacy CLI accepts a bare name for these and resolves it against the command's group.</summary>
    [Fact]
    public async Task Create_NameOrIdArguments_ExpandBareNamesIntoResourceIds()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.VnetName = "my-vnet";
            processor.Subnet = "default";
            processor.PublicIpAddress = "my-pip";
            processor.NetworkSecurityGroup = "my-nsg";
        });

        var ipProperties = IpConfiguration(body).GetProperty("properties");
        Assert.Equal(SubnetId, ipProperties.GetProperty("subnet").GetProperty("id").GetString());
        Assert.Equal($"{ProviderPrefix}/publicIPAddresses/my-pip", ipProperties.GetProperty("publicIPAddress").GetProperty("id").GetString());
        Assert.Equal(
            $"{ProviderPrefix}/networkSecurityGroups/my-nsg",
            body.GetProperty("properties").GetProperty("networkSecurityGroup").GetProperty("id").GetString());
    }

    /// <summary>An argument that already looks like a resource id is passed through untouched, even with --vnet-name.</summary>
    [Fact]
    public async Task Create_ResourceIdArguments_PassThroughUntouched()
    {
        const string otherScope = "/subscriptions/other/resourceGroups/other-rg/providers/Microsoft.Network";
        const string subnetId = otherScope + "/virtualNetworks/other-vnet/subnets/other-subnet";
        const string pipId = otherScope + "/publicIPAddresses/other-pip";
        const string nsgId = otherScope + "/networkSecurityGroups/other-nsg";
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.VnetName = "my-vnet";
            processor.Subnet = subnetId;
            processor.PublicIpAddress = pipId;
            processor.NetworkSecurityGroup = nsgId;
        });

        var ipProperties = IpConfiguration(body).GetProperty("properties");
        Assert.Equal(subnetId, ipProperties.GetProperty("subnet").GetProperty("id").GetString());
        Assert.Equal(pipId, ipProperties.GetProperty("publicIPAddress").GetProperty("id").GetString());
        Assert.Equal(nsgId, body.GetProperty("properties").GetProperty("networkSecurityGroup").GetProperty("id").GetString());
    }

    /// <summary>A bare subnet name cannot be resolved without its virtual network; fail before sending anything.</summary>
    [Fact]
    public async Task Create_BareSubnetNameWithoutVnet_IsRejected()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.Subnet = "default"));

        Assert.Contains("--vnet-name", error.Message);
    }

    [Fact]
    public async Task Create_OptionalArguments_PopulateTheBody()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.IpConfigurationName = "primary-config";
            processor.DnsServers = ["10.0.0.4", "10.0.0.5"];
            processor.InternalDnsNameLabel = "myhost";
            processor.EnableAcceleratedNetworking = true;
            processor.EnableIpForwarding = true;
            processor.ExtendedLocation = "microsoftlosangeles1";
        });

        Assert.Equal("primary-config", IpConfiguration(body).GetProperty("name").GetString());
        Assert.Equal("microsoftlosangeles1", body.GetProperty("extendedLocation").GetProperty("name").GetString());
        Assert.Equal("EdgeZone", body.GetProperty("extendedLocation").GetProperty("type").GetString());

        var properties = body.GetProperty("properties");
        Assert.True(properties.GetProperty("enableAcceleratedNetworking").GetBoolean());
        Assert.True(properties.GetProperty("enableIPForwarding").GetBoolean());
        var dnsSettings = properties.GetProperty("dnsSettings");
        Assert.Equal(["10.0.0.4", "10.0.0.5"], dnsSettings.GetProperty("dnsServers").EnumerateArray().Select(server => server.GetString()));
        Assert.Equal("myhost", dnsSettings.GetProperty("internalDnsNameLabel").GetString());
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

    /// <summary>Azure CLI tags are KEY[=VALUE]: a bare key is a tag with an empty value.</summary>
    [Fact]
    public async Task Create_BareTagKey_GetsAnEmptyValue()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.TagSpecs = ["owner", "env=dev"]);

        var tags = body.GetProperty("tags");
        Assert.Equal("", tags.GetProperty("owner").GetString());
        Assert.Equal("dev", tags.GetProperty("env").GetString());
    }

    [Fact]
    public async Task Create_TagWithoutKey_IsRejected()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.TagSpecs = ["=dev"]));

        Assert.Contains("KEY[=VALUE]", error.Message);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("ipConfigurations/publicIPAddress", true)]
    public async Task Show_AppendsExpand_OnlyWhenRequested(string expand, bool expectExpand)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json($$"""{ "name": "{{NicName}}" }""");
        });

        var processor = new GetNetworkInterfaceProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = NicName,
            Expand = expand
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        var query = Uri.UnescapeDataString(requested!.Query);
        if (expectExpand)
        {
            Assert.Contains("$expand=ipConfigurations/publicIPAddress", query);
        }
        else
        {
            Assert.DoesNotContain("$expand", query);
        }
    }

    /// <summary>
    /// `$expand` returns the whole referenced resource where ARM otherwise returns only its id. The
    /// extra members must survive into the output instead of being flattened back to the id.
    /// </summary>
    [Fact]
    public async Task Show_ExpandedReference_KeepsTheReferencedResourceVerbatim()
    {
        string pipId = $"{ProviderPrefix}/publicIPAddresses/my-pip";
        var pipeline = new FakeHttpPipeline((_, _) => Json($$"""
            {
              "name": "{{NicName}}",
              "location": "eastus",
              "properties": {
                "ipConfigurations": [ {
                  "name": "ipconfig1",
                  "properties": {
                    "subnet": { "id": "{{SubnetId}}" },
                    "publicIPAddress": { "id": "{{pipId}}", "name": "my-pip", "properties": { "ipAddress": "20.0.0.1" } }
                  }
                } ]
              }
            }
            """));

        var output = new FakeOutputHandler();
        var processor = new GetNetworkInterfaceProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = NicName,
            Expand = "ipConfigurations/publicIPAddress"
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        var nic = Assert.IsType<NetworkInterface>(Assert.Single(output.WrittenValues));
        var ipProperties = Assert.Single(nic.Properties.IpConfigurations!).Properties;
        Assert.Equal(SubnetId, ipProperties.Subnet?.Id);
        Assert.Null(ipProperties.Subnet?.AdditionalProperties);
        Assert.Equal(pipId, ipProperties.PublicIpAddress?.Id);

        // What the CLI prints: the expanded members serialize back inline next to the id.
        var printed = JsonSerializer.SerializeToElement(ipProperties.PublicIpAddress, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(pipId, printed.GetProperty("id").GetString());
        Assert.Equal("my-pip", printed.GetProperty("name").GetString());
        Assert.Equal("20.0.0.1", printed.GetProperty("properties").GetProperty("ipAddress").GetString());
    }

    /// <summary>Omitting the resource group must widen the list to the whole subscription.</summary>
    [Theory]
    [InlineData("", "/subscriptions/{0}/providers/Microsoft.Network/networkInterfaces")]
    [InlineData(ResourceGroupName, "/subscriptions/{0}/resourceGroups/" + ResourceGroupName + "/providers/Microsoft.Network/networkInterfaces")]
    public async Task List_ScopesTheUri_ToTheResourceGroupOnlyWhenSupplied(string resourceGroup, string expectedPath)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json("""{ "value": [] }""");
        });

        var processor = new ListNetworkInterfaceProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = resourceGroup
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        Assert.Equal(string.Format(expectedPath, TestSubscriptionId), requested!.AbsolutePath);
    }

    /// <summary>List follows <c>nextLink</c> and writes every page's interfaces as one array.</summary>
    [Fact]
    public async Task List_FollowsNextLink_AndWritesEveryPage()
    {
        const string page2 = "https://management.azure.com/page2";
        var sentUris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return sentUris.Count == 1
                ? Json($$"""{ "value": [ { "name": "nic1" }, { "name": "nic2" } ], "nextLink": "{{page2}}" }""")
                : Json("""{ "value": [ { "name": "nic3" } ] }""");
        });

        var output = new FakeOutputHandler();
        var processor = new ListNetworkInterfaceProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, sentUris.Count);
        Assert.Equal(page2, sentUris[1]);
        var nics = Assert.IsType<List<NetworkInterface>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["nic1", "nic2", "nic3"], nics.Select(nic => nic.Name!).ToArray());
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

        var processor = new DeleteNetworkInterfaceProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = NicName
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal([statusUri, resultUri], gets);
        Assert.Empty(output.WrittenValues);
    }

    /// <summary>
    /// <c>ParameterMetadata.Aliases</c> only renders help; the parser resolves short flags through
    /// <c>CommandMetadata.ParameterAliases</c>, so the two must stay in sync or `-g` prints in help
    /// but fails to bind.
    /// </summary>
    [Theory]
    [InlineData("network nic create", "g", "resource-group")]
    [InlineData("network nic create", "n", "name")]
    [InlineData("network nic create", "l", "location")]
    [InlineData("network nic show", "g", "resource-group")]
    [InlineData("network nic show", "n", "name")]
    [InlineData("network nic list", "g", "resource-group")]
    [InlineData("network nic delete", "g", "resource-group")]
    [InlineData("network nic delete", "n", "name")]
    public void CliCommands_MapLegacyShortFlags_InBothHelpAndParserTables(string command, string alias, string canonical)
    {
        var metadata = new NetworkIndex().GetCommandMetadata(command, ClientType.CLI);

        Assert.True(metadata.ParameterAliases.TryGetValue(alias, out var resolved), $"'{command}' does not resolve -{alias}");
        Assert.Equal(canonical, resolved);
        Assert.Contains(alias, metadata.Parameters[canonical].Aliases);
    }
}
