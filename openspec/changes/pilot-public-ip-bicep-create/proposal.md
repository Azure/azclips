## Why

The deployment-contract prerequisites are implemented, but no real command has exercised a Bicep-backed create path. Public IP already has a pinned TypeSpec source, generated atomic processor, shared CLI/PowerShell customization and regression scenarios, making it a bounded first pilot that can preserve the command contract.

## What Changes

- Establish the complete deployment contract for `Microsoft.Network/publicIPAddresses@2025-07-01` from Network's existing pinned source commit `90e63e5fe2c596e8bd807aad7dd9d62cee1746dd`.
- Resolve the real extraction blocker discovered during proposal research: the global Network projection stops at Application Gateway, and Public IP independently has official ARM lifecycle/operation associations but no modern `getArmResource` registration. Use supported source metadata rather than class-name or handwritten route/resource inference.
- Add explicitly selected deployment metadata and its complete writable-schema closure without pruning canonical Network IR. Unselected resource operations are outside this audit, not certified compatible; referenced or embedded models do not implicitly select more resource-create operations.
- Reuse pinned upstream `@azure-tools/typespec-bicep` code for TypeSpec-to-Bicep resource-type interpretation instead of maintaining a competing schema implementation. Derive atomic-parameter-to-resource-field bindings through shared source identities and containing property paths, including existing flattening metadata, before generating the pilot template.
- Treat the handwritten pilot binder as an offline prototype/regression baseline. Replace its structural mapping with the generated bridge, retaining command customization, eligibility checks and deployment transport as separate AzCLIPS responsibilities.
- Once the upstream-derived path has proven replacement coverage, retire superseded custom discovery/schema interpretation and redundant facet/audit code. Preserve canonical linkage, independent safeguards and historical evidence, not duplicate implementations or obsolete tests.
- Require an explicit, coverage-complete resource audit, reviewed command-level input evidence for each bridged/excluded difference, and verified build-time Bicep compilation before pilot activation. Missing versions, unaccounted differences and unresolved source associations block activation; an unsupported full-API report is never relabeled compatible.
- Preserve writable root API `id` in canonical IR/processors, but do not map it to Bicep. Pilot input binding rejects every non-null root ID and retains normalized Int32 timeout values without widening public inputs. Nested NAT Gateway inputs are outside pilot eligibility, not silently dropped.
- Generate and build-compile a single-resource Bicep template from the source-aware bridge and implement an internally opted-in execution adapter for a standalone Standard, static IPv4 Public IP in an existing resource group.
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
- Upstream TypeSpec-to-Bicep integration and correspondence generation are build-time tooling only. Pin the source/package and compatible TypeSpec/ARM libraries, verify real legacy Public IP support, and expose any missing upstream correspondence seam explicitly rather than silently falling back to handwritten mappings.
- Network customization/execution integration and focused tests may change; generated files must only change through the existing generator. The current Network/Resources projects depend on shared runtime/core, not each other, so transport reuse must not introduce an implicit feature-module dependency or duplicate deployment logic.
- Build tooling will separately pin upstream schema-generation code and a verified Bicep CLI, then package compiled ARM JSON. Bicep type-definition generation is not deployment-template compilation. No runtime TypeSpec/Bicep compiler, compiler-core API or provisioning SDK is proposed.
- Actual extraction findings and unchecked gates are recorded in [design.md](design.md). This proposal does not claim a successful complete Public IP audit or live deployment.
- Deployment write/read permissions, deployment records and transport requests differ from the direct create path. These are intentional only inside the explicit pilot opt-in and must be documented/tested before activation.
- No resource group, VM, NIC, VNet, Public IP prefix or DDoS plan is created by this pilot. No Azure resources are created while preparing this proposal.
