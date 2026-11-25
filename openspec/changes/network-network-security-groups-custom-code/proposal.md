# Proposal

## Why

The new Network extension has generated atomic processors for the NetworkSecurityGroups operations, but they only pass inputs through as given. The network security group commands need a small set of request-shaping and routing behaviors on top of them (location default, list scope, tag parsing, PowerShell remove confirmation) so that users get the documented create, list, and delete results.

## What Changes

- Create without a location reads the resource group and uses its location; with a location, no resource-group read is made; a failed resource-group read stops the create (F1).
- List calls the resource-group list when a resource group is given, else the subscription list, and returns all pages (F2).
- CLI `--tags key[=value] ...` is parsed into the tags dictionary sent in the create body (F12).
- PowerShell `Remove-AzNetworkSecurityGroup` confirms before deleting unless `-Force` is given, and writes `true` with `-PassThru` (F8).

Non-goals (not selected for this change): a PowerShell existence probe and overwrite prompt before create, a GET after create, always sending `flushConnection`, inline security rules on create, PowerShell wildcard filtering, CLI `--ids`, a CLI output envelope, location display-name normalization, PowerShell output shaping beyond the model, Tag validation that the service already performs, and `$expand`, long-running-operation, and paging handling that the generated processors already own.

## Capabilities

### New Capabilities
- `network-network-security-groups`: behavior of the Network extension's network security group commands (create, show, list, delete) on the CLI and PowerShell surfaces.

### Modified Capabilities
- None.

## Impact

- New handwritten code under the Network extension's `Customization` folder (create and list), built on the generated NetworkSecurityGroups processors without changing them, and composing the Resources extension's resource-group read processor for the location default.
- PowerShell `Remove-AzNetworkSecurityGroup` wrapper behavior in the Network PowerShell module (created when the PowerShell module is produced).
- No generated model or atomic processor changes; no new Network REST operations.
