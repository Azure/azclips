## Context

Azclips's Cirrus codegen pipeline (`toolkit/packages/{ir,typespec-emitter,compiler}`) already
goes TypeSpec -> `IR` (complete) -> a `TransformStep` pipeline + `Generator` (backbone complete,
but `identityPipeline`/`noopGenerator` — empty). This change designs what a PowerShell-targeting
`TransformStep` should produce; it does not implement the C# generator itself.

**Real hand-written precedent (traced end-to-end during design: `NewResourceGroupProcessor.cs` +
`NewResourceGroupCmdlet.cs` + `Resources/ExtensionIndex.cs`)**:
- A **Cmdlet** class: `[Cmdlet(verb, noun, SupportsShouldProcess, ConfirmImpact)]` +
  `[OutputType]` + per-property `[Parameter(Mandatory, ParameterSetName)]`. `ProcessRecord` is
  near-identical boilerplate everywhere: build a `Dictionary<string,object>` from
  `MyInvocation.BoundParameters`, construct a `CommandInvocation(extensionName, invocationName,
  parameters, ...)`, call `engine.OpenExecutionSession(invocation, streams, token).Run()`. The
  Cmdlet never references its Processor type directly — resolution is by name via
  `CommandMetadata`/`IExtensionIndex` at runtime.
- A **Processor** class (e.g. `NewResourceGroupProcessor : ResourcesProcessorBase<ResourceGroup>`):
  properties decorated with `[ArgumentInfo(Required, Description, SerializedName, ...)]`
  (duplicating the Cmdlet's own parameters today, by hand). The only override needed is
  `CreateRequestMessage()`, building the raw `HttpRequestMessage`; the shared
  `*ProcessorBase<T>` (one per extension) already handles send/deserialize/error-mapping/
  telemetry. Azclips has **no separate AutoRest-style low-level client class** — the Processor
  fuses "layer 2" (HTTP call) and business logic.
- An **`ExtensionIndex`** registration: one `CommandMetadata` per (processor x client-type) — a
  `_PS` entry (PascalCase parameter keys, `InvocationName` like `"New-AzResourceGroup"`) and a
  `_CLI` entry (kebab-case keys, `"group create"`), both pointing at the same `ProcessorType`.
- `GetResourceGroupCmdlet` already merges Get+List operations into ONE cmdlet with two parameter
  sets (`"Get"`, `"List"`) — real precedent for multi-operation merge (see Decision 5).

**AutoRest.PowerShell** (reference only — not a dependency, not architecturally compatible) was
mined for reusable *policy*, not architecture: `create-commands-v2.ts`/`ps-namer-v2.ts`/
`modifiers-v2.ts`, the `PwshModel`/`CommandOperation` shapes, `docs/directives.md`'s
`select`/`where`/`set` customization grammar, and `docs/autorest-configuration.md`'s default
`verb-mapping:` table. Its single-class-calls-generated-client-directly model and proxy-cmdlet
layer are explicitly rejected — they don't match azclips's Processor-based, CLI+PS dual-surfaced
architecture.

## Goals / Non-Goals

**Goals:**
- PowerShell surface only (confirmed scope decision — no CLI dual-registration this change).
- The new schema targets/is a superset of azclips's EXISTING runtime attribute shapes
  (`ArgumentInfoAttribute`, `[Parameter]`, `[Cmdlet]`/`[OutputType]`, `ProcessorInfoAttribute`,
  `CommandMetadata`) rather than inventing a disconnected parallel model.
- One canonical parameter representation drives both the Cmdlet's `[Parameter]` and the
  Processor's `[ArgumentInfo]`, eliminating today's hand-written duplication/drift risk.
- Include a customization/override mechanism (directive-style), designed now rather than deferred.
- Reuse IR's existing `FlattenHint` as-is for body-expansion; no new IR field for this.

**Non-Goals:**
- CLI-surface command generation/dual-registration (`_CLI`-style `CommandMetadata` entries) —
  deferred; only the `_PS` shape is in scope.
- The actual Roslyn/C# generator implementation that consumes this schema — separate, later
  effort; `noopGenerator` remains a stub after this change.
- `AsJob` (background-job execution) — real, near-universal need in mature Az PowerShell
  modules, but blocked on a named prerequisite: azclips's `Engine`/`ExecutionSession` runs
  commands synchronously (`session.Run()`) with no async/job execution path today. Modeling an
  `AsJob` schema field now would be dead vocabulary with nothing to generate against. Revisit
  once `Engine` gains background-execution support.
- Azure API-profile gating (AutoRest's `ProfileAttribute`, e.g. Azure Stack Hub multi-version
  support) — architectural mismatch, not a maturity gap: it solves multi-simultaneous-API-version
  builds, while azclips builds one API version per module. Revisit only if azclips adopts
  multi-profile builds.
- `DefaultProfile`/`Break`/`HttpPipelineAppend`/`HttpPipelinePrepend` (legacy Az PowerShell
  context-override/debugger-attach/HTTP-pipeline-interception parameters) — tied to the legacy
  Az PowerShell HTTP-pipeline runtime, which azclips's `Engine` deliberately doesn't replicate.
  Azclips's real equivalent is `-Profile` — resolved as generator-level boilerplate, not schema
  vocabulary (see Decision 3's closing note): it's operation-independent infrastructure injected
  identically into every command, unlike every other schema field, which traces back to a
  specific IR operation/parameter/model.
- The breaking-change/deprecation attribute family (`CmdletBreakingChangeAttribute`,
  `ParameterBreakingChangeAttribute`, `OutputBreakingChangeAttribute`,
  `ParameterSetBreakingChangeAttribute`, `PreviewMessageAttribute`) — a large, self-contained
  vocabulary (versioning, effective dates, replacement-cmdlet suggestions) that deserves its own
  dedicated design effort as a follow-on change, not folded into this schema's initial scope. The
  simpler lifecycle-tag concept (Preview/Deprecated/Experimental/GA) this family also covers is
  already in scope via `hints.powershell`'s help/lifecycle-tag fields (Decision 3) — only the
  richer migration-messaging mechanics are deferred.
- `ModelCmdletAttribute`-style helper cmdlets (e.g. `New-AzXObject`, for constructing complex
  nested model instances as pipeline input) — no azclips pattern needs complex nested-object
  pipeline construction today, and Decision 5's Expanded/flattened variant already covers the
  common case. Revisit if flattening proves insufficient for deeply-nested models.
- `ExternalDocsAttribute` — low value-add (a single URL link) relative to complexity; a safe,
  purely-additive field to add later with no architectural impact, so it isn't worth the initial
  scope.
- AutoRest-internal bookkeeping attributes with no identified azclips use case even on merit:
  `ExportAsAttribute` (enum export-type override), `OriginAttribute` (flatten provenance —
  already covered by reusing `FlattenHint`, see Decision 6), `ConstantAttribute`,
  `InternalExportAttribute`, `NotSuggestDefaultParameterSetAttribute`.
- `FormatTableAttribute`/`DoNotFormatAttribute` (format.ps1xml table-column layout) — output
  *formatting* (view layer) is orthogonal to command/parameter *shape* modeling; a distinct
  future schema if azclips adopts custom table views, not this change's concern.
- `ViaIdentity` and `ViaJsonString`/`ViaJsonFilePath` variant fan-out — no proxy-cmdlet-layer
  need justifies them yet.
- A generated `[Category]` (Path/Query/Body/Runtime) wire-location attribute — would duplicate
  what IR's operation model already encodes (path/query/body parameter kind) rather than fill a
  gap. Only the `Azure`/`Runtime` categories (cross-cutting globals like `-AsJob`/`-DefaultProfile`
  with no IR equivalent) are a real open question, tracked under the `-Profile`-as-vocabulary
  discussion above.

## Decisions

### 1. Layering: two-stage derivation, three generated artifacts per command

**Decision**: Derivation happens in two separate stages, mirroring AutoRest.PowerShell's own
real split (see Decision 8): (i) the `create-ps-commands` `TransformStep` — genuinely `IR -> IR`,
since that's what the real `TransformStep` interface requires — enriches each `IROperation`'s
`hints.powershell` in place (verb, noun, parameter-set name, `ConfirmImpact`), the same way
AutoRest's `CommandOperation extends ProgrammaticOperation` bolts PS fields onto the same
operation object; (ii) a separate, plain function, `derivePsCommands(ir): PsCommand[]` (in the
`ps-commands` package, not a pipeline stage), groups operations sharing a resolved verb+noun
into one `PsCommand` with one `PsParameterSet` per source operation. Together these drive generation
of the three artifacts azclips already hand-writes: (a) a Cmdlet class (attributes + boilerplate
`ProcessRecord`), (b) a Processor class subclassing the extension's existing `*ProcessorBase<T>`
with `[ArgumentInfo]`-decorated properties and a `CreateRequestMessage()` built from the IR
operation's method/pathTemplate/requestBody (illustrative forward context — the actual
request-building codegen is the later, separate C# generator's concern; this change produces the
enriched IR + `PsCommand[]` model, not C# output), (c) an `ExtensionIndex` `CommandMetadata`
registration entry (PS-only `ClientType`).

**Rationale**: Matches the real, currently-working convention exactly (see Context) — new
generated code integrates with the existing `Engine`/`ExecutionSession`/telemetry/help
infrastructure with no special-casing, and reviewers/maintainers see the same shape they already
know from hand-written extensions. The two-stage split isn't optional structuring — it's required
by the real `TransformStep` interface (`run(ir: IR, context): IR`, and `IR` is a strict Zod
schema with no room for an arbitrary `PsCommand[]`), discovered when implementation started. See
Decision 8 for the full reasoning and the AutoRest precedent that validates this exact split.

**Alternative considered**: AutoRest's model — one class per cmdlet variant that both carries
`[Cmdlet]`/`[Parameter]` attributes AND calls a generated low-level API client directly in
`ProcessRecord`. Rejected: this doesn't match azclips's actual architecture, where business logic
is deliberately separated into a shared-CLI+PS `IProcessor`, not fused into the PS surface.

**Note on CLI sharing the Processor**: the Processor is consumed by both `_PS` and `_CLI`
`CommandMetadata` entries, yet this (nominally PS-scoped) vocabulary still owns its
`[ArgumentInfo]` fields. This is safe today because the Processor is already surface-agnostic in
the real code: `ExtensionIndex.cs`'s `s_newResourceGroup_PS` and `s_newResourceGroup_CLI` point at
the identical `typeof(Processors.NewResourceGroupProcessor)`, with the same `[ArgumentInfo]`
properties — only `CommandMetadata.Parameters`' key casing (`"Name"` vs `"name"`) and
`InvocationName` differ per surface. If CLI's parameter needs ever diverge from PS's at the
Processor level, this schema would need a CLI-aware dimension too — but per the Non-Goals above,
that's an explicitly deferred, not a live, concern.

### 2. Vocabulary lives as IR hints, not a parallel schema

**Decision**: Per-parameter/per-command PS vocabulary (mandatory, wireName, mutability flags,
help, etc.) is modeled as `hints.powershell` fields directly on `IROperation`/`IRParameter` —
mirroring AutoRest's real `CommandOperation extends ProgrammaticOperation` (same object, PS
facet added), adapted to IR's hint-mutation mechanism since IR nodes are immutable Zod data, not
classes that can be subtyped. The separate `ps-commands` package holds only the thin
cross-operation aggregate (`PsCommand`/`PsParameterSet`, referencing IR operation ids) that hints
structurally cannot express — not a redeclaration of `ArgumentInfoAttribute`/`[Parameter]`/
`[Cmdlet]`/`[OutputType]`/`CommandMetadata`'s fields.

**Rationale**: These runtime types are already in production use and consumed by existing
infrastructure (argument binding, telemetry, help); reusing their field shapes (now as hints,
not a redundant parallel package) avoids a translation-layer maintenance burden. Attaching the
data directly to the same IR node it describes — rather than a separate schema that references
the node by id — is both the AutoRest-precedented approach and requires no new package to hold
fields that are properties of a single node, not a cross-node relationship.

**Alternative considered**: A full independent `ps-commands` schema redeclaring every
per-parameter/per-command field (the original plan). Rejected once the real `TransformStep`
interface and AutoRest's actual `CommandOperation` precedent were checked — it would duplicate
data that already has a natural home on the IR node itself, and the package would need to
re-implement id-based dereferencing for data that's naturally already collocated in IR.

### 3. Vocabulary scope (as `hints.powershell` fields)

**Decision**: Vocabulary is limited to what's enumerated in Context/Non-Goals, modeled as
`hints.powershell` fields on `IROperation.rendering` (per-command: Verb/Noun/
`SupportsShouldProcess`/`ConfirmImpact`/`OutputType`/Alias plus a `ProcessorInfoAttribute`-style
Summary/Examples/Tags(Preview/Deprecated/Experimental/GA) block and an `HttpPath`-equivalent
— cheap since IR already carries the path template/api-version) and on `IRParameter.rendering`
(per-parameter: Mandatory, `ParameterSetName`, `HelpMessage`, pipeline-binding, wildcards,
aliases, `SerializedName`, `ValidateSet` values, completer reference, mutability flags
(`readOnly`/`create`/`update`, mirroring AutoRest's real `InfoAttribute.ReadOnly/Create/Update`
— needed so generated Update variants correctly omit create-only/server-computed properties),
and an optional default-value expression (script/name/set-condition, mirroring
`DefaultInfoAttribute` — needed for patterns like defaulting `-SubscriptionId` from the current
context)).

**Rationale**: `ProcessorInfoAttribute`'s `Tags`/`Examples` already give PlatyPS-consumable help
metadata for free — the schema doesn't need to invent its own help model. The mutability flags
and default-value expression were confirmed as real, low-cost gaps (not present in azclips's
current `ArgumentInfoAttribute`) by cross-checking AutoRest's actual runtime attribute
definitions (`InfoAttribute`/`DefaultInfoAttribute` in a real generated module's
`generated/runtime/` folder) rather than just AutoRest's config docs — both map directly onto
real, everyday Az PowerShell needs (Update cmdlets that must not resend read-only properties;
parameters that default from ambient context).

**Alternative considered**: Port AutoRest's full vocabulary including the breaking-change/
preview/API-profile/`ExternalDocs`/`Category`/format-table attributes. Rejected per the
reasoning in Non-Goals above — each is either an architectural mismatch, a large separate effort,
or redundant with IR — not simply "unused today."

**Resolved note — `-Profile`**: `-Profile` stays generator-level boilerplate, not schema
vocabulary. It's identical, operation-independent infrastructure injected into every command
(today, via `ExtensionIndex`'s static-constructor loop) — unlike every field above, it has no
connection to any IR operation/parameter/model, so folding it into `hints.powershell` would
model something the schema has no way to derive.

### 4. Verb/noun policy

**Decision**: Verb is derived from the IR operation's leading action word via a table lookup
seeded from AutoRest's real default table (`autorest.powershell/powershell/
autorest-configuration.md`'s `verb-mapping:` block: `Create`->`New`, `CreateOrUpdate`->`New`,
`Delete`->`Remove`, `Get`->`Get`, `List`->`Get` (merged as a parameter set — see Decision 5),
`Update`->`Update`). **A table match always wins regardless of HTTP method.** Only when the
action word has no table entry at all does the schema fall back to an HTTP-method-based default:
PUT->`Set`, DELETE->`Remove`, PATCH->`Update`, GET->`Get`, POST->`Invoke`. Noun is the
singularized resource/model name plus a configurable subject prefix (confirmed real: `"Az"` in
`AzResourceGroup`).

**Rationale**: This is a solved problem with an existing, tunable default table; reusing it
avoids re-deriving verb heuristics from scratch and stays consistent with PowerShell's approved
verbs guidance that AutoRest's table already encodes. Table-match-wins precedence is required by
real precedent, not just convenience: azclips's own hand-written `NewResourceGroupProcessor`
issues an HTTP PUT for `ResourceGroups.createOrUpdate`, yet the real cmdlet is `New-AzResourceGroup`
(verb `New`), not `Set-AzResourceGroup`. An earlier draft of this decision special-cased PUT to
always win for `Update`/`CreateOrUpdate` actions — that would have generated the wrong verb for
this exact, already-shipping cmdlet. The HTTP-method fallback exists only for the case where the
operationId's action word doesn't match any table entry at all (e.g. a non-standard verb word),
mirroring how AutoRest's own table has independent `Create: New` and `Put: Set` entries rather
than one conditioned on the other.

**Alternative considered**: Derive verbs purely from HTTP method (GET->Get, PUT->Set, POST->
Invoke, DELETE->Remove, PATCH->Update) without an action-word table. Rejected as too coarse —
loses the ability to distinguish e.g. `Reboot`->`Restart` or `Capture`->`Export` from a generic
POST->`Invoke` fallback, and — per the `ResourceGroups` case above — would misname well-known
create operations that are implemented as PUT.

### 5. Variant fan-out for v1

**Decision**: Two distinct mechanisms, both needed, and both implemented in `ps-commands`'s
`derivePsCommands()` grouping function (not inside the `create-ps-commands` `TransformStep` itself —
see Decision 8):
  (a) **Multi-operation merge**: when two or more IR operations under the same `id.namespace`
      resolve to the same verb+noun (e.g. `get` + `list`), merge them into one `PsCommand` with
      one parameter set per source operation (real precedent: `GetResourceGroupCmdlet`'s
      `"Get"`/`"List"` parameter sets).
  (b) **Single-operation variant fan-out**: limited to `base` (whole body object) and `Expanded`
      (flattened body via IR's existing `FlattenHint`) for v1. `ViaIdentity` and
      `ViaJsonString`/`ViaJsonFilePath` are deferred (see Non-Goals).

**Rationale**: (a) is a real, already-adopted azclips convention, distinct from AutoRest's
per-operation variant model, and must be modeled explicitly rather than conflated with (b).
Limiting (b) to two variants matches azclips's simpler no-proxy-cmdlet architecture — there's no
current product need for identity-piping or JSON-escape-hatch variants.

**Alternative considered**: Adopt all four of AutoRest's variants (base/Expanded/ViaIdentity/
ViaJsonString) up front. Rejected — no proxy-cmdlet layer or build-time customization system
exists in azclips to justify the extra generated surface area yet; revisit if product need
emerges.

### 6. Flatten/body-expansion

**Decision**: Reuse IR's existing `FlattenHint` (`toolkit/packages/ir/src/hints.ts`) as-is for
the `Expanded` variant. No new IR field.

**Rationale**: IR already models this; adding a parallel PS-specific flattening heuristic would
create two sources of truth for the same concept.

**Alternative considered**: A PS-specific flatten threshold/naming heuristic (mirroring
AutoRest's own bespoke logic). Rejected — unnecessary given IR already has this.

### 7. Customization/override hook

**Decision**: A directive-style (`select`/`where`/`set`) configuration, passed as the
`create-ps-commands` `TransformStep`'s `options` (matching `CompileConfig.pipeline`'s existing
per-step options bag), for bulk/discoverable renames/hides/adjustments — applied during the same
enrichment stage that writes resolved values into `hints.powershell`, layered on top of inline
`hints.powershell` overrides for single-node authoring overrides. Modeled on AutoRest's
`docs/directives.md` grammar.

**Rationale**: Directives give bulk, greppable, version-controllable overrides without touching
generated-from-IR authoring hints per node; this mirrors a proven, working mechanism. Applying
them inside `create-ps-commands` (rather than in the later `derivePsCommands()` grouping step) keeps
all of a command's naming/shape resolution — inference, directives, inline hints — in one place,
before grouping ever reads the result.

**Alternative considered**: azure-powershell's `custom/` partial-class pattern (post-generation,
hand-written overrides). Explicitly out of scope for this change — that's a generator-stage
concern (how generated code coexists with hand overrides), not a schema-stage one.

**Precedence**: when a directive and an inline `hints.powershell` override both target the same
node, the inline hint wins — it is authored at the specific node, while a directive is a bulk/
pattern-based default. Directives apply first as defaults; inline hints are resolved last.

### 8. `create-ps-commands` stays IR -> IR; grouping is a separate function

**Decision**: `create-ps-commands` is a genuine `TransformStep` — `run(ir: IR, context): IR` — that
enriches each `IROperation`'s `hints.powershell` in place (verb, noun, parameter-set name,
`ConfirmImpact`) rather than returning a `PsCommand[]`. A separate, plain function,
`derivePsCommands(ir: IR): PsCommand[]` (in `ps-commands`, not a pipeline stage), reads the enriched
IR afterward and groups operations sharing a resolved verb+noun into one `PsCommand` with one
`PsParameterSet` per source operation.

**Rationale**: This was discovered as a hard implementation blocker, not chosen for style: the
real `TransformStep` interface (`toolkit/packages/compiler/src/transform.ts`) requires returning
an `IR`, and `IRSchema` is a `.strict()` Zod schema (`schemaVersion`/`sources`/`models`/`enums`/
`operations`) with no room for an arbitrary `PsCommand[]`. The `Generator` interface (`generate.ts`)
is similarly constrained — `{ files: Map<string,string> }`, meant for text output, not
structured objects. Checking AutoRest.PowerShell's own real architecture confirmed this exact
two-stage split is the working precedent, not an azclips-specific workaround: its
`create-commands-v2.ts` plugin enriches each operation individually (`CommandOperation extends
ProgrammaticOperation`, with a single `variant: string` — no merge field), while the actual
grouping into one exported command with multiple parameter sets happens in a completely separate,
later stage — `resources/psruntime/BuildTime/Models/PsProxyTypes.cs`'s `VariantGroup`/`Variant`
classes, which run via C# reflection over already-generated proxy cmdlet classes, grouping them
by `CmdletName`/`CmdletNoun`. AutoRest doesn't do the merge inside its per-operation enrichment
step either.

**Alternative considered**: Extend `GeneratorOutput` with an optional generic `data?: unknown`
field so `create-ps-commands` could run as a `Generator` and return `PsCommand[]` via `files`
(serialized as a JSON manifest, e.g. `commands.json`). Rejected for this change — it would
modify the shared `compiler` package's public interface for every future generator, a more
invasive change than reusing IR's existing hint-mutation mechanism, which already solves the
same problem for every other per-node PS override.

## Open Questions (Phase 2/3 design work still in progress — not yet resolved)

- Exact Zod type definitions for `ps-commands`'s thin `PsCommand`/`PsParameterSet` aggregate —
  drafted as implementation tasks in `tasks.md`, not finalized here.
- Exact field list/types for the `powershell` hints namespace in `hints.ts` — vocabulary is
  decided (Decision 3) but not yet typed as Zod schema; drafted as a task in `tasks.md`.
- Exact directive grammar (the `select`/`where`/`set` config shape and its TransformStep-options
  binding) — high-level mechanism is decided (Decision 7); the concrete grammar is a task in
  `tasks.md`.

**RESOLVED**: home for the new, thin Zod aggregate package is `toolkit/packages/ps-commands`
(npm name `@azure-client-engine/ps-commands`), matching the existing
`@azure-client-engine/<name>` convention (`ir`, `compiler`, `typespec-emitter`, `cli`). Renamed
from the original `ps-model` once its scope shrank to just the `PsCommand`/`PsParameterSet`
aggregate + `derivePsCommands()` (see Decisions 2 and 8) — "model" no longer fit once it stopped
holding the full vocabulary.

## Appendix: Full Schema Reference

A consolidated, field-by-field view of everything this change models, so nothing already decided
gets lost across Decisions/tasks. Each field is tagged with the milestone (`tasks.md`) that
introduces it. Split into two parts per Decisions 2/8: per-node vocabulary lives as
`hints.powershell` on IR; the cross-operation grouping shape lives in `ps-commands`.

### `hints.powershell` on `IROperation.rendering` (per-command)
| Field | Milestone | Notes |
|---|---|---|
| `verb` | 0 | Resolved by `create-ps-commands`; overridable by authors |
| `noun` | 0 | Bare, singularized, WITHOUT subject prefix (e.g. `"ResourceGroup"`) — resolved by `create-ps-commands`; overridable by authors |
| `subjectPrefix` | 0 | Own field, not baked into `noun` (e.g. `"Az"`) — combined with `noun` at grouping time into `PsCommand.noun` |
| `parameterSetName` | 0 | Source-operation-derived (e.g. `"Get"`, `"List"`) |
| `confirmImpact` | 0 | From HTTP method; overridable by authors |
| `alias` | 1 | |
| `hidden` | 1 | |
| completer reference | 1 | Mirrors `CompleterInfoAttribute` |
| `summary` / `description` / `examples` | 1 | Mirrors `ProcessorInfoAttribute` |
| lifecycle tag | 1 | Preview / Deprecated / Experimental / GA |
| `httpPath` equivalent | 1 | Path template + api-version |

### `hints.powershell` on `IRParameter.rendering` (per-parameter)
None of this exists yet in Milestone 0 — `PsParameterSet` is a bare `{ name, sourceOperationId }`
reference (task 0.5) with no parameter-level detail, and M0's own proof (task 0.15) only asserts
`PsCommand`-level fields. All of it, including `mandatory`, is introduced together in Milestone 1.

| Field | Milestone | Notes |
|---|---|---|
| `mandatory` | 1 | |
| `parameterSetName` | 1 | |
| `helpMessage` | 1 | |
| `valueFromPipeline` / `valueFromPipelineByPropertyName` | 1 | |
| `supportsWildcards` | 1 | |
| `aliases` | 1 | |
| `allowedValues` | 1 | `ValidateSet` equivalent |
| completer reference | 1 | Mirrors `CompleterInfoAttribute` |
| `readOnly` / `create` / `update` | 1 | Mutability flags; mirrors `InfoAttribute.ReadOnly/Create/Update` |
| default-value expression | 1 | Mirrors `DefaultInfoAttribute` (script/name/set-condition) |

### `ps-commands` package: `PsParameterSet` (thin aggregate)
| Field | Milestone | Notes |
|---|---|---|
| `name` | 0 | Source-operation-derived (e.g. `"Get"`, `"List"`) |
| `sourceOperationId` | 0 | Reference into IR \u2014 hints.powershell holds the parameter details |

### `ps-commands` package: `PsCommand` (thin aggregate)
| Field | Milestone | Notes |
|---|---|---|
| `verb` / `noun` | 0 | Copied from the grouped operations' resolved `hints.powershell` |
| `subjectPrefix` | 0 | Configurable, e.g. `"Az"` |
| `supportsShouldProcess` / `confirmImpact` | 0 | Copied from `hints.powershell` |
| `outputTypes` | 0 | By-id refs into IR |
| `parameterSets` | 0 | `PsParameterSet[]` \u2014 one per merged source operation |
| `sourceOperationIds` | 0 | IR operation id(s) this command derives from |

### `derivePsCommands(ir: IR): PsCommand[]` (plain function, not a pipeline stage)
Groups IR operations sharing a resolved `hints.powershell.verb`+`noun` (within the same
`id.namespace`) into one `PsCommand` with one `PsParameterSet` per source operation. Also applies
single-operation variant fan-out (`base`/`Expanded`, reusing `FlattenHint`) per Decision 5.

### Directive grammar (`select`/`where`/`set` — applied inside `create-ps-commands`, Milestone 2)
- `select`: `command` \| `parameter`
- `where` (command): `verb`, `subject`, `subject-prefix`, `parameter-name`
- `where` (parameter): `parameter-name`, optionally `verb`/`subject`/`subject-prefix`
- `set`: alias, hidden, and other per-selector overrides
- Precedence: inline `hints.powershell` wins over directives (Decision 7)

### Explicitly excluded (see Non-Goals for full reasoning)
CLI dual-registration, the actual C# generator's output, `AsJob`, API-profile gating, legacy
`DefaultProfile`/`Break`/HTTP-pipeline parameters, the breaking-change/deprecation attribute
family, `ModelCmdletAttribute`-style object-constructor cmdlets, `ExternalDocsAttribute`,
AutoRest-internal bookkeeping attributes (`ExportAs`/`Origin`/`Constant`/`InternalExport`/
`NotSuggestDefaultParameterSet`), `FormatTableAttribute`/`DoNotFormatAttribute`,
`ViaIdentity`/`ViaJsonString`/`ViaJsonFilePath` variants, and the `[Category]` wire-location
attribute. `-Profile` is resolved as generator-level boilerplate, not modeled vocabulary (see
Decision 3's closing note).


