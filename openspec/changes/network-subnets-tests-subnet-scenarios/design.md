# Design

## Context

The test cases T1-T7 come from the `/integrate-scenario-test` summary of the Subnets operation group and cover all four command keys: `network vnet subnet create`, `network vnet subnet show`, `network vnet subnet list`, `network vnet subnet delete`. The PowerShell command table has no Subnets command, so every case is CLI-only. The Network extension may not yet be registered with the scenario host; the change `network-virtual-networks-tests-vnet-scenarios` also adds that registration.

## Goals / Non-Goals

**Goals:**
- One scenario per test case, with assertions exactly as the test spec scenarios state them.
- Register the Network extension with the scenario host when it is still missing.

**Non-Goals:**
- No product code changes. Behavior gaps found while writing tests are reported, not fixed.
- No PowerShell scenarios: no Subnets cmdlet is exposed.
- No scenarios for IPAM allocations, service endpoint policies, route tables, or NAT gateways: their prerequisite resources cannot be created with Azclips commands.

## Decisions

- **Placement**: one file `tests/Scenario.Tests/Network/SubnetScenarioTests.cs` holding one test method per case. Each case creates its own resource group, so cases record independently.
- **Requirement mapping**: T1 sits under "Address prefix single or plural" (it also checks the network policy defaults), T2 under "Referenced resource name or ID", T3 under "Service endpoint names", T4 under "Delegation values", T5 under "Deprecated disable network policy flags". T6 (default outbound access) and T7 (sharing scope) check create properties no behavior requirement covers, so they sit under a requirement named for the create command.
- **Placeholders**: the spec writes resource names and locations as placeholders (`<rg>`, `<vnet>`, `<subnet1>`, `<subnet2>`, `<subnet3>`, `<nsg>`, `<location>`). The tasks carry the actual values.
- **Setup and cleanup**: resource groups use the Resources command `group create --name --location`; T2 creates its network security group with `network nsg create`.

## Risks / Trade-offs

- T4 step 4 sends a delegation whose name contains `/` (`Microsoft.Sql/managedInstances`); the service may reject that name at record time. Report it if it does.
- Until recorded, playback of every new scenario fails with `recording-required`; that is expected.
- The scenario test project may not compile while other cross-cutting dependents are out of date; a build failure outside the new files is reported, not fixed here.
