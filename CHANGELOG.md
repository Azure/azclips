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
