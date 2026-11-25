# Proposal

## Why

The Network extension's virtual network commands (`network vnet create/show/list/delete` and `New-AzVirtualNetwork`, `Get-AzVirtualNetwork_Get`, `Get-AzVirtualNetwork_List`, `Remove-AzVirtualNetwork`) have no record/playback scenario tests. Without them, the behavior stated in the `network-virtual-networks` spec is not checked against recorded service traffic on either surface.

## What Changes

- Add record/playback scenario tests T1-T8 for the VirtualNetworks operation group, each driving the CLI and PowerShell surfaces against one recording:
  - T1 virtual network lifecycle with DNS server and subnet (create, show, list by resource group, list by subscription, delete).
  - T2 create defaults the address space to `10.0.0.0/16`.
  - T3 create with flow timeout.
  - T4 create with BGP community.
  - T5 create with encryption settings.
  - T6 create with private endpoint VNet policies.
  - T7 create with summarized gateway prefixes.
  - T8 create in an edge zone.
- Register the Network extension with the scenario test host (`ExtensionUnderTest` entry and `ProjectReference`), which a new extension needs before any scenario can run.
- No product code changes.

## Capabilities

### New Capabilities

- `network-virtual-networks-tests`: record/playback scenario tests that verify the `network-virtual-networks` behavior spec and the virtual network commands on the CLI and PowerShell surfaces.

### Modified Capabilities

None.

## Impact

- `tests/Scenario.Tests/Network/` gains the virtual network scenario test file.
- `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs` and `tests/Scenario.Tests/Scenario.Tests.csproj` gain the Network extension registration.
- `tests/Scenario.Tests/SessionRecords/` gains recordings only after the user approves recording. T7 needs a canary region (`centraluseuap`) and T8 needs edge zone access (`eastus2euap` / `MicrosoftRRDCLab1`) on the recording subscription.
