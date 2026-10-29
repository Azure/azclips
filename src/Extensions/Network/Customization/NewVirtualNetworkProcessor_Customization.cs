// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using System.Text;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network vnet create</c> / <c>New-AzVirtualNetwork</c> surface onto the
/// generated <see cref="NewVirtualNetworkProcessor"/>: the compound <c>--subnets</c> argument, the
/// positional subnet name/prefix pair, <c>KEY[=VALUE]</c> tags, and the legacy address-space default.
/// The generated processor stays a straight spec-to-body mapping and takes already-typed values.
/// </summary>
public class NewVirtualNetworkProcessor_Customization : NewVirtualNetworkProcessor
{
    // Compound form: one occurrence per subnet, each a list of key=value tokens.
    public List<List<string>> SubnetSpecs { get; set; } = [];

    // PowerShell also accepts Subnet objects produced by New-AzVirtualNetworkSubnetConfig.
    public List<object> SubnetInputs { get; set; } = [];

    // Positional form, kept for the simple name+prefix case.
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];

    public List<string> TagSpecs { get; set; } = [];

    // legacy `az network vnet create` defaults the address space to 10.0.0.0/16, and
    // `New-AzVirtualNetwork` makes it mandatory; without a default an omitted value PUTs an empty
    // address space and ARM rejects it. Skipped when IPAM pools supply the prefixes instead.
    private const string DefaultAddressPrefix = "10.0.0.0/16";

    protected override VirtualNetwork? CreateRequestBody()
    {
        if (AddressPrefixes.Count == 0 && IpamPoolPrefixAllocations.Count == 0)
        {
            AddressPrefixes = [DefaultAddressPrefix];
        }

        Subnets = SubnetInputs.Count > 0
            ? SubnetInputs.Select(BuildSubnetInput).ToList()
            : SubnetSpecs.Count > 0
                ? SubnetSpecs.Select(BuildSubnet).ToList()
                : BuildPositionalSubnets();
        Tags = ParseTags();

        return base.CreateRequestBody();
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

        foreach (var (rawKey, value) in KeyValueTokens.Parse(tokens, "--subnets"))
        {
            var key = NormalizeKey(rawKey);
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
                    properties.IpamPoolPrefixAllocations = SplitList(value)
                        .Select(id => new IpamPoolPrefixAllocation { Pool = ToSubResource(id) })
                        .ToList();
                    break;
                case "disable-private-endpoint-network-policies":
                    properties.PrivateEndpointNetworkPolicies = ParseBool(value, key) ? "Disabled" : "Enabled";
                    break;
                case "disable-private-link-service-network-policies":
                    properties.PrivateLinkServiceNetworkPolicies = ParseBool(value, key) ? "Disabled" : "Enabled";
                    break;
                default:
                    throw AzclipsErrorException.InvalidInput(
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
            throw AzclipsErrorException.InvalidInput("Each --subnets value requires a 'name=<value>' key.");
        }

        if (string.IsNullOrEmpty(properties.AddressPrefix) && properties.AddressPrefixes is { Count: > 0 })
        {
            properties.AddressPrefix = properties.AddressPrefixes[0];
        }

        return new Subnet { Name = name, Properties = properties };
    }

    private static Subnet BuildSubnetInput(object input) => input switch
    {
        Subnet subnet => subnet,
        IEnumerable<string> tokens => BuildSubnet(tokens.ToList()),
        _ => throw AzclipsErrorException.InvalidInput(
            $"Subnet input must be a Subnet object or a collection of KEY=VALUE tokens, but received '{input.GetType().FullName}'.")
    };

    private Dictionary<string, string>? ParseTags()
    {
        if (TagSpecs.Count == 0)
        {
            return null;
        }
        // Azure CLI tags are KEY[=VALUE], and the last occurrence of a repeated key wins.
        var tags = new Dictionary<string, string>();
        foreach (var (key, value) in KeyValueTokens.Parse(TagSpecs, "--tags", allowBareKey: true))
        {
            tags[key] = value;
        }
        return tags;
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
            : throw AzclipsErrorException.InvalidInput($"'{key}' expects true or false, but received '{value}'.");

    private static SubResource ToSubResource(string id) => new() { Id = id };
}
