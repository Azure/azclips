## Why

The deployment-contract prerequisites are implemented, but no real command has exercised a Bicep-backed create path. Public IP already has a pinned TypeSpec source, generated atomic processor, shared CLI/PowerShell customization and regression scenarios, making it a bounded first pilot that can preserve the command contract.

## What Changes

- Establish the complete deployment contract for `Microsoft.Network/publicIPAddresses@2025-07-01` from Network's existing pinned source commit `90e63e5fe2c596e8bd807aad7dd9d62cee1746dd`.
- Resolve the real extraction blocker discovered during proposal research: the global Network projection stops at Application Gateway, and Public IP independently has official ARM lifecycle/operation associations but no modern `getArmResource` registration. Use supported source metadata rather than class-name or handwritten route/resource inference.
- Add explicitly selected deployment metadata and its complete writable-schema closure without pruning canonical Network IR. Unselected resource operations are outside this audit, not certified compatible; referenced or embedded models do not implicitly select more resource-create operations.
- Require an explicit, coverage-complete resource audit, reviewed command-level input evidence for each bridged/excluded difference, and verified build-time Bicep compilation before pilot activation. Missing versions, unaccounted differences and unresolved source associations block activation; an unsupported full-API report is never relabeled compatible.
- Preserve writable root API `id` in canonical IR/processors, but do not map it to Bicep. Pilot input binding rejects every non-null root ID and retains normalized Int32 timeout values without widening public inputs. Nested NAT Gateway inputs are outside pilot eligibility, not silently dropped.
- Implement a build-compiled, single-resource Bicep template and an internally opted-in execution adapter for a standalone Standard, static IPv4 Public IP in an existing resource group.
- Preserve generated atomic inputs and the existing customization/defaulting logic, send an Incremental resource-group deployment, await completion and return the final Public IP through the existing resource GET/output path.
- Keep the default direct backend, public command metadata, read/list/delete/update behavior and advanced create combinations unchanged. No new public CLI flag or PowerShell parameter is proposed.
- Add focused default-path, template/input, deployment/polling/output, both-surface record/playback and separately authorized live regression gates, including explicit permission, existing-resource and failure behavior checks.

## Capabilities

### New Capabilities

- `public-ip-bicep-create-pilot`: Explicitly gated, version-aligned Bicep execution for a narrow Public IP create case, preserving the existing command contract and requiring source/catalogue/compiler and regression evidence.

### Modified Capabilities

None. The existing deployment commands' public requirements do not change; the pilot consumes or factors existing transport mechanisms without reimplementing their command surface.

## Impact

- Toolkit deployment extraction/audit infrastructure may need narrowly evidenced legacy ARM association or comparison fixes. Existing disabled generation must remain identical; broad Network migration is outside this change.
- Network customization/execution integration and focused tests may change; generated files must only change through the existing generator. The current Network/Resources projects depend on shared runtime/core, not each other, so transport reuse must not introduce an implicit feature-module dependency or duplicate deployment logic.
- Build tooling will pin a verified Bicep CLI and package compiled ARM JSON. No runtime Bicep compiler, compiler-core API or provisioning SDK is proposed.
- Actual extraction findings and unchecked gates are recorded in [design.md](design.md). This proposal does not claim a successful complete Public IP audit or live deployment.
- Deployment write/read permissions, deployment records and transport requests differ from the direct create path. These are intentional only inside the explicit pilot opt-in and must be documented/tested before activation.
- No resource group, VM, NIC, VNet, Public IP prefix or DDoS plan is created by this pilot. No Azure resources are created while preparing this proposal.
