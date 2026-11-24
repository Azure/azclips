## Context

See [proposal.md](proposal.md) for motivation and scope. The [toolkit](../../../toolkit/README.md) deliberately runs one canonical IR through a compiler-owned linear pipeline. Its TypeSpec worker returns that IR and structured diagnostics, not arbitrary emitted sidecars.

Current IR preserves operation routes, wire names, model references, lifecycle visibility and API versions, but does not describe ARM deployment identity or retain primitive constraints and sensitivity. The upstream [native Bicep emitter](https://github.com/Azure/bicep-types-az/tree/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep) is useful prior art, but its inspected version selector and numeric/name handling are not sufficient grounds for a production dependency.

The existing resource-group deployment processor is pure deployment transport. It is neither a resource-schema source nor appropriate transport for subscription-scoped resource-group creation. Neither processor changes nor transport selection belong in this change.

## Goals / Non-Goals

**Goals:**

- Make deployment metadata traceable to the same version-specific source as the existing HTTP contract.
- Keep descriptors small by referencing canonical operations, types and properties.
- Make an offline catalogue comparison reproducible and explicit about unsupported shapes.
- Keep default generation and all product command surfaces unchanged.

**Non-Goals:**

- Generate or author Bicep command implementations.
- Adopt AVM defaults, reconstruct legacy workflows, or change customization.
- Compile templates, replace the compiler's embedded catalogue, or certify runtime parity.
- Add backend flags, production pilots, live Azure tests or runtime dependencies.

## Decisions

### 1. Add an optional deployment section to the existing IR

Introduce a typed, optional deployment-contract section, absent unless explicitly requested through the existing emitter-option mechanism. It contains resource descriptors, supplemental property facets and generation provenance. An enabled run with no eligible resources has an explicitly present empty resource collection, distinct from disabled generation.

Use the existing IR schema/version discipline and update directly dependent TypeScript and .NET readers, reference validation and transforms together. Existing documents without the section remain valid; current generated C# ignores the section and produces the same code.

**Rationale:** This follows the single-IR pipeline and existing IPC contract. A separately emitted sidecar would require new artifact transport, lifetime and synchronization rules before it could reliably reference canonical IR.

### 2. Extract from the explicitly selected TypeSpec version

Require a concrete API version for opt-in generation. Preserve existing behavior for `latest`, `all` and omitted versions when the feature is disabled; reject those selectors when deployment-contract generation is enabled.

Use official ARM discovery and version-projection APIs against the same effective source version used by TCGC. Match discovered request models, operations and source properties to the emitter's canonical identities. Never select the final `Versions` member, infer resource identity from class names, or obtain a different specification snapshot.

If the resolved toolchain cannot provide a consistent projection or binding, report a source-located diagnostic and fail opted-in generation. Do not substitute a broader model or another API version.

### 3. Reference contracts instead of copying schemas

Each descriptor identifies the create-or-update operation, ARM resource type, API version, writable scope and ordered resource-name/scope bindings. Request and response types reference the existing operation contract. Distinct resource routes must not be collapsed merely because they share a model.

Supplemental facets reference canonical source-property identities and retain constraints and sensitivity that current IR omits. Preserve wire names, integer/numeric distinctions, nullability, lifecycle and enum extensibility through their existing contracts. Do not represent floating-point inputs as integers or erase unsupported unions to `any`.

Transforms that rename identities or rematerialize properties must keep references valid. Optional metadata must not silently disappear during serialization or through the .NET generator reader.

### 4. Add a build-only catalogue checker

Provide an explicit audit entry point in the existing toolkit infrastructure. Reuse IR readers and official Bicep type loaders in a build-only .NET component; do not depend on the unsupported compiler-core API or the native TypeSpec-to-Bicep emitter.

Pin the loader/catalogue dependency through repository dependency management. Resolve the catalogue index and referenced definitions locally. Audit only explicitly selected descriptors and reject unknown selections.

Normalize intentional declaration differences: API resource-name path inputs become Bicep `name`, scope inputs become deployment scope, and standardized read-only `id`, `type` and `apiVersion` are not PUT-body omissions. Compare writable property membership, types, requiredness, nullability, constraints, enum openness, discriminator structure and supported scopes. Extra response-only fields do not create a writable-contract mismatch.

Traverse referenced and recursive models with cycle-aware memoization. Compare by wire paths and structural meaning, not logical C# names or catalogue-local type indices. Unsupported comparison cases produce an explicit failure result, not compatibility by omission.

### 5. Report catalogue compatibility, not migration approval

Emit a deterministic machine-readable report with source/operation/resource identity, API version, catalogue identity/content digest, resolved toolchain versions, comparison coverage and per-path reasons. Selected results are `compatible`, `incompatible` or `unsupported`; any non-compatible selection causes a non-zero audit exit.

Source provenance identifies the resolved specification commit when available and a content digest of the actual specification inputs. Local fixtures use content identity rather than inventing a commit. Avoid machine-specific paths in stable report identity and include no invocation credentials or secret values.

A catalogue audit does not prove that a particular compiler release embeds that catalogue. Record compiler association only when verified, and state when it was not checked. It also does not prove template correctness, unchanged permissions, error behavior, polling, output mapping or existing-resource semantics.

## Risks / Trade-offs

- **Version projection differs between compiler libraries** -> Prove version alignment with a multi-version fixture before expanding resource coverage; fail explicitly when alignment cannot be established.
- **Additive IR metadata is dropped or references become stale** -> Test parsing and serialization through pipeline boundaries and the .NET reader, including a rename transform.
- **A catalogue is broader, narrower or older than the selected contract** -> Report exact writable-path differences; never downgrade or switch API versions to obtain a pass.
- **The comparison encounters unrepresentable types** -> Return `unsupported` with a reason rather than weakening the contract.
- **Strict comparison limits early applicability** -> Accept that this is a gate, not an automatic migration engine; expand supported cases only with focused evidence.
- **Deployment execution adds permissions and changes transport/output behavior** -> Keep product backends untouched and require a separately reviewed migration with those regression gates.

## Migration Plan

1. Add the optional schema and reference handling without enabling it for any production extension.
2. Implement explicit-version extraction and prove it using committed offline fixtures.
3. Add the pinned, build-only catalogue checker and compatibility/failure fixtures.
4. Verify unchanged default code generation and document opt-in use and limitations.
5. Obtain separate approval for any real command pilot. Disabling the emitter option returns to current generation; there is no runtime migration to roll back in this change.
