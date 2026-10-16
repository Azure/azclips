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
