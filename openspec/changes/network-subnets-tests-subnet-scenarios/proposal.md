# Proposal

## Why

The Network extension's subnet commands (`network vnet subnet create`, `network vnet subnet show`, `network vnet subnet list`, `network vnet subnet delete`) have no record/playback scenario tests. Without them, the behavior stated in the `network-subnets` spec is not checked against recorded service traffic.

## What Changes

- Add record/playback scenario tests T1-T7 for the Subnets operation group on the CLI surface (the PowerShell surface exposes no Subnets command):
  - T1 subnet lifecycle with one and several address prefixes (create, list, show, delete).
  - T2 subnet with a network security group by name and by ID.
  - T3 subnet with a service endpoint.
  - T4 subnet delegation in dotted and slash form.
  - T5 deprecated disable network policy flags.
  - T6 subnet default outbound access true and false.
  - T7 subnet sharing scope Tenant.
- Register the Network extension with the scenario test host (`ExtensionUnderTest` entry and `ProjectReference`) if no earlier change has added it.
- No product code changes.

## Capabilities

### New Capabilities

- `network-subnets-tests`: record/playback scenario tests that verify the `network-subnets` behavior spec and the subnet commands on the CLI surface.

### Modified Capabilities

None.

## Impact

- `tests/Scenario.Tests/Network/` gains the subnet scenario test file.
- `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs` and `tests/Scenario.Tests/Scenario.Tests.csproj` gain the Network extension registration when it is still missing.
- `tests/Scenario.Tests/SessionRecords/` gains recordings only after the user approves recording.
