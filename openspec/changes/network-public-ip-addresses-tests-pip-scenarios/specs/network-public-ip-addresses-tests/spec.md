# Spec Delta

## Purpose

Record/playback scenario tests for the Network extension's public IP address commands on the CLI surface (`network public-ip create`, `network public-ip show`, `network public-ip list`, `network public-ip delete`) and the PowerShell surface (`New-AzPublicIpAddress`, `Get-AzPublicIpAddress_Get`, `Get-AzPublicIpAddress_List`, `Remove-AzPublicIpAddress`). Each test replays its recording and verifies the behavior stated in the `network-public-ip-addresses` spec.

## ADDED Requirements

### Requirement: List scope by resource group presence
The scenario tests SHALL verify the behavior requirement "List scope by resource group presence".

#### Scenario: T1 Public IP lifecycle: DNS label scope, IP tags, zones, list, show, delete
- **WHEN** (CLI) step 2 runs `network public-ip create -g <rg> -n <pip1> -l <location> --dns-name <dns label> --dns-name-scope TenantReuse --allocation-method Static`
- **THEN** output is one PublicIPAddress with Name <pip1>
- **AND** PublicIPAllocationMethod is Static
- **AND** DnsSettingsDomainNameLabel is <dns label>
- **AND** DnsSettingsDomainNameLabelScope is TenantReuse
- **AND** ProvisioningState is Succeeded
- **WHEN** (CLI) step 3 runs `network public-ip create -g <rg> -n <pip2> -l <location> --version IPv4 --sku Standard --zone 1 2 3 --ip-tags RoutingPreference=Internet`
- **THEN** output is one PublicIPAddress with Name <pip2>
- **AND** PublicIPAddressVersion is IPv4
- **AND** PublicIPAllocationMethod is Static
- **AND** Zones contains exactly 1, 2, 3 (any order)
- **AND** IpTags has one entry with IpTagType RoutingPreference and Tag Internet
- **AND** DnsSettingsDomainNameLabel is null
- **WHEN** (CLI) step 4 runs `network public-ip list -g <rg>`
- **THEN** output is PublicIPAddress items whose names are exactly <pip1> and <pip2>
- **WHEN** (CLI) step 5 runs `network public-ip show -g <rg> -n <pip1>`
- **THEN** output is one PublicIPAddress with Name <pip1> and Location <location>
- **AND** DnsSettingsDomainNameLabel is <dns label>
- **WHEN** (CLI) step 6 runs `network public-ip delete -g <rg> -n <pip1>`
- **THEN** request is DELETE on .../resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses/<pip1> and the step returns no error
- **WHEN** (CLI) step 7 runs `network public-ip list -g <rg>`
- **THEN** output contains <pip2> and no PublicIPAddress named <pip1>
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip1> -Location <location> -DomainNameLabel <dns label> -DomainNameLabelScope TenantReuse -AllocationMethod Static`
- **THEN** output is one PublicIPAddress with Name <pip1>
- **AND** PublicIPAllocationMethod is Static
- **AND** DnsSettingsDomainNameLabel is <dns label>
- **AND** DnsSettingsDomainNameLabelScope is TenantReuse
- **AND** ProvisioningState is Succeeded
- **WHEN** (PowerShell) step 3 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip2> -Location <location> -IpAddressVersion IPv4 -Sku Standard -Zone @('1','2','3') -IpTag @{ RoutingPreference = 'Internet' }`
- **THEN** output is one PublicIPAddress with Name <pip2>
- **AND** PublicIPAddressVersion is IPv4
- **AND** PublicIPAllocationMethod is Static
- **AND** Zones contains exactly 1, 2, 3 (any order)
- **AND** IpTags has one entry with IpTagType RoutingPreference and Tag Internet
- **AND** DnsSettingsDomainNameLabel is null
- **WHEN** (PowerShell) step 4 runs `Get-AzPublicIpAddress_List -ResourceGroupName <rg>`
- **THEN** output is PublicIPAddress items whose names are exactly <pip1> and <pip2>
- **WHEN** (PowerShell) step 5 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip1>`
- **THEN** output is one PublicIPAddress with Name <pip1> and Location <location>
- **AND** DnsSettingsDomainNameLabel is <dns label>
- **WHEN** (PowerShell) step 6 runs `Remove-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip1>`
- **THEN** request is DELETE on .../resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses/<pip1> and the step returns no error
- **WHEN** (PowerShell) step 7 runs `Get-AzPublicIpAddress_List -ResourceGroupName <rg>`
- **THEN** output contains <pip2> and no PublicIPAddress named <pip1>

#### Scenario: T8 List public IP addresses in the subscription (synthesized)
- **WHEN** (CLI) step 3 runs `network public-ip list`
- **THEN** request is GET /subscriptions/<sub>/providers/Microsoft.Network/publicIPAddresses (no resourceGroups segment)
- **AND** output contains a PublicIPAddress named <pip>
- **WHEN** (PowerShell) step 3 runs `Get-AzPublicIpAddress_List`
- **THEN** request is GET /subscriptions/<sub>/providers/Microsoft.Network/publicIPAddresses (no resourceGroups segment)
- **AND** output contains a PublicIPAddress named <pip>

### Requirement: SKU default on create
The scenario tests SHALL verify the behavior requirement "SKU default on create".

#### Scenario: T2 SKU defaults, tags, Global tier, and StandardV2
- **WHEN** (CLI) step 2 runs `network public-ip create -g <rg> -n <pip1> -l <location>`
- **THEN** request body has sku.name Standard, properties.publicIPAllocationMethod Static, properties.publicIPAddressVersion IPv4, properties.idleTimeoutInMinutes 4
- **AND** output SkuName is Standard and SkuTier is Regional
- **AND** PublicIPAllocationMethod is Static
- **AND** DnsSettingsDomainNameLabel is null
- **WHEN** (CLI) step 3 runs `network public-ip show -g <rg> -n <pip1>`
- **THEN** SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static, PublicIPAddressVersion is IPv4
- **WHEN** (CLI) step 4 runs `network public-ip create -g <rg> -n <pip2> -l <location> --sku Standard --tags foo=doo`
- **THEN** request body tags is exactly {foo: doo}
- **WHEN** (CLI) step 5 runs `network public-ip show -g <rg> -n <pip2>`
- **THEN** SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static
- **AND** Tags has foo = doo
- **WHEN** (CLI) step 6 runs `network public-ip create -g <rg> -n <pip3> -l <location> --sku Standard --tier Global`
- **THEN** request body has sku.tier Global
- **WHEN** (CLI) step 7 runs `network public-ip show -g <rg> -n <pip3>`
- **THEN** SkuName is Standard, SkuTier is Global, PublicIPAllocationMethod is Static
- **WHEN** (CLI) step 8 runs `network public-ip create -g <rg> -n <pip4> -l <location> --sku StandardV2 --allocation-method Static`
- **THEN** output SkuName is StandardV2
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip1> -Location <location>`
- **THEN** request body has sku.name Standard, properties.publicIPAllocationMethod Static, properties.publicIPAddressVersion IPv4, properties.idleTimeoutInMinutes 4
- **AND** output SkuName is Standard and SkuTier is Regional
- **AND** PublicIPAllocationMethod is Static
- **AND** DnsSettingsDomainNameLabel is null
- **WHEN** (PowerShell) step 3 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip1>`
- **THEN** SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static, PublicIPAddressVersion is IPv4
- **WHEN** (PowerShell) step 4 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip2> -Location <location> -Sku Standard -Tag @{ foo = 'doo' }`
- **THEN** request body tags is exactly {foo: doo}
- **WHEN** (PowerShell) step 5 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip2>`
- **THEN** SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static
- **AND** Tags has foo = doo
- **WHEN** (PowerShell) step 6 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip3> -Location <location> -Sku Standard -Tier Global`
- **THEN** request body has sku.tier Global
- **WHEN** (PowerShell) step 7 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip3>`
- **THEN** SkuName is Standard, SkuTier is Global, PublicIPAllocationMethod is Static
- **WHEN** (PowerShell) step 8 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip4> -Location <location> -Sku StandardV2 -AllocationMethod Static`
- **THEN** output SkuName is StandardV2

### Requirement: IP version default on create
The scenario tests SHALL verify the behavior requirement "IP version default on create".

#### Scenario: T3 IPv6 public IP address
- **WHEN** (CLI) step 2 runs `network public-ip create -g <rg> -n <pip> -l <location> --allocation-method Static --version IPv6`
- **THEN** request body has properties.publicIPAddressVersion IPv6
- **AND** output PublicIPAddressVersion is IPv6 and PublicIPAllocationMethod is Static
- **WHEN** (CLI) step 3 runs `network public-ip show -g <rg> -n <pip>`
- **THEN** PublicIPAddressVersion is IPv6 and ProvisioningState is Succeeded
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip> -Location <location> -AllocationMethod Static -IpAddressVersion IPv6`
- **THEN** request body has properties.publicIPAddressVersion IPv6
- **AND** output PublicIPAddressVersion is IPv6 and PublicIPAllocationMethod is Static
- **WHEN** (PowerShell) step 3 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip>`
- **THEN** PublicIPAddressVersion is IPv6 and ProvisioningState is Succeeded

### Requirement: Idle timeout default on create
The scenario tests SHALL verify the behavior requirement "Idle timeout default on create".

#### Scenario: T4 Explicit idle timeout
- **WHEN** (CLI) step 2 runs `network public-ip create -g <rg> -n <pip> -l <location> --idle-timeout 15 --allocation-method Static --dns-name <dns label>`
- **THEN** request body has properties.idleTimeoutInMinutes 15
- **WHEN** (CLI) step 3 runs `network public-ip show -g <rg> -n <pip>`
- **THEN** IdleTimeoutInMinutes is 15
- **AND** PublicIPAllocationMethod is Static and DnsSettingsDomainNameLabel is <dns label>
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip> -Location <location> -IdleTimeoutInMinutes 15 -AllocationMethod Static -DomainNameLabel <dns label>`
- **THEN** request body has properties.idleTimeoutInMinutes 15
- **WHEN** (PowerShell) step 3 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip>`
- **THEN** IdleTimeoutInMinutes is 15
- **AND** PublicIPAllocationMethod is Static and DnsSettingsDomainNameLabel is <dns label>

### Requirement: DDoS protection mode and plan
The scenario tests SHALL verify the behavior requirement "DDoS protection mode and plan".

#### Scenario: T5 DDoS protection mode on create
- **WHEN** (CLI) step 2 runs `network public-ip create -g <rg> -n <pip> -l <location> --allocation-method Static --sku Standard --protection-mode Enabled`
- **THEN** request body has properties.ddosSettings.protectionMode Enabled and no ddosSettings.ddosProtectionPlan
- **WHEN** (CLI) step 3 runs `network public-ip show -g <rg> -n <pip>`
- **THEN** DdosSettingsProtectionMode is Enabled
- **AND** SkuName is Standard and PublicIPAllocationMethod is Static
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip> -Location <location> -AllocationMethod Static -Sku Standard -DdosProtectionMode Enabled`
- **THEN** request body has properties.ddosSettings.protectionMode Enabled and no ddosSettings.ddosProtectionPlan
- **WHEN** (PowerShell) step 3 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip>`
- **THEN** DdosSettingsProtectionMode is Enabled
- **AND** SkuName is Standard and PublicIPAllocationMethod is Static

### Requirement: Allocation method default from the SKU
The scenario tests SHALL verify the behavior requirement "Allocation method default from the SKU".

#### Scenario: T6 Basic SKU with Dynamic allocation
- **WHEN** (CLI) step 2 runs `network public-ip create -g <rg> -n <pip> -l <location> --allocation-method Dynamic --sku Basic --dns-name <dns label>`
- **THEN** request body has sku.name Basic and properties.publicIPAllocationMethod Dynamic
- **AND** output SkuName is Basic and PublicIPAllocationMethod is Dynamic (verify at record: the service may reject Basic SKU creation; see review_gaps)
- **WHEN** (CLI) step 3 runs `network public-ip show -g <rg> -n <pip>`
- **THEN** SkuName is Basic, PublicIPAllocationMethod is Dynamic, DnsSettingsDomainNameLabel is <dns label>
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip> -Location <location> -AllocationMethod Dynamic -Sku Basic -DomainNameLabel <dns label>`
- **THEN** request body has sku.name Basic and properties.publicIPAllocationMethod Dynamic
- **AND** output SkuName is Basic and PublicIPAllocationMethod is Dynamic (verify at record: the service may reject Basic SKU creation; see review_gaps)
- **WHEN** (PowerShell) step 3 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip>`
- **THEN** SkuName is Basic, PublicIPAllocationMethod is Dynamic, DnsSettingsDomainNameLabel is <dns label>

### Requirement: Edge zone as extended location
The scenario tests SHALL verify the behavior requirement "Edge zone as extended location".

#### Scenario: T7 Public IP in an edge zone
- **WHEN** (CLI) step 2 runs `network public-ip create -g <rg> -n <pip> -l <location> --edge-zone <edge zone> --sku Standard`
- **THEN** request body has extendedLocation.name <edge zone> and extendedLocation.type EdgeZone
- **WHEN** (CLI) step 3 runs `network public-ip show -g <rg> -n <pip>`
- **THEN** ExtendedLocationName is <edge zone> and ExtendedLocationType is EdgeZone
- **WHEN** (PowerShell) step 2 runs `New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip> -Location <location> -EdgeZone <edge zone> -Sku Standard`
- **THEN** request body has extendedLocation.name <edge zone> and extendedLocation.type EdgeZone
- **WHEN** (PowerShell) step 3 runs `Get-AzPublicIpAddress_Get -ResourceGroupName <rg> -Name <pip>`
- **THEN** ExtendedLocationName is <edge zone> and ExtendedLocationType is EdgeZone
