# Design

## Context

The test cases T1 to T8 come from the `/integrate-scenario-test` summary of the PublicIPAddresses operation group and cover all eight command keys: `network public-ip create`, `network public-ip show`, `network public-ip list`, `network public-ip delete`, `New-AzPublicIpAddress`, `Get-AzPublicIpAddress_Get`, `Get-AzPublicIpAddress_List`, `Remove-AzPublicIpAddress`. The behavior they verify is stated in the `network-public-ip-addresses` spec. The Network extension may not yet be registered with the scenario host; earlier Network test changes also add that registration.

## Goals / Non-Goals

**Goals:**
- One test method per test case, with assertions exactly as the test spec scenarios state them.
- Register the Network extension with the scenario host when it is still missing.

**Non-Goals:**
- No product code changes. Behavior gaps found while writing tests are reported, not fixed.
- No test for DDoS protection plan or DDoS custom policy name/ID expansion, or for public IP prefix name/ID expansion: the Azclips surface has no command to create the referenced plan, policy, or prefix.
- No test for `Remove-AzPublicIpAddress` confirmation, `-Force`, or `-PassThru` (behavior requirement "PowerShell remove confirmation and PassThru"): that behavior lives in the cmdlet, which the scenario host's `RunPowerShell` path does not run.
- No test for idle timeout of 0 or less: no upstream case covers it.

## Decisions

- **Placement**: three files under `tests/Scenario.Tests/Network/`, one recording group each:
  - `PublicIpAddressLifecycleScenarioTests.cs`: T1, with T8 placed in the same scenario after T1 step 3 so it shares T1's resource group and recording.
  - `PublicIpAddressSkuScenarioTests.cs`: T2 to T6, sharing the setup resource group `scenario-pip-sku-rg`.
  - `PublicIpAddressEdgeZoneScenarioTests.cs`: T7.
- **Requirement mapping**: each case sits under the behavior requirement its distinctive request assertion checks: T1 and T8 under "List scope by resource group presence" (T1 also checks "IP tags on create"); T2 under "SKU default on create" (it also checks the allocation, version, and idle timeout defaults); T3 under "IP version default on create"; T4 under "Idle timeout default on create"; T5 under "DDoS protection mode and plan"; T6 under "Allocation method default from the SKU"; T7 under "Edge zone as extended location".
- **Surfaces**: every case drives the CLI and PowerShell surfaces with the same values, so both send identical requests and can share a recording.
- **Placeholders**: the spec writes resource names, locations, DNS labels, and the edge zone as placeholders (`<rg>`, `<pip>`, `<location>`, `<dns label>`, `<edge zone>`). The tasks carry the actual values.
- **Setup and cleanup**: resource groups use the Resources commands `group create` and `New-AzResourceGroup`, so the scenario host needs both the Resources and the Network extensions.

## Risks / Trade-offs

- T6: the service may reject Basic SKU public IP creation; its output expectations are marked "verify at record".
- T7: edge zone `microsoftlosangeles1` needs an allowlisted subscription.
- T5: DDoS IP Protection (`protection-mode Enabled`) is billed while the resource exists; clean up promptly after recording.
- T2: StandardV2 and the Global tier may not be available in every region.
- T8: the subscription-wide list (PublicIPAddresses_ListAll) records every public IP in the subscription; the recording may be large and may need a list sanitiser that keeps only scenario items. Only presence of the test resource is asserted.
- Until recorded, playback of every new scenario fails with `recording-required`; that is expected.
- The scenario test project may not compile while other cross-cutting dependents are out of date; a build failure outside the new files is reported, not fixed here.
