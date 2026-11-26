> Implementation update: the custom discovery/emitter options introduced here were retired by [Public IP pilot task 1.16](../pilot-public-ip-bicep-create/design.md#upstream-derived-source-gate-retirement). The audit contract/readers and independent comparison walker remain; current metadata is produced through upstream correspondence. The original artifacts below describe the historical implementation, not instructions to enable the retired options.

## Why

Selected atomic and customized create commands should eventually be able to use Bicep without redefining their API contracts or public command surfaces. Current codegen preserves HTTP contracts but omits some deployment metadata and constraints, while the upstream TypeSpec-to-Bicep emitter has unresolved version-selection and type-fidelity limitations.

## What Changes

- Add opt-in generation of small, typed deployment descriptors from the same explicitly version-selected TypeSpec source used by current codegen.
- Reference existing operation and model contracts instead of introducing a second handwritten resource schema.
- Capture deployment identity, scope and resource-name bindings, plus source-derived validation and sensitivity information that current IR does not retain.
- Add a build-time checker for explicitly selected descriptors against a pinned Azure Bicep type catalogue, with structured compatibility results and actionable diagnostics.
- Record source, toolchain and catalogue provenance; reject incomplete or lossy comparisons rather than treating them as compatible.
- Preserve existing codegen outputs when the feature is disabled. Do not change processors, CLI/PowerShell metadata, customization, deployment transport or runtime backend selection.
- Document the new contract boundary, opt-in tooling and limitations.

## Capabilities

### New Capabilities

- `typespec-deployment-contracts`: Optional, version-aligned deployment metadata derived from the existing API specification and linked to canonical IR contracts.
- `bicep-contract-compatibility`: Reproducible build-time comparison of selected deployment contracts with a pinned Bicep resource-type catalogue.

### Modified Capabilities

None. Existing deployment-command requirements and public command behavior remain unchanged.

## Impact

- Toolkit IR, TypeSpec emitter, compiler integration and directly dependent readers/transforms may need additive changes to carry the descriptors without losing their references.
- Build-only compatibility tooling will reuse existing IR infrastructure and official Bicep type loaders where suitable. The native TypeSpec-to-Bicep emitter and unsupported Bicep compiler-core APIs are not required dependencies.
- Focused tests cover explicit API versions, resource identity/scopes, wire semantics, incomplete projections, catalogue mismatches and unchanged default generation.
- No Azure resources are created, no production extension is regenerated in place, and no create command is selected for migration in this change.
- This establishes migration prerequisites only. Authored Bicep templates, result adapters, backend activation and live regression evidence require a subsequent reviewed change.
