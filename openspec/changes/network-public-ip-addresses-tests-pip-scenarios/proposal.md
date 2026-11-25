# Proposal

## Why

The Network extension's public IP address commands (`network public-ip create`, `network public-ip show`, `network public-ip list`, `network public-ip delete`, `New-AzPublicIpAddress`, `Get-AzPublicIpAddress_Get`, `Get-AzPublicIpAddress_List`, `Remove-AzPublicIpAddress`) have no record/playback scenario tests. Without them, the behavior stated in the `network-public-ip-addresses` spec is not checked against recorded service traffic.

## What Changes

- Add record/playback scenario tests for the PublicIPAddresses operation group, each on the CLI and PowerShell surfaces:
  - T1 public IP lifecycle: DNS label scope, IP tags, zones, list by resource group, show, delete.
  - T2 SKU defaults, tags, Global tier, and StandardV2.
  - T3 IPv6 public IP address.
  - T4 explicit idle timeout.
  - T5 DDoS protection mode on create.
  - T6 Basic SKU with Dynamic allocation.
  - T7 public IP in an edge zone.
  - T8 list public IP addresses in the subscription.
- Register the Network extension with the scenario test host (`ExtensionUnderTest` entry and `ProjectReference`) if no earlier change has added it.
- No product code changes.

## Capabilities

### New Capabilities

- `network-public-ip-addresses-tests`: record/playback scenario tests that verify the `network-public-ip-addresses` behavior spec and the public IP address commands on the CLI and PowerShell surfaces.

### Modified Capabilities

None.

## Impact

- `tests/Scenario.Tests/Network/` gains the public IP address scenario test files.
- `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs` and `tests/Scenario.Tests/Scenario.Tests.csproj` gain the Network extension registration when it is still missing.
- `tests/Scenario.Tests/SessionRecords/` gains recordings only after the user approves recording.
