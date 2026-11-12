## v0.1.7 - 2026-11-04 19:14 UTC

#### Agent
- Store Agent data under `~/.azclips/agent`; data under the local app data `.azclips/agent` directory from 0.1.6 is no longer used and can be deleted (#1242)
- `az config`: Hide internal builtin model settings `agent.builtin.modelId` and `agent.builtin.apiVersion` from customer configuration (#1187)
- [BREAKING CHANGE] `az troubleshoot recover`: Rename the `analyze` subcommand to `recover` (#1198)
- Read recovery help and environment context in process with on-demand catalog regeneration (#924)
- `az troubleshoot analyze`: Do not write spinner frames when stdout is redirected (#1181)

#### Auth
- [BREAKING CHANGE] Move profile and authentication cache files to `~/.azclips`; files in the previous local application data directory are not migrated automatically (#1238)

#### CLI
- Preserve processor lifecycle tags in runtime help command listings (#1222)
- Reject extra values for single-value parameters such as `--name` instead of silently dropping them (#1217)
- Keep the last value when a single-value parameter is repeated, matching classic Azure CLI (#1217)
- `az vm create`: Add `--storage-sku` to set the storage SKU of the managed OS disk (#1221)
- `az login`: Add service principal client-secret authentication and saved-profile re-authentication (#1210)
- `az vm create`: Add `--zone` as a single-value input mapped to a one-item zones array in VM create payload (#1200)
- Load the extension copy selected by routing (#1186)
- `az network vnet create`: Accept bare tag keys and stop sending default subnet network policies (#1162)

#### Compute
- `az vm create` / `New-AzVM`: Enable secure boot and VTPM functionality (#1090)

#### Network
- `Get-AzVirtualNetworkSubnetConfig`, `Remove-AzVirtualNetworkSubnetConfig`: Add subnet read and remove cmdlets (#1177)
- `az network vnet subnet create`: Accept bare and repeated `--ipam-allocations` keys and dotted delegation names (#1177)

#### PowerShell
- [BREAKING CHANGE] Network cmdlets expose resource properties directly instead of under `.Properties` (#1226)
- [BREAKING CHANGE] `New/Get-AzPublicIpAddress`: Rename the output type from `PublicIpAddress` to `PublicIPAddress` (#1226)
- [BREAKING CHANGE] `Get-AzPublicIpAddress`: Replace the nested `IpConfiguration` with `IpConfigurationId`, `PrivateIPAddress` and `SubnetId` (#1226)
- [BREAKING CHANGE] Remove `New-AzVirtualNetworkSubnetConfig`; pass hashtables to `New-AzVirtualNetwork -Subnet` (#1226)
- Remove stray control characters from help examples while preserving description order and blank-line spacing (#1266)
- `New-AzVM`: Add `-StorageSku` to set the storage SKU of the managed OS disk (#1221)
- `Connect-AzAccount`: Add service principal client-secret authentication with SecureString and PSCredential support (#1210)
- `Get-AzPublicIpAddress`: Display the assigned IP address and attached IP configuration instead of a blank Properties value (#1213)
- `New-AzVM`: Add `-Zone` as a single-value input while preserving zones array wire compatibility via orchestrator pass-through (#1200)
- `Remove-Az*` (Network, Storage): `-Force` no longer deletes when `$WhatIfPreference` is set (#1177)
- `New-AzVirtualNetwork`: Accept array values in subnet hashtables (#1162)

#### Resources
- Move and Rename Resources Deployment Management Scenario Test Files (#1179)

#### Telemetry
- Use injected persistence for telemetry traces and boundary cache (#1274)

#### Az.Accounts 0.1.4
* Added service principal client-secret sign-in with `-ClientSecret <SecureString>` and backward-compatible `-ServicePrincipal -Credential <PSCredential>` syntax.

#### Az.Network 0.1.3
* Removed `New-AzVirtualNetworkSubnetConfig`
    - Pass hashtables to `New-AzVirtualNetwork -Subnet` instead.
* Fixed `Get-AzPublicIpAddress` output exposing a blank `Properties` value instead of the assigned IP address.
    - Resource properties are available at the top level.
    - The attached IP configuration is flattened into `IpConfigurationId`, `PrivateIPAddress` and `SubnetId` (the last two with `-ExpandResource`); the nested `IpConfiguration` object is no longer exposed.
    - `ServicePublicIPAddress` and `LinkedPublicIPAddress` are no longer exposed.
* Updated `New-AzVirtualNetwork`, `Get-AzVirtualNetwork` and `Remove-AzVirtualNetwork`
    - `-Name` accepts the `ResourceName` alias.
    - `-Subnet` hashtables accept array values for multi-valued keys such as `AddressPrefixes`.
    - Subnets no longer send private endpoint / private link service network policies unless requested.
* Virtual network, subnet, network security group, network interface and public IP address outputs now use the TypeSpec-generated models
    - Resource properties are exposed directly on the output object (for example `$vnet.Subnets`, `$subnet.AddressPrefixes`) instead of under `.Properties`.
    - The public IP address output type is now `PublicIPAddress`.

## v0.1.6 - 2026-10-26 20:00 UTC

#### Agent
- `az troubleshoot analyze`: Add manual AI troubleshooting from saved failures (#934)
- [BREAKING CHANGE] Replace `agent.enabled` and `agent.sessionStyle` with `agent.recovery.trigger` and `agent.recovery.style` (#934)
- Store Agent data under the local app data `.azclips/agent` directory and remove entries older than 90 days (#1152)
- `az agent tool resource find`: Stop recording tool telemetry when `telemetry.enabled` is false (#1146)
- Choose the PPE model endpoint from the tenant data boundary (#1094)
- Send builtin model requests without encrypted reasoning so that Agent sessions no longer fail with invalid_encrypted_content (#1093)
- Add configurable reasoning effort with a medium default for AI error recovery (#919)
- Use gpt-5.4-mini by default for builtin AI error recovery while preserving configured model overrides (#918)

#### CLI
- Route commands using extension sidecars before loading the selected extension, with per-extension DLL discovery fallback (#1129)
- `az config set`: Include allowed values in invalid configuration value errors (#1119)
- `az deployment group show`: Add command to show a resource group deployment (#1000)
- `az deployment group list`: Add command to list resource group deployments, with `--filter` support (#1000)
- `az deployment group cancel`: Add command to cancel a running resource group deployment (#1000)
- `az deployment group delete`: Add command to delete a resource group deployment (#1000)
- `az deployment group validate`: Add resource group scoped deployment validation (#1000)
- `az deployment group export`: Add support for exporting the template used for a resource group deployment (#1000)
- `az deployment group create`: Accept JSON parameter files and objects in `--parameter` and reject `--query-string` without `--template-uri` (#1000)
- `az vm create`: Add subnet selection, existing NIC attachment, and NIC delete options (#1097)
- Reject explicitly supplied collection options without values, including empty repeated occurrences (#1099)
- `az config set`: Invalid configuration keys and values now report the `InvalidInput` code with exit code 2 instead of a raw .NET exception message (#1087)
- Malformed user input across commands now reports the `InvalidInput` code with exit code 2, and the agent is offered the error first when enabled (#1087)
- `az extension add` and `az extension update`: Unknown packages now report the `ExtensionNotFound` code with exit code 4 (#1087)

#### Compute
- `az vm create` / `New-AzVM`: Add `--tags` and `-Tag` parameter support (#1092)

#### Key Vault
- Add certificate creation, import, retrieval, listing, version listing, attribute and policy updates, public download and confirmed soft-delete for CLI and PowerShell (#997)
- Add Undo-AzKeyVaultCertificateRemoval for PowerShell soft-delete recovery (#997)
- Support encrypted PFX and PEM import, public-only DER and PEM export, and bounded certificate creation polling (#997)
- Add secret set, retrieval, listing, version listing, attribute update and deletion for CLI and PowerShell, plus PowerShell secret recovery, with metadata-only output by default (#928)

#### Network
- `az network nsg`: Add create, show, list, and delete commands (#1018)
- `*-AzNetworkSecurityGroup`: Add New, Get, and Remove cmdlets (#1018)
- `az network nic`: Add `create`, `show`, `list` and `delete` (#1147)
- `New-AzNetworkInterface`: Add network interface cmdlets for create, get and remove (#1147)
- `az network vnet subnet`: Add `create`, `show`, `list`, and `delete` subnet lifecycle commands (#1014)
- `New-AzVirtualNetworkSubnetConfig`: Add local subnet configuration object creation (#1014)

#### Packaging
- AZL3 Docker Images for Azure CLI + Powershell (#892)

#### PowerShell
- `Get-AzResourceGroupDeployment`: Add cmdlet to get or list resource group deployments, including by `-Id` (#1000)
- `Stop-AzResourceGroupDeployment`: Add cmdlet to cancel a running resource group deployment (#1000)
- `Remove-AzResourceGroupDeployment`: Add cmdlet to delete a resource group deployment (#1000)
- `Test-AzResourceGroupDeployment`: Add resource group scoped deployment validation (#1000)
- `Save-AzResourceGroupDeploymentTemplate`: Add support for saving the template used for a resource group deployment to a file (#1000)
- `New-AzResourceGroupDeployment`: Accept JSON parameter files and objects in `-Parameter` and reject `-QueryString` without `-TemplateUri` (#1000)
- `New-AzVM`: Add subnet selection, existing NIC attachment, and NIC delete options (#1097)

#### Profile
- Ensure the `Profile List`, `Login`, and `Show` responses are consistent (#1100)

#### Resources
- `az deployment mg`: Add `create`, `list`, `show`, `cancel`, `delete`, `export`, `validate` and `what-if` (#987)
- `New-/Get-/Stop-/Remove-/Test-AzManagementGroupDeployment`: Add management-group-scope deployment cmdlets (#987)

#### Storage
- `az storage account create/show/list/delete`, `New-`/`Get-`/`Remove-AzStorageAccount`: Add storage account commands (#789)

#### Az.KeyVault 0.1.1
* Accept classic CLI and PowerShell policy inputs alongside REST policy JSON.
* Align certificate results with classic CLI fields and PowerShell properties. Add explicit Wait and PassThru behavior for certificate cmdlets.
* Add secret set, retrieval, listing, version listing, attribute update and soft-delete commands.
* Add Undo-AzKeyVaultSecretRemoval to recover a soft-deleted secret and its versions.
* Return secret metadata by default, with explicit plaintext retrieval through AsPlainText.
* Add certificate creation, import, retrieval, listing, version listing, attribute and policy updates, public download and soft-delete commands.
* Add Undo-AzKeyVaultCertificateRemoval to recover a soft-deleted certificate and its versions.
* Support encrypted PFX and PEM import, public-only DER and PEM export, and bounded certificate creation polling.
* Purge, backup and restore are not exposed in this preview.

#### Az.Network 0.1.2
* Added `New-AzVirtualNetworkSubnetConfig`.
* Added `New-AzNetworkInterface`, `Get-AzNetworkInterface` and `Remove-AzNetworkInterface`
    - `New-AzNetworkInterface` creates a network interface with a single IP configuration bound to `-SubnetId`, optionally associating a public IP address (`-PublicIpAddressId`) and a network security group (`-NetworkSecurityGroupId`).
    - `Get-AzNetworkInterface` serves both the single-interface `Get` set and the `List` set; omitting `-ResourceGroupName` lists every network interface in the subscription.
* Added `New-AzNetworkSecurityGroup`, `Get-AzNetworkSecurityGroup` and `Remove-AzNetworkSecurityGroup`
    - `Get-AzNetworkSecurityGroup` serves both the single-group `Get` set and the `List` set; omitting `-ResourceGroupName` lists every network security group in the subscription.

#### Az.Resources 0.1.4
* Added management-group deployment create, get, stop, remove, and test cmdlets.
    - `New-AzManagementGroupDeployment` and `Test-AzManagementGroupDeployment` take the same template and parameter inputs as the resource group deployment cmdlets (`-TemplateFile`, `-TemplateUri`, `-TemplateSpecId`, `-ParametersFile`, `-ParametersUri`, `-Parameter`, `-Mode`, `-ValidationLevel`).
    - `New-AzManagementGroupDeployment` validates using `Incremental` mode before deploying with the selected `-Mode` and outputs `DeploymentExtended`.
    - `Test-AzManagementGroupDeployment` always validates using `Incremental` mode, outputs `DeploymentValidateResult`, and throws on validation errors.
    - `Get-AzManagementGroupDeployment`, `Stop-AzManagementGroupDeployment`, and `Remove-AzManagementGroupDeployment` accept `-Id`.
    - `Remove-AzManagementGroupDeployment` sends delete directly; removing a nonexistent deployment succeeds without output unless `-PassThru` is supplied.
    - `Stop-AzManagementGroupDeployment -PassThru` outputs `true` only when the cancellation runs, not under `-WhatIf` or a declined confirmation.

## v0.1.5 - 2026-10-12 20:45 UTC

#### CLI
- [BREAKING CHANGE] `--output` formats successful stdout results only; errors remain human-readable text on stderr (#1081)
- `az config get`, `az config list`, and `az config info`: Use the standard structured output pipeline (#1041)

#### Compute
- `az vm create` / `New-AzVM`: Add security type support (#1044)
- `az vm create` / `New-AzVM`: Update `--network-security-group` to `--nsg` params and include default behaviours (#1028)
- `az vm create`: Add `--ssh-key-values` (#1016)
- `az vm create` / `New-AzVM`: Add `--public-ip-address` `-PublicIpAddressName` parameters (#1017)
- `az vm create` / `New-AzVM`: Add marketplace URN image support (#1027)

#### Config
- Clarify core.output.color only permits color; json/yaml never colorize (#1078)

#### Engineering
- Repository-owned agents and tooling with a single x.yml (#1033)

#### PowerShell
- `Import-Module Accounts`: Prevent verbose profile logs from leaking at the default warning level (#1071)
- `Get-AzConfig`, `Get-AzConfigList`, and `Get-AzConfigInfo`: Emit structured pipeline objects (#1041)

#### Resources
- `az role assignment create`: Return the existing assignment instead of failing with a conflict when the auto-generated name collides (#1072)
- `New-AzRoleAssignment`: Return the existing assignment instead of failing with a conflict when the auto-generated name collides (#1072)
- `az role assignment list`: Add the `--include-inherited` `--all` and `--include-groups` scope-breadth filters and default to exact-scope listing (#1076)
- `Get-AzRoleAssignment`: Add `-IncludeInherited` `-ExpandPrincipalGroups` and `-IncludeNestedScopes` switches and default to exact-scope listing (#1076)
- `az role assignment delete`: Add command to delete a role assignment by scope and name (#1042)
- `Remove-AzRoleAssignment`: Add cmdlet to delete an RBAC role assignment by scope and name (#1042)
- `az role assignment create`: Add role assignment creation for CLI and PowerShell (#1022)

## v0.1.4 - 2026-09-30 05:15 UTC

#### Accounts
- Add survey commands to provide feedback via online form (#1013)
- `az account get-access-token` / `Get-AzAccessToken`: Add token retrieval (#851)

#### Agent
- Upgrade Copilot SDK for ARM64 musl compatibility (#891)

#### Auth
- `az login` / `Connect-AzAccount`: Add federated credential sign-in for service principals (#968)

#### CLI
- Respect startup log level during profile loading (#995)
- Constrain table output to terminal width (#943)
- `az <command>`: Render examples after global arguments and prefix commands with `$ ` (#942)
- Use readable JSON escaping for console JSON (#939)
- Handle human-readable errors when default output is JSON (#922)
- `az <group>`/`az <command>`: Bold `--help` section headers on interactive terminals (#890)
- `az <group>`: Show help when invoked without a subcommand (#867)
- `az <group>`: Support additional help aliases for entry points (#867)
- [BREAKING CHANGE] `az <command>`: Remove the bare `?` help alias in favour of `-?` and `--?` (#867)
- `az <command>`: Add guidance when argument validation fails (#867)

#### Compute
- `az vm list` / `Get-AzVM`: Add `--show-details` `-Id` `-Status` `-UserData` (#1008)
- `az vm create`: Update `--size` (#1007)
- `az vm delete` / `Remove-AzVM`: Add `--force-deletion` and `-ForceDeletion` params (#863)
- `az vm stop`, `az vm deallocate`, `Stop-AzVM`: Support stop and deallocation VM commands (#842)

#### Identity
- Rename Identity Extension to ManagedServiceIdentity for backwards compatibility of PS Az Module (#976)

#### Login
- Add client level validations for subscription-id (#888)

#### MsGraph
- `Get-AzMsGraphApplication`: List commands now return individual objects and all pages instead of a raw response envelope (#931)

#### Network
- `az network public-ip`: Add `create`, `show`, `list` and `delete` (#1011)
- `New-AzPublicIpAddress`: Add public IP address cmdlets for create, get and remove (#1011)
- Add virtual network support for CLI and PowerShell (#834)

#### Packaging
- Add generic Linux musl tarballs for x64 and Arm64 (#827)

#### Resources
- `Get-AzRoleAssignment`: Add cmdlet to list RBAC role assignments at a subscription, resource group, or scope (#999)
- `az role assignment list`: Add `--role` to filter role assignments by role definition ID (#999)
- `az role assignment list`: Add command to list RBAC role assignments at a subscription, resource group, or scope (#986)
- `Set-AzResourceGroup`: Add support for PS to enable resource group updates (#937)
- `az group`: Support update command (#929)
- `az group`: Support delete commands (#896)
- `az group`: Support exists command (#894)

#### Telemetry
- Log messages are no longer attached to trace event tags, so message text cannot bypass the property allowlist (#730)
- Add Aria endpoint (#785)

#### Az.Network 0.1.0
* Added `New-AzPublicIpAddress`, `Get-AzPublicIpAddress` and `Remove-AzPublicIpAddress`
    - `Get-AzPublicIpAddress` serves both the single-address `Get` set and the `List` set; omitting `-ResourceGroupName` lists every public IP address in the subscription.

## v0.1.3 - 2026-09-14 23:15 UTC

#### Autocompletion
- Preserve PowerShell tab completion customizations (#860)

#### Cirrus Codegen
- Preserve enum extensibility in IR (#828)

#### CLI
- Add separate default output format for simple results (#869)
- Add `--query` support (#866)
- Help output: Remove indentation (#858)
- Help output: Uppercase and hash-prefix section headers (#858)
- Help output: Stack argument name and description on separate lines (#858)
- Help output: Number examples (#858)
- `--output` yamlc now colorizes YAML output (previously rendered identically to `yaml`) (#852)
- `az login`: Also accept `-s` and `--subscription` for `--subscription-id` and `-t` and `--tenant` for `--tenant-id` and `-i` for `--identity` (#548)
- [BREAKING CHANGE] `--output` no longer accepts `plain`; use `json` for scripting or `jsonc` for colorized JSON. `--output` now supports `table`, `tsv`, `yaml`, and `yamlc` for real (previously accepted but silently rendered as JSON), and its default (and `clients.cli.output.format`'s default) changed from `table` to `json` (#848)
- `core.output.color`: JSON command output is now colorized per configured mode (`always`/`auto`/`never`) (#841)
- `az extension`: Rename the bundled Login extension to Accounts (#804)

#### Compute
- Add an accelerated networking parameter to vm create (#814)
- `az vm start`, `Start-AzVM`: Support start vm commands (#823)

#### Config
- `az config set`/`az config get`/`az config delete`: Accept the configuration key (and value for `set`) as positional arguments in addition to `--key`/`--value` (#824)

#### Identity
- `Az.ManagedServiceIdentity`: Add Get/New/Update/Remove-AzUserAssignedIdentity (#801)
- `az identity`: Add cli support for create, list, show, update, delete (#780)

#### Login
- `Connect-AzAccount`: Add `MSI`, `ManagedIdentity` and `ManagedService` aliases for `-Identity`, and `AccountId` for `-ClientId` (#806)
- `az login`: Add the `-i` alias for `--identity` (#806)
- [BREAKING CHANGE] `az login`: Remove the `--subscription` and `-s` aliases, use `--subscription-id` (#806)

#### MsGraph
- Move ApplyGraphScope into a Customization partial (#793)

#### NonSquash
- Update extension interface, add default pagination (#777)

#### PowerShell
- Include Az.MsGraph in rollup (#856)
- `Connect-AzAccount`: Add parameter aliases so -SubscriptionId also accepts -Subscription and -TenantId also accepts -Tenant and -UseDeviceCode also accepts -DeviceCode and -DeviceAuth and -Device and -UseDeviceAuthentication and -Identity also accepts -ManagedService (existing -MSI and -ManagedIdentity aliases are retained) (#548)
- `Az.Accounts`: Rename the `Az.Login` module to `Az.Accounts` (#804)

#### Profile
- `az profile show`: Support default active selection (#825)

#### Az.MsGraph 0.1.1
* Refactored Microsoft Graph command customization and extension integration.

## v0.1.2 - 2026-08-31 23:07 UTC

#### Help
- Group Login managed-identity params + [ParameterGroup] convention linter (#719)
- Show 'Values from:' hint under --help parameters (#714)
- Group --help parameters into named sections (#711)

#### Login
- `az login` / `Connect-AzAccount`: Bare sign-in now saves the reserved profile as `default` instead of `temp` (#490) (#776)

#### MsGraph
- Module (#423)

## v0.1.1 - 2026-08-17 01:33 UTC

#### CLI
- `az config set core.output.color`: Add `auto` | `always` | `never` for persistent color preference (#649)
- Argument parser: Use the flag or alias the user typed in error messages and verbose logs instead of the internal processor property name (#594)

#### Config
- Remove bundledExtensionsRoot from config (#676)
- Rename assemblyLoading extension-root config keys (breaking) (#668)

#### Extensions
- `extension list` displays all available candidates (#688)
- `az extension list`: Include bundled extensions and expose on-disk `Location` field (#590)
- `az extension show`: Expose on-disk `Location` field (#590)

#### PowerShell
- Argument parser: Route cmdlet parameters to their processor property name so parameters whose PS name differs from the backing property no longer fail with a missing-property error (#594)

#### Telemetry
- Honor `telemetry.enabled` config changes without requiring a PowerShell module reload (#592)

## v0.1.0 - 2026-07-20 23:15 UTC

#### Resources

- `az group create`: Make SubscriptionId an optional parameter that defaults to the current subscription (#555)
- `New-AzResourceGroup`: Make SubscriptionId an optional parameter that defaults to the current subscription (#555)

## v0.0.8 - 2026-07-17 03:17 UTC

#### Compute

- `az vm list`: Fix empty output by writing the ARM list envelope value (#463)
- `az disk list`: Fix empty output by writing the ARM list envelope value (#463)

#### Login

- `az login` / `Connect-AzAccount`: Show surface-correct parameter names in validation and post-sign-in error messages (#530)
- `az login`: Select the MSAL cache account matching the active profile during silent token acquisition to avoid signing requests with a stale or unrelated account (#537)

## v0.0.7 - 2026-07-16 10:53 UTC

- No user-facing CLI or PowerShell module changes were listed for this release.

## v0.0.6 - 2026-07-16 01:40 UTC

#### CLI

- `az vm list`: Report CLI flag names instead of internal property names in missing-required-parameter errors (#461)

## v0.0.5 - 2026-07-13 00:22 UTC

#### Login

- `az login` / `Connect-AzAccount`: Resolve and persist the tenant that owns the requested subscription instead of the signed-in home tenant (#437)

## v0.0.4 - 2026-07-09 23:00 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.3 - 2026-07-09 00:15 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.2 - 2026-07-07 23:44 UTC

#### Login

- `az login --identity` / `Connect-AzAccount -Identity`: Add managed identity sign-in for system-assigned and user-assigned identities (#381)

## v0.0.1 - 2026-07-07 00:27 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.0 - 2026-07-01 00:00 UTC

- Initial baseline before the first stable release.
