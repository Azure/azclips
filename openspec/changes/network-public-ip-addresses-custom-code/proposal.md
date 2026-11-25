# Proposal

## Why

The new Network extension has generated atomic processors for the PublicIPAddresses operations, but they only pass inputs through as given. The public IP address commands need request-shaping defaults, name-or-ID reference expansion, IP tag mapping, list routing, and PowerShell remove confirmation on top of them so that users get the documented create, list, and delete results.

## What Changes

- Create sends `sku.name = Standard` when no SKU is given (F1).
- Create derives the allocation method from the effective SKU when none is given: `Dynamic` for `Basic`, else `Static` (F2).
- Create sends `publicIPAddressVersion = IPv4` when no version is given (F3).
- Create sends `idleTimeoutInMinutes = 4` when no idle timeout, or a value of 0 or less, is given (F4).
- Create turns an edge zone into `extendedLocation = {name, type: EdgeZone}` (F7).
- Create sends the DDoS protection mode, and expands a DDoS protection plan name into its resource ID (F8).
- Create expands a DDoS custom policy name into its resource ID (F9).
- Create expands a public IP prefix name into its resource ID without reading the prefix (F10).
- Create maps IP tags into `ipTags = [{ipTagType, tag}, ...]` in the given order (F12).
- List calls the resource-group list when a resource group is given, else the subscription list, and returns all pages (F25).
- PowerShell `Remove-AzPublicIpAddress` confirms before deleting unless `-Force` is given, and returns `$true` with `-PassThru` (F28).

Non-goals (not selected for this change): a separate tier rule, DNS settings construction, reading the public IP prefix to overwrite version, SKU, tier, or zones, the first-party service tag ID, a location default from the resource group, CLI warnings, parameter validation that binding or the service already performs, a PowerShell existence probe and overwrite prompt before create, a GET after create, PowerShell output shaping beyond the model, a CLI create output envelope, CLI table output, CLI `--no-wait` and paging tokens, PowerShell `-AsJob`, wildcard filtering, and VMSS parameter sets, and `$expand` and long-running-operation handling that the generated processors already own.

## Capabilities

### New Capabilities
- `network-public-ip-addresses`: behavior of the Network extension's public IP address commands (create, show, list, delete) on the CLI and PowerShell surfaces.

### Modified Capabilities
- None.

## Impact

- New handwritten code under the Network extension's `Customization` folder (create and list), built on the generated PublicIPAddresses processors without changing them.
- PowerShell `Remove-AzPublicIpAddress` wrapper behavior in the Network PowerShell module (created when the PowerShell module is produced).
- No generated model or atomic processor changes; no new REST operations and no external API.
