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

Run full Network extraction after the narrow fix. Any next unrelated blocker must remain explicit. If resource-targeted extraction is needed, obtain review for that boundary rather than silently skipping failed resources or weakening the source projection. The full Public IP request graph must be covered even though initial runtime activation is narrower.

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
