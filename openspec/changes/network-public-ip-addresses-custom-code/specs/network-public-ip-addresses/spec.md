# Spec Delta

## Purpose

Defines how the Network extension's public IP address commands (create, show, list, delete) shape their requests and route between REST operations on the CLI and PowerShell surfaces.

## ADDED Requirements

### Requirement: SKU default on create
Public IP address create SHALL send `sku.name = Standard` in the PublicIPAddresses_CreateOrUpdate body when no SKU is given, and SHALL send a given SKU name unchanged.

#### Scenario: F1 No SKU given
- **WHEN** public IP address create runs without a SKU
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `sku.name = "Standard"`

#### Scenario: F1 SKU given
- **WHEN** public IP address create runs with SKU `<sku>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `sku.name = "<sku>"` unchanged

### Requirement: Allocation method default from the SKU
Public IP address create SHALL, when no allocation method is given, send `publicIPAllocationMethod = Dynamic` when the effective SKU name is `Basic` (case-insensitive) and `publicIPAllocationMethod = Static` for any other effective SKU name, and SHALL send a given allocation method unchanged.

#### Scenario: F2 Basic SKU without allocation method
- **WHEN** public IP address create runs with SKU `Basic` (any case) and no allocation method
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `publicIPAllocationMethod = "Dynamic"`

#### Scenario: F2 Other SKU without allocation method
- **WHEN** public IP address create runs without an allocation method and the effective SKU name is not `Basic`, such as the default `Standard` or `StandardV2`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `publicIPAllocationMethod = "Static"`

#### Scenario: F2 Allocation method given
- **WHEN** public IP address create runs with allocation method `<method>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `publicIPAllocationMethod = "<method>"` unchanged

### Requirement: IP version default on create
Public IP address create SHALL send `publicIPAddressVersion = IPv4` when no IP address version is given, and SHALL send a given version unchanged.

#### Scenario: F3 No version given
- **WHEN** public IP address create runs without an IP address version
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `publicIPAddressVersion = "IPv4"`

#### Scenario: F3 Version given
- **WHEN** public IP address create runs with IP address version `<version>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `publicIPAddressVersion = "<version>"` unchanged

### Requirement: Idle timeout default on create
Public IP address create SHALL send `idleTimeoutInMinutes = 4` when no idle timeout is given or the given value is 0 or less, and SHALL send a positive idle timeout unchanged.

#### Scenario: F4 No idle timeout given
- **WHEN** public IP address create runs without an idle timeout
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `idleTimeoutInMinutes = 4`

#### Scenario: F4 Idle timeout of 0 or less
- **WHEN** public IP address create runs with an idle timeout of 0 or less
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `idleTimeoutInMinutes = 4`

#### Scenario: F4 Positive idle timeout
- **WHEN** public IP address create runs with idle timeout `<minutes>` greater than 0
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `idleTimeoutInMinutes = <minutes>` unchanged

### Requirement: Edge zone as extended location
Public IP address create SHALL, when an edge zone is given, send `extendedLocation = {name: <edge zone>, type: EdgeZone}`, and SHALL send no `extendedLocation` when no edge zone is given.

#### Scenario: F7 Edge zone given
- **WHEN** public IP address create runs with edge zone `<edge zone>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `extendedLocation = {"name": "<edge zone>", "type": "EdgeZone"}`

#### Scenario: F7 No edge zone
- **WHEN** public IP address create runs without an edge zone
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends no `extendedLocation`

### Requirement: DDoS protection mode and plan
Public IP address create SHALL send a given DDoS protection mode as `ddosSettings.protectionMode`. It SHALL send a DDoS protection plan value that starts with `/` as `ddosSettings.ddosProtectionPlan.id` unchanged, and SHALL expand a DDoS protection plan name into `/subscriptions/<subscription>/resourceGroups/<resource group>/providers/Microsoft.Network/ddosProtectionPlans/<name>` using the command's subscription and resource group, whether or not a protection mode is given.

#### Scenario: F8 Protection mode given
- **WHEN** public IP address create runs with DDoS protection mode `<mode>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `ddosSettings.protectionMode = "<mode>"`

#### Scenario: F8 Protection plan ID without protection mode
- **WHEN** public IP address create runs without a DDoS protection mode and with DDoS protection plan `/subscriptions/<sub>/resourceGroups/<other rg>/providers/Microsoft.Network/ddosProtectionPlans/<plan>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `ddosSettings.ddosProtectionPlan.id` equal to that value unchanged

#### Scenario: F8 Protection plan name
- **WHEN** public IP address create runs in subscription `<sub>` and resource group `<rg>` with DDoS protection plan `<plan>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `ddosSettings.ddosProtectionPlan.id = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/ddosProtectionPlans/<plan>"`

### Requirement: DDoS custom policy name or ID
Public IP address create SHALL send a DDoS custom policy value that starts with `/` as `ddosSettings.ddosCustomPolicy.id` unchanged, and SHALL expand a DDoS custom policy name into `/subscriptions/<subscription>/resourceGroups/<resource group>/providers/Microsoft.Network/ddosCustomPolicies/<name>` using the command's subscription and resource group.

#### Scenario: F9 Custom policy ID
- **WHEN** public IP address create runs with DDoS custom policy `/subscriptions/<sub>/resourceGroups/<other rg>/providers/Microsoft.Network/ddosCustomPolicies/<policy>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `ddosSettings.ddosCustomPolicy.id` equal to that value unchanged

#### Scenario: F9 Custom policy name
- **WHEN** public IP address create runs in subscription `<sub>` and resource group `<rg>` with DDoS custom policy `<policy>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `ddosSettings.ddosCustomPolicy.id = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/ddosCustomPolicies/<policy>"`

### Requirement: Public IP prefix name or ID
Public IP address create SHALL send a public IP prefix value that starts with `/` as `publicIPPrefix.id` unchanged, and SHALL expand a public IP prefix name into `/subscriptions/<subscription>/resourceGroups/<resource group>/providers/Microsoft.Network/publicIPPrefixes/<name>` using the command's subscription and resource group. It SHALL send no PublicIPPrefixes request.

#### Scenario: F10 Prefix ID
- **WHEN** public IP address create runs with public IP prefix `/subscriptions/<sub>/resourceGroups/<other rg>/providers/Microsoft.Network/publicIPPrefixes/<prefix>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `publicIPPrefix.id` equal to that value unchanged
- **AND** Azclips sends no PublicIPPrefixes request

#### Scenario: F10 Prefix name
- **WHEN** public IP address create runs in subscription `<sub>` and resource group `<rg>` with public IP prefix `<prefix>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `publicIPPrefix.id = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/publicIPPrefixes/<prefix>"`
- **AND** Azclips sends no PublicIPPrefixes request

### Requirement: IP tags on create
Public IP address create SHALL send IP tags given as `TYPE=VALUE` pairs on the CLI or as tag entries on PowerShell as `ipTags = [{ipTagType: <TYPE>, tag: <VALUE>}, ...]` in the given order, and SHALL send no `ipTags` when no IP tags are given.

#### Scenario: F12 CLI IP tags
- **WHEN** public IP address create runs on the CLI with IP tags `<type1>=<value1> <type2>=<value2>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `ipTags = [{"ipTagType": "<type1>", "tag": "<value1>"}, {"ipTagType": "<type2>", "tag": "<value2>"}]`

#### Scenario: F12 PowerShell IP tags
- **WHEN** public IP address create runs on PowerShell with IP tag entry `<type>` = `<value>`
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends `ipTags = [{"ipTagType": "<type>", "tag": "<value>"}]`

#### Scenario: F12 No IP tags
- **WHEN** public IP address create runs without IP tags
- **THEN** the PublicIPAddresses_CreateOrUpdate body sends no `ipTags`

### Requirement: List scope by resource group presence
Public IP address list SHALL call PublicIPAddresses_List for the resource group when a resource group is given, and PublicIPAddresses_ListAll for the subscription when it is not, and SHALL output the items of every page.

#### Scenario: F25 Resource group given
- **WHEN** public IP address list runs on the CLI or PowerShell surface with resource group `<rg>`
- **THEN** Azclips sends `GET /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses` (PublicIPAddresses_List)
- **AND** outputs the items of every page

#### Scenario: F25 No resource group
- **WHEN** public IP address list runs on the CLI or PowerShell surface without a resource group
- **THEN** Azclips sends `GET /subscriptions/<sub>/providers/Microsoft.Network/publicIPAddresses` (PublicIPAddresses_ListAll)
- **AND** outputs the items of every page

### Requirement: PowerShell remove confirmation and PassThru
On the PowerShell surface, `Remove-AzPublicIpAddress` SHALL ask for confirmation before PublicIPAddresses_Delete unless `-Force` is given, SHALL send no DELETE when the confirmation is declined, and SHALL output `true` after a completed delete only when `-PassThru` is given.

#### Scenario: F28 Confirmation declined
- **WHEN** `Remove-AzPublicIpAddress` runs without `-Force` and the user declines the confirmation
- **THEN** Azclips sends no PublicIPAddresses_Delete request

#### Scenario: F28 PassThru after delete
- **WHEN** the PublicIPAddresses_Delete request completes and `-PassThru` is given
- **THEN** Azclips outputs `true`

#### Scenario: F28 No PassThru
- **WHEN** the PublicIPAddresses_Delete request completes and `-PassThru` is not given
- **THEN** Azclips outputs nothing
