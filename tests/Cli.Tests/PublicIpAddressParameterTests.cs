// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Reflection;
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
/// name-or-id arguments, <c>KEY=VALUE</c> tags and IP tags, the <c>$expand</c> query option, the
/// subscription- vs resource-group-scoped list URIs, and the long-running delete's polling target.
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
    private static async Task<JsonElement> CaptureCreateBodyAsync(Action<CreateOrUpdatePublicIPAddressesProcessor_Customization> configure)
    {
        string? body = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            body ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return CreatedPip();
        });

        var processor = new CreateOrUpdatePublicIPAddressesProcessor_Customization
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            PublicIpAddressName = PipName,
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
        var body = await CaptureCreateBodyAsync(processor => processor.SkuName = sku);

        Assert.Equal(expected, body.GetProperty("properties").GetProperty("publicIPAllocationMethod").GetString());
    }

    [Fact]
    public async Task Create_ExplicitAllocationMethod_OverridesTheSkuDefault()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.SkuName = "Standard";
            processor.PublicIPAllocationMethod = "Dynamic";
        });

        Assert.Equal("Dynamic", body.GetProperty("properties").GetProperty("publicIPAllocationMethod").GetString());
    }

    [Fact]
    public async Task Create_DnsSettings_AreOmittedUntilOneIsSupplied()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.DnsSettingsDomainNameLabel = "mylabel";
            processor.DnsSettingsDomainNameLabelScope = "NoReuse";
            processor.DnsSettingsReverseFqdn = "contoso.com";
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
            processor.DdosSettingsProtectionMode = "Enabled";
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

    [Fact]
    public async Task Create_ZonesTierAndEdgeZone_PopulateTheEnvelope()
    {
        var body = await CaptureCreateBodyAsync(processor =>
        {
            processor.Zones = ["1", "2", "3"];
            processor.SkuTier = "Regional";
            processor.ExtendedLocationName = "microsoftlosangeles1";
            processor.IpAddress = "20.1.2.3";
        });

        Assert.Equal(["1", "2", "3"], body.GetProperty("zones").EnumerateArray().Select(zone => zone.GetString()));
        Assert.Equal("Regional", body.GetProperty("sku").GetProperty("tier").GetString());
        Assert.Equal("microsoftlosangeles1", body.GetProperty("extendedLocation").GetProperty("name").GetString());
        Assert.Equal("EdgeZone", body.GetProperty("extendedLocation").GetProperty("type").GetString());
        Assert.Equal("20.1.2.3", body.GetProperty("properties").GetProperty("ipAddress").GetString());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("ipConfiguration", true)]
    public async Task Show_AppendsExpand_OnlyWhenRequested(string? expand, bool expectExpand)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json($$"""{ "name": "{{PipName}}" }""");
        });

        var processor = new GetPublicIPAddressesProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            PublicIpAddressName = PipName,
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

    [Theory]
    [InlineData("20.1.2.3", """{ "id": "/nic/ipConfigurations/ipconfig1" }""")]
    [InlineData("20.1.2.3", """{ "id": "/nic/ipConfigurations/ipconfig1", "properties": { "privateIPAddress": "10.0.0.4", "subnet": { "id": "/subnet" } } }""")]
    [InlineData(null, "null")]
    public async Task Show_ExposesPowerShellProperties_WithoutChangingCliJson(string? ipAddress, string configurationJson)
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json($$"""
            {
              "name": "my-pip",
              "location": "westeurope",
              "properties": {
                "ipAddress": {{JsonSerializer.Serialize(ipAddress)}},
                "provisioningState": "Succeeded",
                "ipConfiguration": {{configurationJson}}
              }
            }
            """));
        var processor = new GetPublicIPAddressesProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            PublicIpAddressName = PipName
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        var pip = Assert.IsType<PublicIPAddress>(Assert.Single(output.WrittenValues));
        Assert.Equal(ipAddress, pip.IpAddress);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        using var configuration = JsonDocument.Parse(configurationJson);
        if (configuration.RootElement.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(pip.IpConfigurationId);
        }
        else
        {
            Assert.Equal("/nic/ipConfigurations/ipconfig1", pip.IpConfigurationId);
        }

        using var cli = JsonDocument.Parse(Core.Cli.Output.CliOutputFormatter.FormatValue(pip));
        Assert.False(cli.RootElement.TryGetProperty("ipAddress", out _));
        var properties = cli.RootElement.GetProperty("properties");
        if (ipAddress is null)
        {
            Assert.True(!properties.TryGetProperty("ipAddress", out var ip) || ip.ValueKind == JsonValueKind.Null);
        }
        else
        {
            Assert.Equal(ipAddress, properties.GetProperty("ipAddress").GetString());
        }
        if (configuration.RootElement.ValueKind == JsonValueKind.Object
            && configuration.RootElement.TryGetProperty("properties", out _))
        {
            Assert.Equal("10.0.0.4", pip.PrivateIPAddress);
            Assert.Equal("/subnet", pip.SubnetId);
            Assert.False(properties.TryGetProperty("privateIPAddress", out _));
            var cliConfiguration = properties.GetProperty("ipConfiguration");
            Assert.False(cliConfiguration.TryGetProperty("privateIPAddress", out _));
            Assert.Equal("10.0.0.4", cliConfiguration.GetProperty("properties").GetProperty("privateIPAddress").GetString());
        }
    }

    /// <summary>
    /// PowerShell formats a single nested object inline, so no public Network type may reach a model
    /// it is already inside through one; lists render by name and may recurse.
    /// </summary>
    [Fact]
    public void PublicSurface_HasNoSingleValuedCircularReference()
    {
        var assembly = typeof(PublicIPAddress).Assembly;
        var models = assembly.GetExportedTypes().Where(t => t.Namespace == typeof(PublicIPAddress).Namespace).ToHashSet();

        var cycles = assembly.GetExportedTypes().SelectMany(t => Cycles(t, [t])).Distinct().ToList();

        Assert.Empty(cycles);

        IEnumerable<string> Cycles(Type type, List<Type> path)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var next = property.PropertyType;
                if (!models.Contains(next))
                {
                    continue;
                }
                if (path.Contains(next))
                {
                    yield return $"{type.Name}.{property.Name}";
                    continue;
                }
                path.Add(next);
                foreach (var cycle in Cycles(next, path))
                {
                    yield return cycle;
                }
                path.RemoveAt(path.Count - 1);
            }
        }
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

        var processor = new ListAllPublicIPAddressesProcessor_Orchestrator
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = resourceGroup
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        Assert.Equal(string.Format(expectedPath, TestSubscriptionId), requested!.AbsolutePath);
    }

    /// <summary>
    /// A null final URI makes the runtime fall back to the original DELETE target, which no longer
    /// exists once the operation completes. The final poll must follow the Location header instead.
    /// </summary>
    [Fact]
    public async Task Delete_PollsOperationLocation_AndNeverRereadsDeletedResource()
    {
        const string operationUri = "https://management.azure.com/subscriptions/s/providers/Microsoft.Network/locations/eastus/operations/op1?api-version=2025-07-01";
        var gets = new List<string>();

        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Add("Location", operationUri);
                return accepted;
            }
            gets.Add(request.RequestUri!.ToString());
            return Json("""{ "status": "Succeeded" }""");
        });

        var processor = new DeletePublicIPAddressesProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            PublicIpAddressName = PipName
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.NotEmpty(gets);
        Assert.All(gets, uri => Assert.Equal(operationUri, uri));
        Assert.DoesNotContain(gets, uri => uri.StartsWith(ResourceUri, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <c>ParameterMetadata.Aliases</c> only renders help; the parser resolves short flags through
    /// <c>CommandMetadata.ParameterAliases</c>, so the two must stay in sync or `-g` prints in help
    /// but fails to bind.
    /// </summary>
    [Theory]
    [InlineData("network public-ip create", "g", "resource-group")]
    [InlineData("network public-ip create", "n", "name")]
    [InlineData("network public-ip create", "l", "location")]
    [InlineData("network public-ip create", "z", "zone")]
    [InlineData("network public-ip show", "g", "resource-group")]
    [InlineData("network public-ip show", "n", "name")]
    [InlineData("network public-ip list", "g", "resource-group")]
    [InlineData("network public-ip delete", "g", "resource-group")]
    [InlineData("network public-ip delete", "n", "name")]
    public void CliCommands_MapLegacyShortFlags_InBothHelpAndParserTables(string command, string alias, string canonical)
    {
        var metadata = new NetworkIndex().GetCommandMetadata(command, ClientType.CLI);

        Assert.True(metadata.ParameterAliases.TryGetValue(alias, out var resolved), $"'{command}' does not resolve -{alias}");
        Assert.Equal(canonical, resolved);
        Assert.Contains(alias, metadata.Parameters[canonical].Aliases);
    }
}
