# Proposal

## Why

The Network extension's network security group commands (`network nsg create`, `network nsg show`, `network nsg list`, `network nsg delete`, `New-AzNetworkSecurityGroup`, `Get-AzNetworkSecurityGroup_Get`, `Get-AzNetworkSecurityGroup_List`, `Remove-AzNetworkSecurityGroup`) have no record/playback scenario tests. Without them, the behavior stated in the `network-network-security-groups` spec is not checked against recorded service traffic.

## What Changes

- Add record/playback scenario tests for the NetworkSecurityGroups operation group:
  - T1 network security group lifecycle (create, show, list by resource group, list by subscription, delete) on the CLI and PowerShell surfaces.
  - T2 create without a location takes the resource group location, on the CLI surface.
- Register the Network extension with the scenario test host (`ExtensionUnderTest` entry and `ProjectReference`) if no earlier change has added it.
- No product code changes.

## Capabilities

### New Capabilities

- `network-network-security-groups-tests`: record/playback scenario tests that verify the `network-network-security-groups` behavior spec and the network security group commands on the CLI and PowerShell surfaces.

### Modified Capabilities

None.

## Impact

- `tests/Scenario.Tests/Network/` gains the network security group scenario test file.
- `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs` and `tests/Scenario.Tests/Scenario.Tests.csproj` gain the Network extension registration when it is still missing.
- `tests/Scenario.Tests/SessionRecords/` gains recordings only after the user approves recording.
