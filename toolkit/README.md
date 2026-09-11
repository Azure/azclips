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

## Orchestration

The **compiler owns the workflow**. `STAGE_CATALOG` in
`packages/compiler/src/stages.ts` declares what stages exist, in what order they
run, and whether each runs by default:

```ts
{ id: "tsp-emit",      kind: "emit",      default: "mandatory" }
{ id: "flatten-model", kind: "transform", default: "on" }
{ id: "csharp",        kind: "generate",  default: "on" }
```

The resolved plan is a **single linear sequence**. One IR leaves the emit stage
and flows through every enabled transform in catalog order; each generate stage
renders the IR as it stands when its turn comes. There are no branches, so a run
has exactly one IR and the compiler never copies it. Array position *is* the
order.

A host has exactly one orchestration verb: **flip an optional stage**. It cannot
append, insert, or reorder.

```jsonc
// generation-time config
{ "pipeline": { "flatten-model": false } }
```

Disabling a stage simply removes it from the sequence — there is no tree to
reattach, so position stays intrinsic for free. Naming an unknown stage, or
disabling a `mandatory` one, is an error rather than a silent no-op. A run with
no generate stage is reported as a warning: it runs, but writes no files.

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

### Fulfilment, failure, and output

`CompileOptions.registry` is **fulfilment, not orchestration**: a host supplies
implementations for ids the catalog already declared. The two external stages
(`tsp-emit`, `csharp`) need no entry — they run through the host `Runtime`.
Per-stage settings live in `config.stageOptions`, keyed by stage id, because
options are not orchestration.

Stages run to the first failure — in a sequence, every later stage would just
consume the broken IR.

Captured IR is labeled by stage id (`ir/0-tsp-emit.json`, `ir/1-flatten-model.json`).
Generate stages take a sequence position but produce no IR, so index gaps are
expected. Because a run mutates one IR from end to end, a sink that *retains* a
snapshot **must** copy it: the in-memory collector does, and the CLI's filesystem
sink serializes before its first `await`, so capture costs nothing there.
Generated artifacts carry the `stage` that produced them, and two stages writing
the same path is an error rather than last-writer-wins.

## Transformers

`packages/transformers` holds the implementations behind the compiler's
transform stage ids. A step implements `TransformStep` and knows nothing about
where it runs — the catalog already placed it. A run carries one IR, so
returning it mutated in place is the expected shape; returning a replacement
works too.

`flattenModelStep()` fulfils the `flatten-model` stage: it hoists nested object
members onto their containing model (`ModelHints.flattened`) and projects a
request body onto its operation's parameter surface
(`RequestBodyHints.parameters`). See [Flattening](#flattening). It is additive —
it writes only rendering hints and leaves `IRModel.properties` exactly as
emitted.

A host registers it by id; that is fulfilment, not selection:

```typescript
import { createRegistry, FLATTEN_MODEL_STAGE } from "@azure-client-engine/compiler";
import { flattenModelStep } from "@azure-client-engine/transformers";

const registry = createRegistry({
  transforms: { [FLATTEN_MODEL_STAGE]: flattenModelStep },
});
```

The `ace` CLI registers exactly this. Whether the stage *runs* is the catalog's
default (`on`) plus any `config.pipeline` toggle; per-service settings such as
`inliningThreshold` and `noInline` arrive through `config.stageOptions`.
The package participates in the root workspace build and test commands.

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

IR 0.12.0 records property flattening as *resolved* rendering metadata, so a
transform decides naming and collisions once and the generator does not
re-derive them.

`ModelHints.flattened` lists the members hoisted onto a model from nested object
properties. Each entry carries the final C#-facing `name`, the `path` of
property ids from the containing model down to the leaf (at least two entries,
since a flattened member always crosses a nesting hop), `optional` (true when
any hop is optional), and the `visibility` phases every hop shares. An absent
`visibility` means unconstrained, matching `IRProperty.visibility`. The list
belongs to the model because collision resolution considers the model's whole
member set, and because every payload use site must agree on the result.

`RequestBodyHints.parameters` projects a body onto an operation's parameter
surface when `flatten.into` is `self`. Entries are deliberately not references
into the body model's `flattened` table: the projection filters by HTTP binding
and lifecycle phase, and resolves names against *that* operation's path, query,
and header parameters, so two operations sharing a body model can legitimately
disagree. A single-entry `path` is a top-level body property promoted as-is.

`IRModel.properties` is untouched by either field. The IR keeps wire truth — the
wire path is each hop's `wireName` — and ids are invariant under renaming
transforms, so the paths stay correct however members are later renamed.

This is metadata only: no transform populates these fields yet, and the C#
generator does not consume them. Unlike the 0.8.0/0.9.0/0.11.0 changes, both
fields are optional additions, so existing captures remain valid and do not need
regeneration. Regenerate the IR JSON Schema and the C# POCOs to pick up the new
types.

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
