# Spec Delta

## Purpose

Record/playback scenario tests for the Network extension's subnet commands on the CLI surface: `network vnet subnet create`, `network vnet subnet show`, `network vnet subnet list`, and `network vnet subnet delete`. Each test replays its recording and verifies the behavior stated in the `network-subnets` spec.

## ADDED Requirements

### Requirement: Address prefix single or plural
The scenario tests SHALL verify the behavior requirement "Address prefix single or plural".

#### Scenario: T1 Subnet lifecycle with one and several address prefixes
- **WHEN** (CLI) step 3 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet1> --address-prefixes 20.0.0.0/24 --default-outbound false`
- **THEN** Name is `<subnet1>`
- **AND** AddressPrefix is `20.0.0.0/24`
- **AND** AddressPrefixes is null or empty
- **AND** DefaultOutboundAccess is false
- **AND** PrivateEndpointNetworkPolicies is `Disabled`
- **AND** PrivateLinkServiceNetworkPolicies is `Enabled`
- **WHEN** (CLI) step 4 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet2> --address-prefixes 10.0.1.0/28 10.0.2.0/28 --default-outbound false`
- **THEN** Name is `<subnet2>`
- **AND** AddressPrefixes is [`10.0.1.0/28`, `10.0.2.0/28`] in that order
- **AND** AddressPrefix is null
- **WHEN** (CLI) step 5 runs `network vnet subnet list -g <rg> --vnet-name <vnet>`
- **THEN** output is a list of Subnet items whose names include `<subnet1>` and `<subnet2>`
- **WHEN** (CLI) step 6 runs `network vnet subnet show -g <rg> --vnet-name <vnet> -n <subnet1>`
- **THEN** Name is `<subnet1>`
- **AND** AddressPrefix is `20.0.0.0/24`
- **WHEN** (CLI) step 7 runs `network vnet subnet show -g <rg> --vnet-name <vnet> -n <subnet2>`
- **THEN** AddressPrefixes is [`10.0.1.0/28`, `10.0.2.0/28`]
- **WHEN** (CLI) step 10 runs `network vnet subnet list -g <rg> --vnet-name <vnet>`
- **THEN** no item is named `<subnet1>` or `<subnet2>`

### Requirement: Referenced resource name or ID
The scenario tests SHALL verify the behavior requirement "Referenced resource name or ID".

#### Scenario: T2 Subnet with a network security group by name and by ID
- **WHEN** (CLI) step 4 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet1> --address-prefixes 20.0.0.0/24 --network-security-group <nsg> --default-outbound false`
- **THEN** NetworkSecurityGroupId ends with `/networkSecurityGroups/<nsg>` (case-insensitive)
- **WHEN** (CLI) step 5 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet2> --address-prefixes 20.0.1.0/24 --network-security-group <step 2: id> --default-outbound false`
- **THEN** NetworkSecurityGroupId ends with `/networkSecurityGroups/<nsg>` (case-insensitive)
- **WHEN** (CLI) step 6 runs `network vnet subnet list -g <rg> --vnet-name <vnet>`
- **THEN** output is a list of Subnet items whose names include `<subnet1>` and `<subnet2>`
- **WHEN** (CLI) step 7 runs `network vnet subnet show -g <rg> --vnet-name <vnet> -n <subnet1>`
- **THEN** Name is `<subnet1>`
- **AND** NetworkSecurityGroupId ends with `/networkSecurityGroups/<nsg>` (case-insensitive)

### Requirement: Service endpoint names
The scenario tests SHALL verify the behavior requirement "Service endpoint names".

#### Scenario: T3 Subnet with a service endpoint
- **WHEN** (CLI) step 3 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet1> --address-prefixes 10.0.1.0/24 --service-endpoints Microsoft.Storage --default-outbound false`
- **THEN** ServiceEndpoints has one item whose Service is `Microsoft.Storage`

### Requirement: Delegation values
The scenario tests SHALL verify the behavior requirement "Delegation values".

#### Scenario: T4 Subnet delegation in dotted and slash form
- **WHEN** (CLI) step 3 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet1> --address-prefixes 10.0.0.0/24 --delegations Microsoft.Web.serverFarms --default-outbound false`
- **THEN** Delegations has one item whose ServiceName is `Microsoft.Web/serverFarms`
- **WHEN** (CLI) step 4 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet2> --address-prefixes 10.0.1.0/24 --delegations Microsoft.Sql/managedInstances --default-outbound false`
- **THEN** Delegations has one item whose ServiceName is `Microsoft.Sql/managedInstances`

### Requirement: Deprecated disable network policy flags
The scenario tests SHALL verify the behavior requirement "Deprecated disable network policy flags".

#### Scenario: T5 Deprecated disable network policy flags
- **WHEN** (CLI) step 3 runs `network vnet subnet create -g <rg> --vnet-name <vnet> --address-prefixes 10.0.1.0/24 --name <subnet1> --disable-private-endpoint-network-policies true --default-outbound false`
- **THEN** AddressPrefix is `10.0.1.0/24`
- **AND** PrivateEndpointNetworkPolicies is `Disabled`
- **AND** PrivateLinkServiceNetworkPolicies is `Enabled`
- **WHEN** (CLI) step 4 runs `network vnet subnet create -g <rg> --vnet-name <vnet> --address-prefixes 10.0.2.0/24 --name <subnet2> --disable-private-link-service-network-policies true --default-outbound false`
- **THEN** AddressPrefix is `10.0.2.0/24`
- **AND** PrivateEndpointNetworkPolicies is `Disabled`
- **AND** PrivateLinkServiceNetworkPolicies is `Disabled`
- **WHEN** (CLI) step 5 runs `network vnet subnet create -g <rg> --vnet-name <vnet> --address-prefixes 10.0.3.0/24 --name <subnet3> --disable-private-endpoint-network-policies true --disable-private-link-service-network-policies true --default-outbound false`
- **THEN** AddressPrefix is `10.0.3.0/24`
- **AND** PrivateEndpointNetworkPolicies is `Disabled`
- **AND** PrivateLinkServiceNetworkPolicies is `Disabled`

### Requirement: Subnet create
The scenario tests SHALL verify the `network vnet subnet create` command for subnet properties no other behavior requirement covers.

#### Scenario: T6 Subnet default outbound access true and false
- **WHEN** (CLI) step 3 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet1> --address-prefixes 10.0.3.0/24 --default-outbound-access false`
- **THEN** DefaultOutboundAccess is false
- **WHEN** (CLI) step 4 runs `network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet2> --address-prefixes 10.0.4.0/24 --default-outbound-access true`
- **THEN** DefaultOutboundAccess is true
- **WHEN** (CLI) step 5 runs `network vnet subnet show -g <rg> --vnet-name <vnet> -n <subnet2>`
- **THEN** DefaultOutboundAccess is true

#### Scenario: T7 Subnet sharing scope Tenant
- **WHEN** (CLI) step 4 runs `network vnet subnet show -g <rg> --vnet-name <vnet> -n <subnet1>`
- **THEN** SharingScope is `Tenant`
