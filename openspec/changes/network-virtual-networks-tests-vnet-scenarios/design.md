# Design

## Context

The Network extension is new to the scenario test project: `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs` has no `ExtensionUnderTest` entry for it and `tests/Scenario.Tests/Scenario.Tests.csproj` has no `ProjectReference` to `Extensions.Network`. The test cases T1-T8 come from the `/integrate-scenario-test` summary of the VirtualNetworks operation group and cover all eight command keys: `network vnet create`, `network vnet show`, `network vnet list`, `network vnet delete`, `New-AzVirtualNetwork`, `Get-AzVirtualNetwork_Get`, `Get-AzVirtualNetwork_List`, `Remove-AzVirtualNetwork`.

## Goals / Non-Goals

**Goals:**
- One scenario per test case, each driving both surfaces against one recording, with assertions exactly as the test spec scenarios state them.
- Register the Network extension with the scenario host so the scenarios can run.

**Non-Goals:**
- No product code changes. Behavior gaps found while writing tests are reported, not fixed.
- No tests for `network vnet subnet *` commands; they belong to the Subnets operation group.
- No scenario for deriving a subnet prefix when only a subnet name is given, and none for rejecting encryption enabled without a policy: neither behavior is in Azclips, so T2 and T5 pass the values explicitly.

## Decisions

- **Placement**: one file `tests/Scenario.Tests/Network/VirtualNetworkScenarioTests.cs` holding one test method per case. Each case creates and deletes its own resource group, so cases record independently.
- **Requirement mapping**: T1 sits under "List scope by resource group presence" because its distinctive request-path checks are on list; it also exercises show, delete, and subnet-from-name. T2 sits under "Default virtual network address prefix", T8 under "Edge zone maps to extended location". T3-T7 check create properties no behavior requirement covers, so they sit under a requirement named for the create command.
- **Placeholders**: the spec writes resource names and locations as placeholders (`<rg>`, `<vnet>`, `<subnet>`, `<location>`, `<edge-zone>`). The tasks carry the actual values.
- **Setup and cleanup**: resource groups use the Resources commands `group create --name --location` / `group delete --name` and `New-AzResourceGroup -Name -Location` / `Remove-AzResourceGroup -Name`.

## Risks / Trade-offs

- T7 needs region `centraluseuap` and T8 needs region `eastus2euap` with edge zone `MicrosoftRRDCLab1`. If the recording subscription lacks access, those scenarios stay unrecorded and are reported as such.
- Until recorded, playback of every new scenario fails with `recording-required`; that is expected.
- The scenario test project may not compile while other cross-cutting dependents are out of date; a build failure outside the new files is reported, not fixed here.
