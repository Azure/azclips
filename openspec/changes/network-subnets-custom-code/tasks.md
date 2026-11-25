# Tasks

These tasks change product code only. Scenario tests come later in a separate change. Generated models and atomic processors under `Processors/` and `Models/` are read-only. Before the extension moves to `src/Extensions/Network` (migration skill step 12), the extension root is the codegen output folder; afterwards it is `src/Extensions/Network`. Do not compile before migration step 13.

## 1. Subnet create request shaping (customization)

- [x] 1.1 Add a subnet create customization class under `<extension root>/Customization` that inherits `CreateOrUpdateSubnetsProcessor` and shapes the request body through the smallest request-construction entry in `eng-docs/howto/Extension-Development-For-Private-Preview.md`, with no `Method`, `CreateRequestUri()`, api-version, or URL; verify by review that the file exists, inherits the processor, and no file under `Processors/` or `Models/` changed
- [x] 1.2 Implement F1 (one address prefix -> `addressPrefix` only, several -> `addressPrefixes` only, none -> neither); verify by review against "Address prefix single or plural"
- [x] 1.3 Implement F3 for the CLI surface (IPAM allocation values `id=<pool-id> [number-of-ip-addresses=<n>]` or a bare pool ID -> `ipamPoolPrefixAllocations` entries); verify by review against "IPAM allocation values"
- [x] 1.4 Implement F4 (service endpoint names -> `[{ service }]`, input order); verify by review against "Service endpoint names"
- [x] 1.5 Implement F5 and F7 with one private name-or-ID helper (value starting with `/` unchanged, else `Microsoft.Network` ID from command subscription and resource group; only `id` sent); verify by review against "Service endpoint policy name or ID" and "Referenced resource name or ID"
- [x] 1.6 Implement F6 (delegation value -> `{ name: value, serviceName: value }`, dotted three-part values normalized to `Microsoft.X/y` for `serviceName`); verify by review against "Delegation values"
- [x] 1.7 Implement F8 (private endpoint policies default `Disabled`, private link service policies default `Enabled`); verify by review against "Network policy defaults"
- [x] 1.8 Implement F9 for the CLI surface (deprecated disable-* booleans -> `Disabled`/`Enabled`, override the policy values, write a deprecation warning); verify by review against "Deprecated disable network policy flags"

## Workflow follow-up

- Show, list, and delete use the atomic processors unchanged.
- Compile at migration step 13 and fix errors in the customization files only.
- Scenario tests for these requirements are proposed separately with `/integrate-scenario-test` and `/scenario-test-spec`.
- Archive the change after code and scenario tests are reviewed.
