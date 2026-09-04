# Compiler-Owned Stage Orchestration: a Fork-Only Stage Tree

> **Status:** Accepted (design) — **implementation pending.** Today's
> host-supplied ordered pipeline and no-op-generator default stay until we act.
> _(2026-08-26)_
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
