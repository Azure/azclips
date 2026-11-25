# Design

## Context

The Network extension is being built in the codegen output folder before it moves to `src/Extensions/Network` (migration skill step 12). The generated atomic processors for Subnets are `CreateOrUpdateSubnetsProcessor`, `GetSubnetsProcessor`, `ListSubnetsProcessor`, and `DeleteSubnetsProcessor`. They are read-only. `CreateOrUpdateSubnetsProcessor` exposes flat body inputs such as `AddressPrefix`, `AddressPrefixes`, `ServiceEndpoints`, `ServiceEndpointPolicies`, `Delegations`, `IpamPoolPrefixAllocations`, `NetworkSecurityGroupId`, `RouteTableId`, `NatGatewayId`, `PrivateEndpointNetworkPolicies`, and `PrivateLinkServiceNetworkPolicies`. There is no `ExtensionIndex.cs` yet, and Subnets has no PowerShell cmdlet. See proposal.md for motivation and the spec for required behavior.

## Goals / Non-Goals

**Goals:**
- Add create request shaping (F1, F3, F4, F5, F6, F7, F8, F9) as handwritten customization on top of `CreateOrUpdateSubnetsProcessor`.

**Non-Goals:**
- No customization for show, list, or delete; they stay atomic.
- No change to generated models or atomic processors, no new endpoints, no api-version or URL in customization.
- No PowerShell code.
- No compile in this change while the code lives in the output folder (the migration skill compiles at step 13).

## Decisions

- **One create customization inherits the create processor.** A `Customization` class for subnet create inherits `CreateOrUpdateSubnetsProcessor` (its interface covers every parent input; it may add user-facing inputs) and fills the body in the smallest request-construction entry that `eng-docs/howto/Extension-Development-For-Private-Preview.md` allows, not `ProcessAsync`. Alternative: compose with `CreateExtendedProcessor<T>`; rejected because only one REST call is made.
  - F1: a list of address prefixes goes to `AddressPrefix` when it has one item, to `AddressPrefixes` when it has more, and to neither when empty.
  - F3: IPAM allocation strings are parsed into `IpamPoolPrefixAllocation` entries (`id=`, `number-of-ip-addresses=`, or a bare pool ID).
  - F4: service names map to `ServiceEndpointPropertiesFormat { Service }`.
  - F5, F7: name-or-ID values go through one private helper that returns a value starting with `/` unchanged and otherwise builds the `Microsoft.Network` ID from the command subscription and resource group; results feed `ServiceEndpointPolicies` (`{ Id }` only), `NetworkSecurityGroupId`, `RouteTableId`, and `NatGatewayId`. No other referenced-resource input (location, tags, rules, routes) is set.
  - F6: delegation values map to `Delegation { Name = value, ServiceName = normalized value }`.
  - F8: `PrivateEndpointNetworkPolicies` defaults to `Disabled` and `PrivateLinkServiceNetworkPolicies` to `Enabled` when not given.
  - F9: the two deprecated boolean inputs, when given, override the policy values and write a warning through the runtime's warning output.
- **CLI-only features stay CLI-only through the command table.** F3 and F9 inputs are added on the customization class; Subnets has no PowerShell cmdlet, so they are exposed only when the CLI command table is built (migration step 14).

## Risks / Trade-offs

- [F6 names each delegation after its value (target surface) instead of a numeric index (upstream CLI)] → recorded as a decision for user review; switch the requirement if the user prefers index names.
- [F1 sends one prefix only as `addressPrefix`, which differs from the step 8 workflow wording "also AddressPrefix"] → recorded as a decision for user review.
- [How a warning is written from customization depends on the runtime API] → follow the warning pattern in `src/Extensions/Compute/Customization`; if none exists, report it at apply time.
- [No compile before step 13] → each task is verified by review against the spec scenarios; compile errors surface at step 13.
