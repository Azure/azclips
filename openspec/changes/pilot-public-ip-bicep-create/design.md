## Context

See [proposal.md](proposal.md) for motivation and scope. The prior [deployment-contract change](../add-typespec-deployment-contract/design.md) established opt-in IR metadata and an offline catalogue checker, not a runtime backend.

**Active experiment:** source `965a33b4c632fb1aaf5f2ea02e6d603e1c81baa6`, API `2026-03-01`, explicitly approved after the May catalogue check. Earlier 2025 and May sections below are historical evidence, not the current selector. See [March requalification](#approved-march-experiment-and-independent-audit).

Network selects source commit `90e63e5fe2c596e8bd807aad7dd9d62cee1746dd` and API `2025-07-01` in its [configuration](../../../src/Extensions/Network/aceconfig.yaml). Its generated create processor sends a direct PUT and polls the resource operation. The existing customization constructs the canonical body and applies Standard/Static/IPv4/idle-timeout defaults; the CLI and PowerShell scenarios share a record/playback baseline.

### Actual pre-implementation evidence

The pinned source was acquired in an isolated sparse checkout without changing the existing specs checkout or production Network configuration. Compilation used the toolkit's existing locked libraries and host runtime.

- Enabled deployment extraction returned no IR and one error: `deployment-contract-invalid`, with `ARM resource association is unresolved or ambiguous` for `Microsoft.Network/ApplicationGateways/createOrUpdate`.
- A separate selected-version SDK probe of Public IP found `PublicIPAddress`, `createOrUpdate` lifecycle metadata, the exact resource-group route, a `PublicIPAddress` request body and `ResourceGroup` base scope.
- The Public IP model's official operation list includes that create operation, but `getArmResource` returns no modern registration. The current extractor requires both and therefore excludes this legacy model. Official association information exists; class/route guessing is not a justified fallback.
- The actual `Azure.Bicep.Types.Az` assembly identifies itself as `0.2.972+e899dc9a90`, and its index contains `Microsoft.Network/publicIPAddresses@2025-07-01`.
- **At this initial discovery stage, no complete Public IP structural audit had run or passed.** Later full audit results are preserved below; catalogue presence alone is only a version-availability check. Compiler association, template compilation and live/runtime parity remain unchecked.

### Source-gate implementation and boundary review

The legacy association fix passed 16 deployment-contract tests and advanced full pinned Network extraction past Application Gateway. Extraction then stopped at `SwapResources.create` with unresolved resource-group scope bindings. That operation is a legacy VIP-swap extension beneath a cloud service; interpreting its scope requires more than changing the expected parameter count.

The user approved explicitly scoped Public IP metadata after reviewing schema dependencies versus resources to provision. The normal canonical Network IR remains complete. Deployment metadata records its selected resource/version roots, and supplemental facets follow their complete writable type graph: inherited properties, nested models, arrays/dictionaries, unions/discriminators and recursive references. Read-only fields remain in canonical retrieval/output models but do not contribute writable facets solely through read-only edges.

Generic ID references and full embedded resource-shaped models do not select additional create operations. A future multi-resource workflow must explicitly select its provisioning roots. Unselected operations, including the unsupported VIP-swap scope, are visibly outside this deployment audit. Missing/ambiguous selected roots, selected scope/facet failures and genuine source/compiler errors still fail; no partial Public IP contract is accepted.

### Selected-source and full audit evidence

Selected pinned extraction succeeded with `Microsoft.Network/publicIPAddresses@2025-07-01`, 1,060 canonical models, 768 operations and four supplemental facets. A separate disabled extraction was deeply equal to the entire selected document after removing only `deployment`. No command/model/output generation input was pruned or changed.

The initial complete [audit report](public-ip-audit.json), preserved unchanged as the baseline, records the exact selection, source/tool/catalogue identities, IR SHA-256, coverage and diagnostics. Its status is **unsupported**, exit code **1**: 102 compared paths, two recursive references and 17 unsupported paths. The 19 diagnostics comprise 16 unsupported catalogue-union comparisons, one writable top-level identity (`$.id`) and two integer-range differences (Public IP and nested NAT Gateway idle timeouts).

At this extraction stage, the selection boundary was approved but the newly measured contract differences were **not** approved exceptions. Subsequent normalization and command-level decisions are recorded below. Intrinsic integer widths must not be silently widened, and narrowing the runtime case never turns the full report into schema compatibility.

Validation used the pinned SDK `10.0.404` and existing locked toolchain:

| Command / check | Result |
| --- | --- |
| `npm run build` from `toolkit` | Passed |
| `npm exec -- vitest run packages\ir\test\deployment.test.ts packages\typespec-emitter\test\deployment.e2e.test.ts` from `toolkit` | 34 passed before the final embedded-resource case |
| `npm exec -- vitest run packages\typespec-emitter\test\deployment.e2e.test.ts -t 'conflicting legacy\|embedded resource'` from `toolkit` | 2 passed, including selected ambiguity and the added embedded-resource case |
| `npm run schema --workspace=@azure-client-engine/ir` and `npm run schema:check` from `toolkit` | Generated; drift check passed |
| `dotnet run --project tools\AceIrModelGen --no-restore -- --out src\Model\Ir\Ir.Generated.cs --namespace Ace.Generator.Model.Ir` from `toolkit\packages\generator` | Generated reader |
| `dotnet test toolkit\packages\generator\test\Ace.DeploymentAudit.Tests\Ace.DeploymentAudit.Tests.csproj --no-restore --filter FullyQualifiedName~CompatibilityAuditTests --verbosity minimal` | 17 passed |
| `dotnet test toolkit\packages\generator\test\Ace.Generator.Tests\Ace.Generator.Tests.csproj --no-restore --filter 'FullyQualifiedName~IrModelDeserializationTests\|FullyQualifiedName~IrFidelityTests' --verbosity minimal` | 12 passed |
| Real host-runtime selected and disabled Network extraction at the pinned source/API | Passed; complete canonical IR equality |
| `node toolkit\packages\generator\tools\Ace.DeploymentAudit\run.mjs --ir <selected-ir> --catalogue Azure.Bicep.Types.Az@0.2.972 --select Microsoft.Network/publicIPAddresses@2025-07-01` | Completed, exit 1 / unsupported; report preserved |
| `openspec validate pilot-public-ip-bicep-create --strict` and `git diff --check` | Passed |

There are 64 distinct targeted tests across these runs; the final focused run repeats one earlier ambiguity test. No new dependency, public command surface, generated Network processor or live Azure resource was added.

### Audit-gap resolution and remaining binding review

All sixteen failed catalogue unions were inspected through the pinned official loader, not inferred from the SKU example. Each contains one unconstrained, non-sensitive `StringType` plus string literals. All corresponding canonical enums have `isFixed: false`. Their allowed values are exactly those of the string branch, so the auditor now compares them as open strings after the independent nullability check. It still rejects closed-source-enum widening, lost source constraints/sensitivity and constrained, sensitive or mixed-type catalogue unions. No API/model/emitter metadata was changed.

The [post-normalization report](public-ip-audit-after-union-normalization.json) has **118 compared paths**, **two recursive references**, **one unsupported path** and **three diagnostics**, still **unsupported / exit 1**. All sixteen former union paths are compared with no diagnostics. The rerun verified unchanged IR SHA-256, catalogue SHA-256 and complete source provenance against the baseline.

The three remaining diagnostics are evidenced API/declaration differences, not union-walker limitations:

- **Root `$.id`:** canonical input is optional string with no read-only visibility annotation. The generated [create processor](../../../src/Extensions/Network/Processors/CreateOrUpdatePublicIPAddressesProcessor.cs) exposes a writable `Id` forwarded into the body, and [PublicIPAddress](../../../src/Extensions/Network/Models/PublicIPAddress.cs) serializes it as `id`. Existing customization does not normalize the root ID. The exact catalogue entry contains `id` with `ReadOnly, DeployTimeConstant` flags. An optional writable body input cannot be silently removed or reclassified read-only; a source-semantic correction or explicit value-preserving identity binding needs evidence and review.
- **Both idle timeouts:** canonical primitive is `int32`, and generated processor inputs are `int?`. The Public IP and nested NAT Gateway catalogue integers have no explicit bounds. [Bicep integers are 64-bit](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/data-types#integers), with resource-specific limits applied separately. The auditor retains the source range `[-2147483648, 2147483647]` versus the catalogue's effective `[-9223372036854775808, 9223372036854775807]` and now prints both ranges. A future binding must preserve source bounds rather than accept the catalogue's broader range; typed input/bounded-parameter enforcement is a design candidate, not a completed compatibility proof.

Validation:

| Command / check | Result |
| --- | --- |
| `dotnet test toolkit\packages\generator\test\Ace.DeploymentAudit.Tests\Ace.DeploymentAudit.Tests.csproj --no-restore --filter FullyQualifiedName~CompatibilityAuditTests --verbosity minimal` | 31 passed; builds generator, native audit and tests |
| `node toolkit\packages\generator\tools\Ace.DeploymentAudit\run.mjs --ir <preserved-selected-ir> --catalogue Azure.Bicep.Types.Az@0.2.972 --select Microsoft.Network/publicIPAddresses@2025-07-01` | Completed, exit 1 / unsupported; exact counts, all sixteen resolved paths and unchanged provenance verified |

The baseline strict comparison was not weakened to accommodate a proposed adapter. At this stage the remaining identity/integer bindings required review before changing the proof model or authoring the backend; the later approved command-level proof is recorded below. No root product solution build, source re-extraction, compiler or live run was needed or performed for this audit-only change.

### Pinned upstream root-ID investigation

The first reviewed next step was authoritative source investigation while retaining the exact-schema gate, before the later command-level binding decision. At source commit `90e63e5fe2c596e8bd807aad7dd9d62cee1746dd`:

1. [Common Resource](https://github.com/Azure/azure-rest-api-specs/blob/90e63e5fe2c596e8bd807aad7dd9d62cee1746dd/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp#L1083) declares `id?: string` without lifecycle visibility, while `name` and `type` explicitly have `@visibility(Lifecycle.Read)`. [PublicIPAddress](https://github.com/Azure/azure-rest-api-specs/blob/90e63e5fe2c596e8bd807aad7dd9d62cee1746dd/specification/network/resource-manager/Microsoft.Network/Network/Common/PublicIPAddress.tsp) inherits that Resource and does not override `id`.
2. The selected [createOrUpdate operation](https://github.com/Azure/azure-rest-api-specs/blob/90e63e5fe2c596e8bd807aad7dd9d62cee1746dd/specification/network/resource-manager/Microsoft.Network/Network/Network/PublicIPAddress.tsp) uses `LegacyOperations.CreateOrUpdateAsync<PublicIPAddress>` without a request-model override. The locked ARM library `0.68.0` defaults `Request = Resource` and passes it through `@armBodyRoot`; there is no declared root-ID exclusion.
3. Exact `2025-07-01` [virtualNetwork Swagger](https://github.com/Azure/azure-rest-api-specs/blob/90e63e5fe2c596e8bd807aad7dd9d62cee1746dd/specification/network/resource-manager/Microsoft.Network/Network/stable/2025-07-01/virtualNetwork.json) independently confirms PUT `PublicIPAddresses_CreateOrUpdate` takes `#/definitions/PublicIPAddress`, which inherits `./common.json#/definitions/Resource`. That [Resource schema](https://github.com/Azure/azure-rest-api-specs/blob/90e63e5fe2c596e8bd807aad7dd9d62cee1746dd/specification/network/resource-manager/Microsoft.Network/Network/stable/2025-07-01/common.json) contains string `id` without `readOnly`, while `name` and `type` have `readOnly: true`. The IR therefore reflects the pinned request schema; this is not evidence of a lost visibility annotation in extraction.
4. Pinned [default-create](https://github.com/Azure/azure-rest-api-specs/blob/90e63e5fe2c596e8bd807aad7dd9d62cee1746dd/specification/network/resource-manager/Microsoft.Network/Network/stable/2025-07-01/examples/PublicIpAddressCreateDefaults.json) and [customized-create](https://github.com/Azure/azure-rest-api-specs/blob/90e63e5fe2c596e8bd807aad7dd9d62cee1746dd/specification/network/resource-manager/Microsoft.Network/Network/stable/2025-07-01/examples/PublicIpAddressCreateCustomizedValues.json) examples omit root request `id` and return the URI-derived ID. These examples establish successful omission, not equivalence for caller-supplied IDs.

The Learn create/update link requested with `view=rest-virtualnetwork-2025-07-01` currently redirects to `2026-03-01`; that rendered page must not be treated as exact-version evidence. No authoritative rule was found establishing whether the pinned service ignores, rejects or requires equality for a caller-supplied root ID that differs from the PUT URI. Generic resource conventions and examples with omitted IDs do not resolve that question.

Consequently, do not modify canonical IR, mark root `id` read-only or silently remove it from the request contract. Supporting caller-supplied root IDs would still require service-contract clarification or separately authorized behavioral validation for absent, matching and conflicting values. The approved pilot instead rejects supplied root IDs, as described below. Nested ID references remain a separate concern and must not be removed by a root-identity rule. No Azure request or live resource was used in this investigation.

### Approved command-level input proof

The user approved keeping root API `id` in canonical IR/generated processors without creating a Bicep parameter/assignment, rejecting supplied root IDs in the pilot, and preserving existing Int32 inputs/normalization. This explicitly changes the readiness proof from identical source/catalogue schemas to a complete raw audit plus reviewed, executable command-level handling of each measured difference. The raw report remains **unsupported / exit 1**, not a compatibility pass.

The unregistered [input-binding fixture](../../../tests/Cli.Tests/Fixtures/PublicIpBicepInputBinding.cs) lives only in `Cli.Tests`, which already references Network and Resources. It consumes the JSON body serialized after the real Public IP customization, returns the existing Resources `DeploymentParameter` model, and has no HTTP/host registration or product dependency changes. It is an offline input-binding component for the future adapter, not an execution adapter or a compiled Bicep asset.

| Raw diagnostic | Reviewed pilot treatment | Verified evidence |
| --- | --- | --- |
| Writable root `$.id` | Keep API field; no Bicep mapping; reject every non-null supplied root ID before transport | CLI/PS command metadata has no root-ID/raw-body binding; normal direct request omits ID; empty, whitespace, matching and conflicting IDs are rejected without HTTP or value removal; direct processor still sends those values |
| Public IP timeout Int32 versus Int64 | Preserve the normalized Int32 value as an exact JSON integer, without wider input acceptance or independent defaults | Compare real direct PUT body against parameters serialized by the existing atomic RG deployment processor; omitted/nonpositive values retain existing default 4, positive values through Int32 max retain their value; broader integer inputs are rejected |
| Nested NAT Gateway timeout Int32 versus Int64 | Entire NAT Gateway input is ineligible for this first pilot | Binding rejects the nested object explicitly without HTTP, including a supplied nested timeout; source schema and raw diagnostic remain unchanged |

The binder validates allowed fields at the envelope, SKU and properties levels. It rejects unsupported fields (including nested ID references), rather than generically deleting any property named `id` or dropping unhandled values. Nested references remain available in the direct processor, and final GET/output IDs are unaffected. SKU/allocation/IP version values and optional Regional tier/tags keep their normalized values; absent tier/tags stay absent from the parameter map.

[Focused tests](../../../tests/Cli.Tests/PublicIpAddressParameterTests.cs) use the existing protected normalization/serialization seam and real `NewResourceGroupDeploymentProcessor` with fake HTTP. The transport checks exercise its actual parameter serialization (and normal final GET), not Bicep compilation or Azure validation. Int32 representation-boundary tests are not claims that the service accepts every Int32 timeout, nor do they relax PowerShell validation.

Validation on pinned SDK `10.0.404`:

| Command | Result |
| --- | --- |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter FullyQualifiedName~PublicIpAddressParameterTests --verbosity quiet` | 59 passed; builds the test project and its existing dependencies; unrelated pre-existing CA1416 warnings remain |
| `dotnet test toolkit\packages\generator\test\Ace.DeploymentAudit.Tests\Ace.DeploymentAudit.Tests.csproj --no-restore --filter FullyQualifiedName~CompatibilityAuditTests --verbosity minimal` | 31 passed, including open-union and retained strict identity/integer diagnostics |

Only the offline input proof is implemented. The fixture is a handwritten prototype/regression baseline, not the long-term source of structural mappings; the approved upstream-derived bridge below must replace that part before adapter/template integration. Source integer bounds and root-ID rejection remain required. Compiler association, artifact checks, complete adapter/transport behavior, both-surface playback, existing-resource protection and separately authorized live evidence remain pending. Generated Network code, TypeSpec extraction, public command metadata and the complete audit reports are unchanged.

### Approved upstream-derived mapping direction

Reuse `@azure-tools/typespec-bicep` for TypeSpec-to-Bicep schema interpretation and derive the command-to-resource bridge from the common source. Avoid maintaining our own competing Bicep schema converter or an independent handwritten field-mapping table. The pinned-source reuse seam is now built/tested below; real selected-version Public IP generation and command bindings remain unimplemented.

The initially inspected upstream reference, now pinned for the isolated source build, is commit `754a33f2ddc902afe583acb6e46316afbd59bc5d`. Its [resource discovery](https://github.com/Azure/bicep-types-az/blob/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep/src/resources.ts) retains TypeSpec PUT/GET models and the name property in intermediate resource definitions. Its [type generator](https://github.com/Azure/bicep-types-az/blob/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep/src/type-generator.ts) converts those into Bicep type definitions, including standardized read-only root identity fields. The original source-level exports do not constitute a stable packaged API; our explicitly reviewed integration patch is described below.

```text
                         pinned TypeSpec source/API
                            /                \
               existing command codegen   upstream typespec-bicep
                          |                    |
                canonical input IDs,       Bicep resource types,
                flatten/container paths    source correspondence
                            \                /
                         generated input bridge
                            /                \
                 parameter-binding code   pilot Bicep template
                          |                    |
                   runtime values       pinned Bicep CLI
                                               |
                                         packaged ARM JSON
```

The bridge is derived at build time; runtime values flow through existing command binding and customization before generated parameter binding. The upstream emitter produces resource-type definitions/documentation/examples, not the command-specific parameterized deployment template or its runtime adapter. Keep those responsibilities explicit.

Join canonical operation/property IDs and existing flattening container chains to upstream source correspondence at the same selected version. Reuse shared TypeSpec node identities when possible; if projection or separate compilation changes object identity, establish verified source-origin correspondence rather than matching display names. Include the operation/resource/version and complete containing wire path because inherited/reused properties can occur at multiple locations with different roles. Our [flattening support](../../../toolkit/packages/generator/src/Generation/Generators/FlattenSupport.cs) already resolves canonical properties and their container hops without replacing wire truth.

| Atomic input example | Generated bridge role |
| --- | --- |
| Subscription/resource-group inputs | Deployment target scope, not body fields |
| `PublicIpAddressName` | Required resource `name` |
| `SkuName` | Writable `sku.name`, resolved through the canonical container chain |
| `IdleTimeoutInMinutes` | Writable `properties.idleTimeoutInMinutes`, with existing Int32 input semantics |
| Root body `Id` | Retained API field, excluded by pilot eligibility; no Bicep assignment |
| Nested reference ID | Separate writable reference path when supported; currently excluded advanced pilot inputs fail rather than disappearing |

Prefer consuming a reusable pinned upstream API with a narrow source-correlation seam. Verify its actual exports/toolchain compatibility first. If the necessary seam or legacy-resource support is unavailable, report the blocker and review a focused upstream contribution or integration adaptation; do not silently fork/copy the emitter or fall back to a parallel handwritten schema implementation. Existing extraction/audit code remains as proven baseline and canonical-link infrastructure; replace overlapping schema interpretation only after upstream reuse and regression evidence establish equivalence.

Keep schema interpretation, structural binding and command policy separate:

- Upstream TypeSpec-to-Bicep code supplies Bicep resource shapes and normalization.
- The generated bridge supplies canonical input-to-field correspondence and explicit scope/name/computed/excluded roles.
- Existing command metadata and customization supply aliases, normalization/defaults and reference resolution. Pilot policy rejects non-null root IDs and unsupported inputs; transport/output remain our existing runtime responsibilities.

The handwritten fixture's structural mapping will be replaced by the generated bridge, while independent expected-value assertions and its demonstrated policy behavior remain regression evidence. Do not retain it as an alternative production mapping source or merely test the generator against its own generated expectations.

An upstream/local mismatch is not automatically a local bug: root API `id` versus computed Bicep `id` already demonstrates intentional contract normalization. Check exact source/version/toolchain selection, canonical correlation, intended Bicep semantics and possible upstream limitations or defects. Preserve both raw schema diagnostics and reviewed command-level proof. Upstream-generated types and the CLI's bundled catalogue have independent provenance; their association still needs verification, and upstream samples are not a compiled pilot artifact.

### Upstream integration investigation (task 1.10)

Initial inspection of upstream commit `754a33f2ddc902afe583acb6e46316afbd59bc5d` confirmed integration requirements, not a working dependency pin. At that investigation stage no upstream dependency was added, compiler upgraded or existing source-gate code removed.

| Boundary | Verified evidence | Consequence |
| --- | --- | --- |
| Package entry point | The [package manifest](https://github.com/Azure/bicep-types-az/blob/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep/package.json) declares version `0.1.0` and exports only `"."`; the [entry point](https://github.com/Azure/bicep-types-az/blob/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep/src/index.ts) exports `$lib` and `$onEmit`, not resource discovery or type generation | The reusable source-level functions are not exposed through the package API. Do not bypass its export map with filesystem/deep imports. |
| Source correlation | Resource definitions retain PUT/GET models, but the [type generator](https://github.com/Azure/bicep-types-az/blob/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep/src/type-generator.ts) returns `BicepType[]`; its property conversion and model/reference caches are internal, with no correspondence hook | Calling the public emitter yields type files, not a verified source-property-to-emitted-field map. Exporting discovery alone would not complete the bridge. |
| Exact version | The private [version helper](https://github.com/Azure/bicep-types-az/blob/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep/src/resources.ts#L516) selects the last member of a namespace's `Versions` enum; the [options schema](https://github.com/Azure/bicep-types-az/blob/754a33f2ddc902afe583acb6e46316afbd59bc5d/src/typespec-bicep/src/lib.ts) accepts no options | An explicit selected-version projection/integration must be proven. Do not assume a selected SDK API automatically constrains this emitter's program or version label. |
| Legacy source coverage | Resource processing iterates `getArmResources`; HTTP metadata supplements those resources | Successful discovery of the pinned legacy Public IP model is still unproven. Static inspection is not task 1.11 execution evidence. |

The package's declared peer requirements do not accept the currently locked toolkit set:

| Dependency | Upstream peer | Installed toolkit |
| --- | --- | --- |
| `@typespec/compiler`, `@typespec/http` | `^1.15.0` | `1.13.0` |
| `@typespec/rest` | `^0.85.0` | `0.82.0` |
| `@azure-tools/typespec-azure-core`, `@azure-tools/typespec-azure-resource-manager` | `^0.71.0` | `0.68.0` |

The toolkit's installed TCGC `0.68.4` also peers on ARM/core `^0.68.0`. Therefore this is not a safe dependency-only addition to the current compilation context. Prefer evaluating an isolated upstream build-time toolchain before proposing a broad toolkit upgrade. Separate programs would require verified stable source-origin/context correspondence, not object-identity assumptions.

**Review boundary:** choose between awaiting a supported upstream API and using an explicitly pinned upstream source build with narrowly reviewed integration changes. The latter must preserve the upstream conversion implementation, expose discovery/generation and optional source-correspondence metadata, and establish explicit version selection. It must not become an independently maintained schema converter. Correspondence needs to identify source properties/models and emitted references/fields, including synthesized identity roles; containing resource/version/path context must survive caching and reuse. Normal upstream emitter output should remain unchanged when correspondence is not requested.

Publication of the source-declared `0.1.0` package was not verified: both `npm view` and a system-TLS `curl` request to the public registry failed TLS handshakes. TLS verification was not disabled. GitHub source inspection succeeded at the exact revision. Those initial checks did not constitute an upstream build, successful package installation or source-generation test; task 1.10 remained unchecked until the subsequent approved integration below.

### Approved pinned-source integration (task 1.10)

The user approved pinned upstream source with minimal integration patches and an isolated compatible build-time toolchain. The standalone [tool package](../../../toolkit/tools/typespec-bicep/package.json) is deliberately outside the toolkit workspaces and product dependencies:

- [upstream.json](../../../toolkit/tools/typespec-bicep/upstream.json) pins the original repository and full revision. [prepare.mjs](../../../toolkit/tools/typespec-bicep/prepare.mjs) fetches only that source package into an ignored build checkout, rejects unexpected HEAD/source changes, and applies the exact reviewed [integration patch](../../../toolkit/tools/typespec-bicep/integration.patch).
- The patch exports discovery/generation through the upstream package root and adds an optional property observer to the original converter. Records contain actual PUT/GET generation-source properties, emitted property/container references and ordinary/name/computed-identity/discriminator roles. Container references preserve shared/recursive graph context; they are not yet canonical command bindings.
- The package's own [lockfile](../../../toolkit/tools/typespec-bicep/package-lock.json) pins the required build closure without pulling unrelated upstream lint/test tools. It uses compiler `1.17.0`, HTTP/OpenAPI `1.15.0`, ARM/core/TCGC `0.71.0`, REST/versioning/streams/events/SSE/XML `0.85.0`, Bicep types `0.6.129` and TypeScript `6.0.3`. These are separate from the unchanged default toolkit's compiler `1.13.0` and ARM/core/TCGC `0.68.x`.
- Patched sources are staged below the tool's ignored build output and compiled against only its dependency closure; the original MIT license is retained. The [entry point](../../../toolkit/tools/typespec-bicep/index.mjs) resolves the built upstream public package export rather than bypassing internal export paths. Version-label checking rejects missing/mismatched versions; it deliberately does not pretend to project the source program.
- Real source imports SDK decorators from TCGC. Omitting that dependency initially fell through to the parent toolkit and produced incompatible-library warnings; the isolated lock now includes its compatible peer closure. Tests reject mixed-library diagnostics.

Validation on Node `24.22.2`:

| Command / check | Result |
| --- | --- |
| `npm --prefix toolkit\tools\typespec-bicep run setup` | Passed; restores the isolated lock with install scripts disabled and type-checks/builds upstream conversion |
| `npm --prefix toolkit\tools\typespec-bicep run build` from a new upstream checkout | Passed; exact source fetch, patch application and compilation without dependency restoration |
| `npm --prefix toolkit\tools\typespec-bicep test` | 6 passed: original upstream golden output, observational correspondence, reused/recursive PUT/GET context and discriminator provenance, root/nested identity distinction, version mismatch, empty discovery and rejection of unreviewed source additions |
| `npm exec -- vitest run packages\typespec-emitter\test\deployment.e2e.test.ts` from `toolkit` | 23 passed, including disabled canonical equality and legacy/selection/constraint regressions |

Dependency acquisition used Microsoft's public Azure SDK feed and the upstream lock's public feed for `@azure/bicep-types`; direct public npm TLS remained unavailable. An initial attempt to restore all upstream development dependencies encountered an unavailable lint-package feed. The final isolated lock contains only the required build closure; no credentials, TLS weakening or global/toolkit dependency changes were introduced.

### Real-source preflight and task 1.11 blocker

The [real-source probe](public-ip-upstream-probe.json) uses the exact pinned Network commit and the isolated toolchain. It compiles the original Network entry point with **zero errors and no mixed-library diagnostics**. Public IP's `Common.PublicIPAddress` has official lifecycle associations, including `createOrUpdate`, but still has no modern ARM registration. Upstream discovery returns **no Public IP definition**. No class-name/route guess or synthetic descriptor was substituted.

The source also declares versions through `2026-03-01`; upstream's last-enum-member helper is not the requested `2025-07-01` selector. This probe is explicitly **unprojected**, reports **blocked**, and exits **1**. It is not successful selected-version generation or evidence that the whole API is incompatible.

Task 1.11 remains unchecked pending reviewed legacy discovery integration and a verified selected-version program. The existing canonical association infrastructure may still be necessary to supply upstream's missing legacy-resource linkage; its Bicep schema conversion must not be reimplemented. Do not remove the earlier extractor/audit or handwritten fixture until their replacements actually work. Tasks 1.12-1.16, template/compiler/runtime gates and live validation remain pending.

### Supported-toolchain generic/legacy writable-scope reproduction

A later newer-source experiment exposed a separate writable-scope problem after resource registration succeeded. That experiment used ARM/core `0.74.0`, outside upstream's declared `^0.71.0` peer range, so it was not sufficient by itself to establish an upstream defect. The focused reproduction below confirms the problem with the existing supported pins; it does not upgrade the Network source or any dependencies.

The [fixture](../../../toolkit/tools/typespec-bicep/test/generic-resource-scope.tsp) contains one version (`2025-07-01`), a standard `TrackedResource` control and an official `Legacy.GenericResource` with legacy GET/PUT templates. Both have an unambiguous resource-group-scoped PUT. The [reproducer, now a passing regression](../../../toolkit/tools/typespec-bicep/test/generic-resource-scope.test.mjs), verifies actual model identities, registrations, officially resolved resource types, PUT body identities and exact paths before checking upstream output.

Run after the existing tool setup:

```powershell
npm --prefix toolkit\tools\typespec-bicep run repro:generic-scope
```

Before the approved local fix: **one control passed, one regression failed with exit 1.** The assertion expected correct writable scope rather than accepting the bug as expected behavior.

| Case | Registration | Official PUT | Expected writable scope | Upstream writable scope |
| --- | --- | --- | --- | --- |
| Standard control | `Tracked`, collection `standardWidgets` | RG-scoped, matching request model | `ResourceGroup` (`8`) | `ResourceGroup` (`8`) |
| Generic + legacy operations | `Generic`, no registration-level collection name | RG-scoped, matching request model | `ResourceGroup` (`8`) | `None` (`0`) |

Both emitted definitions have readable scope `ResourceGroup`. The generic model even declares its own name/key/segment; the official generic registration deliberately does not require or populate a collection name. This is not a missing PUT, an unregistered model, an ambiguous API selector or a newer-library mismatch.

The original upstream `resources.ts` rejects resolved records when `collectionName` is absent in `resolvedRepresentsModel`. Legacy operations are retained in the older lifecycle API's `lists` collection, so its fallback supplies no writable lifecycle scope. GET-route fallback then exposes the resource with readable scope only. Official resolved-operation metadata already contains the correct PUT and resource path. The evidence supports an upstream generic/legacy discovery defect, not a demonstrated Network API-definition defect.

Reproduction commit `a6474dce16297457d21de7f338d99cee6a32f70c` used compiler `1.17.0`, HTTP/OpenAPI `1.15.0`, ARM/core `0.71.0` and REST/versioning `0.85.0`, against upstream revision `754a33f2ddc902afe583acb6e46316afbd59bc5d`. `npm ls` reported a valid deduplicated dependency tree. At that point upstream `resources.ts` was byte-for-byte unchanged relative to the revision; the integration patch only touched exports and observational source correspondence.

The reproduction was initially opt-in and separate from the six passing `npm test` checks. That evidence established the defect; it did not prove real selected-version Public IP generation or complete task 1.11.

### Approved local generic-resource scope fix

The user approved a scope-only local patch with **all upstream, Network source and dependency pins unchanged**. The reviewed [integration patch](../../../toolkit/tools/typespec-bicep/integration.patch) now also adapts upstream resource discovery and its scope helper; the schema converter and its existing property-correspondence hook remain the implementation of field/type generation.

- Generic records must reference the actual registered model and provider. An exact resolved instance operation must supply a successful resource response or a matching write-body model; error responses and unrelated read-styled actions are not resource-identity evidence. Existing collection-name matching for standard resources is unchanged.
- Read/write scopes come from each verified instance's lifecycle operations and actual paths. Records are grouped only by the same resource type, merging readable and writable flags independently. A subscription GET cannot make that scope writable, and a PUT cannot make a different read-only type writable.
- Generic PUT schema selection uses only those verified PUT operations, not the global type-segment-only HTTP lookup. Distinct PUT/GET body models are supported; missing or conflicting PUT request models produce an explicit diagnostic and skip that resource type rather than selecting one.
- An unknown generic scope is diagnosed and skipped instead of becoming resource-group scoped. Existing standard callers retain their original scope fallback.
- Git preserves the integration patch's exact bytes, including CRLF context from the upstream scope-helper file. Text normalization would corrupt that context and break clean application/source verification after checkout.

The [additional fixture](../../../toolkit/tools/typespec-bicep/test/generic-resource-guards.tsp) covers a reused model with an RG PUT/subscription GET, a separate read-only type, an unrelated successful response with the resource appearing only in an error body, a foreign provider, distinct PUT/GET models, conflicting PUT models and an unknown scope. The original fixture also verifies the **serialized generated Bicep resource type**, not just discovery metadata: generic readable and writable scopes are now both `8`.

The seven scope checks are included in the default suite alongside the original six checks. Test files run serially because the source-integrity check intentionally creates a temporary unreviewed upstream source file.

| Command / check | Result |
| --- | --- |
| `npm --prefix toolkit\tools\typespec-bicep run build` | Passed; isolated upstream source type-check/build, exact patch verification |
| `npm --prefix toolkit\tools\typespec-bicep run repro:generic-scope` | 7 passed; exits 0 |
| `npm --prefix toolkit\tools\typespec-bicep test` | 13 passed, including original upstream golden output and source-integrity checks |
| Pristine pinned-source patch application with `core.autocrlf=false`, matching `prepare.mjs` | Passed; all four patched source files match the built checkout byte-for-byte |
| `git diff --exit-code HEAD -- toolkit\tools\typespec-bicep\upstream.json toolkit\tools\typespec-bicep\package-lock.json` | Passed; upstream and dependency pins unchanged |
| `openspec validate pilot-public-ip-bicep-create --strict` | Passed |

This unblocks the reproduced registered-generic scope defect, **not the original unregistered Network source**. No Network discovery hook, source/toolchain upgrade, selected-version integration, command binding, template or runtime change is included. Task 1.11 and the later pilot gates remain pending; this local patch has not been submitted upstream.

### Approved create-only 2026 experiment

The user approved Network source `965a33b4c632fb1aaf5f2ea02e6d603e1c81baa6` and resource API `2026-05-01` for a local experiment, using the same API for canonical command inputs and Bicep schema generation. The normal [Network configuration](../../../src/Extensions/Network/aceconfig.yaml), toolkit dependencies and supported private Bicep-tool lock remain unchanged. The earlier `2025-07-01` source/audit reports above remain historical evidence; their blocked upstream-generation gate has not passed.

The user then reaffirmed **Public IP create only**, not a whole Network or Compute migration. The ARM operation is the officially associated `createOrUpdate` PUT. Its schema dependency graph may contain embedded models and read-only information, but it does not select other resource-create operations. The converter's selected response model supplies resource/read-only schema information; no read/list/delete command implementation is generated by this probe.

A broader preliminary command-generation attempt emitted 1,162 models, 838 operations and 2,002 C# files. Its full-module build exposed C# CS0542 collisions in unrelated Bastion session and Load Balancer migration types. Regenerating the preserved 2025 IR produced the same two C# files byte-for-byte, confirming a pre-existing naming limitation. Those fixes and whole-module C# qualification are outside this create-only schema task; neither is a prerequisite or a claimed pass.

The opt-in [probe](../../../toolkit/tools/typespec-bicep/project-public-ip.mjs) now:

- Checks the exact source Git revision and an unmodified tracked checkout, the tested isolated dependency versions, the existing reviewed upstream patch and its source integrity.
- Builds the same upstream converter beneath the experimental dependency closure. That closure uses compiler/HTTP/OpenAPI `1.18.0`, Azure core/ARM/TCGC `0.74.0`, REST/versioning `0.88.0` and Bicep types `0.6.129`. It is still outside upstream's declared `^0.71.0` ARM peer range, not upstream-certified compatibility.
- Resolves the resource with the public `resolveArmResources(program, { version: "2026-05-01" })` API. It requires one exact Public IP association and one actual instance PUT with a model body. No fake Program, version-label-only projection, handwritten route parser or schema converter is used.
- Retains the upstream resource descriptor, substitutes the actual projected PUT/resource models, and correlates the projected name property through its source node. Missing or ambiguous source/version/name/create associations fail explicitly.
- Supplies only that resource to upstream `generateTypes`, with empty action/provider-operation selections. It writes Bicep type-definition JSON, property-source correspondence and a provenance/diagnostic report. This is not JSON Schema, a deployment template or command-binding code.

The real run emitted **one** `Microsoft.Network/publicIPAddresses@2026-05-01` resource, readable/writable resource-group scope `8`, **557** type definitions and **533** property-source correspondence records. Neither unrelated collision model occurs in the correspondence. The type-definition JSON SHA-256 is `b40dc36e568754305cbb4a9fa35dc655be21fe81204ae921d38eb97faf69db9e`; the tested experimental dependency-lock SHA-256 is `78fd25723a37ce2f0d656e7f86d9af662f0460df5bc090dfba4f46c30d3897b5`.

Whole-source upstream discovery reports 131 `no-resource-scopes` warnings and one `missing-api-version` warning, preserved in the report. Compilation/conversion has no errors or mixed-library diagnostics, and the selected Public IP root is resolved and generated. This does not certify the rest of Network.

Reproduce with the existing isolated newer closure and source checkout, without dependency restoration:

```powershell
$toolchain = 'C:\workplace\azure-ps-workspace\azclips\toolkit\tools\typespec-bicep\dist\newer-network-probe'
$source = Join-Path $toolchain 'source'
npm --prefix toolkit run build --workspace @azure-client-engine/typespec-emitter
node --test toolkit\tools\typespec-bicep\test\projected-create.test.mjs
node toolkit\tools\typespec-bicep\project-public-ip.mjs $toolchain $source
npm --prefix toolkit\tools\typespec-bicep test
```

The experimental test defaults to that tool-local cache; set `AZCLIPS_NEWER_TSP_ROOT` for a different prepared closure. It fails if the required dependencies are missing or mismatched; it does not restore packages, silently use the normal older libraries, or join the default supported-toolchain suite. Outputs live below `<toolchain>\public-ip-create`.

| Validation | Result |
| --- | --- |
| Experimental projected-create tests | 3 passed: latest-version removal and single-resource scope, invalid/ambiguous/missing-create rejection, ordinary canonical emitter loading with the newer ARM public API |
| Actual pinned-source create-only probe | Passed with the schema/provenance counts above |
| Existing supported-toolchain upstream tests | 13 passed, including original golden output and scope/source-integrity regressions |
| Existing canonical deployment tests | 23 passed, including unchanged disabled generation and original ARM parser behavior |

The focused canonical-emitter change accommodates the old `getResourcePathElements` and newer `parseArmResourceInstancePath` public exports without copying either parser. It is necessary to load canonical command generation under the experimental library, even when supplemental deployment metadata is disabled.

Task 1.11 is complete for this explicitly approved newer-source experiment only. The 533 source records are not 533 command bindings. The following bridge step accounts for the selected command separately. Create customization compatibility, an independent 2026 catalogue audit/compiler association, template compilation, runtime transport and live gates remain unproven. No public registration, normal source pin, generated product C#, runtime backend or Azure resource changed.

### Source-aware Public IP create bindings

Tasks 1.12 and 1.13 are complete. The [bridge generator](../../../toolkit/tools/typespec-bicep/create-bindings.mjs) joins actual canonical source observations to the upstream property-source records. The opt-in probe runs the ordinary canonical emitter on the same real Program, then the existing command transforms with only C# generation disabled. It retains the normal Network flatten setting `noInline: ["IPConfiguration"]`; it does not prune canonical models/operations or reproduce command generation.

The optional [canonical observation API](../../../toolkit/packages/typespec-emitter/src/context.ts) records model, operation, property and raw parameter objects at their actual canonical IDs. Ordinary `$onEmit` delegates to the same implementation without an observer. A regression compares the entire ordinary and observed IR, including disabled supplemental deployment metadata. No observation records or new fields are added to canonical IR.

The join validates the selected canonical operation, official PUT instance path, API version, request model and single atomic command. It follows each parameter's property ID and `via` container chain, verifies the containing source model at every step, and matches upstream PUT properties only within the corresponding Bicep container. Shared declaration nodes link projected clones, but model ownership and generic template arguments must also agree; declaration-node equality alone would incorrectly conflate distinct `ResourceInstance` instantiations. Aliases and renamed parameter/property display names do not determine the wire or Bicep paths.

Scope/name handling is deliberately limited to one top-level resource-group resource with the three officially resolved instance path parameters. Resource-name correspondence remains distinct from the two deployment-scope inputs. Synthesized `content-type` and `accept` headers have protocol roles, not fabricated source properties. Missing/ambiguous field records, broken containing paths, version mismatches and unsupported union-container traversal fail explicitly rather than dropping inputs or guessing branches.

The bridge carries canonical type, required/nullable/default metadata and source constraints through the existing source-facet helper. Bicep primitive widening never replaces the canonical Int32 domain. Computed root ID remains an explicit rejected input; ordinary nested IDs remain ordinary mapped fields. Read-only Bicep fields are recorded separately and are not turned into command inputs.

The real `2026-05-01` run writes `bindings.json`, with **all 43** `CreateOrUpdatePublicIPAddresses` inputs accounted for:

| Role | Inputs |
| --- | --- |
| Deployment scope | 2 |
| Required resource name | 1 |
| Protocol headers | 2 |
| Computed root identity, rejected | 1 |
| Ordinary fields | 37 |

Seven exact field paths are allowed by the separate pilot field policy: `location`, `tags`, `sku.name`, `sku.tier`, `properties.publicIPAllocationMethod`, `properties.publicIPAddressVersion` and `properties.idleTimeoutInMinutes`. The other 30 ordinary fields are explicitly excluded, including nested NAT Gateway inputs. Eligibility compares actual path segments, not ambiguous dot-joined strings. This is **field eligibility only**: it does not accept unsupported SKU/IP-version values, normalize inputs, apply defaults, reject a concrete invocation or enforce omission. Those existing runtime policies remain independently required.

The output also has 186 read-only field records across the selected type graph; these are neither additional writable command inputs nor fully enumerated root-to-leaf usage paths. Schema SHA-256 remains `b40dc36e568754305cbb4a9fa35dc655be21fe81204ae921d38eb97faf69db9e`. Binding SHA-256 is `dbe24cce0e8e9d5b13b4d5d59a0c6df0bb5b96c0c1b1ed78fa68a338b6848729`, recorded separately from the schema and experimental dependency lock.

All diagnostics remain visible. Canonical emission adds the two known ambiguous-polling warnings and one open-enum warning to the 132 upstream discovery warnings. Command transformation retains intentional `no-generator`, two library model-name collision and two circular-reference warnings. The selected bridge has no unmapped inputs or conversion errors; this is not whole-Network qualification.

| Command / check | Result |
| --- | --- |
| `npm --prefix toolkit run build --workspace @azure-client-engine/typespec-emitter` | Passed; source-observation and reused constraint API type-check/build |
| `node --test toolkit\tools\typespec-bicep\test\projected-create.test.mjs` | 7 passed: actual source joins, inheritance/reuse/flattening/client rename/CLI dialect alias independence, identity roles, constraints, fail-closed mapping, exact-segment eligibility and version projection |
| `node toolkit\tools\typespec-bicep\project-public-ip.mjs $toolchain $source` | Passed; actual pinned source, 43 bindings, unchanged schema hash and seven eligible field paths verified |
| `npm --prefix toolkit\tools\typespec-bicep test` | 13 passed; original upstream golden output, scope and source-integrity checks preserved |
| `npm --prefix toolkit exec -- vitest run packages\typespec-emitter\test\deployment.e2e.test.ts` | 24 passed; includes whole-IR observational equality and existing disabled/legacy parser behavior |

The following offline fixture step proves consumption of this bridge while retaining independent normalization, eligibility, direct-ID, exact integer and omission assertions. JSON generation alone does not prove newer-source customization parity, a compiled template or deployment execution. No normal pins, generated product C#, runtime registration or live resources changed.

### Generated bridge consumed by the offline fixture

Task 1.14 is complete. The probe's optional third argument writes the [generated fixture JSON](../../../tests/Cli.Tests/Fixtures/PublicIpBicepBindings.json). This view retains all 43 canonical input bindings, their source identities, wire/Bicep paths, types and constraint metadata, plus source/upstream revision and schema/binding/toolchain-lock hashes. Read-only output-field records are not duplicated in this input fixture.

The generator adds `deploymentParameter` only to the seven allowed fields and the required resource name. It renders the canonical parameter name with a lowercase initial character and rejects duplicate rendered names. Rendering determines an output identifier, not source-to-field correspondence; there is no handwritten parameter-to-body-field table.

The [test project](../../../tests/Cli.Tests/Cli.Tests.csproj) embeds the checked-in generated JSON. Ordinary .NET tests do not require Node, TypeSpec, a private cache, network access or runtime code generation. The [offline binder](../../../tests/Cli.Tests/Fixtures/PublicIpBicepInputBinding.cs) reads that asset, validates its schema/resource version and unambiguous parameter names, derives permitted containing paths from generated field eligibility, and copies present normalized leaves into the generated parameter names. It skips only absent optional leaves, never supplied unsupported fields. Dictionary-valued tags remain an opaque leaf with the existing string-value policy.

The binder no longer manually assigns location, SKU or `properties` fields. Existing command normalization and separate Standard/Regional, Static, IPv4, tag-shape and Int32 policy remain unchanged. Every non-null root ID still fails; an explicit null root ID stays untouched in the input and is not assigned. Duplicate fields in generated containers, malformed required containers and fields missing from the bridge fail explicitly. Even missing/duplicate names for omitted optional parameters are invalid fixture metadata, not success-shaped omissions.

The independent [Public IP assertions](../../../tests/Cli.Tests/PublicIpAddressParameterTests.cs) remain the expected-value oracle. They compare existing direct/customized bodies with parameter JSON serialized by the real generated resource-group deployment processor on a fake HTTP pipeline, including Int32 upper-bound values, defaulted non-positive timeouts, tags and omitted tier/tags. Added regressions change generated output names to prove that the fixture consumes them, remove a supplied field's bridge entry to prove it cannot be silently dropped, and cover invalid version/path/name metadata and duplicate container properties.

Regenerate with the already prepared isolated closure:

```powershell
node toolkit\tools\typespec-bicep\project-public-ip.mjs $toolchain $source tests\Cli.Tests\Fixtures\PublicIpBicepBindings.json
```

| Command / check | Result |
| --- | --- |
| Probe with the fixture output argument above | Passed; pinned real source, 43 retained bindings and 8 generated parameter names; schema and source-binding hashes unchanged |
| Exact JSON comparison with `createBindingFixture` applied to the real generated bridge | Passed; persisted fixture matches its generator output |
| `node --test toolkit\tools\typespec-bicep\test\projected-create.test.mjs` | 8 passed, including fixture metadata preservation, excluded/computed inputs and rendered-name collision rejection |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter FullyQualifiedName~PublicIpAddressParameterTests -v quiet` | 69 passed, none skipped; repository SDK `10.0.404`, xUnit/VSTest; build succeeded with warnings in untouched projects |

This proves **existing checked-in customization/body behavior bound through the generated 2026 bridge**, not compilation or execution of a newly generated 2026 Network processor. No whole-Network C# qualification, compiler catalogue audit, Bicep template compilation, product adapter, new public command or live deployment is claimed. **Task 1.15 remains next:** independently audit exact-2026 correspondence/coverage against upstream types and the pinned Bicep compiler catalogue before retiring superseded schema machinery.

### Approved March experiment and independent audit

Task 1.15 is complete as coverage/difference evidence, **not activation or identical-schema approval**. The exact pinned `Azure.Bicep.Types.Az` assembly is `0.2.972+e899dc9a90`, SHA-256 `ce08193659d798083d99611f2d2895e8cc8bd27986f72d8b7b8180c77cf42066`. Its newest Public IP entry is `2026-03-01`; it has no May entry. The [NuGet page](https://www.nuget.org/packages/Azure.Bicep.Types.Az) also identifies `0.2.972` as current, and [current upstream Network data](https://github.com/Azure/bicep-types-az/tree/754a33f2ddc902afe583acb6e46316afbd59bc5d/generated/network/microsoft.network) has only January and March 2026. Updating a catalogue pin would not unblock May.

The user explicitly chose to switch only the experiment to `2026-03-01`, retaining the same newer source and all dependency/upstream/product pins. The five May files (types, bindings, correspondence, report and generated offline fixture) are preserved under the prepared toolchain's `public-ip-create-2026-05-01` directory with verified schema identity. Prior 2025 raw reports remain unchanged.

The probe now resolves an exact **registered Version object** through the public versioning API and passes its namespace/version to `resolveArmResources(..., { serviceVersions })`. Only after obtaining the real projected resource/PUT models does it stamp their registered version on the selected upstream descriptor. An earlier-version regression proves removed fields return, later-added fields disappear, projected request models differ and canonical command bindings follow those changes. An inconsistent descriptor label or unknown/unregistered selector still fails. This is not relabeling the May schema.

Actual March output has one resource with RG scopes `8`, **556** type definitions, **532** correspondence records and **43** inputs (same role counts and seven allowed fields). Schema SHA-256 is `fbc5b3487025d1852c09f044e6e7310ae21ed12b77cd8e07377fa4da07a00227`; source-binding SHA-256 is `f2ba1ed98c5798879672a0296d7abc835010de1b68c4a4cfb2f403709cd83a19`. The experimental dependency lock and upstream peer-range caveat are unchanged.

Full selected-contract extraction retains **1,161** canonical models, **838** operations and four source facets. Removing only `deployment` gives deep equality with ordinary March canonical IR. Full-audit mode runs separately from type/binding generation: retaining two large SDK contexts in one process exhausted Node's array-buffer allocation. Isolation fixes that without raising shared-host memory limits or altering the schema.

The existing comparison walker is reused for both:

- [Independent pinned-catalogue report](public-ip-audit-2026-03-01.json).
- [Upstream-generated-type report](public-ip-upstream-audit-2026-03-01.json), loaded by the official `Azure.Bicep.Types` serializer through a small indexed loader. No additional schema converter or comparison traversal is introduced.

Both reports use the same real source IR and record **118 compared paths**, **two recursive references**, **two unsupported paths**, and these four diagnostics. Both remain **unsupported / exit 1**:

| Measured path / gap | Existing pilot treatment and fresh evidence |
| --- | --- |
| `$.id`, writable API identity versus computed/read-only declaration identity | Canonical/direct inputs unchanged; every non-null root ID rejected without removal or HTTP; null stays unassigned |
| `$.properties.idleTimeoutInMinutes`, Int32 versus Bicep Int64 | Existing normalization and exact integer serialization preserved; wider integers rejected |
| `$.properties.natGateway.properties.idleTimeoutInMinutes`, same integer-domain gap | Whole NAT Gateway input explicitly excluded and rejected |
| `$.properties.ipTags[].firstPartyServiceTagId`, source `ArmId` lexical format not provable from catalogue strings | Whole IP-tags input was already outside policy; new tests reject it including a supplied ARM-ID field, retaining its value and sending no HTTP |

The reports agree on the canonical writable-contract comparison, **not complete read-only schema identity**. The additional ARM-ID gap is newly measured for this source/version, not assumed from the 2025 audit. Its policy exclusion is executable evidence; the diagnostic is not suppressed or reclassified compatible.

Reproduce from the repository root with the prepared closure:

```powershell
$toolchain = 'C:\workplace\azure-ps-workspace\azclips\toolkit\tools\typespec-bicep\dist\newer-network-probe'
$source = Join-Path $toolchain 'source'
$output = Join-Path $toolchain 'public-ip-create'
node toolkit\tools\typespec-bicep\project-public-ip.mjs $toolchain $source tests\Cli.Tests\Fixtures\PublicIpBicepBindings.json
node toolkit\tools\typespec-bicep\project-public-ip.mjs $toolchain $source (Join-Path $output 'audit-ir.json') --audit-ir
node toolkit\packages\generator\tools\Ace.DeploymentAudit\run.mjs --ir (Join-Path $output 'audit-ir.json') --catalogue Azure.Bicep.Types.Az@0.2.972 --select Microsoft.Network/publicIPAddresses@2026-03-01
node toolkit\packages\generator\tools\Ace.DeploymentAudit\run.mjs --ir (Join-Path $output 'audit-ir.json') --catalogue Azure.Bicep.Types.Az@0.2.972 --select Microsoft.Network/publicIPAddresses@2026-03-01 --upstream-types (Join-Path $output 'types.json')
```

| Command / check | Result |
| --- | --- |
| Both probe modes above; full canonical equality and persisted fixture comparison | Passed; exact March selection, unchanged ordinary IR and source/tool pins |
| Both full audit commands above | Completed unsupported / exit 1; identical coverage and four retained diagnostics, distinct catalogue identities/content hashes |
| `node --test toolkit\tools\typespec-bicep\test\projected-create.test.mjs` | 9 passed, including actual earlier-version projection and inconsistent-descriptor rejection |
| `npm --prefix toolkit\tools\typespec-bicep test` | 13 passed; supported upstream golden/scope/source-integrity checks preserved |
| `dotnet test toolkit\packages\generator\test\Ace.DeploymentAudit.Tests\Ace.DeploymentAudit.Tests.csproj --no-restore --filter FullyQualifiedName~CompatibilityAuditTests -v quiet` | 33 passed; official generated-reference loading and unchanged comparison behavior |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter FullyQualifiedName~PublicIpAddressParameterTests -v quiet` | 71 passed; March fixture, independent normalized-value assertions and IP-tag/ARM-ID exclusion |

Compiler association, build-compiled template, newly generated March C# processor execution, adapter/transport, both-surface playback and live authorization remain unchecked. The following retirement step updates the remaining consumers without removing these raw reports or their independent safeguards.

### Upstream-derived source-gate retirement

Task 1.16 is complete. The former canonical-emitter `deployment.ts` resource discovery, path/scope interpretation and SDK-package facet scans are removed. `deploymentContract` and `deploymentResources` are no longer accepted emitter options; compiler validation rejects them explicitly. Their implementation-specific tests and unused service/scoped/empty fixtures are removed rather than retained to preserve test counts.

The separate probe's `--audit-ir` mode now builds the same pinned upstream converter, selects its actual registered/version-projected Public IP PUT/resource models, and generates real property correspondence before emitting ordinary canonical IR once. The shared bridge context validates canonical operation/source version, actual PUT/body identity, generated writable scope and name/path input coverage. `createAuditContract` produces the existing metadata shape through these identities, never through a legacy class/route fallback.

Source observation additionally carries the original SDK input type. The retained `constraints.ts` helper reads source-only bounds, sensitivity and literals, including collection element/value paths and explicit failure for encoding/range/nullability loss. It does not discover resource models or convert Bicep schema.

A replacement regression exposed why the minimal **canonical writable-dependency selector must remain**: upstream correspondence also includes nested output-only model properties whose own fields appear writable. Using all observed PUT owners incorrectly selected an encoded read-only descendant. The selector follows canonical writable edges from the actual selected body, with inheritance/union/recursive guards; observed inputs then supply facets without rescanning SDK clients or property caches. Full writable source correspondence is required, so a missing field cannot become an empty-facet compatibility claim.

The consumed IR deployment contract fields/readers and the single native comparison walker remain necessary, including for historical audit inputs. No unused contract fields were identified that could be safely removed. The ordinary host's `sourceCommit` pass-through is also retained because acquisition/compiler workers still supply it. No IR/schema/reader version or dependency pin changes are required.

Real March replacement input still contains **1,161 models and 838 operations**, deeply equal to both the prior audit input and newly generated ordinary IR after removing only `deployment`. Resource descriptors and source digest are unchanged. It has two genuine scope-source facets instead of four: the removed two are synthetic `accept`/`contentType` literals, not source fields or inputs compared by the native walker. Both protocol roles remain explicit in all 43 command bindings; transport semantics are still an unchecked separate gate.

Fresh reports preserve the original March reports:

- [Post-retirement independent catalogue audit](public-ip-audit-after-retirement-2026-03-01.json).
- [Post-retirement upstream-type audit](public-ip-upstream-audit-after-retirement-2026-03-01.json).

Both result arrays are deeply equal to the prior March result: **118 compared paths, two recursive references, two unsupported paths, four diagnostics, unsupported / exit 1**. Source provenance now additionally identifies the pinned upstream revision and the complete tested isolated dependency versions. Source digest, ordinary schema/binding hashes and the persisted offline fixture are unchanged; removing implementation machinery does not relabel raw compatibility.

Use the prepared `$toolchain`, `$source` and `$output` variables from the March section, from the repository root:

```powershell
npm --prefix toolkit run build
node toolkit\tools\typespec-bicep\project-public-ip.mjs $toolchain $source
node toolkit\tools\typespec-bicep\project-public-ip.mjs $toolchain $source (Join-Path $output 'audit-ir-upstream.json') --audit-ir
node toolkit\packages\generator\tools\Ace.DeploymentAudit\run.mjs --ir (Join-Path $output 'audit-ir-upstream.json') --catalogue Azure.Bicep.Types.Az@0.2.972 --select Microsoft.Network/publicIPAddresses@2026-03-01
node toolkit\packages\generator\tools\Ace.DeploymentAudit\run.mjs --ir (Join-Path $output 'audit-ir-upstream.json') --catalogue Azure.Bicep.Types.Az@0.2.972 --select Microsoft.Network/publicIPAddresses@2026-03-01 --upstream-types (Join-Path $output 'types.json')
```

| Command / check | Result |
| --- | --- |
| `npm --prefix toolkit run build` | Passed; unchanged normal toolkit pins |
| `npm --prefix toolkit exec -- vitest run packages\typespec-emitter\test\source-observation.e2e.test.ts` | 5 passed; ordinary equality, source/SDK identity, rejected retired options and invalid observer versions |
| `node --test toolkit\tools\typespec-bicep\test\projected-create.test.mjs` | 11 passed; includes writable/read-only/unselected isolation, recursion/inherited sensitivity, collection/additional-property facets, missing correspondence and encoding/range/nullability rejection |
| `npm --prefix toolkit\tools\typespec-bicep test` | 13 passed; pinned upstream golden/scope/source-integrity checks |
| `dotnet test toolkit\packages\generator\test\Ace.DeploymentAudit.Tests\Ace.DeploymentAudit.Tests.csproj --no-restore --filter FullyQualifiedName~CompatibilityAuditTests -v quiet` | 33 passed; retained shared walker, independent constraints and historical metadata shape |
| `npm --prefix toolkit exec -- vitest run packages\compiler\test\runtime.test.ts -t 'forwards the entrypoint'` | 1 passed; 11 nonselected tests skipped; source commit/acquisition pass-through remains intact |
| Both real producer modes and audits above | Passed production of complete input/types/bindings; audits completed unsupported / exit 1 with exact retained result arrays |
| Entire canonical equality, old/new descriptors/source digest and persisted fixture comparison | Passed; 43 inputs, schema/binding hashes unchanged |
| `openspec validate pilot-public-ip-bicep-create --strict`, `openspec validate add-typespec-deployment-contract --strict` and `git diff --check` | Passed |

Source-gate retirement is complete. The following independent compiler gate advances Section 2 without qualifying a command-specific template, runtime registration, new generated March C# execution or live resource.

### Official Bicep CLI and selected resource association

Task 2.1 is complete. The [build-only compiler pin](../../../toolkit/tools/typespec-bicep/compiler.json) selects official Azure/bicep release `v0.49.2`, source commit `b34271ac51d820e7d448555fe7a6c1dbdb9e82c8`. The [tagged package versions](https://github.com/Azure/bicep/blob/b34271ac51d820e7d448555fe7a6c1dbdb9e82c8/src/Directory.Packages.props) pin `Azure.Bicep.Types.Az@0.2.972` and `Azure.Bicep.Types@0.6.144`, exactly matching the independent audit. Normal/toolkit/upstream pins remain unchanged.

The official `bicep-win-x64.exe` release asset is **126,668,256 bytes**, SHA-256 `acfff0453c9bcf442aa97103acbcd845ef366634e6ea2a2ba5734514409ce9c3`, as recorded by GitHub's release asset metadata. The actual executable reports `Bicep CLI version 0.49.2 (b34271ac51)`. Setup stores only this asset under the ignored artifact cache; each invocation verifies size/digest/banner before execution. Missing/modified binaries fail instead of using PATH or another version, and normal verification never installs a fallback.

The [helper](../../../toolkit/tools/typespec-bicep/compiler.mjs) uses supported CLI commands, not `Azure.Bicep.Core` APIs or a new product/runtime dependency. It invokes `build --stdout --no-restore --config <explicit-fixed-config> --diagnostics-format Sarif`. The [fixed config](../../../toolkit/tools/typespec-bicep/compiler-config.json) disables lint only; compiler/resource schema diagnostics remain enabled and parent config discovery is avoided.

The first attempted whole-body `resourceInput<...>` probe failed with **BCP394**: resource-derived type expressions must select a property, not the whole resource body. Property-derived parameter metadata then compiled even for the missing May version without diagnostics. That annotation-only result is **not** catalogue association evidence and was not accepted as a pass.

The final [association fixture](../../../toolkit/tools/typespec-bicep/test/compiler-association.bicep) includes property-derived type metadata and an actual exact-March Public IP declaration. The positive run returns **exit 0, no diagnostics, one declared Public IP at `2026-03-01`**, with expected parameter source references and the tested integer property. This is an offline schema probe, not a generated bridge-derived command template or an Azure deployment.

Each negative probe returns **exit 0 with a schema diagnostic**:

| Probe | Exact diagnostic | Qualification |
| --- | --- | --- |
| Missing `2026-05-01` resource types | `BCP081`: no available resource type validation | Rejected |
| String `idleTimeoutInMinutes` | `BCP036`: expected `int \| null` | Rejected |
| Unknown Public IP property | `BCP037`: property not allowed | Rejected |

The helper rejects **all** diagnostics, not just nonzero compiler exit codes. Tests assert the exact warning-only exit/codes, so a syntax error or a source label is not a substitute for real schema validation. The SARIF output omits explicit levels for these default-warning results; the report keeps that absence as `null` rather than inventing a level. The rejection gate does not depend on diagnostic severity.

The new [compiler-association report](public-ip-compiler-association.json) records official asset/source identity, tagged package association, the independently hashed audit report, probe/config/compiled-output digests and positive/negative results. The CLI's compressed single-file embedded catalogue DLL was **not** separately extracted/hashed; the recorded assembly hash explicitly identifies the independent audit assembly. Association evidence is official binary identity plus pinned source package versions and observed selected-schema behavior, not a claim of byte-extracted assembly identity.

Prior raw audits and their `compilerAssociation: "unverified"` fields are preserved as historical catalogue-only evidence. Their four differences and unsupported status remain; the new separate association gate does not relabel them compatible or waive command input/eligibility constraints.

| Command / check | Result |
| --- | --- |
| `gh release list --repo Azure/bicep --limit 5 --json tagName,publishedAt,isPrerelease,name` and exact release/tag/source package inspection | Official `v0.49.2`, exact commit, asset digest/size and matching package pins verified |
| `npm --prefix toolkit\tools\typespec-bicep run compiler:setup` | Passed; missing CLI acquired from official release, size/digest/banner verified |
| Isolated copy of `compiler.mjs` / `compiler.json`, then `node <isolated-tool-path>\compiler.mjs --install` | Passed fresh download/verification branch; temporary cache removed without modifying the shared verified compiler |
| `npm --prefix toolkit\tools\typespec-bicep run compiler:verify` | Passed after replacing insufficient annotation-only probe; exact March declaration and three rejected warning-only cases |
| `npm --prefix toolkit\tools\typespec-bicep run test:compiler` | 2 passed; actual compiler association plus same-size tampering/missing-executable rejection before invocation |
| `node toolkit\tools\typespec-bicep\compiler.mjs --verify openspec\changes\pilot-public-ip-bicep-create\public-ip-compiler-association.json` | Passed; persistent separate report generated |
| `openspec validate pilot-public-ip-bicep-create --strict`, `node --check toolkit\tools\typespec-bicep\compiler.mjs` and `git diff --check` | Passed; normal pins, bridge fixture and prior raw reports unchanged |

The Windows x64 compiler tests/setup are opt-in; the normal supported upstream suite does not acquire a CLI. **Task 2.2 remains next**: generate the actual command-specific resource/parameter assignments from the existing bridge. Packaging/omission/default proof, product adapter/transport, both-surface playback and authorized live gates are still pending.

### Bridge-derived templates and approved root-omission variants

Task 2.2 is complete. The actual pinned compiler exposed a resource-root restriction, not a missing source association: conditional root spreads fail with **BCP401**, while nested conditional spreads are supported. Nullable resource-derived parameters require the `?` type modifier; `| null` produced BCP293, and explicitly assigning their implicit null default produced BCP326. None of these diagnostics was waived.

The user explicitly approved **two generated immutable variants**, tags absent/present, in place of the original one-template design. Each variant still declares exactly one selected Public IP. This does not authorize substituting null or `{}` for absent tags, mutating compiled assets at runtime, creating a second resource, or changing the official compiler pin.

The [generator](../../../toolkit/tools/typespec-bicep/generate-template.mjs) consumes the same [generated bridge](../../../tests/Cli.Tests/Fixtures/PublicIpBicepBindings.json) used by the offline binder. Existing field eligibility now also records normalized requiredness in that one policy table: location, SKU name, allocation, IP version and timeout are required after customization; tags and SKU tier are optional. This is pilot normalization policy, not a change to canonical source requiredness.

The real exact-March source producer regenerated all 43 bindings. Compared with the previous persisted fixture, only seven `normalizedRequired` hints and the bridge digest changed. Schema/source/version/toolchain identities, source paths, parameter names, exclusion policy and values are unchanged. The new binding SHA-256 is `cf9bc5224840fd6c1104fc93286426abe2ea8a7b0358fcdf8a2eda2757448c4a`; the schema SHA remains `fbc5b3487025d1852c09f044e6e7310ae21ed12b77cd8e07377fa4da07a00227` (556 types, 532 correspondence records).

- [Tags-absent template](../../../tests/Cli.Tests/Fixtures/PublicIpBicepCreate.TagsAbsent.bicep): no tags parameter and no root tags assignment.
- [Tags-present template](../../../tests/Cli.Tests/Fixtures/PublicIpBicepCreate.TagsPresent.bicep): required resource-derived tags parameter forwarded unchanged, including an explicitly empty object.
- Both templates require the existing normalized ordinary values without template defaults, derive every field/type/identifier from bridge paths, and preserve source Int32 timeout bounds.
- SKU tier is nullable only as an omission sentinel for a non-nullable source input. The compiled SKU expression uses `shallowMerge` with a conditional empty object when tier is absent, rather than supplying null or Regional.
- Missing normalization policy, ambiguous identifiers/paths, source-nullable or read-only inputs, unaccounted roles and additional unsupported source constraints fail explicitly. No `any`/untyped body bypass or separate structural mapping is added.

Both real generated templates compile with **exit 0 and zero diagnostics**. The [four focused template tests](../../../toolkit/tools/typespec-bicep/test/template.test.mjs) assert exact compiled resource/parameter shapes, true tags omission, no resource defaults, Int32 bounds and consumed renamed identifiers. Wrong ordinary timeout and wrong conditional nested tier assignments still trigger BCP036 and fail qualification; dynamic nested assignment does not suppress schema type checking.

| Command / check | Result |
| --- | --- |
| `node toolkit\tools\typespec-bicep\project-public-ip.mjs toolkit\tools\typespec-bicep\dist\newer-network-probe toolkit\tools\typespec-bicep\dist\newer-network-probe\source tests\Cli.Tests\Fixtures\PublicIpBicepBindings.json` | Passed; real exact-version source projection, unchanged schema and 43 accounted inputs |
| `node toolkit\tools\typespec-bicep\generate-template.mjs tests\Cli.Tests\Fixtures\PublicIpBicepBindings.json tests\Cli.Tests\Fixtures` | Generated both variants from the same bridge |
| `npm --prefix toolkit\tools\typespec-bicep run test:template` | 4 passed; pinned compiler, exact compiled shape/omission, renamed bindings and schema rejection |
| `node --test toolkit\tools\typespec-bicep\test\projected-create.test.mjs` | 11 passed; source correspondence, policy, ordinary IR and audit safeguards |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter FullyQualifiedName~PublicIpAddressParameterTests --verbosity quiet` | 71 passed on SDK 10.0.404; existing normalization/direct-ID/Int32/omission behavior retained; unrelated existing CS8602/CA1416 warnings |
| `node --check` for `generate-template.mjs` / `create-bindings.mjs`, `git diff --check`, unchanged normal pins/raw audits/association report | Passed |

There are **86 distinct targeted tests**. Task 2.3 remains next: package both immutable ARM assets with provenance and explicit asset/variant validation. The future adapter must select by normalized tags **presence**, not tag count. Full canonical-body versus evaluated-declaration proof, runtime selection/transport, newly generated March C# execution, both-surface playback and authorized live gates are not claimed here.

### Immutable ARM assets and fixture-only loading

Task 2.3 is complete. The [packager](../../../toolkit/tools/typespec-bicep/package-template.mjs) first regenerates both expected sources from the bridge in memory and checks both persisted Bicep files. It verifies the actual official executable and its identity/catalogue/configuration against the separate compiler association, then uses the existing strict zero-diagnostic compile helper for each variant. No source generation, normal product configuration or historical report changes are required.

The build validates resource-group symbolic-name template format, exactly one matching March Public IP, only generated root fields/parameters, source-derived parameter metadata, no customization defaults and retained Int32 bounds. Unexpected scope, dependencies, nested resources, extra resource-group/resource declarations and additional orchestration sections fail explicitly.

Both compiled JSON files and the [manifest](../../../tests/Cli.Tests/Fixtures/PublicIpBicepCreate.Assets.json) are checked in and embedded only in the existing integration test assembly. The manifest is published last, after both variants compile and validate; incomplete writes cannot satisfy its digest checks. The actual CLI stale-second-source test confirms failure leaves all existing assets/manifest bytes unchanged.

| Asset | Compiled UTF-8 byte SHA-256 |
| --- | --- |
| [Tags absent](../../../tests/Cli.Tests/Fixtures/PublicIpBicepCreate.TagsAbsent.json) | `1e4b65f4810c8dc1a1e1014b005f2e9b565a36ba5ad9031dd9022134dc270363` |
| [Tags present](../../../tests/Cli.Tests/Fixtures/PublicIpBicepCreate.TagsPresent.json) | `c0070e44327dbde789c3256f682f16c563f925671b72602bd5be62d1b3685fae` |

The manifest records the source commit/upstream/toolchain/schema/binding identities, normalized bridge digest `e8aefaafabdbe2eae1c65eb7ccaf38a33baea9ba125446cc3e56b96fd43b36a1`, both Bicep source digests, official compiler/catalogue identity, configuration/association digests and exit-zero/no-diagnostic results. Bridge/Bicep/association **text** digests normalize UTF-8/LF; compiled JSON is hashed byte-exact, and [scoped Git attributes](../../../tests/Cli.Tests/Fixtures/.gitattributes) preserve LF on checkout. Configuration intentionally retains the association gate's **raw-byte** digest `a0f951a5163f70ced9abb3469225b10bb0b97d78a6b04fe495c6224add60cf02`: the first attempt to normalize that digest failed before asset publication and was corrected, not waived.

The unregistered [fixture reader](../../../tests/Cli.Tests/Fixtures/PublicIpBicepAssets.cs) selects by root-tags presence, not emptiness, and returns detached read-only `JsonElement` data from the embedded compiled asset. It checks bridge/source provenance, compiler/catalogue/configuration identity, digest and resource boundaries; missing/invalid selected assets fail explicitly without a compiler, download, template mutation or alternate-backend retry. The compiler pin/config JSON are embedded as provenance data only, not runtime compiler libraries. Existing input eligibility remains in the binder and must precede future deployment.

[Reader tests](../../../tests/Cli.Tests/PublicIpBicepAssetTests.cs) cover absent/empty/nonempty tags, invalid input before asset reads, missing manifest/selected asset, byte tampering, malformed JSON, missing provenance and compiler/config/source mismatch. Extra resources, dependencies and wrong API versions still fail after their test manifest hashes are updated. No HTTP or product registration is added.

| Command / check | Result |
| --- | --- |
| `node toolkit\tools\typespec-bicep\package-template.mjs tests\Cli.Tests\Fixtures` | Passed; both variants compiled with zero diagnostics and validated; immutable JSON/manifest produced |
| `npm --prefix toolkit\tools\typespec-bicep run test:package` | 3 passed; byte reproduction, exact resource/parameter boundaries and actual CLI stale-source/no-replacement failure |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter 'FullyQualifiedName~PublicIpBicepAssetTests\|FullyQualifiedName~PublicIpAddressParameterTests' --verbosity quiet` | 87 passed before the final config-tampering case; 71 existing Public IP behavior tests plus 16 asset tests |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter FullyQualifiedName~PublicIpBicepAssetTests --verbosity quiet` | Final 17 passed, including config tampering; pinned SDK 10.0.404; unrelated existing CS8602/CA1416 warnings |
| `node --check toolkit\tools\typespec-bicep\package-template.mjs`, `git diff --check`, LF attribute checks and unchanged source/bridge/Bicep/pins/raw reports/association checks | Passed |

There are **91 distinct targeted tests** (the final reader run repeats the initial 16 cases). Task 2.4 remains next: compare existing canonical normalized bodies/parameters against evaluated declaration semantics. This packaging/loading gate is not adapter registration, deployment transport, both-surface playback, newly generated March C# execution, schema identity or live provisioning evidence.

### Independent JSON-backed declaration snapshots

Task 2.4 is complete. [Four offline snapshots](../../../tests/Cli.Tests/Fixtures/PublicIpBicepSnapshots.json) came from `Bicep/get_deployment_snapshot`, with JSON-backed `.bicepparam` inputs referencing exact copies of the packaged ARM assets. Both copied input byte digests were verified before evaluation. The resource template was not recompiled, no handwritten ARM expression evaluator was introduced, and no Azure deployment was performed. Bare relative JSON `using` references are required: an absolute Windows path was rejected as an unrecognized module-reference scheme (BCP189).

The independent evaluator returned exactly one March Public IP, zero diagnostics and no outputs for defaults/absent optional values, explicit ordinary tags/Regional tier/timeout 30, explicitly empty tags with absent tier, and Int32 maximum timeout. Frozen evidence records both asset digests, actual snapshot results, parameter values and synthetic deployment metadata. The evaluator version remains **unverified**; this proof does not qualify another compiler, broaden the schema audit or establish service acceptance of representation-boundary timeouts.

Four pinned-CLI parameter tests compile the committed JSON-backed inputs and compare both parameter maps and resolved templates with the evidence/immutable assets. Four native tests independently run the existing customization/direct processor and generated binder plus real atomic deployment serialization, compare every parameter value, and compare the evaluated declaration body after verifying/removing only declaration identity fields. Tags absent versus `{}`, tier omission and exact Int32 values remain distinct. The existing transport also writes null `reference` and `expression` fields; tests explicitly require those null fields rather than modifying transport or treating them as evaluator inputs.

| Command / check | Result |
| --- | --- |
| `node --test toolkit\tools\typespec-bicep\test\compiler.test.mjs toolkit\tools\typespec-bicep\test\snapshot.test.mjs` | 6 passed, including both preserved compiler-qualification tests and four JSON-backed parameter cases |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter FullyQualifiedName~BicepTemplate_IndependentSnapshot --verbosity quiet` | 4 passed after explicitly accounting for existing transport null fields |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter 'FullyQualifiedName~PublicIpAddressParameterTests\|FullyQualifiedName~PublicIpBicepAssetTests' --verbosity quiet` | 92 passed, including the final snapshot identity/output checks and all existing normalization/asset cases |

Section 3 remains next: isolated runtime selection, existing deployment/GET composition, body-matched both-surface replay and negative paths. No product registration, feature dependency, newly generated March C# execution or live readiness is claimed.

## Goals / Non-Goals

**Goals:**

- Prove a bounded atomic create backend replacement using the complete selected-version API contract and reviewed command-level eligibility/binding evidence, without claiming schema identity.
- Reuse existing argument normalization, model serialization, ARM execution and output conventions.
- Reuse upstream TypeSpec-to-Bicep schema code and generate source-aware structural bindings instead of maintaining parallel Bicep schema rules or handwritten mappings.
- Make pilot selection explicit and fail before mutation for unsupported pilot inputs or unmet gates.
- Keep the default codegen/runtime paths and existing public command metadata unchanged.

**Non-Goals:**

- Replacing the Public IP customization with Bicep, broad Network migration or migration of read/list/delete/update operations.
- Supporting Basic/IPv6/Global/StandardV2, zones, DNS, IP tags, DDoS, prefix, extended-location or nested-resource creation in the initial pilot.
- Runtime template compilation, generic declarative orchestration, deployment rollback policies or AVM-provided defaults.
- Reimplementing/forking the upstream schema emitter or assuming its generated types replace command-specific policy and deployment execution.
- Declaring production readiness or hiding a full-contract audit mismatch behind a happy-path demonstration.

## Decisions

### 1. Repair the source gate before authoring the backend

Reproduce the legacy association gap with a focused fixture, then use official lifecycle/operation-list association and selected SDK model identity to establish eligibility. Preserve unambiguous request/response resource identity, scope, names and API version. A missing modern registration is not sufficient to reject a model whose supported metadata provides those identities; conversely, a guessed class name or route is not sufficient to accept it.

The historical retry and target-boundary review established optional selected metadata without trimming canonical IR. Task 1.16 now retires its custom emitter discovery/options. The current producer selects the actual upstream version-projected Public IP PUT and records its exact `resourceType@apiVersion` root in the retained audit contract. The full writable source-constraint closure and raw comparison remain required even though runtime eligibility is narrower; broader legacy/whole-service extraction is not silently preserved as a fallback.

**Alternative rejected:** inventing a Public IP descriptor with empty facets or switching catalogue/source versions to force a pass.

### 1A. Reuse upstream schema interpretation and generate the bridge first

Complete new tasks 1.10-1.16 before Section 2: pin and verify the upstream integration seam, prove exact-version legacy Public IP generation, derive source-aware bindings, add focused correspondence regressions, replace the prototype's structural mapping, record real coverage against both upstream types and the compiler catalogue, and remove superseded source-gate code. Do not author a second hand-maintained parameter/field map while this prerequisite is unresolved.

Keep the guarantees, not every existing implementation. Replace duplicated resource/schema interpretation with upstream-derived output while retaining the canonical operation/parameter linkage that upstream does not know. Reassess supplemental facet extraction and full-graph audit traversal against their actual remaining consumers; remove redundant machinery and its implementation-only tests once replacement evidence exists. Keep independent input-constraint, version, provenance, omission and eligibility checks. Historical reports remain evidence, not an obligation to retain obsolete runtime/build paths or preserve test counts.

### 2. Preserve the customized request body as the execution input

The pilot consumes the body produced by existing customization, after legacy defaults, tag parsing and argument adaptation. It does not independently derive inputs from command flags or use template defaults to overwrite omission behavior.

Eligibility is narrow: one standalone Standard/Regional, Static, IPv4 Public IP; existing subscription/resource-group/name/location inputs; optional ordinary resource tags and the existing idle timeout. An omitted Regional tier must stay omitted unless the current body explicitly supplies it. Other specified resource fields make the invocation ineligible for the explicit pilot. Normal direct invocations retain all existing combinations.

The first fixture compares minimal/default and tags/explicit-timeout bodies. Advanced existing scenarios remain on the direct path and retain their current tests.

The fixture establishes the reviewed policy boundary for root ID and integer differences. Replace its structural mapping with the generated bridge before the eventual adapter consumes it, and enforce eligibility before preflight or deployment HTTP. Keep the whole raw schema audit; require proof for each bridged/excluded difference instead of changing its result or pruning canonical models. No root `id` parameter is emitted, supplied non-null root IDs are rejected, and only exact normalized Int32 timeout values enter deployment parameters.

### 3. Build-compile and package two single-resource version-fixed variants

Generate tags-absent/present resource-group-scoped experimental Bicep variants at `2026-03-01`, each with one resource and parameter/field assignments derived from the same verified bridge, not a second handwritten structural mapping. This user-approved split preserves root-field omission without prohibited resource-root spreads or a null-tags substitute. Compose only the necessary command/pilot policy around it. Compile with a pinned official CLI during build, record executable/version/template provenance and verify the compiler's resource type association separately from upstream generation and the catalogue-only audit. Package both compiled ARM JSON variants as immutable pilot assets; select by normalized root-tags presence (including `{}`), and do not install or invoke Bicep or modify compiled assets at command runtime.

Parameterization must preserve missing versus explicit values. The compiled declaration must contain exactly the selected Public IP, no resource-group or dependency creation, and no additional resource API version. Dynamic object assignment is not permission to bypass typed compiler checks or the contract gate.

**Alternative rejected:** accepting a compiler success as proof of source compatibility, runtime parity or complete coverage.

### 4. Keep the first execution adapter in an opt-in integration fixture

The initial adapter and registration override live in an opt-in integration fixture that can reference both Network and Resources; the product Network project must not gain a dependency on the Resources feature module. The host explicitly loads both assemblies and registers shared core/runtime services. Neither feature has separate runtime service registrations.

Use the existing extended-processor/execution mechanism to compose the existing atomic resource-group deployment transport and generated Public IP GET. Do not call the public deployment orchestrator: its command-specific parameter parsing, default naming and validation policies are not the Public IP command contract. Do not copy polling, authentication or error transport into a second implementation.

The experimental adapter submits compiled template plus canonical parameters in `Incremental` mode, polls the deployment to terminal success and obtains the final resource through the existing GET/output mechanism at the matching experimental API `2026-03-01`. The deployment transport version remains `2025-04-01`. It returns the same Public IP model and existing output adapters, not a deployment object or fabricated template output.

### Completed isolated runtime and offline regression gates

Sections 3 and the offline Section 4 gates are implemented in the non-packable [fixture assembly](../../../tests/Fixtures/PublicIpBicepPilot/Azclips.Tests.Fixtures.PublicIpBicepPilot.csproj). The existing binder/asset reader are linked into that single fixture assembly rather than duplicated or made product dependencies; their existing embedded resource names remain stable. Both native and scenario tests reference the fixture.

The existing assembly loader expects an assembly-owned `ExtensionIndex`, so explicitly supplying this fixture assembly for Network is the opt-in. Its public discovery table delegates unchanged to the real Network index; only execution metadata for Public IP create substitutes the fixture processor type. Names, aliases, parameter dictionaries and other commands remain unchanged. The normal Network assembly never registers the adapter. A fixture-host-only service supplies the bounded owned deployment name; no public CLI/PowerShell selector or public deployment naming/default policy is introduced.

The feature-specific `CreateExtendedProcessor<T>` constraints cannot cross from Network to Resources. The adapter instead uses the existing public `IProcessor.Init(ProcessorContext)` with the same pipeline, publisher/cancellation token, auth handler and resolved profile, substituting only captured child output. No product-wide execution abstraction, copied transport, polling implementation or feature dependency is needed. Shared services remain those registered by `ScenarioHost`; its optional per-extension fixture registrations add only the pilot host options.

Execution:

1. Run existing pre-processing/customization and serialize its actual canonical body.
2. Apply the existing generated binder/eligibility policy and validate/load the immutable selected asset before any HTTP.
3. Build preflight through the existing generated GET request seam. A specifically handled 404 means absence, without publishing a false command error; other failures use the existing HTTP error helper. Any successful preflight means a detected existing target and blocks deployment.
4. Invoke `NewResourceGroupDeploymentProcessor` in Incremental mode at `2025-04-01`, sharing normal auth/tracing/polling. Capture and require a successful deployment result; cancellation, missing/failed state or an error cannot become resource output.
5. Invoke the existing typed Public IP GET/deserializer at March through an explicit fixture-only query-version override, preserving generated path escaping and other query construction. Capture and validate returned identity/name, then emit exactly one real Public IP model. Escaped resource-group paths are decoded for ARM-ID comparison; incomplete/different or failed retrieval is explicit failure. No direct PUT retry occurs.

This qualifies existing customization/models with the experimental template/GET URI in the fixture. **It does not claim newly generated March C# execution**, whole-Network qualification, exact API/Bicep schema identity or live service acceptance.

[Dedicated scenarios](../../../tests/Scenario.Tests/Network/PublicIpBicepPilotScenarioTests.cs) record real fixture traffic in memory against a [fake deployment backend](../../../tests/Scenario.Tests/Network/FakePublicIpDeploymentBackend.cs). The fake returns independently evaluated frozen resource bodies after checking the actual immutable template and parameter values; it does not interpret ARM expressions or pretend to be Azure validation. Expected transport is preflight GET, deployment PUT, operation GET, final deployment GET and final Public IP GET. Both minimal/default and explicit tags/tier/timeout recordings replay with exact body matching through CLI, the real PS argument provider and the actual `NewPublicIpAddressCmdlet` in a runspace. Test scope uses the recording sanitizer's synthetic zero subscription; final identity checks were retained, not weakened for sanitized responses.

Negative cases cover unsupported zone/SKU/IP-tag inputs and missing assets before any HTTP, existing target, denied preflight/submission, failed/canceled/missing deployment state, failed/empty final retrieval and client cancellation during polling. They require no resource output, no direct-create retry and no unintended deletion. Existing direct Public IP scenarios and generic host tests retain their behavior.

| Command / check | Result |
| --- | --- |
| `dotnet restore tests\Cli.Tests\Cli.Tests.csproj --verbosity quiet` and `dotnet restore tests\Scenario.Tests\Scenario.Tests.csproj --verbosity quiet` | Passed after adding the new project references; no new NuGet package dependency |
| `dotnet build azclips.slnx --no-restore --verbosity quiet` | Passed, zero errors; four unrelated existing Agent test analyzer warnings |
| `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter 'FullyQualifiedName~PublicIpAddressParameterTests\|FullyQualifiedName~PublicIpBicepAssetTests' --verbosity quiet` | 92 passed after linking shared fixture code/resources; existing unrelated generated/analyzer warnings |
| `dotnet test tests\Scenario.Tests\Scenario.Tests.csproj --no-restore --filter 'FullyQualifiedName~PublicIpBicepPilotScenarioTests\|FullyQualifiedName~PublicIpScenarioTests\|FullyQualifiedName~ScenarioHostTests' --verbosity quiet` | 28 passed, including real cmdlet replay, cancellation and escaped ARM identity; one unrelated existing analyzer warning |
| `node --test toolkit\tools\typespec-bicep\test\compiler.test.mjs toolkit\tools\typespec-bicep\test\template.test.mjs toolkit\tools\typespec-bicep\test\package.test.mjs toolkit\tools\typespec-bicep\test\snapshot.test.mjs` | 13 passed; official compiler, strict diagnostics, immutable byte reproduction and JSON-backed parameter evidence |
| `dotnet list tests\Fixtures\PublicIpBicepPilot\Azclips.Tests.Fixtures.PublicIpBicepPilot.csproj reference` | Only explicit Network, Resources and shared Core.DependencyInjection references |
| `openspec validate pilot-public-ip-bicep-create --strict` and `git diff --check` | Passed; implementation progress is 31/33, only live authorization/execution remain |
| `git diff --exit-code` for product Network/Resources source, upstream patch, compiler pins/configuration, bridge and immutable ARM assets | Passed; no product dependency, source/version pin, asset or raw-audit change |

The final focused selections contain **133 passing tests** (92 native, 28 scenario/host and 13 tooling). The native selection was repeated successfully after the final solution build.

#### Limitations, live gate and rollback

- **Live authorization is absent.** No Azure provisioning, real quota/cost validation, permission-restricted live scenario, owned-resource cleanup or live service parity is claimed. Tasks 4.6/4.7 remain unchecked; do not archive.
- Preflight GET is **not atomic create-only protection**. Another caller could create the target between preflight and deployment; Incremental deployment may then update it. Offline rejection tests demonstrate detected-existing behavior, not elimination of that race.
- Additional deployment read/write permissions and deployment history are required, alongside Public IP read/write and explicitly authorized cleanup permissions. The host must provide a fresh owned deployment name; fixture tests' fixed name is synthetic and is not a live naming policy. No role assignments or resource-group creation are included.
- The existing shared LRO transport is reused, not redesigned. The pilot requires final deployment success and uses the publisher's cancellation token; it does not roll back a submitted deployment or delete a resource automatically on command failure/cancellation.
- Original 2025 and historical May evidence, the full March unsupported audit (four diagnostics), normal source/API/toolchain pins and compiler/asset provenance are unchanged. Independent snapshot evaluator version remains unverified.
- **Rollback/default-off path:** use the normal Network assembly instead of the explicitly substituted fixture assembly, and omit pilot host options. Normal public commands continue direct execution at their existing API. No product feature flag, default activation or runtime compiler exists to revert.

The exact fixture factory/visibility seam is an implementation detail to verify against existing helpers. If composing the existing transports requires a new product-wide execution abstraction or feature dependency, stop for design review instead of widening this pilot.

**Alternative rejected:** production default activation, a public backend flag, a Network-to-Resources project reference or a generic deployment engine.

### 5. Fail explicitly and protect pre-existing resources

Pilot selection is internal to the fixture/host configuration and defaults off. Unsupported explicit pilot inputs fail before deployment submission; do not ignore fields, auto-fallback after an error, or retry via the direct PUT.

Use isolated, test-owned names and verify target absence for a create-only pilot. An existing target is rejected rather than updated or reused. A preflight absence check is not a concurrency guarantee: controlled names and exclusive test ownership are required, and the race remains an explicit limitation. Failed/cancelled deployments must report failure, not success or an unrelated final GET result. Cleanup is explicit and restricted to test-owned resources; no Complete mode or automatic rollback deletion.

### 6. Separate regression evidence from transport identity

Default-path recordings remain unchanged. Bicep-path HTTP recordings necessarily differ and need their own assertions; do not use direct-PUT playback as a proxy for deployment execution.

Validate the normalized body versus template parameters/declaration, deployment mode/API versions/polling and final resource GET/output. Replay each pilot recording through both CLI and PowerShell with request-body matching. Keep aliases, parameter metadata, defaults and resource output contracts unchanged.

Live work requires separate authorization of subscription, existing resource group, location, quota, cost and cleanup ownership. Test additional deployment permissions and failure behavior; a fake backend cannot establish Azure authorization or provisioning parity.

## Risks / Trade-offs

- **Legacy ARM metadata differs from modern fixture registrations** -> Fix the shared mechanism with focused evidence and keep all missing associations explicit.
- **Upstream API/toolchain or legacy-model coverage is incomplete** -> Pin and verify the reuse boundary first; report unsupported cases and review a focused upstream integration change instead of silently copying/forking schema rules.
- **Flattening, inheritance or repeated source properties obscure correspondence** -> Join canonical IDs with complete containing paths and selected-version provenance; reject missing/ambiguous mappings rather than matching names.
- **Full contract has enum/constraint/recursive-shape differences** -> Retain `incompatible`/`unsupported` results, extend comparisons only with proof, and block activation for unaccounted differences; reviewed command-level restrictions/bindings never relabel the full API schema compatible.
- **More ARM permissions and deployment records** -> Confine these changes to explicit opt-in, document required actions and verify least-privilege/failure behavior live.
- **Existing-resource races** -> Isolated owned names, absence checks and a documented concurrency limit; never claim a conditional create guarantee that transport does not provide.
- **Cross-feature coupling** -> Separate integration fixture with explicit feature registration and existing execution helpers; no default product/module dependency.
- **Tests match only URI/method** -> Use body matching for both-surface pilot playback and assert actual declaration/parameter shapes.
- **Public IP incurs cost or leaves partial resources** -> Obtain live authorization, bound runs and perform explicit owned-resource cleanup.

## Migration Plan

1. Resolve legacy source discovery, produce a complete exact-version contract/audit report and account for every difference with reviewed binding/eligibility evidence.
2. Pin/reuse upstream TypeSpec-to-Bicep schema generation, prove legacy Public IP/source correspondence, replace the handwritten structural map with the generated bridge and retire superseded source-gate machinery.
3. Generate the single-resource template from the bridge, verify a separately pinned compiler and package the compiled artifact.
4. Implement the isolated adapter/opt-in fixture, leaving production defaults untouched.
5. Prove offline input, transport, error, output and CLI/PowerShell regression gates.
6. Request live authorization and run bounded owned-resource scenarios.
7. Report evidence and limitations for review. Disable the fixture override to revert to the current direct backend; any production activation is a subsequent decision, not an automatic outcome.
