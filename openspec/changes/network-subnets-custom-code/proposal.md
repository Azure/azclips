# Proposal

## Why

The new Network extension has generated atomic processors for the Subnets operations, but the create processor only passes inputs through as nested ARM objects. Subnet create needs request shaping on top of it (prefix placement, name-or-ID references, endpoint and delegation objects, IPAM allocation values, network-policy defaults) so that users get the documented subnet without building nested ARM objects by hand. Show, list, and delete need no custom behavior.

## What Changes

- One address prefix is sent as `addressPrefix`, several as `addressPrefixes` (F1).
- IPAM allocation values written as `id=<pool-id> [number-of-ip-addresses=<n>]` or a bare pool ID become allocation objects on the CLI (F3).
- Service endpoint names become `{ service }` objects (F4).
- Service endpoint policy, network security group, route table, and NAT gateway values accept a name or a resource ID and are sent as `{ id }` references (F5, F7).
- Delegation values become delegation objects named after the value (F6).
- Private endpoint network policies default to `Disabled` and private link service network policies to `Enabled` (F8).
- The deprecated CLI `--disable-private-endpoint-network-policies` and `--disable-private-link-service-network-policies` flags set those policies and write a deprecation warning (F9).

Non-goals (not selected for this change): clearing address prefixes when IPAM allocations are given, the conflict error between endpoint inputs, the multiple-network-identifier error, a duplicate-subnet-name error, in-memory PowerShell subnet config cmdlets (there is no PowerShell subnet surface), custom behavior for show, list, or delete, and `--no-wait`.

## Capabilities

### New Capabilities
- `network-subnets`: behavior of the Network extension's subnet commands (create, show, list, delete) on the CLI surface.

### Modified Capabilities
- None.

## Impact

- New handwritten code under the Network extension's `Customization` folder for subnet create, built on the generated `CreateOrUpdateSubnetsProcessor` without changing it.
- No PowerShell code: Subnets has no PowerShell cmdlet.
- No generated model or atomic processor changes; no new REST operations.
