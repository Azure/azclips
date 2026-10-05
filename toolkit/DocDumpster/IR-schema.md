# The IR schema

> **Status:** Current, `IR_SCHEMA_VERSION = "0.13.0"`. _(2026-10-04)_
>
> Reference for `@azure-client-engine/ir`'s **`IR`** root — the emitter↔transformer
> contract. Its sibling, [`TransformedIR-schema.md`](./TransformedIR-schema.md),
> covers the transformer↔generator contract.
>
> The Zod source in `packages/ir/src` is authoritative. This describes the shape and
> the conventions that are not obvious from the field names; when the two disagree,
> the source wins.

## What it is

`IR` is **wire truth**: everything a service's REST surface declares, normalized out
of TypeSpec/TCGC and detached from it. Nothing in it is C#-specific, and nothing in
it is a presentation decision.

```
TypeSpec ──tsp-emit──▶  IR  ──transform*──▶  IR  ──create-command──▶ TransformedIR ──▶ C#
                        └──────── this document ────────┘
```

| | |
|---|---|
| **Produced by** | the `tsp-emit` stage (`@azure-client-engine/typespec-emitter`), which self-checks with `parseIR` before returning |
| **Consumed by** | every transform stage up to and including `create-command` |
| **Serialized** | yes — it crosses a process boundary as JSON, and `--capture-ir` writes it per stage |
| **Validated by** | `parseIR` / `safeParseIR` (`IRSchema`, `.strict()`) |

## Root

```ts
IR = {
  schemaVersion: string
  sources:       IRSource[]              // ≥ 1
  servers:       IRServerDefinition[]
  models:        IRModel[]
  enums:         IREnum[]
  discriminatedUnions?: IRDiscriminatedUnion[]
  operations:    IROperation[]
}
```

Everything is a **flat collection at the root**, cross-referenced by `IRId`. There
is no nesting of models inside operations or operations inside groups: containment
is expressed by *fields*, never by structure. That keeps lookup uniform (see
[Identity](#identity)) and captured-IR diffs local.

## Identity

Every named node carries an `IRId`:

```ts
IRId = {
  module:     string      // originating module: "<root>", "@scope/pkg", or a custom source id
  namespace:  string[]    // TypeSpec namespace path, in segments
  interface?: string      // declaring interface, when there is one
  name:       string      // terminal name; may contain "#" for synthesized inline names
  typeArgs?:  IRId[]      // template instantiation: Page<User>
}
```

**Ids use the original TypeSpec namespace, not TCGC/SDK client organization**, and
they are *invariant under renaming transforms* — a rename changes a display `name`
and leaves the `id` alone. That invariance is what lets a `FlattenedMember`'s
`property`/`via` references stay correct after later passes rename members.

Three helpers, and picking the wrong one is the classic mistake:

| | use for | note |
|---|---|---|
| `idEquals(a, b)` | comparison | structural, recursive through `typeArgs` |
| `idKey(id)` | **map keys** | agrees with `idEquals` exactly |
| `serializeId(id)` | **display only** | joins namespace with `.`, so `["A","B"]` and `["A.B"]` render identically while `idEquals` separates them |

`indexIR(ir)` builds an `IRIndex` over models, enums, operations, servers,
discriminated unions, **properties** (which are not a root collection), and
`ownerOf(propertyId)`. It is a **snapshot** — transforms mutate in place, so build
it inside a stage after any structural change and never cache it on the document.

## Types

`IRTypeRef` is a `kind`-discriminated union, always a *reference* — never an inline
definition:

```
primitive { name, format? }   model { id }    enum { id }
array { element }             dictionary { value }
union { variants[] }          discriminatedUnion { id }
```

So a model referenced from a property is found through `IR.models`, not embedded.

## Models

```ts
IRModel = {
  id, name, source
  parent?              // direct extends; walk recursively for ancestry
  discriminatorValue?  // tag when an ancestor's discriminator selects this model
  polymorphism?        // set iff THIS model is a polymorphic base
  properties           // own + inherited, DENORMALIZED
  additionalProperties?
  usage?, access?, docs?, summary?, rendering?
}
```

**`properties` is denormalized** — inherited properties are copied onto every
subtype, with `inheritedFrom` pointing at the defining model and `overrides` at the
property being replaced. Consumers therefore never walk the inheritance chain to
enumerate a model's members. It also means one property id legitimately appears on
several models, which is why `IRIndex.property` keeps the first declaration.

`IRProperty` is discriminated on `typeKind` (`primitive | object | collection |
enum | union`), which narrows both `type` and the `rendering` hint variant.

## Operations

```ts
IROperation = {
  id, name, source
  method, pathTemplate
  pathParameters, queryParameters, headerParameters   // IRParameter[]
  requestBody?, responses
  apiVersion            // per-operation stamp: { value, location: query|header, parameterName }
  server                // required IRId reference into IR.servers
  clientLocation?       // group the spec explicitly relocated this op to (@clientLocation)
  semantics?            // { pageable?, longRunning? }
  docs?, summary?, access?, rendering?
}
```

Notes that repeatedly matter:

- **`server` is a required reference**, and an operation's own reference wins over
  its source's default. Resolve the operation's, never the source's.
- **`apiVersion` is per operation**, not per document — a multi-spec emit can mix
  providers on different versions.
- **`clientLocation` is client-layer, `id` is declaration-layer.** It is set only
  when `@clientLocation` *explicitly* relocates the operation (resolved for this
  emitter's scope), and the two routinely disagree: a brownfield spec declares
  several resources in one interface and separates them only here. `id` keeps
  recording where the operation was declared. Absent means "not relocated" — TCGC's
  default placement is deliberately not carried, since it only restates
  `id.interface`.
- **A multipart tuple body omits `type`** entirely; its whole shape is in
  `multipart.parts`. Check `body.multipart` as well as `body.type` before concluding
  a request has no body.
- **`responses` is a list including errors** (`isError`), each with its own
  `IRStatusCode` (`exact | range | default`).
- **Paging metadata records declared roles, not a chosen strategy.**
  `IRPageableSemantics.output.pageItems.bindings[].path` is a **wire** path of
  segments; `sourcePath` is declaration provenance and is *not* guaranteed to be a
  model-property reference. Resolving either to emitted members is the consumer's
  job — `create-command` does it once.

## Hints (`rendering`)

`rendering` is **metadata, not mutation**: it says how to *surface* a node, and the
IR always keeps wire truth beside it.

```
CommonHints          { clientApi?, docs? }
ObjectPropertyHints  + flatten?, serialization?
ValuePropertyHints   + wrapperType?, serialization?
CollectionPropertyHints, ModelHints (+ flattened?), EnumHints, ParameterHints, OperationHints
RequestBodyHints     { flatten?, parameters? }
```

The emitter writes almost none of these — they are where **transforms** record
resolved decisions. `flatten-model` writes `ModelHints.flattened`,
`RequestBodyHints.parameters`, and `flatten.into = "self"` on hoisted containers;
the generator consumes them without re-deriving names or collisions. Each entry
names its target as an `IRPropertyRef`: the leaf `property` id plus the `via`
container ids walked to reach it, outermost first — empty for a top-level body
property, never empty for a flattened member.

Two traps:

- `ClientApiHint.visibility` is `"public" | "internal"` — it describes the **emitted
  C# member**, not a client dialect. Client-facing presentation lives on commands
  (see the sibling document).
- `ModelHints`, `EnumHints`, `ParameterHints`, and `OperationHints` are structurally
  similar, and `zodToJsonSchema` **collapses identical shapes**. That is why
  `IRModel.rendering` once generated as `OperationHints?` in C#. Adding a
  kind-specific field (as `flattened` did for `ModelHints`) separates them.

## Optionality convention

An absent field usually means *unconstrained*, not *empty*:

| field | absent means |
|---|---|
| `IRProperty.visibility` | visible in **every** lifecycle phase |
| `IRProperty.access` / `IRModel.access` | `public` |
| `IRModel.usage` | `Usage.None` |
| `IRId.typeArgs` | not a template instantiation — **distinct from `[]`** |

## Validation

`IRSchema` is `.strict()` at every level, so an unknown key is an error rather than
silently carried. Beyond shape, its `superRefine` enforces:

- server definitions are unique by id;
- every `IRSource.server` and `IROperation.server` resolves to one.

Cross-field rules expressed with `.superRefine` **do not survive into the derived
JSON Schema**, and therefore not into the C# POCOs — authoritative validation stays
in Zod.

## Downstream artifacts

```
packages/ir/src/*.ts  ──emit-json-schema.mjs──▶  ir.schema.json  ──AceIrModelGen──▶  Ir.Generated.cs
```

`ir.schema.json` is a **build artifact, not committed**. The C# POCOs are generated,
never hand-edited. See `toolkit/README.md` for the commands.

> **Pending.** The emit script currently roots the JSON Schema at `IRSchema`, so
> `Ir.Generated.cs` has no command, dialect, or target POCOs yet. Because the
> generator only ever reads a *transformed* document, the root should move to
> `TransformedIRSchema` — leaving `IR`'s types as shared definitions and keeping
> `AceIrModelGen` single-rooted. `IR` remains the TypeScript-side contract for the
> emitter and the early transforms either way.

## Versioning

`IR_SCHEMA_VERSION` is stamped onto every document and bumped on any breaking shape
change. Producers stamp; consumers verify. Recent history:

| version | change |
|---|---|
| 0.13.0 | `IRPropertyRef` (`property` + `via`) replaces id `path`s in flattening, body sources, and command pagination (`items`/`nextLink`) |
| 0.12.0 | flattening — `ModelHints.flattened`, `RequestBodyHints.parameters` |
| 0.11.0 | `IRVisibility` moved to `primitives.ts` |
| 0.9.0 | multipart metadata |
| 0.8.0 | shared server definitions |
