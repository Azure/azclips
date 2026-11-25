## Context

See [proposal.md](proposal.md) for motivation and scope. The prior [deployment-contract change](../add-typespec-deployment-contract/design.md) established opt-in IR metadata and an offline catalogue checker, not a runtime backend.

Network selects source commit `90e63e5fe2c596e8bd807aad7dd9d62cee1746dd` and API `2025-07-01` in its [configuration](../../../src/Extensions/Network/aceconfig.yaml). Its generated create processor sends a direct PUT and polls the resource operation. The existing customization constructs the canonical body and applies Standard/Static/IPv4/idle-timeout defaults; the CLI and PowerShell scenarios share a record/playback baseline.

### Actual pre-implementation evidence

The pinned source was acquired in an isolated sparse checkout without changing the existing specs checkout or production Network configuration. Compilation used the toolkit's existing locked libraries and host runtime.

- Enabled deployment extraction returned no IR and one error: `deployment-contract-invalid`, with `ARM resource association is unresolved or ambiguous` for `Microsoft.Network/ApplicationGateways/createOrUpdate`.
- A separate selected-version SDK probe of Public IP found `PublicIPAddress`, `createOrUpdate` lifecycle metadata, the exact resource-group route, a `PublicIPAddress` request body and `ResourceGroup` base scope.
- The Public IP model's official operation list includes that create operation, but `getArmResource` returns no modern registration. The current extractor requires both and therefore excludes this legacy model. Official association information exists; class/route guessing is not a justified fallback.
- The actual `Azure.Bicep.Types.Az` assembly identifies itself as `0.2.972+e899dc9a90`, and its index contains `Microsoft.Network/publicIPAddresses@2025-07-01`.
- **No complete Public IP structural audit has run or passed.** Catalogue presence is only a version-availability check. Compiler association, template compilation and live/runtime parity remain unchecked.

### Source-gate implementation and boundary review

The legacy association fix passed 16 deployment-contract tests and advanced full pinned Network extraction past Application Gateway. Extraction then stopped at `SwapResources.create` with unresolved resource-group scope bindings. That operation is a legacy VIP-swap extension beneath a cloud service; interpreting its scope requires more than changing the expected parameter count.

The user approved explicitly scoped Public IP metadata after reviewing schema dependencies versus resources to provision. The normal canonical Network IR remains complete. Deployment metadata records its selected resource/version roots, and supplemental facets follow their complete writable type graph: inherited properties, nested models, arrays/dictionaries, unions/discriminators and recursive references. Read-only fields remain in canonical retrieval/output models but do not contribute writable facets solely through read-only edges.

Generic ID references and full embedded resource-shaped models do not select additional create operations. A future multi-resource workflow must explicitly select its provisioning roots. Unselected operations, including the unsupported VIP-swap scope, are visibly outside this deployment audit. Missing/ambiguous selected roots, selected scope/facet failures and genuine source/compiler errors still fail; no partial Public IP contract is accepted.

### Selected-source and full audit evidence

Selected pinned extraction succeeded with `Microsoft.Network/publicIPAddresses@2025-07-01`, 1,060 canonical models, 768 operations and four supplemental facets. A separate disabled extraction was deeply equal to the entire selected document after removing only `deployment`. No command/model/output generation input was pruned or changed.

The initial complete [audit report](public-ip-audit.json), preserved unchanged as the baseline, records the exact selection, source/tool/catalogue identities, IR SHA-256, coverage and diagnostics. Its status is **unsupported**, exit code **1**: 102 compared paths, two recursive references and 17 unsupported paths. The 19 diagnostics comprise 16 unsupported catalogue-union comparisons, one writable top-level identity (`$.id`) and two integer-range differences (Public IP and nested NAT Gateway idle timeouts).

The selection boundary is approved; these newly measured contract differences are **not** approved exceptions. Catalogue string-union normalization needs evidence, top-level API identity versus declaration identity needs explicit review, and intrinsic integer-width differences must not be silently widened. The backend/template/compiler/live gates remain blocked; narrowing the runtime case does not turn this report into compatibility.

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

The baseline strict comparison is not weakened to accommodate a proposed adapter. Remaining identity/integer bindings require review before changing the proof model or authoring the backend. No root product solution build, source re-extraction, compiler or live run was needed or performed for this audit-only change; existing activation gates remain blocked.

## Goals / Non-Goals

**Goals:**

- Prove a bounded atomic create backend replacement using the complete selected-version API contract.
- Reuse existing argument normalization, model serialization, ARM execution and output conventions.
- Make pilot selection explicit and fail before mutation for unsupported pilot inputs or unmet gates.
- Keep the default codegen/runtime paths and existing public command metadata unchanged.

**Non-Goals:**

- Replacing the Public IP customization with Bicep, broad Network migration or migration of read/list/delete/update operations.
- Supporting Basic/IPv6/Global/StandardV2, zones, DNS, IP tags, DDoS, prefix, extended-location or nested-resource creation in the initial pilot.
- Runtime template compilation, generic declarative orchestration, deployment rollback policies or AVM-provided defaults.
- Declaring production readiness or hiding a full-contract audit mismatch behind a happy-path demonstration.

## Decisions

### 1. Repair the source gate before authoring the backend

Reproduce the legacy association gap with a focused fixture, then use official lifecycle/operation-list association and selected SDK model identity to establish eligibility. Preserve unambiguous request/response resource identity, scope, names and API version. A missing modern registration is not sufficient to reject a model whose supported metadata provides those identities; conversely, a guessed class name or route is not sufficient to accept it.

The full Network retry and target-boundary review are complete. Add optional `deploymentResources` emitter configuration using resource types at the existing concrete API selector. With no selection, existing whole-service extraction remains strict; with explicit selection, record exact `resourceType@apiVersion` roots in optional contract `selection` and require one create descriptor per root. The full Public IP writable request graph must be covered even though initial runtime activation is narrower. Do not trim the canonical command/output schema to achieve a pass.

**Alternative rejected:** inventing a Public IP descriptor with empty facets or switching catalogue/source versions to force a pass.

### 2. Preserve the customized request body as the execution input

The pilot consumes the body produced by existing customization, after legacy defaults, tag parsing and argument adaptation. It does not independently derive inputs from command flags or use template defaults to overwrite omission behavior.

Eligibility is narrow: one standalone Standard/Regional, Static, IPv4 Public IP; existing subscription/resource-group/name/location inputs; optional ordinary resource tags and the existing idle timeout. An omitted Regional tier must stay omitted unless the current body explicitly supplies it. Other specified resource fields make the invocation ineligible for the explicit pilot. Normal direct invocations retain all existing combinations.

The first fixture compares minimal/default and tags/explicit-timeout bodies. Advanced existing scenarios remain on the direct path and retain their current tests.

### 3. Build-compile and package one version-fixed template

Author one resource-group-scoped Bicep resource at `2025-07-01`. Compile with a pinned official CLI during build, record executable/version/template provenance and verify the compiler's resource type association separately from the catalogue-only audit. Package the compiled ARM JSON as an immutable pilot asset; do not install or invoke Bicep at command runtime.

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
- **Full contract has enum/constraint/recursive-shape differences** -> Retain `incompatible`/`unsupported` results, extend comparisons only with proof, and block activation pending review; narrow runtime inputs do not erase full-contract gaps.
- **More ARM permissions and deployment records** -> Confine these changes to explicit opt-in, document required actions and verify least-privilege/failure behavior live.
- **Existing-resource races** -> Isolated owned names, absence checks and a documented concurrency limit; never claim a conditional create guarantee that transport does not provide.
- **Cross-feature coupling** -> Separate integration fixture with explicit feature registration and existing execution helpers; no default product/module dependency.
- **Tests match only URI/method** -> Use body matching for both-surface pilot playback and assert actual declaration/parameter shapes.
- **Public IP incurs cost or leaves partial resources** -> Obtain live authorization, bound runs and perform explicit owned-resource cleanup.

## Migration Plan

1. Resolve legacy source discovery and produce a complete exact-version contract/audit report.
2. Verify a pinned compiler and the compiled single-resource template.
3. Implement the isolated adapter/opt-in fixture, leaving production defaults untouched.
4. Prove offline input, transport, error, output and CLI/PowerShell regression gates.
5. Request live authorization and run bounded owned-resource scenarios.
6. Report evidence and limitations for review. Disable the fixture override to revert to the current direct backend; any production activation is a subsequent decision, not an automatic outcome.
