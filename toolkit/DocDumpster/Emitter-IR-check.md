# IR fidelity checklist — ElasticSan (api-version 2026-04-01-preview)

Audit of emit fidelity: every model / property / enum / operation in the authored
TypeSpec was compared against the emitted IR. Goal: the produced IR should lose no
information relative to the source `.tsp`.

- **Source of truth:** `C:\workspace\azure-rest-api-specs\specification\elasticsan\resource-manager\Microsoft.ElasticSan\ElasticSan\*.tsp` (all 10 files)
  - `node C:\workspace\azclips\toolkit\packages\cli\dist\src\cli.js compile --tsp-location C:\workspace\azure-rest-api-specs\specification\elasticsan\resource-manager\Microsoft.ElasticSan\ElasticSan --out-dir . --capture-ir`
- **Emitted IR:** `C:\workspace\playground\tsp-test\ir\0-emit.json` (schemaVersion 0.2.0; 59 models / 21 enums / 30 operations / 202 properties)
- **Emitter:** `@azure-client-engine/typespec-emitter`; **IR schema:** `@azure-client-engine/ir`
- **Compared against:** authored `.tsp` + TCGC-expanded program (not the swagger), api-version 2026-04-01-preview only.

**Legend**
- `[schema]` — the IR has no field to hold this (needs an IR schema addition).
- `[emitter]` — the IR could hold it but the emitter doesn't populate it.
- `[design]` — documented deferral in `packages/typespec-emitter/src/lib.ts`.
- **silent** — no diagnostic is emitted (only C and E warn, each once).

---

## Design decisions (cross-cutting — settled in discussion)

These principles govern how each A–N bullet is resolved (resolution is still done
per-bullet, individually):

- **Fidelity target:** ace's generated client is intentionally **different from Azure's
  official C# SDK**. The spec's `"csharp"`-scoped decorators encode that official SDK's
  shaping, so ace **ignores every C#-client-specific override**.
- **Source-faithful emit:** ace compile+emit honors whatever the source declares **for
  ace's TCGC scope** — unscoped / all-scope decorators (incl. unscoped `@clientName`)
  apply; other-language scopes do not — and applies **no naming conventions of its own**.
  Decorators (including unscoped `@clientName`) are part of the source; the emitter
  respects whatever the source provides for ace's scope.
- **Where ace shaping lives:** ace-specific renaming / overriding is layered **later by
  transformer pipeline stages** reading the **named `CompileConfig`** (exposed read-only on
  the transform/generate contexts as `ctx.config`). The former free-form
  `CompileInput.extensions` dict was removed (item F) — every app-resolved setting is now a
  named field on `CompileConfig`. No ace shaping is ever added to the spec via decorators.
- **Resolving a bullet:** a *client-shaping* loss may be **intentional** (ace re-applies
  it via `config`, not the spec); a *contract-truth* loss **must** be preserved (usually
  an IR-schema addition).

---

## Naming (`@clientName` / `@alternateType`)

Property renames ARE captured: IR `name` = PascalCase + **unscoped** `@clientName`
(e.g. `EncryptionProperties.identity`→`EncryptionIdentity`, `KeyVaultProperties.keyName`→`KeyName`,
`ManagedByInfo.resourceId`→`ResourceId`); IR `wireName` = wire truth. All 202 props have `name != wireName`.
What's lost is only the **`"csharp"`-scoped** customizations — the emitter registers with TCGC as
`@azure-client-engine/typespec-emitter` (`options.ts:30`), which TCGC does not treat as the `csharp`
scope, so every `"csharp"`-scoped decorator is ignored.

- [x] <a id="a1"></a>**A1 — Type (model/enum) `@clientName(...,"csharp")` renames — RESOLVED (by design, no change).**
  ace emit is source-faithful for ace's TCGC scope. Type names emit as the source resolves them for
  ace (`Volume`, `SkuName`, `ProvisioningStates`); the other-language-scoped renames
  (`ElasticSanVolume`, `ElasticSanSkuName`, `ElasticSanProvisioningState`; ~30 csharp renames in
  `client.tsp`) are **not ace's** and are correctly ignored. Unscoped `@clientName` *is* source and is
  honored. ace-specific type renaming, if ever needed, is layered later by a transformer via
  `CompileInput.extensions` — never by adding decorators to the spec. **No emitter change.**

  - [ ] **TODO A1.1 — Make ace's TCGC scope token deliberate** → full task + `context.emitterName` trace: [TODO-1](#todo-1) in the [TODOs](#todos) section.
- [x] **A2 — Enum-member `@clientName(...,"csharp")` renames — RESOLVED.**
  Same as [A1](#a1): csharp-scoped renames aren't ace's and are ignored; source names are honored
  (`Premium_LRS`, not `PremiumLrs`); `enums.ts` applies no casing. No change.
- [x] <a id="a3"></a>**A3 — Property/param `csharp` renames — RESOLVED.**
  Same as [A1](#a1): csharp-scoped renames are ignored, and params are already source-verbatim
  (`x-ms-delete-snapshots`, not csharp `deleteSnapshots`). **Caveat:** the emitter still PascalCases
  property + response-header `name` (a C# convention that conflicts with "no conventions") — tracked
  as TODO A3.1.

  - [ ] **TODO A3.1 — Strip PascalCase from the emitter (source-verbatim names)** → [TODO-2](#todo-2) in the [TODOs](#todos) section.
- [x] **A4 — `@alternateType(...,"csharp")` type overrides — RESOLVED.**
  Same as [A1](#a1): all 11 sites are csharp-scoped, so ace ignores them and emits the **source**
  type (`VolumeProperties.volumeId` → `string`). The earlier `[schema+emitter]` tag is moot (not
  consumed → no IR-format work). Note: bullet **N** (`armResourceIdentifier` used *directly* as a
  scalar → `string`) is a separate type-mapping question, still open. No change.

## Constraints

- [x] <a id="b1"></a>**B1 — `@minLength` / `@maxLength` — RESOLVED (will support; tracked).**
  Contract truth → must be preserved. These live in the compiler stateMap (not on the type) and TCGC
  doesn't surface them, so the emitter must extract them via `getMinLength`/`getMaxLength(program,
  prop.__raw)` and the IR needs a field to hold them. Targets: ElasticSan 3–24, Volume 3–63,
  VolumeGroup 3–63, Snapshot 1–80.

  - [ ] **TODO B1.1 — Support `@minLength` / `@maxLength`** → [TODO-3](#todo-3) · issue [Azure/CLIPS#529](https://github.com/Azure/CLIPS/issues/529).
- [x] <a id="b2"></a>**B2 — `@pattern` (`NamePattern`) — RESOLVED (will support; tracked).**
  Contract truth. Same mechanism as [B1](#b1): read `getPattern(program, prop.__raw)` and add a
  `pattern` field to the IR. Verified `ResourceNameParameter` needs no special handling — it expands
  to a standard `@pattern` with the concrete regex (readable on both the `name` property and the path
  param). Folded into [TODO-3](#todo-3) · issue [Azure/CLIPS#531](https://github.com/Azure/CLIPS/issues/531).
- [x] <a id="b3"></a>**B3 — `@minValue` / `@maxValue` — RESOLVED (will support; tracked).**
  Contract truth. Same mechanism as [B1](#b1)/[B2](#b2): read `getMinValue`/`getMaxValue(program,
  prop.__raw)` and add numeric bounds to the IR `constraints`. Target here: `@minValue(0)` on
  `DeleteRetentionPolicy.retentionPeriodDays`. Folded into [TODO-3](#todo-3) · issue
  [Azure/CLIPS#532](https://github.com/Azure/CLIPS/issues/532).

## Enums / values

- [x] <a id="c"></a>**C — Extensible (`union { string, … }`) enums flattened to closed — RESOLVED (by design for now; tracked).**
  For now every open enum is emitted as closed (`open-enum-as-closed`, one-shot warning). 17 instances:
  SkuName, SkuTier, ProvisioningStates, PrivateEndpointServiceConnectionStatus, PublicNetworkAccess,
  AutoScalePolicyEnforcement, IdentityType, StorageTargetType, EncryptionType, Action, PolicyState,
  VolumeCreateOption, OperationalStatus, DeleteType, x-ms-delete-snapshots, x-ms-force-delete,
  x-ms-access-soft-deleted-resources. Preserving open/closed-ness is planned.

  - [ ] **TODO C.1 — Add `isFixed` to enums (open vs closed)** → [TODO-4](#todo-4) · issue [Azure/CLIPS#541](https://github.com/Azure/CLIPS/issues/541).
- [x] <a id="d"></a>**D — Default values (`= value` syntax) — RESOLVED (will support; emitter-only, no schema change).**
  Contract truth. **`[emitter]`** (not `[schema]` — the IR already has the fields:
  `IRProperty.defaultValue` in `model.ts` and `IRParameter.defaultValue` in `operation.ts`, both
  `IRLiteralValue`). Verified findings:
  - TCGC surfaces `= value` defaults **inconsistently**: params get one (`clients.js:53` reads
    `param.defaultValue`), but a model property's `clientDefaultValue` comes **only** from the
    `@clientDefaultValue` decorator (`types.js:998`), **not** the `= value` syntax — so it's empty
    for these (verified: 0/173 ElasticSan model props carry a TCGC default; the lone param default is
    the synthetic `apiVersion`).
  - The value lives on the compiler node, reachable via `sdkProp.__raw.defaultValue` (a compiler
    `Value`) — same `__raw` escape hatch as constraints (B1–B3). Verified `VirtualNetworkRule.action`
    → `StringValue "Allow"`.
  - **Emitter change:** in `propertyFrom` (`properties.ts`, ~L118-126) add an
    `else if (prop.__raw?.defaultValue !== undefined)` branch converting the compiler `Value` →
    `IRLiteralValue` (new `literalFromValue` helper beside `literalFromConstant`); today it only
    handles `unwrapped.kind === "constant"`. Same for params in `paramOf` (`http.ts`) — either
    `p.__raw?.defaultValue` or TCGC's `p.clientDefaultValue`.
  - **ElasticSan target:** `VirtualNetworkRule.action?: Action = Action.Allow` → `defaultValue "Allow"`.

  - [ ] **TODO D.1 — Populate `= value` defaults (properties + params)** → [TODO-5](#todo-5) · issue [Azure/CLIPS#549](https://github.com/Azure/CLIPS/issues/549).

## Auth / versioning / visibility

- [x] <a id="e"></a>**E — Service authentication — RESOLVED (implemented in code).**
  Contract truth. Added an `IRAuth` node (`packages/ir/src/auth.ts`; OR-of-options / AND-of-schemes,
  discriminated by `kind`: oauth2 / apiKey / http / openIdConnect / noAuth) on `IRSource.auth`, and an
  emitter mapper (`packages/typespec-emitter/src/auth.ts` `authFrom`) that reads
  `getAuthentication(program, serviceNamespace)` and maps it faithfully — oauth2 keeps the **full**
  flow set (`IROAuth2Flow` discriminated by flow `type`: authorizationCode / implicit / password /
  clientCredentials, with every URL + scopes as `{value, description?}`); only the non-serializable
  `model` back-ref is dropped. The `auth-not-emitted` warning + flag were removed. Verified on real
  ElasticSan → oauth2 `azure_auth`, `implicit` flow, `authorizationUrl`
  `https://login.microsoftonline.com/common/oauth2/authorize`, scope
  `https://management.azure.com/.default` (0 errors, warning gone); tests: emitter 9/9 (added apiKey
  positive-path fixture + pets no-auth path). ARM source: `@armProviderNamespace` (`main.tsp:33`) →
  `setAuthentication` (ARM `namespace.js:126`).
  - [ ] **TODO E.1 — Per-operation / per-interface auth override** → [TODO-6](#todo-6) · issue [Azure/CLIPS#550](https://github.com/Azure/CLIPS/issues/550).
- [x] <a id="f"></a>**F — Multi-version collapsed to a single snapshot — RESOLVED (version selection is now a config input).**
  Each compile still emits **one** snapshot (multi-version-in-one-IR remains out of scope by
  design), but *which* snapshot is now caller-controlled instead of always latest.
  `CompileConfig.apiVersion` (a specific version, `"latest"` = default, or `"all"`) threads
  end-to-end: `CompileInput.config.apiVersion` → `compile()` → `tsp-compile` producer maps it
  onto `TspCompileRequest.apiVersion` → IPC → `tsp-compile-worker` injects the kebab
  `"api-version"` key into the emitter's `options` block → TCGC's `createSdkContext` reads
  `context.options["api-version"]` and re-projects the `@versioned` program **before** the IR is
  built. Two fixes made it work: (1) the emitter option-schema (`options.ts` + `lib.ts`) now
  **declares** `"api-version"` — its `additionalProperties:false` schema was rejecting the key
  outright (`invalid-schema`); (2) `resolveOptions` treats it as a pass-through (like `sourceId`),
  since the emitter never reads it (TCGC reads the raw `context.options`). CLI surface:
  `--api-version <v>` (sugar for / overrides `config.apiVersion`). **Also removed** the
  free-form `CompileInput.extensions` dict as part of this work — the single named
  `CompileConfig` is now the only app→compile channel (exposed read-only as `ctx.config` on the
  transform/generate contexts). Verified on real ElasticSan: `--api-version 2025-09-01` → 58
  models / 18 enums vs. default latest → 59 / 21 (`encryptionInTransit` + `SoftDeleting` present
  only at latest); an out-of-range version reaches TCGC → `api-version-undefined` warning + latest
  fallback. Tests: CLI 30/30 (added `--api-version` happy path + `config.apiVersion`→TCGC forward),
  compiler 14/14 (extensions test rewritten to `config`), emitter 9/9; `verify-ace.ps1` 21/21.
- [x] <a id="g"></a>**G — Visibility granularity reduced to `readOnly` / `writeOnly` — RESOLVED (full 5-phase visibility preserved).**
  Contract truth. Replaced the `readOnly`/`writeOnly` boolean pair on `IRPropertyCommon` with an
  optional `visibility?: IRVisibility[]` where `IRVisibility = "read" | "create" | "update" |
  "delete" | "query"` (ace-owned string enum; the IR stays TypeSpec-free — no `@typespec/http`
  import). The emitter (`properties.ts` `visibilityPhases`) maps TCGC's resolved
  `prop.visibility` (a `@typespec/http` `Visibility` numeric-flags array) → the phase strings.
  **Option (b) — omit-when-unconstrained:** a property visible in all five phases (TypeSpec's
  default when no `@visibility` is declared) omits the field entirely (absent = all phases),
  keeping the IR terse like `defaultValue`/`docs`. `IR_SCHEMA_VERSION` bumped 0.2.0 → 0.3.0
  (breaking property shape); `pets.ir.json` re-baselined. Verified on real ElasticSan — the exact
  create-vs-update distinction that was lost is now preserved:
  - Read+Create (immutable after create): `ElasticSanProperties.baseSizeTiB`,
    `.extendedCapacitySizeTiB` → `["read","create"]`; read-only e.g. `provisioningState` → `["read"]`.
  - Update-only: `ElasticSanUpdateProperties.baseSizeTiB`, `.extendedCapacitySizeTiB` → `["update"]`.
  - Unconstrained (`sku`, `availabilityZones`, `publicNetworkAccess`, …) → omitted
    (104 of 202 props carry visibility, 98 omit).
  Design note: `getSdkVisibility` reads *declared lifecycle* visibility (TCGC types.js:893), so the
  phase set is independent of HTTP operation context; a consumer wanting read-only can still derive
  it (`has "read" && !"create" && !"update"`). Tests: emitter 11/11 (added a `visibility-service.tsp`
  fixture asserting `["read"]` / `["read","create"]` / omitted, plus a pets omission check); CLI
  30/30, compiler 14/14.

## Array / flatten / grouping / misc

- [x] <a id="h"></a>**H — `@identifiers` (x-ms-identifiers) dropped — RESOLVED (ignored by design).**
  **What it is:** `@identifiers` is an ARM-library decorator
  (`Azure.ResourceManager.identifiers`, `@azure-tools/typespec-azure-resource-manager`
  `lib/decorators.tsp:276`; impl `$identifiers` `resource.js:926` → stores a `string[]` in the
  program stateMap `armIdentifiers`, read via `getArmIdentifiers` `resource.js:946`). It names the
  properties that **uniquely identify the elements of an array** (e.g. `@identifiers(#["name"])`;
  `#[]` = "no identifying property"). Its sole consumer is the **swagger/AutoRest** side: it emits
  the OpenAPI `x-ms-identifiers` extension, which tells ARM/AutoRest how to **match array elements
  when diffing/merging during PATCH** (array-merge identity), and it drives ARM's
  `missing-x-ms-identifiers` linter rule. It is *not* client-shaping and *not* wire-contract: the
  array, its element type, and the JSON payload are all identical with or without it.
  **Why ignore:** (1) it is a swagger-generation/ARM-PATCH-merge concern, orthogonal to the C#
  client ace generates; (2) TCGC — the client-relevant projection ace builds on — already drops it
  entirely (0 references; verified), consistent with ace leaning on TCGC's client view; (3) the
  actual contract (array + element shape) is fully preserved in the IR, so nothing about the wire
  payload is lost. If ace ever implements PATCH array-merge semantics it can be revived by reading
  `getArmIdentifiers(ctx.program, prop.__raw)` into a new IR field (adds an ARM-package dep). Source:
  `SkuInformation.locationInfo` / `.capabilities` (both `@identifiers(#[])`, `models.tsp:274,281`).
- [x] <a id="i"></a>**I — `@Legacy.flattenProperty` intent dropped — RESOLVED (emitter now records the hint; no schema change).**
  Client-shaping hint. Unlike H, **TCGC surfaces this**: `@Legacy.flattenProperty`
  (`Azure.ClientGenerator.Core.Legacy`, `legacy.tsp:171`; impl `$flattenProperty`
  `decorators.js:425`) is read by `shouldFlattenProperty` (`decorators.js:443`) and stamped onto
  `SdkModelPropertyType.flatten` (`types.js:1015`). ElasticSan applies it with scope `"!javascript"`
  (`back-compatible.tsp`); ace's emitter name resolves to scope **`engine`** (parseEmitterName regex
  `client-engine`), and since `engine ∉ {javascript}` the negation scope applies →
  **`flatten=true`** (empirically: 9 `.properties` bags). The IR already had the slot
  (`ObjectPropertyHints.flatten: FlattenHint = { into: "self" | "never" }`, `hints.ts:66,110`), so
  this was purely `[emitter]`: `properties.ts` `flattenHintFor(prop)` now sets
  `rendering.flatten = { into: "self" }` on object properties when `prop.flatten` is true, omitting
  otherwise (absent = not flattened). **Intent only — the emitter never actually hoists**; a future
  transform/generator decides the C# rendering (config-driven flatten policy layered there, not in
  the emitter — per the source-vs-ace-shaping boundary). Wire truth is untouched either way.
  Verified on ElasticSan: exactly the 9 bags (ElasticSan, VolumeGroup, Volume, Snapshot,
  PrivateEndpointConnection, PrivateLinkResource, ElasticSanUpdate, VolumeGroupUpdate, VolumeUpdate)
  → `rendering.flatten.into="self"`. Tests: emitter 12/12 (added `flatten-service.tsp` fixture that
  also pins the `"!javascript"`→`engine` scope resolution); CLI 30/30, compiler 14/14.
- [x] <a id="j"></a>**J — `@summary` vs `@doc` collapsed to one `docs` field — RESOLVED (added `summary` to the IR; emitter reads both).**
  Contract truth. `@summary` and `@doc` are **core** TypeSpec decorators (`compiler/lib/std/decorators.tsp:17,30`)
  and TCGC surfaces them as **two separate strings** on every doc-bearing type:
  `doc = getClientDoc(...)` (`types.js:1001`; core `@doc`/doc-comment folded with any `@clientDoc`
  append/replace, `internal-utils.js:735`) and `summary = getSummary(...)` (`types.js:1002`, core
  `@summary`). ace previously mapped only `.doc → docs: string` and dropped `.summary`. Added a
  parallel optional `summary?: string` beside every `docs?: string` on the 8 nodes whose TCGC type
  carries summary — `IREnumMember`, `IREnum`, `IRPropertyCommon`, `IRModel`, `IRParameter`,
  `IRRequestBody`, `IRResponseHeader`, `IROperation` (source of `.summary`:
  `SdkTypeBase`/`SdkModelPropertyTypeBase`/`SdkServiceMethodBase`, `interfaces.d.ts:169,449,807`).
  **Not** added to `IRResponse` (uses `.description`, no `.summary`) or `IRServerParameter`/`IRSource`
  (no TCGC summary). Emitter now emits `summary` alongside `docs` at all 8 sites (omitted when absent).
  `IR_SCHEMA_VERSION` 0.3.0 → 0.4.0 (additive); `pets.ir.json` re-baselined (pets has no `@summary`).
  ElasticSan reality: only one `@summary` (`SkusOperationGroup.list`, `routes.tsp:21`) whose text is
  *identical* to its doc-comment — so nothing was lost *here*, but the summary channel is now
  preserved for specs where they diverge. Verified on ElasticSan: `list` op carries both
  `summary`+`docs`, plus 5 ARM library models with summary. Tests: emitter 13/13 (added
  `summary-service.tsp` fixture with `@summary` ≠ `@doc` on model/property/operation, plus a
  doc-comment-only property → summary omitted); CLI 30/30, compiler 14/14.
- [x] <a id="k"></a>**K — `@clientLocation` (operation grouping/relocation) — RESOLVED (false alarm; already honored via `id.namespace`, no change).**
  Client-shaping, and — like [A1](#a1) — a **scoped** TCGC decorator (`$clientLocation(context, source,
  target, scope)`, stored/read via `setScopedDecoratorData`/`getScopedDecoratorData`, `decorators.js:1003,1046,1056`),
  resolved for ace's `engine` scope by the *same* mechanism as `@clientName`. Same principle as A1
  (source-faithful for ace's scope); here the outcome is *honored* rather than *ignored* because
  ElasticSan's `@clientLocation`s are **unscoped** or **`"!csharp"`**, both of which apply to `engine`
  (the `"!csharp"` was added, per the source comment, to spare the *official C#* SDK's resource
  detection — ace isn't that SDK, so it correctly receives the relocation). The emitter walks the TCGC
  **client tree** (`operations.ts:4`) and `opIdFor` bakes the relocated client name into `id.namespace`,
  so the audit's original "flat, grouping lost" claim was wrong. **Empirically verified** — every
  relocation is reflected: `SkusOperationGroup.list`→`…Skus`, `ElasticSans.listByElasticSan`→
  `…PrivateLinkResources`, `VolumeGroups.preBackup`/`preRestore`→`…Volumes`, `Snapshots.*`→
  `…VolumeSnapshots`. **No emitter/schema change.**
  - Full capability surface (TCGC doc): `@clientLocation` has two categories — (1) **operation
    relocation** [`source=Operation`] to a *new named* sub-client (string), an *existing* Interface, or
    the *root client* (Namespace) — all captured by ace via `id.namespace`; and (2) **parameter
    relocation** [`source=ModelProperty`] between operation-level and client-`.clientInitialization`.
    ElasticSan uses only category (1).
  - [ ] **Follow-up K.1 — Category-2 parameter relocation / client-init params** (not used by
    ElasticSan, no current loss). `@clientLocation(param → client)` lifts a param onto the client's
    `.clientInitialization`; ace's IR has **no client-init-param concept** (ops carry their own
    path/query/header/body params), so such a param would silently vanish. `client → operation`
    (keep-on-op) is fine. This is orthogonal to operation grouping — it belongs to a future
    **client-initialization modeling** effort (also relevant to ARM's default `subscriptionId`/endpoint
    elevation). Flag for later; assign an issue when client-init modeling is scoped.
- [x] <a id="l"></a>**L — `@usage` / `@access` not represented — RESOLVED (both now carried to the IR; internal no longer dropped).**
  Contract truth + client-shaping. Both are **scoped TCGC decorators**, but TCGC *computes* `.usage`
  (a `UsageFlags` bitfield) and `.access` (public/internal) on **every** type regardless of any
  decorator, and surfaces them on `Sdk*` types (`types.js:1001-1015`, `interfaces.d.ts:167-169,…`).
  Per the user's directive — *"the emitter should carry everything the TSP source provides to the IR,
  not dropping anything"* — ace now stores both:
  - **usage**: new `IRUsage[]` (ace-owned string union of the `UsageFlags` members — `input`, `output`,
    `json`, `xml`, `lroInitial`, …; `primitives.ts`), on `IRModel` + `IREnum`. Decoded from the
    bitfield by `annotations.ts` `usageFrom`; omitted only when empty (`Usage.None`).
  - **access**: new `IRAccess = "public" | "internal"` (`primitives.ts`), on `IRModel` + `IREnum` +
    `IROperation` + `IRProperty`. `annotations.ts` `accessFrom` omits `"public"` (the default) and
    carries `"internal"`.
  - **Stopped dropping internal-access types/operations.** The emitter previously *skipped* every
    `access === "internal"` model/enum/operation with a one-shot `internal-access-skipped` warning
    (that diagnostic + the `internalAccessWarned` flag are removed). They are now emitted with
    `access: "internal"`, so nothing is lost.
  `IR_SCHEMA_VERSION` 0.4.0 → 0.5.0; `pets.ir.json` re-emitted. The `@versioned` api-version enum is
  still folded onto `IRSource.apiVersion` (represented, not dropped). The `@@usage(PrivateLinkResource,
  Usage.input, "csharp")` is `"csharp"`-scoped → doesn't apply to ace's `engine` scope (same as
  [A1](#a1)) — **verified**: `PrivateLinkResource` emits `usage=[output,json]`, *not* `input`. ElasticSan
  has 0 internal types/ops, so all 59 models carry `usage` and none carry `access` (all public). Tests:
  emitter 15/15 (added `access-service.tsp` fixture proving usage flags + internal model *and*
  operation now emitted with `access:"internal"`, public omitted); CLI 30/30, compiler 14/14.
  - Note (omit convention): `access` absent = `public`, `usage` absent = `Usage.None` — lossless
    encodings consistent with the IR's `visibility`/`flatten` handling; no information is dropped.
- [x] <a id="m"></a>**M — `@Legacy.markAsPageable` dropped — RESOLVED (by design; modern paging already handled, this op is genuinely non-paginated + csharp-scoped force).**
  The audit misframed this as a general loss. **Modern pagination = core TypeSpec `@list` + `@pageItems`
  + `@nextLink` (+ `@continuationToken`/`@offset`/`@pageIndex`/`@pageSize`)** (`compiler/lib/std/decorators.tsp:690,741,775,…`);
  `Azure.Core.Page<T>` is the ARM template stamping `@pageItems value` + `@nextLink nextLink`.
  `@Legacy.markAsPageable` merely force-flags an op for generators that can't otherwise detect paging.
  ace already resolves modern paging two ways (`semantics.ts`): TCGC `pagingMetadata` (path 1) and the
  compiler's `getPagingOperation` decorators (path 2). **Verified** — 8 ElasticSan ops are pageable
  (`items=value`, `nextLink=nextLink`), all via `Azure.Core.Page<T>`.
  The only excluded list is `ElasticSans.listByElasticSan` (→ `PrivateLinkResources`), for two
  independent reasons: (1) its result `PrivateLinkResourceListResult` has `@pageItems value` but **no
  `@nextLink`** (`models.tsp:962`) — a **single-page** collection, so `assemblePageable` (`semantics.ts:115`)
  correctly drops the pageItems-only signal (the IR pageable requires nextLink XOR continuation); and
  (2) the `@Legacy.markAsPageable(ElasticSans.listByElasticSan, "csharp")` forcing it (`client.tsp:106`)
  is **`"csharp"`-scoped** → doesn't apply to ace's `engine` scope (same as [A1](#a1)). The response
  **contract is fully preserved** — `PrivateLinkResourceListResult.value: PrivateLinkResource[]` is a
  normal emitted model property; only the `@pageItems` marker on a nextLink-less result is uncarried,
  which is a client-shaping choice ace declines (single-page ≠ pageable). **No emitter/schema change.**
- [x] <a id="n"></a>**N — Direct `armResourceIdentifier` scalar → plain `string` — RESOLVED (semantic scalars now carry an IR `format`).**
  Contract truth. Azure.Core's `armResourceIdentifier` (and `uuid`, `ipV4Address`, `ipV6Address`)
  `extends string`, so TCGC flattens them to `kind: "string"` — but **retains their identity in
  `crossLanguageDefinitionId`** (verified: `DiskSnapshotList.diskSnapshotIds` element →
  `name="armResourceIdentifier"`, `crossLanguageDefinitionId="Azure.Core.armResourceIdentifier"`).
  ace's `formatFor` (`types.ts`) previously only recognized date/duration/url/bytes, so the identity
  was dropped → `string[]`. Now `formatFor` maps `crossLanguageDefinitionId` → the IR `format` hint via
  `SEMANTIC_SCALAR_FORMATS`: `Azure.Core.armResourceIdentifier`→**`arm-id`** (new `FormatKind`),
  `Azure.Core.uuid`→`uuid`, `Azure.Core.ipV4Address`/`ipV6Address`→`ip-address` (the `uuid`/`ip-address`
  formats were already declared but never populated). Added `"arm-id"` to `FormatKinds`
  (`primitives.ts`); `IR_SCHEMA_VERSION` 0.5.0 → 0.6.0; `pets.ir.json` re-emitted. **Verified** on
  ElasticSan: `DiskSnapshotList.diskSnapshotIds` → `{array, element:{primitive string, format:"arm-id"}}`
  (the 1 direct usage; the `@@alternateType(…, armResourceIdentifier, "csharp")` occurrences stay
  csharp-scoped → ignored, [A4](#a4)). Tests: emitter 16/16 (added `scalar-service.tsp` fixture asserting
  arm-id/uuid/ip-address on scalar + array props, plain string unformatted); CLI 30/30, compiler 14/14.
  - Note: no Azure.Core `hostname` scalar exists, so the pre-declared `hostname` format has no source to
    map from (left declared for completeness); `eTag`/`azureLocation` have no IR format and remain plain
    `string` (add formats later if a generator needs them).

---

## Secondary accuracy item (not a loss — verify separately)

- [ ] **All 16 LRO operations resolved to `pollingStrategy=location` / `finalStateVia=location`.**
  Confirm each matches its ARM `LroHeaders` (`ArmLroLocationHeader` + `RetryAfterHeader`); no
  `ambiguous-polling-strategy` / `unknown-final-state-via` warnings fired.

## Verified preserved (no loss) — bounds the list above

All 59 models / 21 enums / 30 operations / 202 properties present · **docs** (100% models, ~99.5%
properties, enum-member docs) · `wireName`, `optional`, `nullable`, `readOnly` · inheritance chains ·
`Record<>`→dictionary (`tags`, `userAssignedIdentities`) · arrays · HTTP framing (method / path /
path+query+header params incl. `$filter`, `deleteType`, the `x-ms-*` headers) · request bodies ·
responses (status codes, error flag, body types) · **LRO** semantics + async headers (Location,
Retry-After) · **pageable** (nextLink / items) · api-version (via source stamp) · base scalar formats
(`url`→uri, `utcDateTime`→date-time) · property renames (unscoped `@clientName` + PascalCase).

---

## TODOs

Actionable follow-ups pulled out of the bullets above. Each item records the bullet it came from.
New todos append here.

### <a id="todo-1"></a>TODO-1 — Make ace's TCGC scope token deliberate

**Belongs to:** [A1](#a1) — source-faithful emit / scope identity.

The honor/ignore boundary IS TCGC scope resolution, keyed by `context.emitterName`. Today that token
is the *incidental* `engine` — an accident of the org name, not a decision. Choose an intentional token
and set the `emitterName` string handed to `createSdkContext` so `parseEmitterName` yields it. This is
**not** bypassing scope resolution: unscoped decorators stay honored, other-language scopes stay excluded.

**Trace `context.emitterName` (TCGC 0.68.4):**
1. `packages/typespec-emitter/src/emitter.ts` (~L45): `createSdkContext(context, options.emitterName, { exportTCGCoutput: false })`.
2. `options.emitterName` defaults to `@azure-client-engine/typespec-emitter` — `packages/typespec-emitter/src/options.ts` (`EMITTER_OPTION_DEFAULTS.emitterName` ~L30, applied by `resolveOptions` ~L41).
3. TCGC `createSdkContext` → `createTCGCContext(program, emitterName ?? context.options["emitter-name"])` — `node_modules/@azure-tools/typespec-client-generator-core/dist/src/context.js` (~L100–102).
4. `createTCGCContext` sets `emitterName: parseEmitterName(program, emitterName ?? program.emitters[0]?.metadata?.name)` — `context.js` (~L10–15).
5. `parseEmitterName` regex `/(?:cadl|typespec|client|server)-([^\\/-]*)/` — `internal-utils.js` (~L144–162). `@azure-client-engine/typespec-emitter` matches **`client-engine` first** → captures `engine`.
6. Consumed as the scope: `getScopedDecoratorData` (~L52–71) and `listScopedDecoratorData` (~L26–51) use `scope = languageScope ?? context.emitterName`.

**Empirical check** (Node REPL / `.mjs`):
`"@azure-client-engine/typespec-emitter".match(/(?:cadl|typespec|client|server)-([^\\/-]*)/)?.[1]` → `"engine"`

**Gotcha:** the org segment `azure-client-engine` always matches `client-engine` first, so renaming the
package *suffix* won't change the token. Pass an explicit `emitterName` (e.g. a dedicated constant such
as `"typespec-ace"` → token `ace`) to `createSdkContext`, decoupled from the package name.

**Acceptance:** `context.emitterName` equals the chosen token; unscoped-decorator honoring is unchanged;
other-language (`csharp` / `python` / …) scopes stay excluded.

### <a id="todo-2"></a>TODO-2 — Strip PascalCase from the emitter (source-verbatim naming)

**Belongs to:** [A3](#a3) — source-faithful emit / no conventions.

Per the source-faithful, no-conventions rule (see [A1](#a1)), the emitter must not apply C# casing.
Property + response-header `name` should emit **verbatim** as the source resolves them for ace's scope;
C# PascalCasing moves to a downstream transformer.

**Change sites:**
- `packages/typespec-emitter/src/properties.ts` (~L135): `name: pascalCase(libName)` → `name: libName`.
- `packages/typespec-emitter/src/http.ts` (~L198, response headers): `name: pascalCase(h.name)` → `name: h.name`.
- `packages/typespec-emitter/src/naming.ts`: `pascalCase` becomes unused here — drop it or relocate to the casing transformer.

**Safe because:** property ids already use the pre-cased `libName` (`properties.ts` ~L130 / ~L85) and
`wireName` is independent, so only the logical `name` changes. Params and enum members are already
verbatim (`http.ts` ~L85, `enums.ts` ~L49-50). Unscoped `@clientName` stays honored (it's part of `libName`).

**Aligns with** `packages/ir/src/model.ts` — property `name` is documented as "Logical (C#-facing) name.
Mutable by renaming transforms." Casing is a transform's job, not the emitter's.

**Acceptance:** e.g. `ElasticSanProperties.totalMBps` emits `name: "totalMBps"` (not `TotalMBps`);
`wireName` and ids unchanged; unscoped renames (e.g. `EncryptionIdentity`) still present; C# casing is
applied later by a transformer.

### <a id="todo-3"></a>TODO-3 — Support validation constraints (length / pattern / numeric bounds)

**Belongs to:** [B1](#b1), [B2](#b2), [B3](#b3) — contract truth.

**Decorators & issues:**
- `@minLength` / `@maxLength` (string length) — [Azure/CLIPS#529](https://github.com/Azure/CLIPS/issues/529)
- `@pattern` (regex) — [Azure/CLIPS#531](https://github.com/Azure/CLIPS/issues/531)
- `@minValue` / `@maxValue` (numeric bounds) — [Azure/CLIPS#532](https://github.com/Azure/CLIPS/issues/532)

Add these constraints to the IR and populate them in the emitter (TCGC surfaces no constraints, so
extraction goes through the compiler).
- **IR schema** (`packages/ir/src`): add a `constraints` object to `IRProperty` / `IRParameter`
  (`minLength` / `maxLength` / `pattern` / `minValue` / `maxValue`).
- **Emitter** (`properties.ts`, and the param path in `http.ts`): read `getMinLength` / `getMaxLength`
  / `getPattern` / `getMinValue` / `getMaxValue` from `@typespec/compiler` against `prop.__raw` (the
  `ModelProperty` TCGC exposes); check the property and, if absent, its scalar type.
- **`ResourceNameParameter` (verified):** no special handling — the template expands to standard
  `@pattern` / `@minLength` / `@maxLength` with concrete values, present on the resource `name` body
  property **and** the path parameter (both carry `__raw`).
- **Guards:** treat an empty/whitespace `getPattern` result (`""`, e.g. `NamePattern=""` on
  PrivateEndpointConnection) as no constraint; note numeric bounds can legitimately be `0` (e.g.
  `@minValue(0)`), so use an `undefined` check, not falsy; decide whether constraints land on the body
  property only or also the path parameter.
- **ElasticSan targets:** names — ElasticSan 3–24, Volume 3–63, VolumeGroup 3–63, Snapshot 1–80;
  patterns per resource (PrivateEndpointConnection has none); `@minValue(0)` on
  `DeleteRetentionPolicy.retentionPeriodDays`.

**Acceptance:** e.g. `ElasticSan.name` emits `minLength: 3`, `maxLength: 24`, `pattern:
"^[A-Za-z0-9]+((-|_)[a-z0-9A-Z]+)*$"`; `DeleteRetentionPolicy.retentionPeriodDays` emits `minValue: 0`;
empty patterns omitted; values round-trip through `parseIR`.

### <a id="todo-4"></a>TODO-4 — Preserve open vs closed enums (`isFixed`)

**Belongs to:** [C](#c) — contract truth. **Issue:** [Azure/CLIPS#541](https://github.com/Azure/CLIPS/issues/541).

Today every enum is emitted closed; the open/closed distinction (TCGC's `SdkEnumType.isFixed`) is lost.
Plan:
1. **IR schema** (`packages/ir/src/enum.ts`): add an `isFixed` boolean to `IREnum` (open enum → `false`,
   closed → `true`).
2. **Emitter** (`packages/typespec-emitter/src/enums.ts`): set `isFixed` from TCGC's `e.isFixed` instead
   of flattening to closed + one-shot warning (the `open-enum-as-closed` diagnostic can then be removed).
3. **Generator:** use the enum values for auto-completion regardless; when `isFixed === true`, additionally
   emit validation that rejects values outside the member set (open enums accept unknown values).

**Acceptance:** open unions (e.g. `SkuName`, `ProvisioningStates`) emit `isFixed: false`; genuinely closed
enums emit `isFixed: true`; round-trips through `parseIR`; no `open-enum-as-closed` warning.

### <a id="todo-5"></a>TODO-5 — Populate `= value` defaults (properties + params)

**Belongs to:** [D](#d) — contract truth. **Issue:** [Azure/CLIPS#549](https://github.com/Azure/CLIPS/issues/549).

Emitter-only (the IR fields already exist: `IRProperty.defaultValue`, `IRParameter.defaultValue`, both
`IRLiteralValue`). TCGC does not surface the TypeSpec `= value` default on model properties
(`clientDefaultValue` is only set by the `@clientDefaultValue` decorator), so read it from the compiler
node via `__raw`.
- **Emitter — properties** (`properties.ts`, `propertyFrom`): today only `unwrapped.kind === "constant"`
  yields a default. Add `else if (prop.__raw?.defaultValue !== undefined)` converting the compiler
  `Value` → `IRLiteralValue` via a new `literalFromValue` helper (StringValue / BooleanValue /
  NumericValue / NullValue), beside the existing `literalFromConstant`.
- **Emitter — params** (`http.ts`, `paramOf`): set `defaultValue` too — either `p.__raw?.defaultValue`
  (uniform with properties) or TCGC's `p.clientDefaultValue` (populated for params).
- **`__raw` guard:** `__raw` is optional (undefined for TCGC-synthesized nodes), so guard it — those
  nodes never carry a user default anyway (matches the existing `properties.ts:107` pattern).
- **ElasticSan target:** `VirtualNetworkRule.action?: Action = Action.Allow` → `defaultValue`
  `{ kind: "string", value: "Allow" }` (enum default resolves to its wire string).

**Acceptance:** `VirtualNetworkRule.action` emits `defaultValue: { kind: "string", value: "Allow" }`;
booleans / numbers / null round-trip; values validate through `parseIR`.

### <a id="todo-6"></a>TODO-6 — Per-operation / per-interface auth override

**Belongs to:** [E](#e) — contract truth. **Issue:** [Azure/CLIPS#550](https://github.com/Azure/CLIPS/issues/550).

E emits only the **service-level** default (`IRSource.auth`, from `getAuthentication(program, serviceNs)`).
TypeSpec lets `@useAuth` be applied at three levels — it targets `Namespace | Interface | Operation`
(`@typespec/http` `$useAuth`) — and an override at interface/op level **replaces** (not merges) the
inherited auth for that scope. `@useAuth(NoAuth)` turns auth off for a scope. (Library-injected auth via
`setAuthentication` — e.g. ARM — lands in the same state and is already handled.)

Plan:
- **IR schema:** add an optional `IROperation.auth?: IRAuth` carrying an operation's *effective*
  override (present only when it differs from `IRSource.auth`).
- **Emitter** (`auth.ts` / `operations.ts`): resolve per-op effective auth via
  `getAuthenticationForOperation(program, op.__raw)` (walks op → interface → namespace, first match wins),
  or use `resolveAuthentication(httpService)` which returns `{ defaultAuth, operationsAuth }` in one pass.
  Prefer `resolveAuthentication`: it also **de-dupes scheme ids** (collisions get a trailing `_`) and
  **merges oauth2 scopes** for a repeated scheme id — behavior the current simple mapper skips. Set
  `IROperation.auth` only when the resolved op auth differs from the source default (avoid noise).
- **`__raw` guard:** operation `__raw` is optional (synthesized ops) — guard as elsewhere.

**Acceptance:** an op with its own `@useAuth` (or an interface-level one) emits `IROperation.auth`
reflecting the override; ops inheriting the service default omit it; `@useAuth(NoAuth)` yields a `noAuth`
scheme; round-trips through `parseIR`. (ElasticSan declares no per-op override, so this is exercised by a
dedicated fixture, like the service-level apiKey fixture.)
