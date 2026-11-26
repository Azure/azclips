## Context

See [proposal.md](proposal.md) for motivation and scope. The prior [deployment-contract change](../add-typespec-deployment-contract/design.md) established opt-in IR metadata and an offline catalogue checker, not a runtime backend.

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

The full Network retry and target-boundary review are complete. Add optional `deploymentResources` emitter configuration using resource types at the existing concrete API selector. With no selection, existing whole-service extraction remains strict; with explicit selection, record exact `resourceType@apiVersion` roots in optional contract `selection` and require one create descriptor per root. The full Public IP writable request graph must be covered even though initial runtime activation is narrower. Do not trim the canonical command/output schema to achieve a pass.

**Alternative rejected:** inventing a Public IP descriptor with empty facets or switching catalogue/source versions to force a pass.

### 1A. Reuse upstream schema interpretation and generate the bridge first

Complete new tasks 1.10-1.16 before Section 2: pin and verify the upstream integration seam, prove exact-version legacy Public IP generation, derive source-aware bindings, add focused correspondence regressions, replace the prototype's structural mapping, record real coverage against both upstream types and the compiler catalogue, and remove superseded source-gate code. Do not author a second hand-maintained parameter/field map while this prerequisite is unresolved.

Keep the guarantees, not every existing implementation. Replace duplicated resource/schema interpretation with upstream-derived output while retaining the canonical operation/parameter linkage that upstream does not know. Reassess supplemental facet extraction and full-graph audit traversal against their actual remaining consumers; remove redundant machinery and its implementation-only tests once replacement evidence exists. Keep independent input-constraint, version, provenance, omission and eligibility checks. Historical reports remain evidence, not an obligation to retain obsolete runtime/build paths or preserve test counts.

### 2. Preserve the customized request body as the execution input

The pilot consumes the body produced by existing customization, after legacy defaults, tag parsing and argument adaptation. It does not independently derive inputs from command flags or use template defaults to overwrite omission behavior.

Eligibility is narrow: one standalone Standard/Regional, Static, IPv4 Public IP; existing subscription/resource-group/name/location inputs; optional ordinary resource tags and the existing idle timeout. An omitted Regional tier must stay omitted unless the current body explicitly supplies it. Other specified resource fields make the invocation ineligible for the explicit pilot. Normal direct invocations retain all existing combinations.

The first fixture compares minimal/default and tags/explicit-timeout bodies. Advanced existing scenarios remain on the direct path and retain their current tests.

The fixture establishes the reviewed policy boundary for root ID and integer differences. Replace its structural mapping with the generated bridge before the eventual adapter consumes it, and enforce eligibility before preflight or deployment HTTP. Keep the whole raw schema audit; require proof for each bridged/excluded difference instead of changing its result or pruning canonical models. No root `id` parameter is emitted, supplied non-null root IDs are rejected, and only exact normalized Int32 timeout values enter deployment parameters.

### 3. Build-compile and package one version-fixed template

Generate one resource-group-scoped Bicep resource at `2025-07-01` with parameter/field assignments derived from the verified bridge, not a second handwritten structural mapping. Compose only the necessary command/pilot policy around it. Compile with a pinned official CLI during build, record executable/version/template provenance and verify the compiler's resource type association separately from upstream generation and the catalogue-only audit. Package the compiled ARM JSON as an immutable pilot asset; do not install or invoke Bicep at command runtime.

Parameterization must preserve missing versus explicit values. The compiled declaration must contain exactly the selected Public IP, no resource-group or dependency creation, and no additional resource API version. Dynamic object assignment is not permission to bypass typed compiler checks or the contract gate.

**Alternative rejected:** accepting a compiler success as proof of source compatibility, runtime parity or complete coverage.

### 4. Keep the first execution adapter in an opt-in integration fixture

The initial adapter and registration override live in an opt-in integration fixture that can reference both Network and Resources; the product Network project must not gain a dependency on the Resources feature module. Both feature runtimes are explicitly registered by that fixture, avoiding an implicit module-loading requirement.

Use the existing extended-processor/execution mechanism to compose the existing atomic resource-group deployment transport and generated Public IP GET. Do not call the public deployment orchestrator: its command-specific parameter parsing, default naming and validation policies are not the Public IP command contract. Do not copy polling, authentication or error transport into a second implementation.

The adapter submits compiled template plus canonical parameters in `Incremental` mode, polls the deployment to terminal success and obtains the final resource by its original generated GET at `2025-07-01`. The deployment transport version remains `2025-04-01`. It returns the same Public IP model and existing output adapters, not a deployment object or fabricated template output.

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
