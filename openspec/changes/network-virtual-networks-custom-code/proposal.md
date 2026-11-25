# Proposal

## Why

The new Network extension has generated atomic processors for the VirtualNetworks operations, but they only pass inputs through as given. The virtual network commands need a small set of request-shaping and routing behaviors on top of them (address-space defaults, subnet construction, edge zone mapping, list scope) so that users get the documented create and list results without building nested ARM objects by hand.

## What Changes

- Create sends a default address prefix `10.0.0.0/16` when neither address prefixes nor IPAM pool allocations are given (F1).
- IPAM pool allocations are sent as address-space allocations and replace any address prefixes (F2).
- An edge zone name becomes an `extendedLocation` of type `EdgeZone` (F3).
- Subnet definitions are sent inline in the create body (F4), and a subnet name plus prefixes builds one subnet (F5).
- IP allocation IDs are sent as `[{ id }]` references (F22).
- List calls the resource-group list when a resource group is given, else the subscription list, and returns all pages (F10).
- PowerShell `Remove-AzVirtualNetwork` confirms before deleting unless `-Force` is given, and writes `$true` with `-PassThru` (F18).

Non-goals (not selected for this change): default subnet prefix derived from the VNet prefix, a VNet-level subnet NSG shortcut, network-policy defaults for subnet definitions, DDoS protection plan name expansion, location defaulting from the resource group, a CLI output envelope, a PowerShell existence probe before create, a GET after create, FlowTimeout filtering, PowerShell wildcard filtering, PowerShell output shaping beyond the model, and parameter validation that the service or argument binding already performs.

## Capabilities

### New Capabilities
- `network-virtual-networks`: behavior of the Network extension's virtual network commands (create, show, list, delete) on the CLI and PowerShell surfaces.

### Modified Capabilities
- None.

## Impact

- New handwritten code under the Network extension's `Customization` folder (create and list), built on the generated VirtualNetworks processors without changing them.
- PowerShell `Remove-AzVirtualNetwork` wrapper behavior in the Network PowerShell module (created when the PowerShell module is produced).
- No generated model or atomic processor changes; no new REST operations.
