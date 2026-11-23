# Design

## Context

The test cases T1 to T4 come from the `/integrate-scenario-test` summary of the NetworkInterfaces operation group and cover all eight command keys: `network nic create`, `network nic show`, `network nic list`, `network nic delete`, `New-AzNetworkInterface`, `Get-AzNetworkInterface_Get`, `Get-AzNetworkInterface_List`, `Remove-AzNetworkInterface`. The behavior they verify is stated in the `network-network-interfaces` spec. The Network extension may not yet be registered with the scenario host; earlier Network test changes also add that registration.

## Goals / Non-Goals

**Goals:**
- One test method per test case, with assertions exactly as the test spec scenarios state them.
- Register the Network extension with the scenario host when it is still missing.

**Non-Goals:**
- No product code changes. Behavior gaps found while writing tests are reported, not fixed.
- No test for `Remove-AzNetworkInterface` confirmation, `-Force`, or `-PassThru` (behavior requirement "PowerShell remove confirmation and PassThru"): that behavior lives in the cmdlet, which the scenario host's `RunPowerShell` path does not run.
- No test for the CLI-only `--private-ip-address-version` (behavior requirement "CLI private IP address version"): no upstream case uses it.
- No test for wildcard list (`-ResourceGroupName '*'`): the list does not implement wildcards.

## Decisions

- **Placement**: four files under `tests/Scenario.Tests/Network/`, one case and one recording each:
  - `NetworkInterfaceLifecycleScenarioTests.cs`: T1.
  - `NetworkInterfaceCreateOptionsScenarioTests.cs`: T2.
  - `NetworkInterfaceReferencesScenarioTests.cs`: T3.
  - `NetworkInterfaceEdgeZoneScenarioTests.cs`: T4.
- **Requirement mapping**: each case sits under the behavior requirement its distinctive assertions check: T1 under "List scope by resource group presence" (it also checks the `ipconfig1` and `Dynamic` defaults of "Single IP configuration from flat create inputs" and the CLI subnet name expansion); T2 under "Single IP configuration from flat create inputs" (static private IP and public IP; it also checks the CLI public IP name expansion and the PowerShell `-IpConfigurationName`); T3 under "CLI name-or-ID expansion for create references"; T4 under "Edge zone as extended location".
- **Surfaces**: every case drives the CLI and PowerShell surfaces with the same values. CLI subnet setup uses `network vnet subnet create`; PowerShell has no subnet command and uses `New-AzVirtualNetwork -SubnetNames/-SubnetAddressPrefixes`. CLI `network nic create` passes `-l` because Azclips requires it.
- **Placeholders**: the test cases already write resource names, locations, the subscription, and the edge zone as placeholders; the spec keeps them and writes T4's `westus` as `<location>`. The tasks carry the steps as given, with the literal `westus`, and name the literal values the apply chooses for each placeholder.
- **Setup and cleanup**: resource groups use the Resources commands `group create`/`group delete` and `New-AzResourceGroup`/`Remove-AzResourceGroup`, so the scenario host needs both the Resources and the Network extensions.

## Risks / Trade-offs

- T4: edge zone `microsoftlosangeles1` in `westus` needs edge-zone access on the recording subscription; if unavailable, T4 cannot be recorded.
- T2: accelerated networking on a network interface without a VM must be accepted in the chosen region.
- T1: the subscription-wide list (NetworkInterfaces_ListAll) records every network interface in the subscription; the recording may be large. Only presence of the test resource is asserted.
- Subscription-scoped IDs are asserted by suffix; full-ID equality holds only in playback.
- Until recorded, playback of every new scenario fails with `recording-required`; that is expected.
- The scenario test project may not compile while other cross-cutting dependents are out of date; a build failure outside the new files is reported, not fixed here.
