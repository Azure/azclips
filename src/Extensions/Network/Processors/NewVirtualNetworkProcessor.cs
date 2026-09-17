// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using System.Text;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewVirtualNetworkProcessor : NetworkProcessorBase<VirtualNetwork, VirtualNetwork>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> AddressPrefixes { get; set; } = [];

    // Compound form: one occurrence per subnet, each a list of key=value tokens.
    public List<List<string>> Subnets { get; set; } = [];

    // Positional form, kept for the simple name+prefix case.
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];

    public List<string> DnsServers { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public bool EnableDdosProtection { get; set; }
    public string DdosProtectionPlan { get; set; } = string.Empty;
    public bool EnableVmProtection { get; set; }

    // 0 means "not specified"; the CLI binder has no nullable-int support.
    public int FlowTimeoutInMinutes { get; set; }

    public string BgpCommunity { get; set; } = string.Empty;
    public bool EnableEncryption { get; set; }
    public string EncryptionEnforcementPolicy { get; set; } = string.Empty;
    public string ExtendedLocation { get; set; } = string.Empty;
    public string PrivateEndpointVNetPolicies { get; set; } = string.Empty;
    public List<string> IpAllocations { get; set; } = [];
    public List<string> IpamPoolPrefixAllocations { get; set; } = [];
    public List<string> SummarizedGatewayPrefixes { get; set; } = [];

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{Name}?api-version={ApiVersion}";

    // legacy `az network vnet create` defaults the address space to 10.0.0.0/16, and
    // `New-AzVirtualNetwork` makes it mandatory; without a default an omitted value PUTs an empty
    // address space and ARM rejects it. Skipped when IPAM pools supply the prefixes instead.
    private const string DefaultAddressPrefix = "10.0.0.0/16";

    protected override VirtualNetwork? CreateRequestBody()
    {
        var ipamAllocations = IpamPoolPrefixAllocations.Select(ToPoolAllocation).ToList();
        List<string> addressPrefixes = AddressPrefixes.Count == 0 && ipamAllocations.Count == 0
            ? [DefaultAddressPrefix]
            : AddressPrefixes;

        var properties = new VirtualNetworkProperties
        {
            AddressSpace = new AddressSpace
            {
                AddressPrefixes = addressPrefixes,
                IpamPoolPrefixAllocations = ipamAllocations
            },
            Subnets = Subnets.Count > 0 ? Subnets.Select(BuildSubnet).ToList() : BuildPositionalSubnets(),
            VirtualNetworkPeerings = [],
            EnableDdosProtection = EnableDdosProtection,
            IpAllocations = IpAllocations.Select(ToSubResource).ToList()
        };

        if (DnsServers.Count > 0)
        {
            properties.DhcpOptions = new DhcpOptions { DnsServers = DnsServers };
        }
        if (FlowTimeoutInMinutes > 0)
        {
            properties.FlowTimeoutInMinutes = FlowTimeoutInMinutes;
        }
        if (EnableVmProtection)
        {
            properties.EnableVmProtection = true;
        }
        if (DdosProtectionPlan.Length > 0)
        {
            properties.DdosProtectionPlan = ToSubResource(DdosProtectionPlan);
        }
        if (BgpCommunity.Length > 0)
        {
            properties.BgpCommunities = new VirtualNetworkBgpCommunities { VirtualNetworkCommunity = BgpCommunity };
        }
        if (EnableEncryption || EncryptionEnforcementPolicy.Length > 0)
        {
            properties.Encryption = new VirtualNetworkEncryption
            {
                Enabled = EnableEncryption,
                Enforcement = EncryptionEnforcementPolicy.Length > 0 ? EncryptionEnforcementPolicy : null
            };
        }
        if (PrivateEndpointVNetPolicies.Length > 0)
        {
            properties.PrivateEndpointVNetPolicies = PrivateEndpointVNetPolicies;
        }
        if (SummarizedGatewayPrefixes.Count > 0)
        {
            properties.SummarizedGatewayPrefixes = new AddressSpace { AddressPrefixes = SummarizedGatewayPrefixes };
        }

        return new VirtualNetwork
        {
            Location = Location,
            Properties = properties,
            Tags = ParseTags(),
            ExtendedLocation = ExtendedLocation.Length > 0
                ? new Models.ExtendedLocation { Name = ExtendedLocation }
                : null
        };
    }

    private List<Subnet> BuildPositionalSubnets() =>
        SubnetNames.Select((name, index) =>
        {
            var subnetPrefixes = SubnetAddressPrefixes.ElementAtOrDefault(index) ?? [];
            return new Subnet
            {
                Name = name,
                Properties = new SubnetProperties
                {
                    AddressPrefix = subnetPrefixes.FirstOrDefault() ?? string.Empty,
                    AddressPrefixes = subnetPrefixes
                }
            };
        }).ToList();

    private static Subnet BuildSubnet(List<string> tokens)
    {
        var properties = new SubnetProperties();
        string? name = null;

        foreach (var (key, value) in ParseKeyValues(tokens, "--subnets", normalizeKey: true))
        {
            switch (key)
            {
                case "name": name = value; break;
                case "address-prefix": properties.AddressPrefix = value; break;
                case "address-prefixes": properties.AddressPrefixes = SplitList(value); break;
                case "nsg":
                case "network-security-group": properties.NetworkSecurityGroup = ToSubResource(value); break;
                case "route-table": properties.RouteTable = ToSubResource(value); break;
                case "nat-gateway": properties.NatGateway = ToSubResource(value); break;
                case "default-outbound-access": properties.DefaultOutboundAccess = ParseBool(value, key); break;
                case "service-endpoints":
                case "service-endpoint":
                    properties.ServiceEndpoints = SplitList(value).Select(s => new ServiceEndpoint { Service = s }).ToList();
                    break;
                case "service-endpoint-policy":
                case "service-endpoint-policies":
                    properties.ServiceEndpointPolicies = SplitList(value).Select(ToSubResource).ToList();
                    break;
                case "delegations":
                case "delegation":
                    properties.Delegations = SplitList(value)
                        .Select(s => new Delegation { Name = s, Properties = new DelegationProperties { ServiceName = s } })
                        .ToList();
                    break;
                case "ipam-pool-prefix-allocations":
                case "ipam-pool-prefix-allocation":
                    properties.IpamPoolPrefixAllocations = SplitList(value).Select(ToPoolAllocation).ToList();
                    break;
                case "disable-private-endpoint-network-policies":
                    properties.PrivateEndpointNetworkPolicies = ParseBool(value, key) ? "Disabled" : "Enabled";
                    break;
                case "disable-private-link-service-network-policies":
                    properties.PrivateLinkServiceNetworkPolicies = ParseBool(value, key) ? "Disabled" : "Enabled";
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown --subnets key '{key}'. Supported keys: name, address-prefix, address-prefixes, nsg, route-table, " +
                        "nat-gateway, default-outbound-access, service-endpoints, " +
                        "service-endpoint-policy, delegations, " +
                        "ipam-pool-prefix-allocations, disable-private-endpoint-network-policies, " +
                        "disable-private-link-service-network-policies. PascalCase spellings (AddressPrefix, " +
                        "DefaultOutboundAccess, ...) are accepted as well.");
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Each --subnets value requires a 'name=<value>' key.");
        }

        if (properties.AddressPrefix.Length == 0 && properties.AddressPrefixes.Count > 0)
        {
            properties.AddressPrefix = properties.AddressPrefixes[0];
        }

        return new Subnet { Name = name, Properties = properties };
    }

    private Dictionary<string, string>? ParseTags()
    {
        if (Tags.Count == 0)
        {
            return null;
        }
        return ParseKeyValues(Tags, "--tags").ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseKeyValues(IEnumerable<string> tokens, string argument, bool normalizeKey = false)
    {
        foreach (var token in tokens)
        {
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"'{token}' is not a valid {argument} value; expected KEY=VALUE.");
            }
            var key = token[..separator].Trim();
            yield return new KeyValuePair<string, string>(
                normalizeKey ? NormalizeKey(key) : key,
                token[(separator + 1)..].Trim());
        }
    }

    /// <summary>
    /// Folds a key to lower kebab-case so the CLI spelling (<c>address-prefix</c>) and the PowerShell
    /// hashtable spelling (<c>AddressPrefix</c>) resolve to the same subnet property. Acronyms stay
    /// intact except where a word starts inside them, so <c>NSG</c> stays <c>nsg</c> while
    /// <c>IPConfigurations</c> becomes <c>ip-configurations</c>.
    /// </summary>
    private static string NormalizeKey(string key)
    {
        var builder = new StringBuilder(key.Length + 8);
        for (var index = 0; index < key.Length; index++)
        {
            var character = key[index];
            if (character == '_')
            {
                builder.Append('-');
                continue;
            }
            var previous = index > 0 ? key[index - 1] : '\0';
            var startsWord = char.IsUpper(character)
                && index > 0
                && previous != '-'
                && previous != '_'
                && (char.IsLower(previous) || char.IsDigit(previous) || (index + 1 < key.Length && char.IsLower(key[index + 1])));
            if (startsWord)
            {
                builder.Append('-');
            }
            builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private static List<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool ParseBool(string value, string key) =>
        bool.TryParse(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"'{key}' expects true or false, but received '{value}'.");

    private static SubResource ToSubResource(string id) => new() { Id = id };

    private static IpamPoolPrefixAllocation ToPoolAllocation(string id) => new() { Pool = ToSubResource(id) };

    // swagger declares final-state-via azure-async-operation with a
    // final-state-schema, but ARM returns no such body here; fall back to no final URI.
    protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
