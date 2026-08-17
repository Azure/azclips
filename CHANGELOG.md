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

- No user-facing PowerShell module changes were listed for this release.

## v0.0.4 - 2026-07-09 23:00 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.3 - 2026-07-09 00:15 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.2 - 2026-07-07 23:44 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.1 - 2026-07-07 00:27 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.0 - 2026-07-01 00:00 UTC

- Initial baseline before the first stable release.
