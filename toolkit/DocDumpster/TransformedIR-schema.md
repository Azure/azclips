# The TransformedIR schema

> **Status:** Current, shares `IR_SCHEMA_VERSION = "0.17.0"`. _(2026-10-07; revised 2026-10-16)_
>
> Reference for `@azure-client-engine/ir`'s **`TransformedIR`** root — the
> transformer↔generator contract. Its sibling, [`IR-schema.md`](./IR-schema.md),
> covers the emitter↔transformer contract.
>
> The Zod source in `packages/ir/src/command.ts`, `dialect.ts`, `target.ts`, and
> `ir.ts` is authoritative.

## What it is

`TransformedIR` is `IR` **plus** the two things a generator cannot work without:

```ts
TransformedIR = IR & {
  commands: IRCommand[]   // required
  target:   IRTarget      // required
}
```

Two roots rather than one optional field, because the boundaries genuinely differ:
the emitter *cannot* produce commands and must not reference them; the generator
*cannot run* without them. Making both required turns "the command stage never ran"
into a validation failure at the generator boundary instead of a null dereference
inside it. `target` may already sit on an `IR`, where it is optional (see
[Target](#target)).

```
IR ──flatten-model──▶ IR ──create-command──▶ TransformedIR ──ps/cli transforms──▶ TransformedIR ──▶ C#
                                             └──────────────── this document ────────────────┘
```

| | |
|---|---|
| **Produced by** | the `create-command` stage (`@azure-client-engine/transformers`) |
| **Consumed by** | client transforms (`ps`, `cli`, …) and every generator |
| **Validated by** | `parseTransformedIR` / `safeParseTransformedIR`, called at the generate boundary |
| **Enforced by** | the catalog: `csharp` declares `requires: ["create-command"]`, so disabling the projection is a plan-time config error |

Both roots are `.strict()`, so they reject each other's documents — `parseIR` on a
transformed document fails with *"Unrecognized key(s): 'commands'"*. That is
deliberate: it makes the phase detectable rather than assumed.

## Why commands exist

`IROperation` is wire truth; `IRCommand` is a **processor**. Everything the
generator would otherwise infer is decided once, here:

- the **parameter surface** — previously a union of path/query/header/body computed
  inside the C# generator, and the only thing client transforms can annotate;
- the **request path**, as ordered segments instead of a template the generator
  string-replaces `{wireName}` in;
- **pagination references**, resolved to property ids instead of wire segments the
  generator PascalCases and hopes match;
- the **surface types** the processor's generics are built from;
- **names**, resolved with collisions settled.

Commands are a **projection beside** operations, never a replacement:
`IR.operations` is carried through untouched and every step points back at the
operation it projects, so a captured document can always be traced to wire truth.

Commands live **flat at the root**. Array order is not meaningful — group by
`id.subject` / `id.action`, never by position.

## Identity

```ts
IRCommandId = {
  module:   string   // IRSource.id of the primary operation
  subject:  string   // what it acts on
  action:   string   // what it does: Get, Create, Update
  variant?: string   // which input surface within the action
}
```

Four axes, which is why this is **not an `IRId`**: that has three usable slots
(`module`, `interface`, `name`), and two sibling commands over one action collide on
all three. Unlike an `IRId` it is **synthesized** — a command has no declaration
site — so the transform that creates commands owns its uniqueness.

`subject` is deliberately **not** called `interface`: `IRId.interface` asserts *a
TypeSpec interface declared this*, and a subject is derived precisely when that is
the wrong answer (below). It is also not called `group`, a word already spent on CLI
parameter groups and PS parameter sets.

Deliberately **config-independent**: namespace and project name are placement
(`IRTarget`), not identity. Changing the root namespace must not churn
every command's identity, and with it every captured-IR diff.

| helper | |
|---|---|
| `commandKey(id)` | structural key over all four axes |
| `commandActionKey(id)` | key over `(module, subject, action)` — **every sibling variant shares it** |

Siblings are therefore found by an id comparison ignoring `variant`, never by
parsing a name. A *variant* is a **surface** axis, not a behavioral one: siblings
share internals and differ in what they expose. Sequencing is `template`'s job.

### Deriving `subject`: the author's grouping first

A TypeSpec interface is not reliably the operation group. Brownfield specs use one
interface as a *file* container holding several resources and separate them with
`@clientLocation`, so the decorator — resolved for this emitter's scope — is the
author's own answer and wins:

```
@clientLocation  →  declared-name prefix  →  id.interface  →  namespace tail
```

`@clientLocation` is carried by the emitter as `IROperation.clientLocation`, set
**only** when the decorator explicitly relocates an operation. TCGC also reports a
default placement, but that merely restates the declaration site `id.interface`
already records; carrying it would make an authored grouping indistinguishable from
the default.

Measured across 1,161 operations in three specs, the decorator and the prefix
**never disagree** (52 agreements, 0 conflicts), and the decorator covers 228
operations the prefix cannot see — so preferring the inference would only ever
discard a better answer. The prefix is kept beneath it for a spec that renames
without relocating; it fires nowhere in that sample.

The prefix itself recovers a subject from the declared name. TCGC renders swagger's
`ExpressRouteCrossConnectionPeerings_Get` as `expressRouteCrossConnectionPeeringsGet`
while the client name stays `Get`, so stripping the trailing action leaves the group.
The action is matched **word-wise**, not as a string suffix, so it is never sliced
out of a longer word (`blocklist` does not yield `Block` for `List`).

> **Scope matters.** `@clientLocation(..., "!csharp")` applies to ACE, because ACE
> registers with TCGC as `@azure-client-engine/typespec-emitter` — not as `csharp`.
> Specs use that scope to exclude the *legacy AutoRest C# generator*, so ACE
> deliberately receives relocations that emitter does not. Registering ACE as a
> csharp emitter would silently reverse every one of them.

Why not `@tag`? Measured on Network, 80 distinct tags of which only 39 match a
derived subject, and the set includes `applicationSecurityGroups` (camelCase),
`"Network Interfaces"` (a space), and `ExpressRouteCircuitArpTable` (one operation,
not a group). It is free-form documentation text, not identity.

### Deriving `variant`: the `by` qualifier

`variant` is **always populated**, so no consumer branches on an absent one. It is
inferred by splitting the operation name on a `by` qualifier — the same trick
AutoRest's `create-commands-v2` uses:

| operation | `action` | `variant` |
|---|---|---|
| `list` | `List` | `List` *(only surface, named after the action)* |
| `listByResourceGroup` | `List` | `ByResourceGroup` |
| `listBySubscription` | `List` | `BySubscription` |

These are genuinely one action with three surfaces — they differ only in what the
caller must supply — so `commandActionKey` groups them, which is what a variant is
for. A leading or trailing `by` never splits, since it would leave an empty axis.

Scoped to `by` alone: AutoRest splits on `with/at/by/for/in/of` and then needs a
`specialWords` table to stop `signIn` becoming `Sign`, but across 776 Network and 30
ElasticSan operations **only `by` ever fires**, so the wider set buys nothing and
costs that hazard.

### Naming

`name` is the generated C# type stem: `action` + `variant` + `subject`, with the
variant in the middle because it qualifies the action. Concatenation only — the stem
then goes through C# identifier rules (PascalCase, legal characters, no leading
digit) and a document-wide uniqueness check.

Nothing linguistic is applied. AutoRest additionally collapses a subject its
qualifier already named (`ListByElasticSan` over `ElasticSans`, which otherwise
reads `ListByElasticSanElasticSans`) by singularizing every word through an English
pluralization service — but that is a **PowerShell cmdlet-naming rule** and belongs
to a client dialect, not to this step. Measured across 1,105 operations every such
collapse depended on that singularization; a strict comparison fires zero times, so
there is no convention-free rule to keep.

> **Remaining gap.** Two operations that share a subject, an action *and* a variant
> still collide — `create-command` reports `duplicate-command-identity` (error) and
> skips rather than emitting an invalid document. The error fails the stage, so the
> run stops before generation.

## The command

```ts
IRCommand = {
  id, name              // `name` = resolved C# type stem, unique document-wide
  template              // "atomic" | "getPut"
  server                // IRId into IR.servers — decides which generated base it derives from
  steps                 // IRCommandStep[], in order
  parameters            // the whole processor surface, in emission order
  surface               // the processor's generic arguments
  access?, docs?, summary?
  dialect?              // per-client presentation; absent until a client transform runs
}
```

### Templates and steps

`template` is a **processor template** and a property of the *command*, not of any
operation — operations are solely operations; the template knows what to do with the
list. It defines a positional contract over `steps`, validated by arity:

| template | steps | meaning |
|---|---|---|
| `atomic` | 1 | one call |
| `getPut` | 2 | `[read, write]` — the GET's response seeds the PUT's body |

A multi-operation command is a **sequence**, not one request with extra responses:

```ts
IRCommandStep = {
  operation      // IRId back-reference into IR.operations
  request
  responses      // ≥ 1, success and error
  longRunning?   // per step — in a getPut the write may be long-running, the read not
  pagination?
}
```

`atomic` is simply `steps.length === 1`; the shape does not special-case it. The
cost is one nesting level for the common case (`steps[0].request.path`), accepted so
that sequencing never has to be inferred from a flat list.

### Request

```ts
IRCommandRequest = {
  method
  path:    IRCommandUriSegment[]   // { kind:"literal", text } | { kind:"parameter", parameter }
  query:   IRCommandWireEntry[]    // { wireName, value }
  headers: IRCommandWireEntry[]
  body?:   { type?, contentType?, multipart?, projected, docs?, summary? }
}
```

`IRCommandValue` is `{ kind:"parameter", parameter }` or `{ kind:"literal", text }`,
where `parameter` is a command parameter's **`name`** (unique within the command, and
already the text the generator interpolates).

- **The endpoint is absent by design.** It belongs to the command's `server`, and the
  generated base owns it so one override redirects a whole extension.
- **The API version is a literal**, in `query` or `headers` per its stamp — *not* a
  parameter. The IR was emitted *at* a version, so the models correspond to it; a
  caller override would mismatch model and wire.
- **`body.type` is optional** for the same reason as `IRRequestBody`: a multipart
  tuple's whole shape is in `multipart.parts`. A `superRefine` enforces "type
  required unless multipart tuple".
- **`body.projected`** says the flat `parameters` fully cover the body, so the
  payload member can leave the public surface instead of duplicating every one of
  them. A decision, not a generator inference.
- **Values are escaped by the generator, not here.** Segments and entries name
  parameters; the generator percent-encodes each value — every path value except an
  `allowReserved` one, which is why that flag rides on the command parameter — and
  leaves an unset optional query entry off the request rather than sending it empty.

### Parameters

```ts
IRCommandParameter = {
  name              // final C#-facing member, unique within the command
  source            // path | query | header  → { parameter: IRId }
                    // body                   → IRPropertyRef { property, via }  (via: nesting hops crossed)
  type              // carried, not resolved through `source`
  required          // whether THIS command requires it — narrower than the declaration
  nullable, defaultValue?, collectionFormat?, format?, allowReserved?, access?
  docs?, summary?, dialect?
}
```

One flat list covering path, query, header, and body — the single annotation target
for client transforms, and the thing that did not exist anywhere before.

- **`type` is duplicated deliberately.** That redundancy *is* the projection: the
  generator emits a member without a lookup.
- **`source` resolves back to the declaration**, which is what lets a client
  transform read facts it must not rewrite — enum values, docs, visibility.
- **Protocol headers** (`Content-Type`, `Accept`) are carried but marked
  `access: "internal"`: the request needs them, no client should surface them.
- **There is no `apiVersion` source kind** — see above.

### Surface

```ts
IRCommandSurface = { request?, response?, output?, paginated }
```

**A step holds behavior; the surface holds the processor's generics.** The two
coincide for `atomic`; a template defines its own rule otherwise — a `getPut` sends
the write's body but constructs it from the read's response, and an LRO's steps
declare a 202 while `response` is what the processor ultimately returns.

`paginated` is *stated, not derived*: the generator asks one question — "do I emit
the pagination override?" — and would otherwise have to scan steps to answer a
question about the surface. *How* to page stays on the step.

**Error types are deliberately absent.** The error model is selected at runtime from
the status code, so a type parameter could only document what might be thrown —
which is why the processor base needs no `TError` generics.

### Pagination

```ts
IRCommandPagination = { itemType, items: IRPropertyRef, nextLink?: IRPropertyRef, nextLinkVerb?, continuationToken?: IRCommandContinuationToken }
IRCommandContinuationToken = { response: IRPropertyRef, parameter: string }   // parameter: an IRCommandParameter name
IRPropertyRef       = { property: IRId, via: IRId[] }   // via: containers, outermost first
```

Member-**id** references into the response model, resolved once here by walking wire
segments against the model's `wireName`s: the leaf `property`, plus the `via`
containers walked to reach it — empty when the leaf sits on the response model itself. `IRPageableSemantics` records wire paths,
which leaves the generator PascalCasing each segment and hoping it matches an emitted
member — a heuristic made fragile by flattening's renames.

A step follows at most one strategy: `nextLink` (a URL to fetch) or `continuationToken`
(a response property whose value is set on the command `parameter` before the request is
rebuilt). The token is resolved only when there is no next link, and only for a body-bound
response token and a string query or header parameter; otherwise `create-command` warns
`paging-continuation-unresolved` and keeps items-only pagination (first page).

Generated pageable processors override `ReadPagesAsync` to select the runtime's lazy
next-link or token iterator, and `ExecuteAsync` to consume it through `ExecutePagesAsync`.
`ProcessAsync` still owns lifecycle events for both CLI and PowerShell. The default
execution collects all items and writes once, preserving existing output; custom
execution can collect and transform before output.
Page enumeration uses the execution publisher's cancellation token. Each response is
disposed after deserialization, and stopping enumeration releases any request template
without fetching another page. Next-link continuation pages retain JSON deserialization
with the processor's serializer options; token pages retain the processor's custom
deserializer. Initial responses retain their processor-specific diagnostics.
Pagination is no longer generated into `BeforeWriteOutputAsyncAndReturn`; that hook
remains available for single-response output customizations. Existing collection and
write helpers remain compatible and now consume the same lazy iterators.

For example, the VM list orchestrator overrides `ExecuteAsync` to collect `ReadPagesAsync`,
then filter by name, enrich only the matching VMs, and write once. It shares the atomic
VM list processor's next-link strategy without fetching any pages in its output hook.
Collection finishes before enrichment, so a failed later page produces no partial output
or per-VM detail calls.

This is an internal refactor only: commands still follow all pages, preserve item order,
and collect before a single write. It adds no CLI/PowerShell paging flags, result limits,
streaming defaults, or resumable cursors. The next-link request template is still built
once, including for a single-page result, preserving request-creation hooks.

## Dialects

Per-client presentation — how one command is *spoken* by one client.

```ts
IRCommandDialect          = { ps?: PsCommandDialect,   cli?: CliCommandDialect }
IRCommandParameterDialect = { ps?: PsParameterDialect, cli?: CliParameterDialect }
```

**Dialects live on commands and their parameters, and nowhere else.** Models are
client-neutral: they are output shapes that serialize themselves. Hints on models and
properties shape the emitted **C# type**; dialects shape the **client surface**. That
bounds growth at `clients × {command, parameter}` rather than `clients × node kinds`.

Each client's type spreads `clientDialectFields` — the **intersection**
(`name`, `aliases`, `hidden`, `description`), never the union. A field enters the
base only when no client can do without it; otherwise it goes in that client's own
type.

The member set is **closed**: adding a client edits this file plus its schema and
transform, which that client needs regardless — and a closed shape generates far
better C# POCOs than an open map.

`dialect` is **absent** until a client transform runs. `create-command` writes none:
it knows nothing about clients.

> These are skeletons. `PsCommandDialect` carries `verb`, `noun`,
> `defaultParameterSet`, `supportsShouldProcess`; `PsParameterDialect` carries
> `parameterSets[]` of `PsParameterSetMembership` (per-set `mandatory`, `position`,
> pipeline flags), mirroring one `[Parameter(...)]` attribute each. `CliCommandDialect`
> carries `path[]`; `CliParameterDialect` carries `flag`, `shorthand`, `group`.

## Target

```ts
IRTarget = {
  rootNamespace: string[]
  projectName:   string
  serviceName?:  string    // identity facts: temporary home, see below
  description?:  string
}
```

Resolved once, at transform time, from generation-time configuration — not by the
generator. A namespace or project name is a *decision*, and decisions belong in the
IR where a captured document can show them. Where files land inside the project
(`Models`, `Processors`) is not a decision but the generator's own convention, so
0.15.0 removed `directories` from the target.

Document-level rather than per-command because models, the processor base, and the
project file all need placement. Anything that genuinely varies per command stays on
the command.

**Optional on `IR`, required here.** A field is required on `IR` only if the emitter
produces it, and the emitter cannot produce a target. A transform writes it:
`seed-ir`, the first stage after emit, so it appears on every `IR` from that stage
on.

**Identity lives here for now.** `serviceName` and `description` are facts the spec
cannot provide. Keeping them on the target is a temporary choice; a separate node may
replace it as more such facts arrive. `seed-ir` writes them from the facts of the
same names, and the C# generator does not read them yet. The extension's version is
not among them: it is decided at release, not at generation, so 0.15.0 removed
`version` too.

## Validation

Per command (`IRCommandSchema.superRefine`):

- step count matches the template's arity;
- parameter names are unique within the command;
- every path/query/header hole resolves to a declared parameter — otherwise the
  generator would emit uncompilable code;
- `dialect.ps.defaultParameterSet` names a set some parameter declares;
- `surface.paginated` implies some step carries `pagination`.

Per document (`TransformedIRSchema.superRefine`), in addition to `IR`'s server rules:

- every `command.server` resolves into `IR.servers`;
- every `steps[].operation` resolves into `IR.operations`;
- `name` is unique document-wide (it is the generated C# type stem);
- `commandKey(id)` is unique.

## Consuming it

```ts
const document = safeParseTransformedIR(ir);
if (!document.success) { /* report; do not spawn the generator */ }
```

That is what `createCsharpGenerator` does before spawning the out-of-process host,
turning "the command stage never ran" or "the projection is malformed" into a
diagnostic at the boundary.

A step can state its phase in its own type:

```ts
createCommandStep: TransformStep<IR, TransformedIR>
psTransformStep:   TransformStep<TransformedIR, TransformedIR>   // reads ir.commands, no cast
```

The orchestrator erases these parameters — it threads the widest document type, and
*ordering* is what actually guarantees a phase, via `StageDescriptor.requires`.
Nothing forces a step's declared phase and its catalog `requires` to agree; that
stays review discipline, like the additive-after-generate rule.

## Codegen

`emit-json-schema.mjs` roots the JSON Schema at `TransformedIRSchema` and registers
the command, dialect, and target types as definitions, so `Ir.Generated.cs` roots at
the transformed document with `IR`'s types as shared definitions. The generator only
ever reads a transformed document, so it needs no second root and `AceIrModelGen`
stays single-rooted.

```
packages/ir/src/*.ts  ──emit-json-schema.mjs──▶  ir.schema.json  ──AceIrModelGen──▶  Ir.Generated.cs
```

Two things to remember:

- **`.superRefine` rules do not cross into JSON Schema**, and therefore not into C#.
  Everything under [Validation](#validation) is Zod-side only; the C# generator must
  not assume it has been checked unless the pipeline called `parseTransformedIR`.
- **`zodToJsonSchema` collapses structurally identical types.** If `PsCommandDialect`
  and `CliCommandDialect` ever carry the same fields, they will generate as one C#
  type — the same way `IRModel.rendering` once generated as `OperationHints?`.
