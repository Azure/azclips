# Compiler-Owned Stage Orchestration: a Linear Stage Sequence

> **Status:** Implemented _(2026-09-12)_, with the **shape revised**: the
> fork-only tree this record originally decided was replaced by a **single linear
> sequence** before it shipped. Decision point 2 below is superseded — see
> [Revision — from fork-only tree to linear sequence](#revision--from-fork-only-tree-to-linear-sequence).
> A second revision (2026-10-07) moves stage implementations into the compiler,
> implemented the same day, and narrows point 3, which lands with the aceconfig
> work — see
> [Revision — compiler-owned stage implementations](#revision--compiler-owned-stage-implementations).
> A third (2026-10-14) moves the `tsp-emit` tool's code out of the CLI into the
> emitter package — see
> [Revision — the `tsp-emit` tool lives in the emitter package](#revision--the-tsp-emit-tool-lives-in-the-emitter-package).
> Every other point stands as written. _(originally accepted 2026-08-26)_
>
> **Scope:** the Azure Client Engine (ACE) compiler pipeline
> (`emit → transform* → generate`) — specifically *what* runs, *in what order/shape*,
> and *who owns that decision*. The sibling ADR
> [`Apply-JsonRpc-Stage-Protocol.md`](./Apply-JsonRpc-Stage-Protocol.md) covers
> *how* stages talk (protocol/transport). The two are complementary.
>
> This is a decision record of a design discussion. It captures the direction and
> the rationale; it is **not** an implementation plan and does not change code yet.

## Summary

The **compiler owns one predefined workflow**, expressed as a **rooted, fork-only
stage tree**. Hosts do **not** define orchestration — order and parallelism live
in the compiler. A host's *only* verb is to **turn on an optional stage by id**;
it never appends, inserts, reorders, or picks a profile. The pipeline **always
ends in a generator** (default `csharp`, compiler-owned) because generated content
is the product. `--capture-ir` is purely **additive** materialization between
stages and never gates generation.

## Revision — from fork-only tree to linear sequence

_2026-09-12. Supersedes Decision point 2 below. Everything else in this record
stands._

**The shape is now one linear sequence.** `STAGE_CATALOG` is an ordered array;
one IR leaves the emit stage and flows through every enabled transform in that
order, and each generate stage renders the IR as it stands at its position.
`after` is gone — array position *is* the order. Disabling a stage removes it
from the sequence instead of reattaching a subtree.

### Why

- **The parallelism was notional.** Transforms are in-process, synchronous CPU
  work, so `Promise.all` over sibling branches bought no real concurrency. The
  only genuinely parallel stage is an out-of-process generator, and there is one.
- **Fork = clone is a memory and determinism cost with no payer.** Isolating
  branches meant `structuredClone` of the largest object in the run, plus an
  `effect: additive | mutate` annotation on every stage to decide when that copy
  could be skipped, plus `needsIsolation` propagation up each subtree. And
  siblings that *shared* one object could interleave at `await` points, so a
  snapshot captured in one branch depended on another branch's timing — bad for
  diffability.
- **The conceptual load scaled with the catalog.** Every future stage author had
  to answer "additive or mutate?" correctly or silently corrupt a sibling. With
  naming and virtual-parameter transforms queued up, deleting that question was
  cheapest at one transform and one generator.
- **Disabling a stage got simpler, not harder.** "Climb to the nearest enabled
  ancestor" collapses into "filter the array".

### What it cost

**Divergent per-backend transform branches.** Two generators can no longer sit on
differently-shaped IR. The substitute — and arguably the better design — is an
*additive, backend-namespaced* annotation stage on the shared sequence that each
generator reads its own tags from: one IR, no branching, no copies.

**Concurrent generators.** A generate stage is awaited in place so it sees
exactly the IR at its position. Handing a live IR to something running
concurrently would let a later transform change what it renders — silent wrong
codegen, the worst failure class here. If generator parallelism is ever wanted,
the answer is to make the snapshot explicit (serialize in the compiler at the
stage's position) rather than to loosen that contract.

### What replaced the `effect` annotation

Nothing mechanical, deliberately. The ordering discipline is documented on
`STAGE_CATALOG` and upheld by review:

1. Stages that alter existing IR go **before the first generate stage**, so every
   backend renders the same canonical IR.
2. Stages after it are **purely additive** and backend-namespaced, so one
   backend's annotations are inert to another's.

Note the boundary is *the first generate stage*, not "the front": a generate
stage freezes its own view when it runs, so a later mutating stage cannot corrupt
an earlier generator — only a later one. `effect` was deleted rather than left
vestigial, because a declared-but-unchecked field looks enforced. The compiler
owns the catalog and no host can insert into it, which is what makes convention
sufficient here.

### What was added

**`requires: string[]`.** In a linear sequence every stage runs after every
earlier one, so ordering no longer carries the dependency information `after`
implied. A stage that is *wrong* without an earlier stage now declares it, and
disabling that stage becomes a config error naming both ends instead of quietly
wrong generated code.

### Consequences for the rest of this record

- **"Fork = clone is automatic under serialized IR"** (Decision 2, and the
  JSON-RPC ADR cross-reference) no longer applies — there are no forks. The
  JSON-RPC ADR is otherwise unaffected.
- **"No barriers"** under Costs is moot: with no branches there is nothing to
  join.
- **"Invariants rely on discipline"** still holds, and now also covers the
  additive-after-generate rule above.
- **The retain-⇒-copy contract on `OutputDelegate` became load-bearing.** A run
  mutates one IR end to end, so a retaining sink that skips the copy does not get
  a slightly stale snapshot — it gets N references to the run's final state.
  Forking used to mask this.

## Revision — compiler-owned stage implementations

_2026-10-07. Implemented 2026-10-07, except the disable-only switch for Decision
point 3, which lands with the aceconfig work. Supersedes the "Escape hatches
stay" item of the design sketch. Everything else in this record stands._

**The packages layer as `cli` → `compiler` → `transformers`, and every package
depends on `ir`.** The compiler maps catalog ids to transform implementations
itself (`packages/compiler/src/transforms.ts`), so hosts supply no stage code.
The CLI uses only compiler interfaces, plus the host `Runtime` it already
provides for out-of-process tools (the TypeSpec toolchain and the C# generator
host). Resolving those tools is the CLI's job; keeping it there keeps the
compiler free of `child_process` and `fs`.

### What changes

- **The transform contract moves into `transformers`.** `TransformStep` and
  `TransformContext` are defined next to the steps
  (`packages/transformers/src/transform.ts`). `transformers` stops depending on
  `compiler`, and `compiler` starts depending on `transformers`.
- **Diagnostics stay in the compiler.** `CompilerDiagnostic` remains a compiler
  type that hosts use directly. A transform reports through a delegate whose
  parameter shape `transformers` declares inline. Declared as a function-typed
  property rather than a method, the delegate is checked under
  `strictFunctionTypes` when the compiler builds the context, so incompatible
  shapes stop the compiler from building. (Verified: a method declaration would
  accept a drifted shape silently.)
- **The host registry is dropped.** `CompileOptions.registry` and
  `createRegistry` are gone. The built-in external stages (`tsp-emit`, `csharp`)
  still run through the host `Runtime`.
- **Point 3 becomes disable-only.** It shipped as a two-way toggle
  (`config.pipeline`). With nothing off by default, a host can only disable a
  non-mandatory stage (see
  [`Aceconfig-Generation-Time-Config.md`](./Aceconfig-Generation-Time-Config.md)).
  Implemented 2026-10-12: the switch is `stages.<id>.disabled`.
- **A no-output run disables the generate stage**, and the planner reports it as
  a warning (`no-generator`). `noopGenerator` had no remaining injection path and
  is removed.

### Why

- **The CLI stops knowing transformers.** It resolves tools and passes input;
  stage code is the compiler's dependency.
- **The awkward edge was only the contract.** `transformers` imported two types
  from the compiler (`TransformStep`, `TransformContext`), and of those used only
  `reportDiagnostic`.
- **A host can no longer omit a stage the catalog declares.** That matters once a
  mandatory transform (`seed-ir`) exists.

### Costs

- **Tests lost the registry seam.** Tests that stubbed stages through
  `createRegistry` now run the real transforms or disable stages through config.
  Tests that only made sense with fake stages were dropped (see below).
- **The diagnostic shape is declared twice:** once as the compiler type and once
  inline in `transformers`. The compile-time check above keeps them compatible.

### Consequences for the rest of this record

- **Decision 3** ("turn-on only") is now disable-only, as above.
- **"Escape hatches stay"** in the design sketch is superseded for transforms and
  generators. `CompileOptions.emit` and `CompileOptions.catalog` were kept.
- **"`noopGenerator` is reachable only as an explicit escape hatch"** under
  Non-goals is superseded: a no-output run disables the generate stage.
- **The JSON-RPC ADR's in-process channel** for transforms becomes the
  compiler's own. A host binds channels only for out-of-process stages.

### Open points from the implementation (for discussion)

- **Orchestration coverage dropped with the fake-stage tests.** These behaviors
  of `compile()` are no longer covered by unit tests:
  - stopping at the first failing transform;
  - tagging generated artifacts with the stage that produced them;
  - two generate stages writing the same path (`output-path-collision`);
  - rejecting a document a generator cannot render (`invalid-generator-input`);
  - snapshot index gaps around a generate stage in mid-sequence.

  Restoring them would need a way to run fake stages, for example an internal,
  non-exported entry point.
- **Keep `CompileOptions.emit` and `CompileOptions.catalog`?** Both remain as
  embedder and test escape hatches. Now that every standard id has an
  implementation, a custom `catalog` is the only way to reach
  `unimplemented-stage`.
- **`TransformContext` carries no config, but `GeneratorContext` still exposes
  all of `CompileConfig`.** The transform contract cannot name a compiler type,
  so its `config` field was removed (no step read it); options still arrive
  through each step's factory. Routing each stage config into its stage's context
  is the aceconfig work. **Resolved:** since 2026-10-12 no context exposes the
  whole `CompileConfig`, and since 2026-10-14 every context (`TransformContext`,
  `EmitContext`, `GeneratorContext`) carries only its own stage config, as
  `config`. The transform factories are gone.
- **The CLI still imports two stage ids** (`CSHARP_STAGE`, `CREATE_COMMAND_STAGE`)
  to map `--generator` and `--root-namespace` onto config. They are compiler
  exports, not transformer details, and go away with the aceconfig flags.

## Revision — the `tsp-emit` tool lives in the emitter package

_2026-10-14. Implemented the same day. Narrows "resolving those tools is the CLI's
job" from the previous revision: the CLI decides what runs and where it is, but
holds no tool's own code._

**The `tsp-emit` worker moves from the CLI into
`@azure-client-engine/typespec-emitter`, as the C# generator's host lives in its
own package.** The worker drives TypeSpec's `compile()` with the emitter, so it
knows the emitter's options and output: the library name its options are filed
under, `format`, `outputFile`, `pretty`, `api-version`, and the `ir.json` it
writes. In the CLI it came with a request type that copied the compiler's
`EmitToolRequest` field by field.

### What changes

- **The emitter package ships the worker and its contract** as subpath exports:
  `./tsp-emit-worker` (`src/tsp-emit-worker.ts`) and `./tsp-emit-protocol`. The
  library entry stays apart from them, because the worker must not load
  `@typespec/compiler` statically. The worker implements its end of the
  compiler's protocol by hand, so the emitter still depends only on `ir`.
- **The host passes the compiler's request through untouched,** together with the
  toolchain it resolved (`compilerPath`, `emitterPath`). It reads only the
  entrypoint, to find that toolchain.
- **The CLI keeps what runs and where it is:** the runtime's switch over tool ids
  (`packages/cli/src/runtime.ts`), the TypeSpec toolchain and ace's own worker
  (`toolchain.ts`), and the generator host dll (`generator-host.ts`). The worker
  comes from the emitter package the CLI ships with, so its protocol always
  matches the compiler's; it still loads whichever toolchain was resolved, by
  path.
- **Running a child process is generic** (`packages/cli/src/subprocess.ts`):
  `runWorker` forks a Node module for one request and one reply, and
  `spawnStream` reads a command's stdout line by line. Both stop the child past a
  hang guard or on cancellation.
- **Two behavior changes:**
  - Cancelling the worker rejects with an `AbortError`. It used a plain `Error`,
    which the compiler did not recognise as a cancellation.
  - The worker's own failure is reported as
    `@azure-client-engine/typespec-emitter/tsp-emit-worker-failed` (was
    `@azure-client-engine/cli/tsp-compile-worker-failed`).

### Why

- **The CLI stops knowing the emitter's options and output.** Where the emitter
  is remains its concern; that is resolution.
- **Errors about what an entrypoint means belong to the emit side** (see
  [`Aceconfig-Generation-Time-Config.md`](./Aceconfig-Generation-Time-Config.md#open-details-proposed-defaults-confirm-when-implementing)),
  and the worker is the code that sees them. It reports an entrypoint TypeSpec
  cannot load as `invalid-tsp-source` (implemented 2026-10-14).

### Costs

- **The `tsp-emit` request and reply are declared twice:** by the compiler, and by
  hand in the emitter package. The CLI, which sees both, type-checks that the
  compiler's request fits the worker's and that the worker's reply fits the
  compiler's. A field the compiler adds but the worker does not declare is not
  flagged.

## Context — where we are today

- **The backbone is pure and host-agnostic**, sequencing `emit → transform* →
  generate` and reaching external stages through an injected `Runtime` seam.
- **But orchestration currently leaks to the host in two ways this decision
  reverses:**
  - **Order is host-supplied.** `CompileConfig.pipeline` is an ordered array of
    `{ use, options }`; `resolvePipeline` runs it *in array order*
    (`packages/compiler/src/compile.ts`). So today the *host* decides sequence.
  - **"No generator" is a valid default.** `resolveGenerator` returns
    `noopGenerator` when `config.generator` is absent
    (`packages/compiler/src/compile.ts`; `noopGenerator` in
    `packages/compiler/src/generate.ts`; docs in `types.ts`).
- **IR between stages is already materialized.** `--capture-ir` writes
  `ir/<index>-<step>.json` after emit and each transform step — the backbone
  already treats "IR between stages" as first-class data.

## Decision

> Point 2 was revised before implementation — see
> [Revision — from fork-only tree to linear sequence](#revision--from-fork-only-tree-to-linear-sequence).
> Points 1 and 3–6 shipped as written.

1. **The compiler owns one predefined workflow.** Order + parallelism are
   compiler-owned, not host-assembled. Mandatory stages are pre-enabled. The host
   never names the full pipeline.

2. **The stage graph is a rooted, fork-only tree.** Each stage has **exactly one
   predecessor** and **one-or-many successors** ("one before, one or many afters").
   - **Multiple successors ⇒ divergent parallel branches.** Each branch is
     recursively itself a subtree, resolved the same way. Terminal fan-out (e.g.
     `csharp` **and** `typescript` from the same upstream IR) falls out naturally
     as two successors ending in two generators.
   - **Fork = clone.** At a fork the incoming IR is snapshotted per branch, so
     parallel branches are isolated and safe to run concurrently. Under
     serialized-IR-between-stages (see the JSON-RPC ADR) this is **automatic** —
     each branch deserializes its own copy; no extra machinery.
   - **No joins.** A stage never has two predecessors, so branches fork but never
     re-merge. This deliberately sidesteps the "which IR does a merge-stage
     receive" / barrier problem — every branch is an independent IR lineage.
   - **Upheld by discipline, not code.** The compiler is an internal component
     under full developer control, not an externally-exposed tool. The no-join
     rule is honored by *not authoring* a joining stage; it is **not** enforced
     with guard/validation code.

3. **Host surface: turn-on only.** The host's sole verb is **enable an optional
   stage by id**. No append, insert, reorder, or disable-of-structure. Mandatory
   stages are always on; whether an optional stage is on or off by default is a
   per-stage property of the compiler catalog.

4. **No host profiles.** Profiles (whole alternative workflows) would be
   *compiler-exposed* defaults, not a host-facing menu — and are considered
   unlikely. The one workflow + opt-in stages covers the foreseeable need.

5. **The pipeline always ends in a generator.** Generated content is the product;
   IR and staged source are intermediates, never the intended output. The default
   generator is `csharp`, owned by the compiler. **`noopGenerator` is demoted**
   from "the default" to an explicit escape hatch (`CompileOptions.generator`
   injection) for the rare — and doubtful — "produce nothing" case.

6. **`--capture-ir` is additive.** It persists serialized IR between stages (after
   each stage except the final generate), labeled by stage name. It **never**
   affects whether generation runs — generation always runs. (Per the JSON-RPC
   ADR, the snapshots *are* the wire payloads.)

## Why — benefits

- **Thin host, honest boundary.** The host passes ids + boolean intent, never
  orchestration. Order, parallelism, defaults, and meaningful bundles have a
  single source of truth: the compiler.
- **Position is intrinsic, not invocation-order.** An optional stage slots into
  its declared place (e.g. between two mandatory stages) when enabled — the
  ordering cannot be expressed as "defaults first, then append extras," which is
  exactly why it must live in the compiler.
- **Parallelism without merge hazards.** Fork-only means concurrent branches with
  no barrier/merge-conflict problem; terminal fan-out to multiple generators is a
  free consequence.
- **Mandatory generator matches intent.** ACE exists to produce product code, so
  "no output" is not a normal outcome.

## Costs / tradeoffs (recorded honestly)

- **No barriers.** The tree cannot express "run t8 only after **both** t6 and t7."
  Accepted; revisit only if a real need appears.
- **Invariants rely on discipline.** No-join (and single-predecessor) are not
  code-enforced. Acceptable precisely because the compiler is internal.
- **Reverses two current behaviors.** Host-supplied order and the no-op default
  both go away, which means touching `resolvePipeline` / `resolveGenerator` and
  updating early tests (below).

## Conflicts with current code/tests (reconcile when implementing)

Recorded so implementation flips them deliberately. Per project practice, stale
early tests are surfaced here, not used to justify keeping the old model.

- **Source:**
  - `compile.ts` `resolveGenerator`: `config.generator` absent → `noopGenerator`
    (should default to `csharp`).
  - `compile.ts` `resolvePipeline`: treats the host's `config.pipeline` array as
    the source of order.
  - `generate.ts`: `noopGenerator` "Default generator: emits nothing" + the
    "defaulting to a no-op" header.
  - `types.ts`: `CompileConfig.generator` "Absent → the no-op generator" and
    `CompileOptions.generator` "Defaults to a no-op generator."
- **Tests (early-stage, likely stale):**
  - `packages/compiler/test/runtime.test.ts` — "built-in emit" describe runs
    `compile(…, { runtime })` with **no generator** and expects emit-only success.
  - `packages/compiler/test/pipeline.test.ts:248` — asserts `result.files.size ===
    0` for an emit+transform run with no generator.
  - `packages/cli/test/cli.test.ts` — "gen-none" asserts `<out-dir>` is empty
    *because* the no-op generator wrote nothing.

## Design sketch (non-binding)

- **Stage descriptor.** Roughly
  `{ id, kind: emit | transform | generate, after: id[], default: mandatory | on |
  off, group?: "backend" }`. (`before`/`after` are inverse views; which is
  authoritative is an impl detail.)
- **Planner.** `catalog defaults ⊕ host turn-ons → resolved fork-only tree`, then
  execute with parallel branches, cloning/deserializing IR at each fork.
- **Generator group.** Mutually-exclusive terminal members (`csharp`, later
  `typescript`, …); `csharp` is the default-on member. `--generator <id>` becomes
  sugar for "select the backend member," not a bespoke concept.
- **Escape hatches stay.** `CompileOptions.emit/transform/generator` overrides
  (and possibly a low-level explicit `config.pipeline`) remain for
  embedders/tests, including injecting `noopGenerator` for a deliberate no-output
  run.

## Relationship to the JSON-RPC ADR

Complementary, not overlapping:

- **This ADR** decides *what* runs and *in what shape* — a compiler-owned,
  fork-only stage tree ending in a mandatory generator.
- **The JSON-RPC ADR** decides *how* stages talk — a uniform protocol with the IR
  serialized between stages, transport injected by the host.
- They reinforce each other: serialized-IR-between-stages makes **fork = clone**
  automatic, and the compiler-owned tree is the orchestration that protocol drives.

## Non-goals / invariants to preserve

- **The compiler stays pure**; the host stays thin (ids + intent only).
- **The no-join / single-predecessor invariant is not enforced in code.**
- **A generator always terminates the pipeline;** `noopGenerator` is reachable
  only as an explicit escape hatch.

## Phasing (non-binding)

- **Phase 1.** Introduce the stage catalog + planner + a resolved-plan type
  (explicit order). Move the `csharp` default into the catalog; make the generator
  mandatory. Keep today's `config.pipeline` / `config.generator` as low-level
  escape hatches.
- **Phase 2.** Parallel branch execution, `after`/`before`-driven positioning for
  extension-contributed stages, and an `ace compile --explain` that prints the
  resolved tree.
