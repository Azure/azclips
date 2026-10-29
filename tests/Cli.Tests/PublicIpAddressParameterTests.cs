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
/// Offline tests for the public IP address parameter surface: the legacy create defaults, the
/// name-or-id arguments, <c>KEY[=VALUE]</c> tags and <c>TYPE=VALUE</c> IP tags, the <c>$expand</c>
/// query option, the subscription- vs resource-group-scoped list URIs and paging, the silent
/// long-running delete, and the CLI alias / PowerShell routing metadata.
/// </summary>
public class PublicIpAddressParameterTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ResourceGroupName = "my-rg";
    private const string PipName = "my-pip";

    private static readonly string ResourceUri =
        $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}" +
        $"/providers/Microsoft.Network/publicIPAddresses/{PipName}";

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
    private static HttpResponseMessage CreatedPip() =>
        Json($$"""{ "name": "{{PipName}}", "location": "eastus", "properties": { "provisioningState": "Succeeded" } }""");

    /// <summary>Runs a create and returns the JSON body of the initial PUT.</summary>
    private static async Task<JsonElement> CaptureCreateBodyAsync(Action<NewPublicIpAddressProcessor_Customization> configure)
    {
        string? body = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            body ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return CreatedPip();
        });

        var processor = new NewPublicIpAddressProcessor_Customization
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = PipName,
            Location = "eastus"
        };
        configure(processor);
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(body);
        return JsonDocument.Parse(body!).RootElement;
    }

    /// <summary>
    /// `az network public-ip create` with nothing but name/group/location sends Standard/Static/IPv4
    /// and a 4 minute idle timeout. Anything the caller did not ask for must stay out of the body.
    /// </summary>
    [Fact]
    public async Task Create_WithoutOptionalArguments_AppliesTheLegacyDefaultsOnly()
    {
        var body = await CaptureCreateBodyAsync(_ => { });

        Assert.Equal("Standard", body.GetProperty("sku").GetProperty("name").GetString());
        Assert.False(body.GetProperty("sku").TryGetProperty("tier", out _));
        Assert.False(body.TryGetProperty("tags", out _));
        Assert.False(body.TryGetProperty("zones", out _));
        Assert.False(body.TryGetProperty("extendedLocation", out _));

        var properties = body.GetProperty("properties");
        Assert.Equal("Static", properties.GetProperty("publicIPAllocationMethod").GetString());
        Assert.Equal("IPv4", properties.GetProperty("publicIPAddressVersion").GetString());
        Assert.Equal(4, properties.GetProperty("idleTimeoutInMinutes").GetInt32());
        Assert.False(properties.TryGetProperty("dnsSettings", out _));
        Assert.False(properties.TryGetProperty("ddosSettings", out _));
        Assert.False(properties.TryGetProperty("ipTags", out _));
        Assert.False(properties.TryGetProperty("publicIPPrefix", out _));
        Assert.False(properties.TryGetProperty("ipAddress", out _));
    }

    /// <summary>Only the Basic SKU supports dynamic allocation; the Standard tiers are static-only.</summary>
    [Theory]
    [InlineData("Basic", "Dynamic")]
    [InlineData("Standard", "Static")]
    [InlineData("StandardV2", "Static")]
    public async Task Create_AllocationMethod_DefaultsFromTheSku(string sku, string expected)
    {
        var body = await CaptureCreateBodyAsync(processor => processor.Sku = sku);

        Assert.Equal(expected, body.GetProperty("properties").GetProperty("publicIPAllocationMethod").GetString());
    }

    [Fact]
    public async Task Create_ExplicitAllocationMethod_OverridesTheSkuDefault()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.Sku = "Standard";
            processor.AllocationMethod = "Dynamic";
        });

        Assert.Equal("Dynamic", body.GetProperty("properties").GetProperty("publicIPAllocationMethod").GetString());
    }

    [Fact]
    public async Task Create_DnsSettings_AreOmittedUntilOneIsSupplied()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.DomainNameLabel = "mylabel";
            processor.DomainNameLabelScope = "NoReuse";
            processor.ReverseFqdn = "contoso.com";
        });

        var dnsSettings = body.GetProperty("properties").GetProperty("dnsSettings");
        Assert.Equal("mylabel", dnsSettings.GetProperty("domainNameLabel").GetString());
        Assert.Equal("NoReuse", dnsSettings.GetProperty("domainNameLabelScope").GetString());
        Assert.Equal("contoso.com", dnsSettings.GetProperty("reverseFqdn").GetString());
    }

    /// <summary>
    /// <c>New-AzPublicIpAddress</c> silently drops a plan id supplied without a protection mode.
    /// Every DDoS argument must reach the body on its own instead.
    /// </summary>
    [Fact]
    public async Task Create_DdosProtectionPlan_ReachesTheBodyWithoutAProtectionMode()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.DdosProtectionPlan = "myplan");

        var ddosSettings = body.GetProperty("properties").GetProperty("ddosSettings");
        Assert.False(ddosSettings.TryGetProperty("protectionMode", out _));
        Assert.Equal(
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/ddosProtectionPlans/myplan",
            ddosSettings.GetProperty("ddosProtectionPlan").GetProperty("id").GetString());
    }

    /// <summary>The legacy CLI accepts a bare name for these and resolves it against the command's group.</summary>
    [Fact]
    public async Task Create_NameOrIdArguments_ExpandBareNamesIntoResourceIds()
    {
        const string customPolicyId = "/subscriptions/other/resourceGroups/other-rg/providers/Microsoft.Network/ddosCustomPolicies/policy";
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.PublicIpPrefix = "myprefix";
            processor.DdosProtectionMode = "Enabled";
            processor.DdosCustomPolicy = customPolicyId;
        });

        var properties = body.GetProperty("properties");
        Assert.Equal(
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPPrefixes/myprefix",
            properties.GetProperty("publicIPPrefix").GetProperty("id").GetString());

        // An argument that already looks like a resource id is passed through untouched.
        Assert.Equal(customPolicyId, properties.GetProperty("ddosSettings").GetProperty("ddosCustomPolicy").GetProperty("id").GetString());
        Assert.Equal("Enabled", properties.GetProperty("ddosSettings").GetProperty("protectionMode").GetString());
    }

    [Fact]
    public async Task Create_IpTags_MapEachTokenOntoAnIpTagEntry()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.IpTagSpecs = ["FirstPartyUsage=/Sql", "RoutingPreference=Internet"]);

        var ipTags = body.GetProperty("properties").GetProperty("ipTags");
        Assert.Equal(2, ipTags.GetArrayLength());
        Assert.Equal("FirstPartyUsage", ipTags[0].GetProperty("ipTagType").GetString());
        Assert.Equal("/Sql", ipTags[0].GetProperty("tag").GetString());
        Assert.Equal("RoutingPreference", ipTags[1].GetProperty("ipTagType").GetString());
        Assert.Equal("Internet", ipTags[1].GetProperty("tag").GetString());
    }

    [Fact]
    public async Task Create_IpTags_RejectTokensWithoutSeparator()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.IpTagSpecs = ["FirstPartyUsage"]));

        Assert.Contains("--ip-tags", error.Message);
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

    /// <summary>Azure CLI tags are <c>KEY[=VALUE]</c>: a bare key gets an empty value and a repeated key keeps its last value.</summary>
    [Fact]
    public async Task Create_Tags_AcceptBareKeys_AndLastDuplicateWins()
    {
        var body = await CaptureCreateBodyAsync(processor => processor.TagSpecs = ["env=dev", "temporary", "env=prod"]);

        var tags = body.GetProperty("tags");
        Assert.Equal("prod", tags.GetProperty("env").GetString());
        Assert.Equal("", tags.GetProperty("temporary").GetString());
        Assert.Equal(2, tags.EnumerateObject().Count());
    }

    [Fact]
    public async Task Create_Tags_RejectAnEmptyKey()
    {
        var error = await Assert.ThrowsAsync<InvalidInputException>(
            () => CaptureCreateBodyAsync(processor => processor.TagSpecs = ["=value"]));

        Assert.Contains("--tags", error.Message);
    }

    [Fact]
    public async Task Create_ZonesTierAndEdgeZone_PopulateTheEnvelope()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.Zones = ["1", "2", "3"];
            processor.SkuTier = "Regional";
            processor.ExtendedLocation = "microsoftlosangeles1";
            processor.IpAddress = "20.1.2.3";
        });

        Assert.Equal(["1", "2", "3"], body.GetProperty("zones").EnumerateArray().Select(zone => zone.GetString()));
        Assert.Equal("Regional", body.GetProperty("sku").GetProperty("tier").GetString());
        Assert.Equal("microsoftlosangeles1", body.GetProperty("extendedLocation").GetProperty("name").GetString());
        Assert.Equal("EdgeZone", body.GetProperty("extendedLocation").GetProperty("type").GetString());
        Assert.Equal("20.1.2.3", body.GetProperty("properties").GetProperty("ipAddress").GetString());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("ipConfiguration", true)]
    public async Task Show_AppendsExpand_OnlyWhenRequested(string expand, bool expectExpand)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json($$"""{ "name": "{{PipName}}" }""");
        });

        var processor = new GetPublicIpAddressProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = PipName,
            Expand = expand
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        if (expectExpand)
        {
            Assert.Contains("$expand=ipConfiguration", requested!.ToString());
        }
        else
        {
            Assert.DoesNotContain("$expand", requested!.ToString());
        }
    }

    /// <summary>
    /// PowerShell reads the legacy top-level members, including an attached IP configuration, while CLI
    /// JSON keeps the ARM <c>properties</c> envelope (#1117).
    /// </summary>
    [Fact]
    public async Task Show_FlattensPropertiesForPowerShell_AndKeepsTheArmEnvelopeForCli()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""
            {
              "name": "my-pip",
              "location": "westeurope",
              "properties": {
                "ipAddress": "20.1.2.3",
                "provisioningState": "Succeeded",
                "ipConfiguration": {
                  "id": "/nic/ipConfigurations/ipconfig1",
                  "properties": {
                    "privateIPAddress": "10.0.0.4",
                    "subnet": { "id": "/subnet" }
                  }
                },
                "natGateway": { "id": "/nat" },
                "servicePublicIPAddress": { "id": "/service" },
                "linkedPublicIPAddress": { "id": "/linked" }
              }
            }
            """));
        var output = new FakeOutputHandler();
        var processor = new GetPublicIpAddressProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = PipName
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        var pip = Assert.IsType<PublicIpAddress>(Assert.Single(output.WrittenValues));
        Assert.Equal("20.1.2.3", pip.IpAddress);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.Equal("/nic/ipConfigurations/ipconfig1", pip.IpConfiguration?.Id);
        Assert.Equal("10.0.0.4", pip.IpConfiguration?.PrivateIpAddress);
        Assert.Equal("/subnet", pip.IpConfiguration?.Subnet?.Id);
        Assert.Equal("/nat", pip.NatGateway?.Id);
        Assert.Equal("/service", pip.ServicePublicIpAddress?.Id);
        Assert.Equal("/linked", pip.LinkedPublicIpAddress?.Id);

        // PowerShell formats nested values through the model's own serializer, which threw on the
        // previously opaque ipConfiguration and blanked the column.
        var rendered = pip.ToString();
        Assert.Contains("20.1.2.3", rendered);
        Assert.Contains("/nic/ipConfigurations/ipconfig1", rendered);
        Assert.Contains("10.0.0.4", pip.IpConfiguration!.ToString());

        using var cli = JsonDocument.Parse(Core.Cli.Output.CliOutputFormatter.FormatValue(pip));
        Assert.False(cli.RootElement.TryGetProperty("ipAddress", out _));
        var properties = cli.RootElement.GetProperty("properties");
        Assert.Equal("20.1.2.3", properties.GetProperty("ipAddress").GetString());
        Assert.Equal("10.0.0.4", properties.GetProperty("ipConfiguration").GetProperty("properties").GetProperty("privateIPAddress").GetString());
        Assert.Equal("/linked", properties.GetProperty("linkedPublicIPAddress").GetProperty("id").GetString());
    }

    /// <summary>Omitting the resource group must widen the list to the whole subscription.</summary>
    [Theory]
    [InlineData("", "/subscriptions/{0}/providers/Microsoft.Network/publicIPAddresses")]
    [InlineData(ResourceGroupName, "/subscriptions/{0}/resourceGroups/" + ResourceGroupName + "/providers/Microsoft.Network/publicIPAddresses")]
    public async Task List_ScopesTheUri_ToTheResourceGroupOnlyWhenSupplied(string resourceGroup, string expectedPath)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json("""{ "value": [] }""");
        });

        var processor = new ListPublicIpAddressProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = resourceGroup
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        Assert.Equal(string.Format(expectedPath, TestSubscriptionId), requested!.AbsolutePath);
    }

    /// <summary>List follows <c>nextLink</c> and writes every page's addresses as one array.</summary>
    [Fact]
    public async Task List_FollowsNextLink_AndWritesEveryPage()
    {
        const string page2 = "https://management.azure.com/page2";
        var sentUris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return sentUris.Count == 1
                ? Json($$"""{ "value": [ { "name": "pip1" }, { "name": "pip2" } ], "nextLink": "{{page2}}" }""")
                : Json("""{ "value": [ { "name": "pip3" } ] }""");
        });

        var output = new FakeOutputHandler();
        var processor = new ListPublicIpAddressProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, sentUris.Count);
        Assert.Equal(page2, sentUris[1]);
        var pips = Assert.IsType<List<PublicIpAddress>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["pip1", "pip2", "pip3"], pips.Select(pip => pip.Name!).ToArray());
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

        var processor = new DeletePublicIpAddressProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            Name = PipName
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal([statusUri, resultUri], gets);
        Assert.DoesNotContain(gets, uri => uri.StartsWith(ResourceUri, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(output.WrittenValues);
    }

    /// <summary>
    /// <c>ParameterMetadata.Aliases</c> only renders help; the parser resolves aliases through
    /// <c>CommandMetadata.ParameterAliases</c>, so the two must stay in sync or `-g` prints in help
    /// but fails to bind. Long aliases are listed with one leading dash so help renders them as `--`.
    /// </summary>
    [Theory]
    [InlineData("network public-ip create", "g", "resource-group")]
    [InlineData("network public-ip create", "n", "name")]
    [InlineData("network public-ip create", "l", "location")]
    [InlineData("network public-ip create", "z", "zone")]
    [InlineData("network public-ip create", "ddos-protection-mode", "protection-mode")]
    [InlineData("network public-ip show", "g", "resource-group")]
    [InlineData("network public-ip show", "n", "name")]
    [InlineData("network public-ip list", "g", "resource-group")]
    [InlineData("network public-ip delete", "g", "resource-group")]
    [InlineData("network public-ip delete", "n", "name")]
    public void CliCommands_MapLegacyAliases_InBothHelpAndParserTables(string command, string alias, string canonical)
    {
        var metadata = new NetworkIndex().GetCommandMetadata(command, ClientType.CLI);

        Assert.True(metadata.ParameterAliases.TryGetValue(alias, out var resolved), $"'{command}' does not resolve {alias}");
        Assert.Equal(canonical, resolved);
        Assert.Contains(alias.Length == 1 ? alias : $"-{alias}", metadata.Parameters[canonical].Aliases);
    }

    [Theory]
    [InlineData("New-AzPublicIpAddress", typeof(NewPublicIpAddressProcessor_Customization))]
    [InlineData("Get-AzPublicIpAddress_Get", typeof(GetPublicIpAddressProcessor))]
    [InlineData("Get-AzPublicIpAddress_List", typeof(ListPublicIpAddressProcessor))]
    [InlineData("Remove-AzPublicIpAddress", typeof(DeletePublicIpAddressProcessor))]
    public void PowerShellCommands_RouteToTheirProcessors(string command, Type processorType)
    {
        Assert.Equal(processorType, new NetworkIndex().GetCommandMetadata(command, ClientType.PowerShell).ProcessorType);
    }
}
