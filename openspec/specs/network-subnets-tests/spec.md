# network-subnets-tests Specification

## Purpose
Record/playback scenario tests that verify the `network-subnets` behavior requirements through the Azclips subnet commands on the CLI and PowerShell surfaces.

## Requirements
### Requirement: Create sets the subnet name in the body
The scenario tests SHALL verify the behavior requirement "Create sets the subnet name in the body".

#### Scenario: T1 Subnet lifecycle: create, list, show, update prefix, show missing, delete
- **WHEN** (CLI) step 3 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --address-prefixes 20.0.0.0/24 --default-outbound false`
- **THEN** the step sends `PUT .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`
- **THEN** the PUT body has `name` `<subnet>`, `properties.addressPrefix` `20.0.0.0/24`, and no `properties.addressPrefixes`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Enabled`
- **THEN** the PUT body has no `networkSecurityGroup`, `routeTable`, `natGateway`, `serviceEndpoints` or `delegations` under `properties`
- **THEN** one `Subnet` output with name `<subnet>`, addressPrefix `20.0.0.0/24`, addressPrefixes null, provisioningState `Succeeded`, defaultOutboundAccess false, privateEndpointNetworkPolicies `Disabled`, privateLinkServiceNetworkPolicies `Enabled`
- **WHEN** (CLI) step 4 runs `network vnet subnet list --resource-group <resource-group> --vnet-name <vnet>`
- **THEN** one output, a `List<Subnet>` whose names are exactly `<default-subnet>` and `<subnet>`, in any order
- **WHEN** (CLI) step 5 runs `network vnet subnet show --resource-group <resource-group> --vnet-name <vnet> --name <subnet>`
- **THEN** one `Subnet` output with name `<subnet>` and addressPrefix `20.0.0.0/24`
- **WHEN** (CLI) step 6 runs `network vnet subnet update --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --address-prefixes 20.0.3.0/24`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.addressPrefix` `20.0.3.0/24` and no `properties.addressPrefixes`
- **THEN** one `Subnet` output with addressPrefix `20.0.3.0/24`, defaultOutboundAccess false, privateEndpointNetworkPolicies `Disabled`, privateLinkServiceNetworkPolicies `Enabled`
- **WHEN** (CLI) step 7 runs `network vnet subnet show --resource-group <resource-group> --vnet-name <vnet> --name <missing-subnet>`
- **THEN** fails with the service's NotFound error (HttpRequestException) and writes no output
- **WHEN** (CLI) step 8 runs `network vnet subnet delete --resource-group <resource-group> --vnet-name <vnet> --name <subnet>`
- **THEN** no output
- **WHEN** (CLI) step 9 runs `network vnet subnet list --resource-group <resource-group> --vnet-name <vnet>`
- **THEN** one output, a `List<Subnet>` that holds `<default-subnet>` and no subnet named `<subnet>`
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -AddressPrefixes 20.0.0.0/24 -DefaultOutboundAccess $false`
- **THEN** the step sends `PUT .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`
- **THEN** the PUT body has `name` `<subnet>`, `properties.addressPrefix` `20.0.0.0/24`, and no `properties.addressPrefixes`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Enabled`
- **THEN** the PUT body has no `networkSecurityGroup`, `routeTable`, `natGateway`, `serviceEndpoints` or `delegations` under `properties`
- **THEN** one `Subnet` output with name `<subnet>`, addressPrefix `20.0.0.0/24`, addressPrefixes null, provisioningState `Succeeded`, defaultOutboundAccess false, privateEndpointNetworkPolicies `Disabled`, privateLinkServiceNetworkPolicies `Enabled`
- **WHEN** (PowerShell) step 4 runs `Get-AzVirtualNetworkSubnet_List -ResourceGroupName <resource-group> -VirtualNetworkName <vnet>`
- **THEN** one output, a `List<Subnet>` whose names are exactly `<default-subnet>` and `<subnet>`, in any order
- **WHEN** (PowerShell) step 5 runs `Get-AzVirtualNetworkSubnet_Get -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet>`
- **THEN** one `Subnet` output with name `<subnet>` and addressPrefix `20.0.0.0/24`
- **WHEN** (PowerShell) step 6 runs `Set-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -AddressPrefixes 20.0.3.0/24`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.addressPrefix` `20.0.3.0/24` and no `properties.addressPrefixes`
- **THEN** one `Subnet` output with addressPrefix `20.0.3.0/24`, defaultOutboundAccess false, privateEndpointNetworkPolicies `Disabled`, privateLinkServiceNetworkPolicies `Enabled`
- **WHEN** (PowerShell) step 7 runs `Get-AzVirtualNetworkSubnet_Get -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <missing-subnet>`
- **THEN** fails with the service's NotFound error (HttpRequestException) and writes no output
- **WHEN** (PowerShell) step 8 runs `Remove-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet>`
- **THEN** no output
- **WHEN** (PowerShell) step 9 runs `Get-AzVirtualNetworkSubnet_List -ResourceGroupName <resource-group> -VirtualNetworkName <vnet>`
- **THEN** one output, a `List<Subnet>` that holds `<default-subnet>` and no subnet named `<subnet>`

### Requirement: Resource reference name-or-ID
The scenario tests SHALL verify the behavior requirement "Resource reference name-or-ID".

#### Scenario: T2 Update address prefix and attach or detach a network security group
- **WHEN** (CLI) step 4 runs `network vnet subnet update --resource-group <resource-group> --vnet-name <vnet> --name <default-subnet> --address-prefixes 123.0.5.0/24 --network-security-group <nsg>`
- **THEN** the step sends `GET .../virtualNetworks/<vnet>/subnets/<default-subnet>?api-version=2026-03-01`, then `PUT` to the same URI, and no request to `.../networkSecurityGroups/...`
- **THEN** the PUT body has `properties.networkSecurityGroup` equal to exactly `{ "id": <step 3: id> }`
- **THEN** the PUT body has `properties.addressPrefix` `123.0.5.0/24` and no `properties.addressPrefixes`
- **THEN** one `Subnet` output with addressPrefix `123.0.5.0/24` and networkSecurityGroupId equal to `<step 3: id>`
- **WHEN** (CLI) step 5 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <second-subnet> --address-prefixes 123.0.2.0/24 --nsg <step 3: id> --default-outbound-access false`
- **THEN** the PUT body has `properties.networkSecurityGroup` equal to exactly `{ "id": <step 3: id> }`, with the full ID unchanged
- **THEN** one `Subnet` output with networkSecurityGroupId equal to `<step 3: id>`
- **WHEN** (CLI) step 6 runs `network vnet subnet update --resource-group <resource-group> --vnet-name <vnet> --name <default-subnet> --address-prefixes 123.0.5.0/24 --network-security-group ""`
- **THEN** the PUT body has no `properties.networkSecurityGroup`
- **THEN** one `Subnet` output with networkSecurityGroupId null and addressPrefix `123.0.5.0/24`
- **WHEN** (CLI) step 7 runs `network vnet subnet update --resource-group <resource-group> --vnet-name <vnet> --name <second-subnet> --network-security-group ""`
- **THEN** the PUT body has no `properties.networkSecurityGroup`, and keeps `properties.addressPrefix` `123.0.2.0/24` and `properties.defaultOutboundAccess` false as the GET returned them
- **THEN** one `Subnet` output with networkSecurityGroupId null, addressPrefix `123.0.2.0/24` and defaultOutboundAccess false
- **WHEN** (PowerShell) step 4 runs `Set-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <default-subnet> -AddressPrefixes 123.0.5.0/24 -NetworkSecurityGroup <nsg>`
- **THEN** the step sends `GET .../virtualNetworks/<vnet>/subnets/<default-subnet>?api-version=2026-03-01`, then `PUT` to the same URI, and no request to `.../networkSecurityGroups/...`
- **THEN** the PUT body has `properties.networkSecurityGroup` equal to exactly `{ "id": <step 3: id> }`
- **THEN** the PUT body has `properties.addressPrefix` `123.0.5.0/24` and no `properties.addressPrefixes`
- **THEN** one `Subnet` output with addressPrefix `123.0.5.0/24` and networkSecurityGroupId equal to `<step 3: id>`
- **WHEN** (PowerShell) step 5 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <second-subnet> -AddressPrefixes 123.0.2.0/24 -NetworkSecurityGroup <step 3: id> -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.networkSecurityGroup` equal to exactly `{ "id": <step 3: id> }`, with the full ID unchanged
- **THEN** one `Subnet` output with networkSecurityGroupId equal to `<step 3: id>`
- **WHEN** (PowerShell) step 6 runs `Set-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <default-subnet> -AddressPrefixes 123.0.5.0/24 -NetworkSecurityGroup ""`
- **THEN** the PUT body has no `properties.networkSecurityGroup`
- **THEN** one `Subnet` output with networkSecurityGroupId null and addressPrefix `123.0.5.0/24`
- **WHEN** (PowerShell) step 7 runs `Set-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <second-subnet> -NetworkSecurityGroup ""`
- **THEN** the PUT body has no `properties.networkSecurityGroup`, and keeps `properties.addressPrefix` `123.0.2.0/24` and `properties.defaultOutboundAccess` false as the GET returned them
- **THEN** one `Subnet` output with networkSecurityGroupId null, addressPrefix `123.0.2.0/24` and defaultOutboundAccess false

### Requirement: Delegations from service names
The scenario tests SHALL verify the behavior requirement "Delegations from service names".

#### Scenario: T3 Delegations set at create and replaced on update
- **WHEN** (CLI) step 3 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --address-prefixes 10.0.0.0/24 --delegations Microsoft.Web/serverFarms --default-outbound false`
- **THEN** the PUT body has `properties.delegations` with exactly one entry, whose `name` is `"0"` and whose `properties.serviceName` is `Microsoft.Web/serverFarms`
- **THEN** one `Subnet` output whose delegations hold exactly one entry, with serviceName `Microsoft.Web/serverFarms`
- **WHEN** (CLI) step 4 runs `network vnet subnet update --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --delegations Microsoft.Sql/managedInstances`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.delegations` with exactly one entry, whose `name` is `"0"` and whose `properties.serviceName` is `Microsoft.Sql/managedInstances`. No `Microsoft.Web/serverFarms` entry from the GET remains.
- **THEN** one `Subnet` output whose delegations hold exactly one entry, with serviceName `Microsoft.Sql/managedInstances`, and with addressPrefix `10.0.0.0/24`
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -AddressPrefixes 10.0.0.0/24 -Delegations Microsoft.Web/serverFarms -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.delegations` with exactly one entry, whose `name` is `"0"` and whose `properties.serviceName` is `Microsoft.Web/serverFarms`
- **THEN** one `Subnet` output whose delegations hold exactly one entry, with serviceName `Microsoft.Web/serverFarms`
- **WHEN** (PowerShell) step 4 runs `Set-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -Delegations Microsoft.Sql/managedInstances`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.delegations` with exactly one entry, whose `name` is `"0"` and whose `properties.serviceName` is `Microsoft.Sql/managedInstances`. No `Microsoft.Web/serverFarms` entry from the GET remains.
- **THEN** one `Subnet` output whose delegations hold exactly one entry, with serviceName `Microsoft.Sql/managedInstances`, and with addressPrefix `10.0.0.0/24`

### Requirement: Service endpoints from service names
The scenario tests SHALL verify the behavior requirement "Service endpoints from service names".

#### Scenario: T4 Service endpoints set at create
- **WHEN** (CLI) step 3 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --address-prefixes 10.0.1.0/24 --service-endpoints Microsoft.Storage --default-outbound false`
- **THEN** the PUT body has `properties.serviceEndpoints` equal to exactly `[{ "service": "Microsoft.Storage" }]`
- **THEN** one `Subnet` output whose serviceEndpoints hold exactly one entry, with service `Microsoft.Storage`
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -AddressPrefixes 10.0.1.0/24 -ServiceEndpoints Microsoft.Storage -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.serviceEndpoints` equal to exactly `[{ "service": "Microsoft.Storage" }]`
- **THEN** one `Subnet` output whose serviceEndpoints hold exactly one entry, with service `Microsoft.Storage`

### Requirement: Create-time network policy defaults
The scenario tests SHALL verify the behavior requirement "Create-time network policy defaults".

#### Scenario: T5 Network policies at create: given values versus defaults
- **WHEN** (CLI) step 3 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet-1> --address-prefixes 10.0.1.0/24 --private-endpoint-network-policies Disabled --default-outbound false`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Enabled`
- **THEN** one `Subnet` output with addressPrefix `10.0.1.0/24`, privateEndpointNetworkPolicies `Disabled` and privateLinkServiceNetworkPolicies `Enabled`
- **WHEN** (CLI) step 4 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet-2> --address-prefixes 10.0.2.0/24 --private-link-service-network-policies Disabled --default-outbound false`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Disabled`
- **THEN** one `Subnet` output with addressPrefix `10.0.2.0/24`, privateEndpointNetworkPolicies `Disabled` and privateLinkServiceNetworkPolicies `Disabled`
- **WHEN** (CLI) step 5 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet-3> --address-prefixes 10.0.3.0/24 --private-endpoint-network-policies Disabled --private-link-service-network-policies Disabled --default-outbound false`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Disabled`
- **THEN** one `Subnet` output with addressPrefix `10.0.3.0/24`, privateEndpointNetworkPolicies `Disabled` and privateLinkServiceNetworkPolicies `Disabled`
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet-1> -AddressPrefixes 10.0.1.0/24 -PrivateEndpointNetworkPolicies Disabled -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Enabled`
- **THEN** one `Subnet` output with addressPrefix `10.0.1.0/24`, privateEndpointNetworkPolicies `Disabled` and privateLinkServiceNetworkPolicies `Enabled`
- **WHEN** (PowerShell) step 4 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet-2> -AddressPrefixes 10.0.2.0/24 -PrivateLinkServiceNetworkPolicies Disabled -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Disabled`
- **THEN** one `Subnet` output with addressPrefix `10.0.2.0/24`, privateEndpointNetworkPolicies `Disabled` and privateLinkServiceNetworkPolicies `Disabled`
- **WHEN** (PowerShell) step 5 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet-3> -AddressPrefixes 10.0.3.0/24 -PrivateEndpointNetworkPolicies Disabled -PrivateLinkServiceNetworkPolicies Disabled -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.privateEndpointNetworkPolicies` `Disabled` and `properties.privateLinkServiceNetworkPolicies` `Disabled`
- **THEN** one `Subnet` output with addressPrefix `10.0.3.0/24`, privateEndpointNetworkPolicies `Disabled` and privateLinkServiceNetworkPolicies `Disabled`

### Requirement: Update as GET → apply given inputs → PUT
The scenario tests SHALL verify the behavior requirement "Update as GET → apply given inputs → PUT".

#### Scenario: T6 Default outbound access set at create and changed on update
- **WHEN** (CLI) step 3 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --address-prefixes 10.0.3.0/24 --default-outbound-access false`
- **THEN** the PUT body has `properties.defaultOutboundAccess` false
- **THEN** one `Subnet` output with defaultOutboundAccess false
- **WHEN** (CLI) step 4 runs `network vnet subnet update --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --default-outbound-access true`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.defaultOutboundAccess` true, and keeps `properties.addressPrefix` `10.0.3.0/24`, `properties.privateEndpointNetworkPolicies` and `properties.privateLinkServiceNetworkPolicies` as the GET returned them
- **THEN** one `Subnet` output with defaultOutboundAccess true and addressPrefix `10.0.3.0/24`
- **WHEN** (CLI) step 5 runs `network vnet subnet show --resource-group <resource-group> --vnet-name <vnet> --name <subnet>`
- **THEN** one `Subnet` output with defaultOutboundAccess true
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -AddressPrefixes 10.0.3.0/24 -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.defaultOutboundAccess` false
- **THEN** one `Subnet` output with defaultOutboundAccess false
- **WHEN** (PowerShell) step 4 runs `Set-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -DefaultOutboundAccess $true`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.defaultOutboundAccess` true, and keeps `properties.addressPrefix` `10.0.3.0/24`, `properties.privateEndpointNetworkPolicies` and `properties.privateLinkServiceNetworkPolicies` as the GET returned them
- **THEN** one `Subnet` output with defaultOutboundAccess true and addressPrefix `10.0.3.0/24`
- **WHEN** (PowerShell) step 5 runs `Get-AzVirtualNetworkSubnet_Get -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet>`
- **THEN** one `Subnet` output with defaultOutboundAccess true

### Requirement: Address prefix singular/plural mapping
The scenario tests SHALL verify the behavior requirement "Address prefix singular/plural mapping".

#### Scenario: T7 Subnet with several address prefixes
- **WHEN** (CLI) step 3 runs `network vnet subnet create --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --address-prefixes 10.0.3.0/28 10.0.4.0/28 --default-outbound false`
- **THEN** the PUT body has `properties.addressPrefixes` equal to `["10.0.3.0/28", "10.0.4.0/28"]` in input order, and no `properties.addressPrefix`
- **THEN** one `Subnet` output whose addressPrefixes are exactly `10.0.3.0/28` and `10.0.4.0/28`, with addressPrefix null
- **WHEN** (CLI) step 4 runs `network vnet subnet update --resource-group <resource-group> --vnet-name <vnet> --name <subnet> --address-prefixes 10.0.5.0/28 10.0.6.0/28`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.addressPrefixes` equal to `["10.0.5.0/28", "10.0.6.0/28"]` in input order, and no `properties.addressPrefix`
- **THEN** one `Subnet` output whose addressPrefixes are exactly `10.0.5.0/28` and `10.0.6.0/28`
- **WHEN** (CLI) step 5 runs `network vnet subnet list --resource-group <resource-group> --vnet-name <vnet>`
- **THEN** one output, a `List<Subnet>` whose names are exactly `<vnet-subnet>` and `<subnet>`, in any order
- **WHEN** (CLI) step 6 runs `network vnet subnet show --resource-group <resource-group> --vnet-name <vnet> --name <subnet>`
- **THEN** one `Subnet` output with name `<subnet>` and addressPrefixes exactly `10.0.5.0/28` and `10.0.6.0/28`
- **WHEN** (CLI) step 7 runs `network vnet subnet delete --resource-group <resource-group> --vnet-name <vnet> --name <subnet>`
- **THEN** no output
- **WHEN** (CLI) step 8 runs `network vnet subnet list --resource-group <resource-group> --vnet-name <vnet>`
- **THEN** one output, a `List<Subnet>` whose only subnet is `<vnet-subnet>`
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -AddressPrefixes 10.0.3.0/28,10.0.4.0/28 -DefaultOutboundAccess $false`
- **THEN** the PUT body has `properties.addressPrefixes` equal to `["10.0.3.0/28", "10.0.4.0/28"]` in input order, and no `properties.addressPrefix`
- **THEN** one `Subnet` output whose addressPrefixes are exactly `10.0.3.0/28` and `10.0.4.0/28`, with addressPrefix null
- **WHEN** (PowerShell) step 4 runs `Set-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet> -AddressPrefixes 10.0.5.0/28,10.0.6.0/28`
- **THEN** the step first sends `GET .../virtualNetworks/<vnet>/subnets/<subnet>?api-version=2026-03-01`, then `PUT` to the same URI
- **THEN** the PUT body has `properties.addressPrefixes` equal to `["10.0.5.0/28", "10.0.6.0/28"]` in input order, and no `properties.addressPrefix`
- **THEN** one `Subnet` output whose addressPrefixes are exactly `10.0.5.0/28` and `10.0.6.0/28`
- **WHEN** (PowerShell) step 5 runs `Get-AzVirtualNetworkSubnet_List -ResourceGroupName <resource-group> -VirtualNetworkName <vnet>`
- **THEN** one output, a `List<Subnet>` whose names are exactly `<vnet-subnet>` and `<subnet>`, in any order
- **WHEN** (PowerShell) step 6 runs `Get-AzVirtualNetworkSubnet_Get -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet>`
- **THEN** one `Subnet` output with name `<subnet>` and addressPrefixes exactly `10.0.5.0/28` and `10.0.6.0/28`
- **WHEN** (PowerShell) step 7 runs `Remove-AzVirtualNetworkSubnet -ResourceGroupName <resource-group> -VirtualNetworkName <vnet> -Name <subnet>`
- **THEN** no output
- **WHEN** (PowerShell) step 8 runs `Get-AzVirtualNetworkSubnet_List -ResourceGroupName <resource-group> -VirtualNetworkName <vnet>`
- **THEN** one output, a `List<Subnet>` whose only subnet is `<vnet-subnet>`
