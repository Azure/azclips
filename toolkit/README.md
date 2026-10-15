# Azure Client Engine compiler

## Dependency Management

### Emitter

The `@azure-client-engine/typespec-emitter` package uses the **peer + dev duplication** pattern for TypeSpec compiler and related libraries:

- **`peerDependencies`** declare the broad version ranges that consumers must provide (e.g. `"@typespec/compiler": ">=1.7.0 <2.0.0"`). These describe compatibility, not what gets installed when developing this package.
- **`devDependencies`** mirror every peer entry with a specific version that satisfies the peer range (e.g. `"@typespec/compiler": "^1.12.0"`). This is what actually gets installed locally so that `tsc`, tests, and IDEs can resolve the modules during development.

Both lists must stay in sync. To guard against drift, the workspace uses [`syncpack`](https://jamiemason.github.io/syncpack/) at the root, configured in `.syncpackrc.json` with two rules:

1. Every dependency that appears in `peerDependencies` must also appear in `devDependencies`, and the dev version must satisfy the peer range.
2. `peerDependencies` versions must remain ranges (no pinned or caret/tilde-only versions).

**Workflow for adding or upgrading a TypeSpec / Azure peer dependency:**

```bash
# from repo root
npm install -w @azure-client-engine/typespec-emitter --save-dev <pkg>@<version>
# then add or update the matching entry in "peerDependencies" by hand
npm run lint:deps   # verify both lists agree; non-zero exit fails CI
```

**Useful scripts (run from repo root):**

| Script              | Purpose                                                        |
|---------------------|----------------------------------------------------------------|
| `npm run lint:deps` | Fail if a peer dep is missing from devDeps or versions drift. |
| `npm run fix:deps`  | Auto-rewrite mismatched versions where safe.                  |
| `npm run build`     | Build all workspace packages via TypeScript project references.|
| `npm run schema`    | Emit the IR JSON Schema artifact from the Zod IR schema.      |
| `npm run schema:check` | Fail if the emitted IR JSON Schema is stale vs. the Zod schema (CI). |

## Building the whole toolkit

`npm run build` (`tsc -b`) builds only the **TypeScript** packages (`ir`,
`typespec-emitter`, `compiler`, `transformers`, `cli`). The **C# generator** under
`packages/generator` is a standalone .NET package and is *not* part of that build.

For the full cross-toolchain build — TypeScript **and** the .NET generator +
its out-of-process host — run the toolkit build script from the toolkit root:

```powershell
./build.ps1                       # Debug
./build.ps1 -Configuration Release
```

This is the single entry point CI/pipelines should call; it wraps `npm run build`
and `dotnet build` so callers need not know the per-package commands. `npm run
build` stays the fast inner loop for TypeScript-only work.

## Configuration: `aceconfig.yaml`

`ace compile` reads one file per generated extension: `ace compile <path>`, where
the path names the file or a folder holding `aceconfig.yaml`, or
`./aceconfig.yaml` when no path is given. The CLI takes the keys it uses itself,
the run inputs, and passes every other key to the compiler untouched:

```yaml
schema-version: 1                   # required

# Run inputs, taken by the CLI. Relative paths resolve against this file's folder.
tsp-location: ../azure-rest-api-specs/specification/widget/Contoso.Widgets   # the folder to copy
additional-directories:             # folders it imports from outside it; relative to tsp-location
  - ../Contoso.Widgets.Shared
entrypoints:                        # one, for now
  - path: client.tsp                # relative to tsp-location; tsp-location itself when absent
    api-version: 2024-05-01
output-dir: ./generated             # default: this file's folder

# Document facts, for seed-ir.
root-namespace: Contoso.Widgets
description: Widgets for AzClips

# The compiler's own setting: disable a stage, or set its stage config.
stages:
  flatten-model:
    options:
      inliningThreshold: 24
```

- **Paths** resolve against the file's folder, so the same file works from any
  directory, and output goes next to it unless `output-dir` says otherwise. The
  path given to `ace compile` resolves against the current directory.
- **Flags** (`--tsp-location`, `--entrypoint`, `--api-version`, `--out-dir`)
  override the run inputs; no flag sets a stage or a fact. Without a file,
  `--tsp-location` and `--out-dir` are required.
- **Staging:** `tsp-location` must be inside an azure-rest-api-specs checkout:
  the first folder at or above it that holds `specification/`, `.github/` and
  `.azure-pipelines/`. It is copied into `<output-dir>/source` at its path in the
  repo, and each `additional-directories` entry at its own, so imports between
  them keep working; a folder outside the repo lands directly under `source/`, by
  name. `node_modules`, `.git` and `tsp-output` are never copied, and ACE reads no
  `tspconfig.yaml`. `ACE_ALLOW_STANDALONE_SPEC=1` copies a spec from outside a
  checkout as is, for tests and ad-hoc specs.
- **Checks:** the CLI checks its own keys as every stage checks its stage config.
  A key it does not know inside an entrypoint is a warning; an invalid or missing
  value, or a YAML syntax error, is an error that stops the run before anything is
  staged. Staging checks every folder before it touches `<output-dir>/source`.

The design and its rationale:
[`DocDumpster/Aceconfig-Generation-Time-Config.md`](DocDumpster/Aceconfig-Generation-Time-Config.md).

## Orchestration

The **compiler owns the workflow**. `STAGE_CATALOG` in
`packages/compiler/src/stages.ts` declares what stages exist, in what order they
run, and whether each runs by default:

```ts
{ id: "tsp-emit",       kind: "emit",      default: "mandatory" }
{ id: "seed-ir",        kind: "transform", default: "mandatory" }
{ id: "tweak-ir",       kind: "transform", default: "on" }
{ id: "flatten-model",  kind: "transform", default: "on" }
{ id: "create-command", kind: "transform", default: "on" }
{ id: "csharp",         kind: "generate",  default: "on" }
```

The resolved plan is a **single linear sequence**. One IR leaves the emit stage
and flows through every enabled transform in catalog order; each generate stage
renders the IR as it stands when its turn comes. There are no branches, so a run
has exactly one IR and the compiler never copies it. Array position *is* the
order.

A host has exactly one orchestration verb: **disable an optional stage**. It cannot
append, insert, or reorder. A stage's section under `stages` can also carry its
options:

```yaml
# aceconfig.yaml
stages:
  flatten-model:
    disabled: true
  tsp-emit:
    options:
      warnAsError: true
```

Disabling a stage simply removes it from the sequence — there is no tree to
reattach, so position stays intrinsic for free. Naming an unknown stage, or
disabling a `mandatory` one, is an error rather than a silent no-op. A run with
no generate stage is reported as a warning: it runs, but writes no files.
`stages` is the compiler's own setting, so it checks the shape: a non-boolean
`disabled` or non-map `options` is an error before any stage runs, and an unknown
key in a section is a warning. A disabled stage's options are ignored.

### `requires` — dependency, not order

In a linear sequence every stage already runs after every earlier one, so
ordering carries no dependency information. A stage whose output would be
*wrong* without an earlier stage has to say so explicitly:

```ts
{ id: "virtual-params", kind: "transform", default: "on", requires: ["flatten-model"] }
```

Disabling a required stage is an error naming both ends, which turns "I
regenerated and the output was quietly wrong" into a config failure you see
immediately. Each id must name an earlier, IR-producing stage.

### Stage discipline (convention, not enforced)

A generate stage renders whatever the IR looks like at its position, so catalog
order carries a rule that no code checks:

1. **Stages that alter existing IR go before the first generate stage**, so
   every backend renders the same canonical IR.
2. **Stages after the first generate stage are purely additive** — new,
   backend-namespaced fields only — so one backend's annotations are inert to
   another's instead of corrupting it.

Language- and client-specific transforms therefore belong in group 2. This is
upheld by review rather than by a mechanism: the catalog is the single place
ordering is decided, and no host can insert into it.

### What a generator accepts

Because a generate stage renders the IR *at its position*, the orchestrator holds
a plain `IR` and cannot know statically that an earlier transform produced the
richer document a given backend needs. A generator therefore declares its input:

```ts
interface Generator<TIn extends IR = IR> {
  readonly name: string;
  readonly accepts: (ir: IR) => GeneratorAcceptance<TIn>;  // required iff TIn narrows IR
  generate(ir: TIn, context: GeneratorContext): void | Promise<void>;
}
```

`accepts` returns the shape `safeParse` already produces, so a Zod schema *is* an
acceptance check — the built-in `csharp` backend sets `accepts:
safeParseTransformedIR` and receives a genuine `TransformedIR`. The orchestrator
runs the check before dispatch and reports `invalid-generator-input` itself, so
the failure is uniform across backends and names the stage whose position
produced the document.

The requirement is conditional on purpose: a backend happy with a bare `IR` omits
`accepts`, while one that narrows *cannot* omit it, so it can never claim a
richer input than it actually established. That pairs with the catalog's
`requires`, which says the same thing at config time.

### Stage implementations, failure, and output

**The compiler owns the implementation behind every stage id.** It maps each
transform id to its step in `@azure-client-engine/transformers`
(`packages/compiler/src/transforms.ts`), so a host supplies no stage code. The two
external stages (`tsp-emit`, `csharp`) run through the host `Runtime`, which is
all a host provides. Each stage gets only its own stage config, in its context as
`config`: the `options` of its section under `stages`. `tsp-emit` reads
`warnAsError`, `trace`, `nostdlib`, `additionalImports` and `emitter` (the
emitter's own options) from its config; the
API version travels with the entrypoint in `CompileInput`. `stages` is the
compiler's only setting, so every other config key is a document fact, and the
compiler hands those to `seed-ir` as its stage config (see
[Generation target](#generation-target)).

Packages layer as `cli` → `compiler` → `transformers`, and every package depends
on `ir`. The transform contract (`TransformStep`, `TransformContext`) lives in
`transformers`, so that package needs nothing from the compiler.

Stages run to the first failure — in a sequence, every later stage would just
consume the broken IR. An emit or transform stage fails when it throws or reports
an error, so a stage reports every problem it finds and the run stops after it.

Captured IR is labeled by stage id (`ir/0-tsp-emit.json`, `ir/1-seed-ir.json`).
Generate stages take a sequence position but produce no IR, so index gaps are
expected. Because a run mutates one IR from end to end, a sink that *retains* a
snapshot **must** copy it: the in-memory collector does, and the CLI's filesystem
sink serializes before its first `await`, so capture costs nothing there.
Generated artifacts carry the `stage` that produced them, and two stages writing
the same path is an error rather than last-writer-wins.

## Transformers

`packages/transformers` holds the implementations behind the compiler's
transform stage ids, together with the contract they implement. A step
implements `TransformStep` and knows nothing about where it runs — the catalog
already placed it. It reports diagnostics through `TransformContext`'s
`reportDiagnostic` delegate, whose parameter shape is declared here; the
compiler passes its own reporter in, and the compiler stops building if the two
shapes drift apart. A run carries one IR, so returning it mutated in place is the
expected shape; returning a replacement works too.

Every step reads its stage config from `TransformContext.config` and checks it
when it runs. A key it does not know is a warning (`unknown-fact` for `seed-ir`,
`unknown-option` for the others) and is ignored, through one shared function,
`warnUnknownKeys` (`src/stage-config.ts`); a step that takes no options warns about
any key it is given. An invalid value is an error that fails the stage
(`invalid-fact`, `invalid-option`); no default stands in for it. A step is a plain
object: it holds no state between runs.

`seedIrStep` fulfils the mandatory `seed-ir` stage, the first transform. It
turns the document facts, the gen-time config the spec cannot provide, into the
IR's `target` (see [Generation target](#generation-target)). It writes only
top-level IR nodes and never renames anything.

`tweakIrStep` fulfils the `tweak-ir` stage, and is **the only place IR-level
renaming happens**. Today it gives every model and enum a name that is unique
across the type universe, so two same-named declarations from different
namespaces cannot collapse onto one generated type. It runs before the two
stages below it because they denormalize names — `flatten-model` composes
member names from property and model names, and `create-command` copies names
into commands — so a rename after either would leave a stale copy behind. (An
`IRCommand` is different: commands are created downstream and nothing
denormalizes them, so later steps may name their own output freely.) It never
touches `id`, which stays a record of where a declaration came from.

`flattenModelStep` fulfils the `flatten-model` stage: it hoists nested object
members onto their containing model (`ModelHints.flattened`) and projects a
request body onto its operation's parameter surface
(`RequestBodyHints.parameters`). See [Flattening](#flattening). It is additive —
it writes only rendering hints and leaves `IRModel.properties` exactly as
emitted.

The compiler maps each of these steps to its stage id; hosts register nothing.
Every stage *runs* unless its section sets `disabled: true` (`seed-ir` is
mandatory and cannot be disabled); per-service settings such as
`inliningThreshold` and `noInline` arrive through `stages.<id>.options`. The
package participates in the root workspace build and test commands.

## Source identity

Model, enum, and operation IDs use the original TypeSpec namespace in
`IRId.namespace`, not the SDK namespace selected by `@clientNamespace`.
Nested source namespaces are preserved; global declarations use an empty
namespace array. Model and enum names retain their existing SDK-facing naming.
Property IDs and type references use the same source namespaces as their
defining models and enums, including inherited properties.

Operations remain flat in `IR.operations`. Their `IRId` records the source
`module`, TypeSpec `namespace` segments, optional containing `interface`, and
original operation `name`. Namespace-level operations omit `interface`; SDK
client renaming or regrouping does not change this identity. HTTP parameter IDs
carry the same interface scope and retain their original declaration names.
Inherited operations use their effective service interface, not the reusable
base interface or the source of an `op is` declaration.

`IROperation.name` remains the SDK-facing name. Interface identity is not a
rendering hint, and no separate interface collection or metadata inheritance
pass is introduced. Generators group processors using `id.interface`, not the
last namespace segment.

Source-interface identity was introduced in IR 0.7.0. Regenerate older emitted IR to obtain
source-interface ownership; an older SDK client namespace cannot reliably be
split into a TypeSpec namespace and interface.
Regenerate captures emitted with SDK namespace overrides to obtain consistent
source namespaces for models, enums, and their references as well.

## Shared server definitions

IR 0.8.0 stores endpoint definitions once in the root `servers` collection.
Each entry has an `id` (`IRId`) and an `endpoint`, retaining the existing
`literal`, `parameterized`, or `oneOf` endpoint shape.

Every operation still requires `server`, but its value is now a reference to
one of those definition IDs rather than an inline endpoint. `IRSource.server`
also references the shared definition. The emitter creates one definition per
emitted source, with its source module and namespace and the name `server`;
multiple `@server` declarations remain alternatives in that definition.

IR validation rejects missing server definitions and duplicate server IDs.
Generators resolve the operation's reference before constructing its request
URI. Regenerate older IR captures to obtain this breaking wire-format change;
inline operation/source servers are no longer accepted.

## Multipart bodies

IR 0.9.0 preserves resolved `@multipartBody` definitions on request bodies and
individual response alternatives as `multipart`. This is structural metadata,
not an inference from a `Content-Type` header.

`multipart.kind` distinguishes model and tuple forms, `contentTypes` retains all
allowed outer media types, and `parts` preserves declaration order. Each part
carries its MIME name, optionality, repetition, payload type and content types,
typed part headers, and filename/content-type fields when declared. File bodies
also identify their contents field and whether it carries text or raw bytes.
These fields are separate from outer HTTP headers and JSON property names;
model parts reference their corresponding payload-model property IDs.

Model-based bodies retain their existing `type` reference. Multipart tuples
omit that reference rather than substitute `any`; their body shape is fully
described by the ordered parts. Consequently, consumers must inspect
`multipart` as well as `type` when deciding whether a response has a body.
The ordinary `contentType` field remains the selected/default media type.

The emitter includes models/enums referenced only by parts or their metadata,
including tuple payloads. It also recovers ordinary payload-model properties
that TCGC's multipart-response conversion can omit. JSON serialization of a
file model remains distinct from transporting its raw contents as a MIME part.

This contract supplies downstream transformers and renderers with multipart
definitions; it does not implement multipart encoding/decoding in generated
processors. Generation reports a multipart-serialization warning rather than
claiming the processor scaffold is ready to handle MIME data. MIME boundaries
are runtime serialization details, not emitted constants. Regenerate older
captures to obtain the multipart definitions.

## Paging

IR 0.11.0 carries paging at `operation.semantics.pageable`. Semantics are now
independent fields: `pageable` and `longRunning` can coexist on the same
operation, instead of a mutually exclusive `semantics.kind`.

The emitter uses the original REST operation's compiler paging metadata,
including operations with paging properties but no `@list`. `isList` preserves
that distinction. `input` carries offset, page index, page size, and continuation
token roles; `output` carries page items, next/previous/first/last links, and the
response continuation token. Items-only lists and simultaneous link/token roles
are retained without choosing a navigation strategy or inventing a missing
token counterpart. Invalid paging declarations are diagnosed even without
`@list`; ordinary non-paging operations are not diagnosed as malformed lists.

Each role contains its type, optionality/nullability, complete `sourcePath`, and
resolved HTTP `bindings`. Source segments carry declaration identities and
original names, including HTTP envelope properties not materialized in
`IR.models`; those IDs are provenance, not necessarily model-property references.
Bindings distinguish body/query/header/path/cookie locations, preserve wire paths
as arrays (so a literal dot is not a separator), identify emitted request
parameters where available, and qualify response alternatives by status and
media type. Body paths are relative to the actual payload, not source-only
`@body` wrappers. A path of `[]` means the entire body. An unbound role keeps its
source metadata and produces a diagnostic rather than an invented wire path.

The separate `sdk` block preserves pageable disabling/forcing, the next-link
HTTP verb, and parameters to reinject. SDK presentation choices do not erase
REST paging facts. `metadataSource: "sdk"` identifies legacy forced collections
whose items were inferred by `Legacy.markAsPageable` rather than resolved from
standard paging declarations.

This is an IR/emitter contract change, not a generated paging implementation.
The C# IR POCOs carry the new shape. Existing processor rendering consumes the
independent traits and response-body paths; broader pagination strategies and
combined LRO/paging execution still require customization. Rebuild and stage
the generator host after refreshing POCOs. Older captures must be regenerated
or migrated.

## Discriminators

IR 0.10.0 uses TypeSpec's resolved discriminator definitions rather than relying
on TCGC's narrower subtype and union normalization.

For `@discriminator` models, `polymorphism.discriminator` references the
discriminator property ID; its `wireName` is the JSON field name.
`polymorphism.subtypes` is the resolved value-to-model dispatch map. Targets can
be indirect descendants when an intermediate model does not declare a tag;
`parent` still records the actual inheritance chain. Declarations with no known
variants retain an empty map. Multiple string aliases and empty-string tags
are preserved. The child `discriminatorValue` convenience is emitted only when
an ancestor selects it with exactly one value; use the dispatch map for aliases.
No unknown-value fallback policy is imposed by this metadata.

`@discriminated` unions live in `discriminatedUnions`, referenced through
`{ kind: "discriminatedUnion", id }`. Named definitions preserve shared and
recursive unions without recursively embedding their definitions. They record
`discriminatorPropertyName`, `envelope` (`object` or `none`), and
`envelopePropertyName` for object envelopes.

Each union's `variants` preserves declaration order. `value` is the wire tag;
its absence denotes the unnamed default variant, distinct from an empty-string
tag. The payload has a `type`, `nullable`, and an optional exact `literalValue`.
An exactly-null payload omits `type` and carries `literalValue: { kind: "null" }`.
This keeps tagged null payloads separate from an outer nullable envelope.
Single-variant and enum-like discriminated unions are not flattened, while
ordinary structural unions keep their existing representation.

The emitter bypasses only SDK discriminator restrictions for which TypeSpec
provides a valid definition that this IR represents; unrelated diagnostics
remain errors. Generator code and its generated C# contract are unchanged by
this IR/emitter change, so consuming the new union references in the C#
generator remains unsupported.

## Flattening

The IR records property flattening as *resolved* rendering metadata, so a
transform decides naming and collisions once and the generator does not
re-derive them.

`ModelHints.flattened` lists the members hoisted onto a model from nested object
properties. Each entry carries the final C#-facing `name`, the leaf `property`
id, the `via` ids of the container properties walked from the containing model
to reach it (outermost first, and at least one, since a flattened member always
crosses a nesting hop), `optional` (true when any hop is optional), and the
`visibility` phases every hop shares. An absent
`visibility` means unconstrained, matching `IRProperty.visibility`. The list
belongs to the model because collision resolution considers the model's whole
member set, and because every payload use site must agree on the result.

`RequestBodyHints.parameters` projects a body onto an operation's parameter
surface when `flatten.into` is `self`. Entries are deliberately not references
into the body model's `flattened` table: the projection filters by HTTP binding
and lifecycle phase, and resolves names against *that* operation's path, query,
and header parameters, so two operations sharing a body model can legitimately
disagree. An empty `via` is a top-level body property promoted as-is.

`IRModel.properties` is untouched by either field. The IR keeps wire truth — the
wire path is each hop's `wireName` — and ids are invariant under renaming
transforms, so the references stay correct however members are later renamed.

`flattenModelStep` populates both fields, and the C# generator consumes them —
see [Flattening](../packages/generator/README.md#flattening) for the emitted
shape. Both fields are optional, so captures predating 0.12.0 remain valid.
IR 0.13.0 split 0.12.0's id `path` into `property` + `via` — the shared
`IRPropertyRef` shape that a command's body-parameter source and pagination
`items`/`nextLink` also use — so 0.12.0 captures carrying flattening or commands
must be regenerated. Regenerate the IR JSON Schema and the C# POCOs to pick up
the new types.

### When a nested model is flattened

Each object property typed as a named model is decided bottom-up, so a nested
model has already flattened its own members by the time its container considers
it:

- A `properties` or `error` container — the ARM resource and error envelopes —
  always flattens, whatever its size.
- Any other container flattens only while the nested model renders fewer members
  than the **inlining threshold, 100 by default**.

The count is cumulative: the nested model's own properties that were not
themselves flattened, plus every member flattened into it. A model declaring 98
scalars and one 2-member nested model therefore counts 100 and stays nested,
although it declares only 99 properties.

The threshold is a generation-time setting only — `inliningThreshold` in the
`flatten-model` stage options, beside `noInline`, which keeps named models nested
whatever their size. An absent threshold is 100; a value that is not a number, 0
or more, fails the stage. Spec decorators such as `@flattenProperty` do not raise
or bypass it.

### Visibility of what was flattened through

Flattening would otherwise *double* a surface rather than reshape it: the
container and every member hoisted out of it would both be public. Two existing
IR fields resolve that, and the generator reads both:

- **The container leaves the public surface.** A property the transform hoisted
  carries `flatten.into = "self"`, and the generator emits it `internal` with
  `[JsonInclude]` — it is still what serializes, and System.Text.Json skips a
  non-public member without being told otherwise. Hoisting is all-or-nothing per
  container, so nothing becomes unreachable. The container's *type* stays public;
  only the member is hidden, matching what AutoRest PowerShell does.
- **A read-only member gets no setter.** When `FlattenedMember.visibility` names
  no create or update phase, the member is server-owned, so only a getter is
  emitted. Deserialization is unaffected: it fills the nested properties, never
  the `[JsonIgnore]` accessor — which is why this needs none of AutoRest's
  explicit-interface machinery.

Body parameters need no equivalent rule: `projectBodyParameters` already filters
by lifecycle phase, so a read-only member never reaches an operation's parameter
surface.

## Generation target

IR 0.14.0 lets `target` (`IRTarget`) sit on `IR` as an optional field, while
`TransformedIR` still requires it. A root field is required on `IR` only if the
emitter produces it, and a target is resolved from generation-time configuration
by the `seed-ir` stage.

`IRTarget` holds the root namespace and the project name, plus optional
`serviceName` and `description`: facts about the generated extension that the
spec cannot provide. They live on the target only for now; a dedicated node may
replace that as more such facts arrive. The C# generator does not read the two
optional facts yet.

IR 0.15.0 removed `directories` and `version` from `IRTarget`. Where files land in
the generated project (`Models`, `Processors`) is the C# generator's own
convention, and an extension's version is decided at release, not at generation:
the generated `.csproj` carries no `<Version>`, and finalizing an extension adds
one. `IRTarget` is strict, so a 0.14.0 capture that carries either field no longer
parses; regenerate it.

### Document facts and `seed-ir`

The facts are config keys, written as in the config file (kebab-case). The CLI
and the compiler each take their own keys; every other top-level key is a fact,
and the compiler hands those to `seed-ir` untouched:

```yaml
# aceconfig.yaml: the compiler's `stages` beside document facts
stages:
  flatten-model:
    disabled: true
root-namespace: Contoso.Widgets
project-name: Contoso.Widgets.Client
description: Widgets for AzClips
```

| Fact | Target field | Default when absent |
|---|---|---|
| `root-namespace` (dotted) | `rootNamespace` | the first source's namespace, else `Generated` |
| `project-name` | `projectName` | the root namespace |
| `service-name`, `description` | `serviceName`, `description` | unset |

Facts come only from config: no CLI flag sets one. A missing fact gets its
default. A fact with the wrong type or an empty value is an `invalid-fact` error
that fails `seed-ir`: every such error is reported, no default stands in, and the
run stops. A key `seed-ir` does not know is an `unknown-fact` warning, which is
how a misspelled key surfaces, and how a leftover `version` or `directories` is
reported. `create-command` carries the target into the `TransformedIR`, and
fails on a document without one; so does the C# generator
(`ace-generator/missing-target`), which has no namespace of its own to fall back
on.

## IR JSON Schema (cross-language contract)

The IR is defined once, in TypeScript, as [Zod](https://zod.dev) schemas under
`packages/ir/src` — that is the single source of truth. Non-TypeScript consumers
(notably the C# generator) must **not** hand-duplicate those types. Instead, a
JSON Schema is *derived* from the Zod schema; downstream languages code-generate
their own types from it (e.g. C# via NJsonSchema / a Roslyn source generator).

`ir.schema.json` is a **build artifact, not source** — it is not committed.
Generate it on demand into wherever a consumer needs it via `--output-dir`
(default: the package's gitignored `dist/`):

```bash
# Emit to a chosen directory (note the `--` separating npm's args from the script's):
npm run schema -w @azure-client-engine/ir -- --output-dir <dir>

# Verify a previously-emitted artifact is in sync with the Zod schema (CI):
npm run schema:check -w @azure-client-engine/ir -- --output-dir <dir>

# With no --output-dir, both default to packages/ir/dist/ir.schema.json:
npm run schema
```

The `--` is required so npm forwards `--output-dir` to the script rather than
consuming it as its own option. (In PowerShell, invoke through `cmd /c "…"` or
call `node packages/ir/scripts/emit-json-schema.mjs --output-dir <dir>` directly,
since PowerShell strips a bare `--`.)

**Workflow:** a build/codegen step regenerates the artifact into its output
location as needed; `schema:check` guards against drift between the emitted
artifact and the Zod source.

**Fidelity caveat:** JSON Schema is a *structural* contract only. Cross-field
refinements expressed with Zod `.superRefine` (e.g. a `custom` LRO polling
strategy requires `customPollingUrlPath`) cannot be represented and are **not**
carried across. Authoritative validation stays in the Zod emitter; generators may
trust that any IR they receive has already been validated, and should gate on the
`schemaVersion` field (see `IR_SCHEMA_VERSION`).
