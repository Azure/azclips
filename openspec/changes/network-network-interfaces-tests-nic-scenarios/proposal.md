# Proposal

## Why

The Network extension's network interface commands (`network nic create`, `network nic show`, `network nic list`, `network nic delete`, `New-AzNetworkInterface`, `Get-AzNetworkInterface_Get`, `Get-AzNetworkInterface_List`, `Remove-AzNetworkInterface`) have no record/playback scenario tests. Without them, the behavior stated in the `network-network-interfaces` spec is not checked against recorded service traffic.

## What Changes

- Add record/playback scenario tests for the NetworkInterfaces operation group, each on the CLI and PowerShell surfaces:
  - T1 create with defaults, show, list by resource group and by subscription, delete, and show after delete.
  - T2 create with a static private IP, a public IP by name, and optional settings; show with and without expand.
  - T3 create with network security group and public IP references, then re-create without IP forwarding.
  - T4 create a network interface in an edge zone.
- Register the Network extension with the scenario test host (`ExtensionUnderTest` entry and `ProjectReference`) if no earlier change has added it.
- No product code changes.

## Capabilities

### New Capabilities

- `network-network-interfaces-tests`: record/playback scenario tests that verify the `network-network-interfaces` behavior spec and the network interface commands on the CLI and PowerShell surfaces.

### Modified Capabilities

None.

## Impact

- `tests/Scenario.Tests/Network/` gains the network interface scenario test files.
- `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs` and `tests/Scenario.Tests/Scenario.Tests.csproj` gain the Network extension registration when it is still missing.
- `tests/Scenario.Tests/SessionRecords/` gains recordings only after the user approves recording.
