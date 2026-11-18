# network-public-ip-addresses-tests Specification

## Purpose
Verifies the `network-public-ip-addresses` behavior requirements against recorded sessions on the CLI and PowerShell surfaces, covering every public IP address command in the Network command table.

## Requirements
### Requirement: Send DNS settings only when a DNS input is given on create
The scenario tests SHALL verify the behavior requirement "Send DNS settings only when a DNS input is given on create".

#### Scenario: T1 Public IP lifecycle with defaults, DNS settings and remove confirmation
- **WHEN** (CLI) step 2 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip1> --dns-name <dns-label> --dns-name-scope TenantReuse --allocation-method static`
- **THEN** a GET of resource group <rg> is sent before the PUT, and the PUT body location is <location>
- **THEN** the PUT body has sku.name Standard, no sku.tier, publicIPAllocationMethod static, and publicIPAddressVersion IPv4
- **THEN** the PUT body properties.dnsSettings holds only domainNameLabel <dns-label> and domainNameLabelScope TenantReuse, with no fqdn and no reverseFqdn
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, SkuName Standard, PublicIPAllocationMethod Static, DnsSettingsDomainNameLabel <dns-label>, and DnsSettingsDomainNameLabelScope TenantReuse
- **WHEN** (CLI) step 3 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip2>`
- **THEN** a GET of resource group <rg> is sent before the PUT, and the PUT body location is <location>
- **THEN** the PUT body has sku.name Standard, publicIPAllocationMethod Static, publicIPAddressVersion IPv4, and no properties.dnsSettings, sku.tier, or extendedLocation
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, SkuName Standard, PublicIPAllocationMethod Static, PublicIPAddressVersion IPv4, and DnsSettingsDomainNameLabel null
- **WHEN** (CLI) step 4 runs `network public-ip list --subscription-id <subscription> --resource-group <rg>`
- **THEN** the request is GET /subscriptions/<subscription>/resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses
- **THEN** the output holds exactly the PublicIPAddress items named <ip1> and <ip2>, in any order
- **WHEN** (CLI) step 5 runs `network public-ip show --subscription-id <subscription> --resource-group <rg> --name <ip1>`
- **THEN** the output PublicIPAddress has Name <ip1>, Location <location>, PublicIPAllocationMethod Static, and DnsSettingsDomainNameLabel <dns-label>
- **WHEN** (CLI) step 6 runs `network public-ip delete --subscription-id <subscription> --resource-group <rg> --name <ip1>`
- **THEN** a DELETE is sent for <ip1> and the command writes no output
- **WHEN** (CLI) step 7 runs `network public-ip list --subscription-id <subscription> --resource-group <rg>`
- **THEN** the output holds only the PublicIPAddress named <ip2>
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip1> -DomainNameLabel <dns-label> -DomainNameLabelScope TenantReuse -AllocationMethod static`
- **THEN** a GET of resource group <rg> is sent before the PUT, and the PUT body location is <location>
- **THEN** the PUT body has sku.name Standard, no sku.tier, publicIPAllocationMethod static, and publicIPAddressVersion IPv4
- **THEN** the PUT body properties.dnsSettings holds only domainNameLabel <dns-label> and domainNameLabelScope TenantReuse, with no fqdn and no reverseFqdn
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, SkuName Standard, PublicIPAllocationMethod Static, DnsSettingsDomainNameLabel <dns-label>, and DnsSettingsDomainNameLabelScope TenantReuse
- **WHEN** (PowerShell) step 3 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip2>`
- **THEN** a GET of resource group <rg> is sent before the PUT, and the PUT body location is <location>
- **THEN** the PUT body has sku.name Standard, publicIPAllocationMethod Static, publicIPAddressVersion IPv4, and no properties.dnsSettings, sku.tier, or extendedLocation
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, SkuName Standard, PublicIPAllocationMethod Static, PublicIPAddressVersion IPv4, and DnsSettingsDomainNameLabel null
- **WHEN** (PowerShell) step 4 runs `Get-AzPublicIpAddress_List -SubscriptionId <subscription> -ResourceGroupName <rg>`
- **THEN** the request is GET /subscriptions/<subscription>/resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses
- **THEN** the output holds exactly the PublicIPAddress items named <ip1> and <ip2>, in any order
- **WHEN** (PowerShell) step 5 runs `Get-AzPublicIpAddress_Get -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip1>`
- **THEN** the output PublicIPAddress has Name <ip1>, Location <location>, PublicIPAllocationMethod Static, and DnsSettingsDomainNameLabel <dns-label>
- **WHEN** (PowerShell) step 6 runs `Remove-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip1> -Force -PassThru`
- **THEN** no confirmation is requested and a DELETE is sent for <ip1>
- **THEN** the only output is the boolean true
- **WHEN** (PowerShell) step 7 runs `Get-AzPublicIpAddress_List -SubscriptionId <subscription> -ResourceGroupName <rg>`
- **THEN** the output holds only the PublicIPAddress named <ip2>

### Requirement: Default the SKU to Standard on create
The scenario tests SHALL verify the behavior requirement "Default the SKU to Standard on create".

#### Scenario: T2 Create variants: SKU, tier, allocation, version, zones, tags, DDoS mode, idle timeout, retired Basic SKU
- **WHEN** (CLI) step 2 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip1> --location <location>`
- **THEN** no resource group GET is sent for this step, and the PUT body location is <location>
- **THEN** the PUT body has sku.name Standard, no sku.tier, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has SkuName Standard, SkuTier Regional, and PublicIPAllocationMethod Static
- **WHEN** (CLI) step 3 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip2> --location <location> --sku Standard --tags foo=doo`
- **THEN** the PUT body has sku.name Standard, publicIPAllocationMethod Static, and tags {"foo":"doo"}
- **THEN** the output PublicIPAddress has SkuName Standard, SkuTier Regional, PublicIPAllocationMethod Static, and Tags foo = doo
- **WHEN** (CLI) step 4 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip3> --location <location> --sku Standard --tier Global`
- **THEN** the PUT body has sku.name Standard, sku.tier Global, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has SkuName Standard, SkuTier Global, and PublicIPAllocationMethod Static
- **WHEN** (CLI) step 5 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip4> --sku standardv2 --allocation-method static`
- **THEN** a GET of resource group <rg> is sent before the PUT, and the PUT body has location <location>, sku.name standardv2, and publicIPAllocationMethod static
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, SkuName StandardV2, and UpgradedToV2 not true
- **WHEN** (CLI) step 6 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip5> --location <location> --version ipv4 --sku standard -z 1 2 3`
- **THEN** the PUT body has publicIPAddressVersion ipv4, sku.name standard, publicIPAllocationMethod Static, zones ["1","2","3"], and no properties.dnsSettings
- **THEN** the output PublicIPAddress has PublicIPAllocationMethod Static, PublicIPAddressVersion IPv4, Zones ["1","2","3"], and DnsSettingsDomainNameLabel null
- **WHEN** (CLI) step 7 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip6> --location <location> --allocation-method Static --version IPv6`
- **THEN** the PUT body has publicIPAddressVersion IPv6, sku.name Standard, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, PublicIPAddressVersion IPv6, and PublicIPAllocationMethod Static
- **WHEN** (CLI) step 8 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip7> --location <location> --sku Standard --protection-mode Enabled`
- **THEN** the PUT body has properties.ddosSettings.protectionMode Enabled and no ddosSettings.ddosProtectionPlan
- **THEN** the output PublicIPAddress has DdosSettingsProtectionMode Enabled and PublicIPAllocationMethod Static
- **WHEN** (CLI) step 9 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip8> --location <location> --allocation-method Static --idle-timeout 15`
- **THEN** the PUT body has properties.idleTimeoutInMinutes 15
- **THEN** the output PublicIPAddress has IdleTimeoutInMinutes 15 and PublicIPAllocationMethod Static
- **WHEN** (CLI) step 10 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip9> --location <location> --sku Basic --allocation-method Dynamic`
- **THEN** the PUT body has sku.name Basic and publicIPAllocationMethod Dynamic
- **THEN** the command fails with the service error rejecting a new Basic SKU public IP, and writes no output
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip1> -Location <location>`
- **THEN** no resource group GET is sent for this step, and the PUT body location is <location>
- **THEN** the PUT body has sku.name Standard, no sku.tier, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has SkuName Standard, SkuTier Regional, and PublicIPAllocationMethod Static
- **WHEN** (PowerShell) step 3 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip2> -Location <location> -Sku Standard -Tag @{ foo = "doo" }`
- **THEN** the PUT body has sku.name Standard, publicIPAllocationMethod Static, and tags {"foo":"doo"}
- **THEN** the output PublicIPAddress has SkuName Standard, SkuTier Regional, PublicIPAllocationMethod Static, and Tags foo = doo
- **WHEN** (PowerShell) step 4 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip3> -Location <location> -Sku Standard -Tier Global`
- **THEN** the PUT body has sku.name Standard, sku.tier Global, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has SkuName Standard, SkuTier Global, and PublicIPAllocationMethod Static
- **WHEN** (PowerShell) step 5 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip4> -Sku standardv2 -AllocationMethod static`
- **THEN** a GET of resource group <rg> is sent before the PUT, and the PUT body has location <location>, sku.name standardv2, and publicIPAllocationMethod static
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, SkuName StandardV2, and UpgradedToV2 not true
- **WHEN** (PowerShell) step 6 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip5> -Location <location> -Version ipv4 -Sku standard -Zone 1,2,3`
- **THEN** the PUT body has publicIPAddressVersion ipv4, sku.name standard, publicIPAllocationMethod Static, zones ["1","2","3"], and no properties.dnsSettings
- **THEN** the output PublicIPAddress has PublicIPAllocationMethod Static, PublicIPAddressVersion IPv4, Zones ["1","2","3"], and DnsSettingsDomainNameLabel null
- **WHEN** (PowerShell) step 7 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip6> -Location <location> -AllocationMethod Static -IpAddressVersion IPv6`
- **THEN** the PUT body has publicIPAddressVersion IPv6, sku.name Standard, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has ProvisioningState Succeeded, PublicIPAddressVersion IPv6, and PublicIPAllocationMethod Static
- **WHEN** (PowerShell) step 8 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip7> -Location <location> -Sku Standard -DdosProtectionMode Enabled`
- **THEN** the PUT body has properties.ddosSettings.protectionMode Enabled and no ddosSettings.ddosProtectionPlan
- **THEN** the output PublicIPAddress has DdosSettingsProtectionMode Enabled and PublicIPAllocationMethod Static
- **WHEN** (PowerShell) step 9 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip8> -Location <location> -AllocationMethod Static -IdleTimeoutInMinutes 15`
- **THEN** the PUT body has properties.idleTimeoutInMinutes 15
- **THEN** the output PublicIPAddress has IdleTimeoutInMinutes 15 and PublicIPAllocationMethod Static
- **WHEN** (PowerShell) step 10 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip9> -Location <location> -Sku Basic -AllocationMethod Dynamic`
- **THEN** the PUT body has sku.name Basic and publicIPAllocationMethod Dynamic
- **THEN** the command fails with the service error rejecting a new Basic SKU public IP, and writes no output

### Requirement: Set an EdgeZone extended location from EdgeZone on create
The scenario tests SHALL verify the behavior requirement "Set an EdgeZone extended location from EdgeZone on create".

#### Scenario: T3 Create in an edge zone
- **WHEN** (CLI) step 2 runs `network public-ip create --subscription-id <subscription> --resource-group <rg> --name <ip1> --location <location> --edge-zone <edge-zone> --sku Standard`
- **THEN** the PUT body has extendedLocation {"name":"<edge-zone>","type":"EdgeZone"}, location <location>, sku.name Standard, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has ExtendedLocationName <edge-zone> and ExtendedLocationType EdgeZone, or, if the recording subscription has no access to the edge zone, the command fails with the service error saying the resource type does not support that edge zone (the recorded outcome decides which assertion is kept)
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -SubscriptionId <subscription> -ResourceGroupName <rg> -Name <ip1> -Location <location> -EdgeZone <edge-zone> -Sku Standard`
- **THEN** the PUT body has extendedLocation {"name":"<edge-zone>","type":"EdgeZone"}, location <location>, sku.name Standard, and publicIPAllocationMethod Static
- **THEN** the output PublicIPAddress has ExtendedLocationName <edge-zone> and ExtendedLocationType EdgeZone, or, if the recording subscription has no access to the edge zone, the command fails with the service error saying the resource type does not support that edge zone (the recorded outcome decides which assertion is kept)
