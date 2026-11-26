## 1. Real selected-version source and catalogue gate

- [x] 1.1 Add a focused legacy ARM fixture reproducing the official operation association with absent modern resource registration, including an ambiguous association rejection case.
- [x] 1.2 Fix deployment discovery using supported selected-version ARM lifecycle/operation-list and model identities; preserve modern fixtures, source scope/name binding validation and disabled IR equality.
- [x] 1.3 Re-run extraction from the exact pinned Network source at `2025-07-01`; record complete Public IP graph/provenance or explicit remaining blockers, without silently skipping failed resources.
- [x] 1.4 Run the full Public IP catalogue audit at the same resource/API version, resolve narrowly evidenced comparison gaps with focused tests, and retain incompatible/unsupported cases as activation blockers.
- [x] 1.5 Obtain review for any required target-selection boundary or remaining contract mismatch before advancing to backend activation; do not synthesize empty-facet metadata or change API versions to force a pass.
- [x] 1.6 Add validated explicit resource selection and writable-schema facet closure with unchanged canonical IR, exact-version selection provenance and generated reader/schema support.
- [x] 1.7 Prove scope isolation, selected failure/ambiguity handling, nested/inherited/recursive facets and read-only output preservation, then re-run the selected pinned Public IP source/audit gate.
- [x] 1.8 Obtain review for the remaining writable-root-identity and Int32-preserving binding strategy before authoring/activating the backend; retain full-schema diagnostics separately from the approved, tested command-level eligibility/binding proof.
- [x] 1.9 Implement the offline pilot input-binding fixture: reject any non-null root ID and unsupported fields, preserve normalized Int32 timeout values and optional omission through the existing deployment serializer, reject nested NAT Gateway input, and prove ordinary direct-ID behavior remains unchanged.

## 1A. Upstream TypeSpec-to-Bicep correspondence (next, before Section 2)

Task 1.9 remains completed prototype evidence, not the long-term mapping source. Complete 1.10-1.16 in order before Section 2; retain historical extraction/audit evidence, not redundant implementations.

Task 1.10 is complete through the approved pinned-source route: isolated locked tooling, minimal upstream exports/correspondence hooks and fail-closed version-label checking. The original `2025-07-01` upstream gate remains blocked and its evidence is preserved. The user subsequently approved a local newer-source/API experiment and reaffirmed that only Public IP create is in scope. Task 1.11 below explicitly uses that new experimental baseline; it does not certify the old source or upgrade normal configuration. See [design.md](design.md#approved-create-only-2026-experiment).

A separate [supported-toolchain reproduction](design.md#supported-toolchain-genericlegacy-writable-scope-reproduction) confirmed loss of writable scope for a registered generic resource with legacy GET/PUT operations (`None` instead of `ResourceGroup`). The approved [local scope fix](design.md#approved-local-generic-resource-scope-fix) now restores the generated writable scope with actual model/provider/operation checks and isolated per-type scope flags. All pins remain unchanged; seven scope regressions and the original six integration checks pass. The fix does not discover the original unregistered Network resources. The later create-only experiment uses the newer registered resource and official exact-version projection.

- [x] 1.10 Pin upstream `@azure-tools/typespec-bicep` source/package and compatible compiler/HTTP/ARM dependencies; verify the reusable API boundary and source-correlation access. Keep it build-only; if a hook is missing, propose a focused upstream integration change rather than copying the emitter or silently using a parallel schema implementation.
- [x] 1.11 For the explicitly approved local experiment, run the pinned upstream converter against Network source `965a33b4c632fb1aaf5f2ea02e6d603e1c81baa6` at exactly `2026-05-01`; use official version-projected Public IP PUT/resource models, generate only that resource and its schema dependencies, verify writable scope and provenance, and retain the original blocked `2025-07-01` evidence. No whole-module C# build is required for this source/schema gate.
- [x] 1.12 Generate source-aware atomic-parameter-to-Bicep-field bindings from canonical operation/property IDs, containing wire paths and existing flattening metadata joined to upstream resource/property correspondence. Classify scope/name/computed identity and excluded/read-only inputs explicitly; fail on missing/ambiguous mappings rather than matching display names.
- [x] 1.13 Add focused bridge-generation regressions for flattening/aliases, inherited or reused properties in different paths, exact-version selection, scope/name, root versus nested IDs, source constraints/Int32 preservation and explicit unmapped/ambiguous failures; prove disabled/default canonical generation unchanged.
- [ ] 1.14 Replace the handwritten prototype's structural mapping with the generated bridge in the offline fixture, retaining separate normalization/eligibility policy and existing independent expected-value assertions. Re-run direct-ID, unsupported-input, optional-omission and exact integer deployment-serialization tests without changing public commands.
- [ ] 1.15 Produce real Public IP correspondence/coverage evidence against upstream-generated types and the independently pinned compiler catalogue; preserve prior raw audit reports and diagnose version, intentional API/Bicep normalization, local integration or upstream-support differences before advancing.
- [ ] 1.16 After 1.15, retire superseded custom discovery/schema interpretation and redundant facet/audit traversal; remove unused contract fields/readers/tests only after updating all consumers and proving the relevant behavior through the upstream-derived path. Retain canonical input linkage, independent version/provenance/constraint checks, eligibility and historical reports; do not keep a parallel implementation merely to preserve old test counts.

## 2. Verified single-resource build artifact

- [ ] 2.1 After 1.16, pin an official Bicep CLI and verify its identity/resource type association separately from upstream type generation and the catalogue-only report.
- [ ] 2.2 Generate the single resource-group-scoped experimental Public IP template's field/parameter assignments from the source-aware bridge, preserving normalized body values, omission semantics and resource API `2026-05-01`; do not maintain a second handwritten structural mapping.
- [ ] 2.3 Build-compile/package immutable ARM JSON with provenance; validate exactly one declared Public IP, no dependencies/resource group, and explicit failure for missing/invalid assets without runtime compilation.
- [ ] 2.4 Add focused minimal/default and tags/explicit-timeout tests comparing existing canonical request body semantics to template parameters/declaration, including absent optional values.

## 3. Isolated execution adapter and opt-in

- [ ] 3.1 Establish the isolated integration fixture referencing Network and Resources, explicitly registering their runtime services and verifying existing factory/visibility seams without new product feature dependencies.
- [ ] 3.2 Add an internal default-off pilot registration override that preserves public command metadata and routes only explicitly selected fixture invocations to the adapter.
- [ ] 3.3 Wire the generated bridge and tested eligibility policy into the pilot adapter after existing customization normalization and before HTTP; reject advanced/different inputs and detected existing targets before deployment submission.
- [ ] 3.4 Compose the existing atomic RG deployment transport in Incremental mode at its separate transport version, retain repository polling/authentication/error helpers, and avoid copying public deployment orchestration.
- [ ] 3.5 Retrieve the final Public IP through the existing generated GET/output path; surface deployment failure, cancellation and final-GET errors without a fabricated result or direct-backend retry.

## 4. Regression and activation evidence

- [ ] 4.1 Prove default-disabled request/artifact/parameter/output behavior remains unchanged using existing Public IP parameter and scenario fixtures.
- [ ] 4.2 Add dedicated pilot transport recordings/assertions for deployment submission, parameter bodies, polling, final GET and resource output; do not replay direct-PUT recordings as deployment proof.
- [ ] 4.3 Replay the same eligible pilot recording through both CLI and PowerShell with body matching, preserving existing names, aliases, defaults and output shape.
- [ ] 4.4 Cover unsupported pilot inputs, existing target, missing asset, failed/cancelled deployment and failed retrieval with explicit errors/no unintended mutations.
- [ ] 4.5 Run targeted toolkit/runtime/fixture builds and tests, schema/dependency checks when changed, and strict OpenSpec validation; document exact commands/results and any unchecked gates.
- [ ] 4.6 Obtain explicit live subscription/resource-group/location/quota/cost/cleanup authorization before provisioning; leave live tasks blocked until provided.
- [ ] 4.7 After authorization, run bounded owned-resource create/read/delete scenarios for both surfaces and verify additional deployment permissions/failures, existing-resource protection and cleanup ownership.
- [ ] 4.8 Publish evidence, known race/permission/transport limitations and the default-off rollback path for review; do not activate the product default or archive incomplete live gates automatically.
