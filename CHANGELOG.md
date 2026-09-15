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
