using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Network;

[ExtensionVerb("network")]
public class ExtensionIndex : IExtensionIndex
{
    /// <summary>One command input. A null <paramref name="Cli"/> hides it from CLI; a null <paramref name="Ps"/> hides it from PowerShell.</summary>
    private sealed record Arg(string Property, string? Cli, string? Ps, Type Type, string Help, bool Required = false, string[]? CliAliases = null, string[]? Allowed = null);

    private const string AssemblyName = "Microsoft.Azure.Azclips.Extensions.Network";

    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CommandMetadata> s_psTable = new(StringComparer.OrdinalIgnoreCase);

    // ── Shared inputs ──
    private static readonly Arg s_subscription = new("SubscriptionId", "subscription-id", "SubscriptionId", typeof(string), "Azure subscription ID. Defaults to the active profile's subscription.", CliAliases: ["subscription"]);
    private static Arg ResourceGroup(bool required = true) => new("ResourceGroupName", "resource-group", "ResourceGroupName", typeof(string), "Name of the resource group.", required, ["g"]);
    private static Arg Name(string property, string help, bool required = true) => new(property, "name", "Name", typeof(string), help, required, ["n"]);
    private static readonly Arg s_location = new("Location", "location", "Location", typeof(string), "Location. Defaults to the resource group location.", CliAliases: ["l"]);
    private static readonly Arg s_cliTags = new("TagsInput", "tags", null, typeof(string[]), "Space-separated tags in KEY[=VALUE] form.");
    private static readonly Arg s_psTags = new("Tags", null, "Tag", typeof(Dictionary<string, string>), "Tags as key-value pairs.");
    private static readonly Arg s_expand = new("Expand", "expand", "ExpandResource", typeof(string), "Expands referenced resources.");
    private static readonly Arg s_edgeZone = new("EdgeZone", "edge-zone", "EdgeZone", typeof(string), "Name of the edge zone.");

    private static readonly string[] s_ipVersions = ["IPv4", "IPv6"];

    static ExtensionIndex()
    {
        AddVirtualNetworkCommands();
        AddSubnetCommands();
        AddNetworkSecurityGroupCommands();
        AddSecurityRuleCommands();
        AddPublicIpAddressCommands();
        AddNetworkInterfaceCommands();
    }

    private static void AddVirtualNetworkCommands()
    {
        Arg vnetName = Name("VirtualNetworkName", "Name of the virtual network.");
        Add<CreateVirtualNetworkProcessor_Customization>("network vnet create", "New-AzVirtualNetwork",
            vnetName, ResourceGroup(), s_location, s_cliTags, s_psTags,
            new("AddressPrefixes", "address-prefixes", "AddressPrefixes", typeof(List<string>), "Space-separated IP address prefixes in CIDR form. Default: 10.0.0.0/16."),
            new("DnsServers", "dns-servers", "DnsServers", typeof(List<string>), "Space-separated DNS server IP addresses."),
            new("SubnetName", "subnet-name", "SubnetName", typeof(string), "Name of a subnet to create with the virtual network."),
            new("SubnetPrefixes", "subnet-prefixes", "SubnetPrefixes", typeof(List<string>), "Space-separated address prefixes for the new subnet. Used only with the subnet name."),
            new("EnableDdosProtection", "ddos-protection", "EnableDdosProtection", typeof(bool), "Enable DDoS protection for protected resources in the virtual network."),
            new("DdosProtectionPlan", "ddos-protection-plan", "DdosProtectionPlan", typeof(string), "Name or ID of a DDoS protection plan."),
            new("FlowTimeoutInMinutes", "flowtimeout", "FlowTimeoutInMinutes", typeof(int), "Flow timeout in minutes."),
            new("BgpCommunity", "bgp-community", "BgpCommunity", typeof(string), "BGP community attribute, for example 12076:20000."),
            new("EnableEncryption", "enable-encryption", "EnableEncryption", typeof(bool), "Enable encryption on the virtual network."),
            new("EncryptionEnforcementPolicy", "encryption-enforcement-policy", "EncryptionEnforcement", typeof(string), "Encryption enforcement policy. Used only with encryption enabled.", CliAliases: ["encryption-policy"], Allowed: ["AllowUnencrypted", "DropUnencrypted"]),
            s_edgeZone);
        Add<GetVirtualNetworksProcessor>("network vnet show", "Get-AzVirtualNetwork_Get", vnetName, ResourceGroup(), s_expand);
        Add<ListVirtualNetworkProcessor_Customization>("network vnet list", "Get-AzVirtualNetwork_List", ResourceGroup(required: false));
        Add<DeleteVirtualNetworksProcessor>("network vnet delete", "Remove-AzVirtualNetwork", vnetName, ResourceGroup());
    }

    private static void AddSubnetCommands()
    {
        Arg vnetName = new("VirtualNetworkName", "vnet-name", "VirtualNetworkName", typeof(string), "Name of the virtual network.", Required: true);
        Arg[] SubnetInputs(bool create) =>
        [
            Name("Name", "Name of the subnet."), vnetName, ResourceGroup(),
            new("AddressPrefixes", "address-prefixes", "AddressPrefixes", typeof(List<string>), "Space-separated address prefixes in CIDR form.", Required: create),
            new("NetworkSecurityGroup", "network-security-group", "NetworkSecurityGroup", typeof(string), "Name or ID of a network security group. Use \"\" to detach it on update.", CliAliases: ["nsg"]),
            new("RouteTable", "route-table", "RouteTable", typeof(string), "Name or ID of a route table. Use \"\" to detach it on update."),
            new("NatGateway", "nat-gateway", "NatGateway", typeof(string), "Name or ID of a NAT gateway. Use \"\" to detach it on update."),
            new("ServiceEndpoints", "service-endpoints", "ServiceEndpoints", typeof(List<string>), "Space-separated service endpoints, for example Microsoft.Storage."),
            new("Delegations", "delegations", "Delegations", typeof(List<string>), "Space-separated service names to delegate the subnet to, for example Microsoft.Web/serverFarms."),
            new("PrivateEndpointNetworkPolicies", "private-endpoint-network-policies", "PrivateEndpointNetworkPolicies", typeof(string), "Network policies for private endpoints in the subnet.", Allowed: ["Disabled", "Enabled", "NetworkSecurityGroupEnabled", "RouteTableEnabled"]),
            new("PrivateLinkServiceNetworkPolicies", "private-link-service-network-policies", "PrivateLinkServiceNetworkPolicies", typeof(string), "Network policies for private link services in the subnet.", Allowed: ["Disabled", "Enabled"]),
            new("DefaultOutboundAccess", "default-outbound-access", "DefaultOutboundAccess", typeof(bool), "Enable default outbound connectivity for virtual machines in the subnet.", CliAliases: ["default-outbound"]),
        ];
        Arg subnetName = Name("SubnetName", "Name of the subnet.");
        Add<CreateSubnetCustomization>("network vnet subnet create", "New-AzVirtualNetworkSubnet", SubnetInputs(create: true));
        Add<UpdateSubnetCustomization>("network vnet subnet update", "Set-AzVirtualNetworkSubnet", SubnetInputs(create: false));
        Add<GetSubnetsProcessor>("network vnet subnet show", "Get-AzVirtualNetworkSubnet_Get", subnetName, vnetName, ResourceGroup(), s_expand);
        Add<ListSubnetsProcessor>("network vnet subnet list", "Get-AzVirtualNetworkSubnet_List", vnetName, ResourceGroup());
        Add<DeleteSubnetsProcessor>("network vnet subnet delete", "Remove-AzVirtualNetworkSubnet", subnetName, vnetName, ResourceGroup());
    }

    private static void AddNetworkSecurityGroupCommands()
    {
        Arg nsgName = Name("NetworkSecurityGroupName", "Name of the network security group.");
        Add<CreateNetworkSecurityGroupProcessor_Customization>("network nsg create", "New-AzNetworkSecurityGroup", Name("Name", "Name of the network security group."), ResourceGroup(), s_location, s_cliTags, s_psTags);
        Add<GetNetworkSecurityGroupsProcessor>("network nsg show", "Get-AzNetworkSecurityGroup_Get", nsgName, ResourceGroup(), s_expand);
        Add<ListNetworkSecurityGroupProcessor_Customization>("network nsg list", "Get-AzNetworkSecurityGroup_List", ResourceGroup(required: false));
        Add<DeleteNetworkSecurityGroupsProcessor>("network nsg delete", "Remove-AzNetworkSecurityGroup", nsgName, ResourceGroup());
    }

    private static void AddSecurityRuleCommands()
    {
        Arg nsgName = new("NetworkSecurityGroupName", "nsg-name", "NetworkSecurityGroupName", typeof(string), "Name of the network security group.", Required: true);
        Arg ruleName = Name("SecurityRuleName", "Name of the security rule.");
        Arg[] RuleInputs(bool create) =>
        [
            ruleName, nsgName, ResourceGroup(),
            new(create ? "Priority" : "GivenPriority", "priority", "Priority", typeof(int), "Rule priority, from 100 to 4096. A lower number has a higher priority.", Required: create),
            new("Access", "access", "Access", typeof(string), create ? "Network traffic is allowed or denied. Default: Allow." : "Network traffic is allowed or denied.", Allowed: ["Allow", "Deny"]),
            new("Direction", "direction", "Direction", typeof(string), create ? "Direction of the traffic. Default: Inbound." : "Direction of the traffic.", Allowed: ["Inbound", "Outbound"]),
            new("Protocol", "protocol", "Protocol", typeof(string), create ? "Network protocol. Default: *." : "Network protocol.", Allowed: ["Tcp", "Udp", "Icmp", "Esp", "Ah", "*"]),
            new("SourceAddressPrefixes", "source-address-prefixes", "SourceAddressPrefixes", typeof(List<string>), "Space-separated source CIDR prefixes, IP ranges, or service tags."),
            new("SourcePortRanges", "source-port-ranges", "SourcePortRanges", typeof(List<string>), "Space-separated source ports or port ranges."),
            new("DestinationAddressPrefixes", "destination-address-prefixes", "DestinationAddressPrefixes", typeof(List<string>), "Space-separated destination CIDR prefixes, IP ranges, or service tags."),
            new("DestinationPortRanges", "destination-port-ranges", "DestinationPortRanges", typeof(List<string>), "Space-separated destination ports or port ranges."),
            new("SourceAsgs", "source-asgs", "SourceAsgs", typeof(List<string>), "Space-separated names or IDs of source application security groups."),
            new("DestinationAsgs", "destination-asgs", "DestinationAsgs", typeof(List<string>), "Space-separated names or IDs of destination application security groups."),
            new("Description", "description", "Description", typeof(string), "Description of the rule."),
        ];
        Add<CreateSecurityRuleProcessor_Customization>("network nsg rule create", "New-AzNetworkSecurityRule", RuleInputs(create: true));
        Add<UpdateSecurityRuleProcessor_Customization>("network nsg rule update", "Set-AzNetworkSecurityRule", RuleInputs(create: false));
        Add<GetSecurityRulesProcessor>("network nsg rule show", "Get-AzNetworkSecurityRule_Get", ruleName, nsgName, ResourceGroup());
        Add<ListSecurityRulesProcessor>("network nsg rule list", "Get-AzNetworkSecurityRule_List", nsgName, ResourceGroup());
        Add<DeleteSecurityRulesProcessor>("network nsg rule delete", "Remove-AzNetworkSecurityRule", ruleName, nsgName, ResourceGroup());
    }

    private static void AddPublicIpAddressCommands()
    {
        Arg pipName = Name("PublicIpAddressName", "Name of the public IP address.");
        Add<CreateOrUpdatePublicIPAddressesProcessor_Customization>("network public-ip create", "New-AzPublicIpAddress",
            pipName, ResourceGroup(), s_location, s_cliTags, s_psTags,
            new("Sku", "sku", "Sku", typeof(string), "SKU name. Default: Standard.", Allowed: ["Basic", "Standard", "StandardV2"]),
            new("Tier", "tier", "Tier", typeof(string), "SKU tier.", Allowed: ["Regional", "Global"]),
            new("AllocationMethod", "allocation-method", "AllocationMethod", typeof(string), "IP address allocation method. Default: Static for Standard SKUs, else Dynamic.", Allowed: ["Static", "Dynamic"]),
            new("Version", "version", "Version", typeof(string), "IP address version. Default: IPv4.", Allowed: s_ipVersions),
            new("Zones", "zone", "Zone", typeof(List<string>), "Space-separated availability zones.", CliAliases: ["z"], Allowed: ["1", "2", "3"]),
            new("DomainNameLabel", "dns-name", "DomainNameLabel", typeof(string), "Domain name label. Creates a DNS name for the IP address."),
            new("DomainNameLabelScope", "dns-name-scope", "DomainNameLabelScope", typeof(string), "Scope for the hashed domain name label.", Allowed: ["TenantReuse", "SubscriptionReuse", "ResourceGroupReuse", "NoReuse"]),
            new("ReverseFqdn", "reverse-fqdn", "ReverseFqdn", typeof(string), "Reverse FQDN that resolves to this IP address."),
            new("IdleTimeoutInMinutes", "idle-timeout", "IdleTimeoutInMinutes", typeof(int), "Idle timeout in minutes."),
            new("IpAddress", "ip-address", "IpAddress", typeof(string), "IP address to use."),
            new("DdosSettingsProtectionMode", "protection-mode", "DdosProtectionMode", typeof(string), "DDoS protection mode of the IP address.", CliAliases: ["ddos-protection-mode"], Allowed: ["VirtualNetworkInherited", "Enabled", "Disabled"]),
            s_edgeZone);
        Add<GetPublicIPAddressesProcessor>("network public-ip show", "Get-AzPublicIpAddress_Get", pipName, ResourceGroup(), s_expand);
        Add<ListPublicIPAddressesProcessor_Customization>("network public-ip list", "Get-AzPublicIpAddress_List", ResourceGroup(required: false));
        Add<DeletePublicIPAddressesProcessor>("network public-ip delete", "Remove-AzPublicIpAddress", pipName, ResourceGroup());
    }

    private static void AddNetworkInterfaceCommands()
    {
        Arg nicName = Name("NetworkInterfaceName", "Name of the network interface.");
        Add<CreateNetworkInterfaceProcessor>("network nic create", "New-AzNetworkInterface",
            nicName, ResourceGroup(), s_location, s_cliTags, s_psTags,
            new("Subnet", "subnet", "Subnet", typeof(string), "Name or ID of the subnet. A name needs the virtual network name.", Required: true),
            new("VirtualNetworkName", "vnet-name", "VirtualNetworkName", typeof(string), "Name of the virtual network. Needed when the subnet is a name."),
            new("IpConfigurationName", "ip-config-name", "IpConfigurationName", typeof(string), "Name of the IP configuration. Default: ipconfig1."),
            new("PrivateIpAddress", "private-ip-address", "PrivateIpAddress", typeof(string), "Static private IP address. If omitted, the address is dynamic."),
            new("PrivateIpAddressVersion", "private-ip-address-version", "PrivateIpAddressVersion", typeof(string), "Private IP address version. Default: IPv4.", Allowed: s_ipVersions),
            new("PublicIpAddress", "public-ip-address", "PublicIpAddress", typeof(string), "Name or ID of a public IP address."),
            new("NetworkSecurityGroup", "network-security-group", "NetworkSecurityGroup", typeof(string), "Name or ID of a network security group."),
            new("DnsSettingsDnsServers", "dns-servers", "DnsServers", typeof(List<string>), "Space-separated DNS server IP addresses."),
            new("DnsSettingsInternalDnsNameLabel", "internal-dns-name", "InternalDnsNameLabel", typeof(string), "Internal DNS name label."),
            new("EnableIPForwarding", "ip-forwarding", "EnableIPForwarding", typeof(bool), "Enable IP forwarding."),
            new("EnableAcceleratedNetworking", "accelerated-networking", "EnableAcceleratedNetworking", typeof(bool), "Enable accelerated networking."),
            s_edgeZone);
        Add<GetNetworkInterfacesProcessor>("network nic show", "Get-AzNetworkInterface_Get", nicName, ResourceGroup(), s_expand);
        Add<ListNetworkInterfaceProcessor_Orchestrator>("network nic list", "Get-AzNetworkInterface_List", ResourceGroup(required: false));
        Add<DeleteNetworkInterfacesProcessor>("network nic delete", "Remove-AzNetworkInterface", nicName, ResourceGroup());
    }

    private static void Add<T>(string cliKey, string psKey, params Arg[] args)
    {
        Arg profile = new("Profile", "profile", "Profile", typeof(string), "Use the named profile for this command (overrides the active profile).");
        Arg[] all = [.. args, s_subscription, profile];
        s_cliTable.Add(cliKey, Build<T>(cliKey, all, cli: true));
        s_psTable.Add(psKey, Build<T>(psKey, all, cli: false));
    }

    private static CommandMetadata Build<T>(string invocation, Arg[] args, bool cli)
    {
        var parameters = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Arg arg in args)
        {
            string? name = cli ? arg.Cli : arg.Ps;
            if (name is null)
            {
                continue;
            }
            string[] argAliases = cli ? arg.CliAliases ?? [] : [];
            parameters.Add(name, new ParameterMetadata(arg.Property, name, arg.Type, arg.Help, arg.Required, argAliases, arg.Allowed ?? []));
            foreach (string alias in argAliases)
            {
                aliases.Add(alias, name);
            }
        }

        var metadata = new CommandMetadata(typeof(T).Name, invocation, [], typeof(T).FullName!, AssemblyName, typeof(T), parameters, aliases);
        if (!cli)
        {
            return metadata;
        }

        // The CLI checks required inputs only inside parameter sets.
        var defaultSet = new ParameterSetMetadata("Default", IsDefault: true,
            Parameters: parameters.Values.ToDictionary(p => p.Name, p => new ParameterReference(p.Name, p.IsRequired)));
        return metadata with { ParameterSets = new Dictionary<string, ParameterSetMetadata> { ["Default"] = defaultSet } };
    }

    // ── Lookup ──
    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
    {
        return GetCommandTable(clientType).TryGetValue(name, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
    }

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => clientType switch
    {
        ClientType.PowerShell => s_psTable,
        ClientType.CLI => s_cliTable,
        _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
    };

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => GetCommandTable(clientType).Values;

    public IEnumerable<string> GetCommandKeys(ClientType clientType) => GetCommandTable(clientType).Keys;
}
