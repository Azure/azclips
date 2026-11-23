# Design

## Context

The test cases T1 and T2 come from the `/integrate-scenario-test` summary of the NetworkSecurityGroups operation group and cover all eight command keys: `network nsg create`, `network nsg show`, `network nsg list`, `network nsg delete`, `New-AzNetworkSecurityGroup`, `Get-AzNetworkSecurityGroup_Get`, `Get-AzNetworkSecurityGroup_List`, `Remove-AzNetworkSecurityGroup`. The Network extension may not yet be registered with the scenario host; the earlier Network test changes (`network-virtual-networks-tests-vnet-scenarios`, `network-subnets-tests-subnet-scenarios`) also add that registration.

## Goals / Non-Goals

**Goals:**
- One scenario per test case, with assertions exactly as the test spec scenarios state them.
- Register the Network extension with the scenario host when it is still missing.

**Non-Goals:**
- No product code changes. Behavior gaps found while writing tests are reported, not fixed.
- No test for `Remove-AzNetworkSecurityGroup` confirmation, `-Force`, or `-PassThru` (behavior requirement "PowerShell remove confirmation and PassThru"): that behavior lives in the cmdlet, which the scenario host's `RunPowerShell` path does not run.
- No test for the resource-group GET failure branch or for a tag key without a value: no upstream case covers them.
- No test for PowerShell wildcard names: Azclips does not implement wildcard filtering.

## Decisions

- **Placement**: one file `tests/Scenario.Tests/Network/NetworkSecurityGroupScenarioTests.cs` holding one test method per case. Each case creates its own resource group, so cases record independently.
- **Requirement mapping**: T1 sits under "List scope by resource group presence" because it asserts the NetworkSecurityGroups_List and NetworkSecurityGroups_ListAll requests on both surfaces; its create step also checks the "Location given" branch of "Default location from the resource group". T2 sits under "Default location from the resource group".
- **Surfaces**: T1 drives the CLI and PowerShell surfaces with the same values. T2 is CLI-only because PowerShell `-Location` is required.
- **Placeholders**: the spec writes resource names and locations as placeholders (`<rg>`, `<nsg>`, `<location>`). The tasks carry the actual values.
- **Setup and cleanup**: resource groups use the Resources commands `group create` / `group delete` and `New-AzResourceGroup` / `Remove-AzResourceGroup`, so the scenario host needs both the Resources and the Network extensions.

## Risks / Trade-offs

- The subscription-wide list (NetworkSecurityGroups_ListAll) records every network security group in the subscription; the recording may be large and may need a list sanitiser. Only item type and presence or absence of the test resource are asserted.
- The delete output shape is not verified; delete is asserted by its request and the following empty list.
- Until recorded, playback of every new scenario fails with `recording-required`; that is expected.
- The scenario test project may not compile while other cross-cutting dependents are out of date; a build failure outside the new files is reported, not fixed here.
