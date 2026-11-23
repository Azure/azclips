# Proposal

## Why

The new Network extension has generated atomic processors for the NetworkInterfaces operations, but they only pass inputs through as given. The network interface commands need the single IP configuration built from flat create inputs, CLI name-or-ID expansion, edge-zone mapping, list routing, and PowerShell remove confirmation on top of them so that users get the documented create, list, and delete results.

## What Changes

- Create builds exactly one IP configuration named `ipconfig1` from the subnet ID, public IP address ID, and private IP address, with `Static` allocation when a private IP address is given and `Dynamic` otherwise (F1).
- CLI create expands `--subnet` (with `--vnet-name`), `--public-ip-address`, and `--network-security-group` names into `Microsoft.Network` resource IDs without extra REST calls (F3).
- Create turns an edge zone into `extendedLocation = {name, type: EdgeZone}` (F4).
- CLI create sends `--private-ip-address-version` as the IP configuration's `privateIPAddressVersion` (F8).
- PowerShell `New-AzNetworkInterface` names the IP configuration with `-IpConfigurationName` instead of `ipconfig1` (F9).
- List calls the resource-group list when a resource group is given, else the subscription list, and returns all pages (F17).
- PowerShell `Remove-AzNetworkInterface` confirms before deleting unless `-Force` is given, and returns `$true` with `-PassThru` (F22).

Non-goals (not selected for this change): a default private IP address version when none is given, marking the IP configuration primary, load balancer / application gateway / application security group references, object and full IP-configuration parameter sets and auxiliary authorization headers, a location default from the resource group, a PowerShell existence probe and overwrite prompt before create, a GET after create, a CLI create output envelope, PowerShell output decoration beyond the model, wildcard filtering, show by resource ID, VMSS network interface parameter sets, CLI `--no-wait` and PowerShell `-AsJob`, local tag validation, and the NSG reference, DNS settings, IP forwarding, accelerated networking, `$expand`, other create body fields, and long-running-operation handling that the generated processors already own. A dedicated error for a CLI `--subnet` name given without `--vnet-name` is not part of this change.

## Capabilities

### New Capabilities
- `network-network-interfaces`: behavior of the Network extension's network interface commands (create, show, list, delete) on the CLI and PowerShell surfaces.

### Modified Capabilities
- None.

## Impact

- New handwritten code under the Network extension's `Customization` folder (create and list), built on the generated NetworkInterfaces processors without changing them.
- PowerShell `New-AzNetworkInterface` (`-IpConfigurationName`) and `Remove-AzNetworkInterface` wrapper behavior in the Network PowerShell module (created when the PowerShell module is produced).
- No generated model or atomic processor changes; no new REST operations and no external API.
