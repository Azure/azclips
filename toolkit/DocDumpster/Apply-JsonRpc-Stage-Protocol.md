# Applying JSON-RPC as the Compiler's Stage Protocol

> **Status:** Proposed — accepted in principle, **refactor deferred**. NDJSON /
> fork-IPC stay until we act. _(2026-08-26)_
>
> **Scope:** the Azure Client Engine (ACE) compiler pipeline
> (`emit → transform* → generate`) and how the backbone talks to its stages.
>
> This is a decision record of a design discussion. It captures the direction and
> the rationale; it is **not** an implementation plan and does not change code yet.

## Summary

Adopt **JSON-RPC as the compiler's uniform protocol for orchestrating stages**,
with the **IR serialized between every stage**. The split that keeps the design
intact:

- The **compiler** owns the *protocol + orchestration* — the stage methods
  (`emit` / `transform` / `generate`), IR (de)serialization into `params` /
  `result`, id/error correlation, and the run sequence. All pure logic; the
  backbone stays free of `child_process` / sockets / `fs`.
- The **host** (CLI, or a web app) owns *transport* — it provides a duplex
  message **channel** to each stage endpoint (spawn a subprocess + stdio, a
  socket, or an in-process loopback) and maps its own surface (a CLI command, an
  HTTP/WS request) onto a compile run. Hosts stay thin.

Serializing the IR at every stage boundary is treated as a **feature, not a
cost**: it makes per-stage traceability and (later) interactive/web hosts fall
out of the protocol by construction. The performance hit is acceptable for a
build-time codegen tool.

## Context — where we are today

- **Pipeline & hosts.** The `@azure-client-engine/compiler` backbone is pure and
  host-agnostic: it sequences `emit → transform* → generate`, owns the stage
  catalog, and reaches external stages only through an injected host **`Runtime`**
  seam (`spawn` / `spawnStreaming`). The CLI is a thin front end that maps
  `ace compile …` onto `compile()` and supplies the runtime.
- **Bespoke, heterogeneous stage protocols today:**
  - `emit` (`tsp-emit`): Node `fork` + IPC — a single buffered request/response
    that returns the whole IR.
  - `generate` (`csharp-generate`): `spawn` `dotnet` + **NDJSON** frames streamed
    over stdio (`file` / `remove` / terminal `result`).
  - `transform`: in-process TypeScript steps, which the compiler maps to stage
    ids itself (a host registry until 2026-10-07) — no wire protocol at all.
- **The `Runtime` seam already isolates transport.** The compiler owns the *frame
  protocol*; the host owns *spawning*. So swapping the wire protocol is a
  localized change — the orchestration/pipeline does not move.
- **IR is already the cross-language contract.** It is defined once in Zod
  (`packages/ir`), a JSON Schema is derived from it, and the C# generator's POCOs
  are generated from that schema; every document carries a `schemaVersion`.
  JSON-RPC would simply be the *transport* for a contract we already maintain.
- **IR between stages is already materialized.** `--capture-ir` writes
  `ir/<index>-<step>.json` after emit and after each transform step
  (`IRSnapshot`, with `structuredClone` isolation). The backbone already treats
  "IR between stages" as first-class data.

## Decision

1. **JSON-RPC is the compiler's stage-orchestration protocol.** The compiler acts
   as the JSON-RPC orchestrator, invoking `emit` / `transform` / `generate`
   methods on stage endpoints and threading the **serialized IR** through
   `params` / `result`.
2. **Protocol in the compiler, transport in the host.** Today's `Runtime.spawn`
   ("spawn a tool, hand me bytes") is elevated to a **stage-channel seam** ("give
   me a duplex JSON-RPC channel to this stage endpoint"). The compiler layers
   JSON-RPC on top; the host binds the channel (subprocess stdio / socket /
   in-process). The backbone stays pure.
3. **Same vision for every host.** The **CLI** maps `ace compile …` → a compile
   run + binds channels; a **web host** maps an HTTP/WS request → the *same*
   compile run + binds channels (possibly to remote workers). Identical
   orchestration underneath; only the surface mapping and channel binding differ.
4. **Serialization between stages is intrinsic — and desired.** Because each stage
   is reached through a JSON-RPC call, the IR crosses every boundary as serialized
   `params` / `result` by construction. `--capture-ir` stops being a special code
   path: the snapshots *are* the wire payloads.
5. **Default: serialized / uniform.** Every stage — including in-process ones — is
   modeled as a JSON-RPC endpoint over a channel, so the IR is materialized at each
   hop (observable, traceable). A **fast-path mode** that skips serialization for
   in-process stages is an **opt-in escape hatch for guaranteed non-interactive
   runs** — an open investigation (see below), not part of v1.

## Why — benefits

- **One protocol, N stages, any language/location.** `emit` / `transform` /
  `generate` all speak the same thing over the shared IR, whether a stage is
  in-process, a subprocess, or a remote service, in any language.
- **Traceability for free.** Serialized IR between stages is diffable: to locate
  where a bug enters, compare the IR *between* stages, not just endpoints. Capture
  is inherent to the protocol.
- **CLI and web host, both thin.** Directly serves the long-standing goal that a
  CLI and a web service reuse the backbone unchanged.
- **Standard semantics.** Request/response correlation (ids), structured errors,
  notifications (for streamed output like generate's files), and a uniform place
  for progress / cancellation / tracing across *all* stages.
- **Acceptable cost.** ACE produces product code at build time; it is not a
  latency-critical hot path, so per-hop serialization is a fair price for the
  observability and host flexibility it buys.

## Costs / tradeoffs (recorded honestly)

- **Per-hop IR serialization.** In-process transforms pass IR by reference today
  (zero-copy); under a uniform JSON-RPC model each boundary serializes the whole
  (possibly multi-MB) IR. **Accepted deliberately here** — it is the mechanism
  behind traceability and web/interactive readiness — but it is the motivation
  for the in-process escape hatch investigation.
- **More machinery than one-shot-then-exit.** Message dispatch, error/lifecycle
  handling, and (if endpoints become persistent) health/restart/orphan concerns.
- **Version skew across endpoints.** Independently deployed stage endpoints must
  agree on the IR `schemaVersion` and protocol version.
- **Not a fix for huge IR *input*.** A `generate`/`transform` call still carries
  the whole IR in `params`; JSON-RPC does not stream a single large document. IR
  *input* stays one-shot (a separate, harder problem — see Non-goals).

## Design sketch (non-binding)

- **Stage-channel seam.** The host provides a duplex JSON-RPC channel per stage;
  the compiler sends requests and consumes responses/notifications over it.
  `emit` / `generate` bind to subprocess channels; `transform` binds to
  in-process (loopback) or external channels.

  > **Note (2026-10-07):** in-process transforms are now compiler dependencies
  > (see [Revision — compiler-owned stage implementations](./Compiler-Owned-Stage-Orchestration.md#revision--compiler-owned-stage-implementations)),
  > so their loopback channel is the compiler's own. A host binds channels only
  > for out-of-process stages: `emit`, `generate`, and any external transform.
- **Methods over IR.** Roughly: `emit(source/config) → IR`,
  `transform(IR, options) → IR`, `generate(IR, options) → { diagnostics, success }`
  with generated **files/removes streamed as notifications** (the JSON-RPC analog
  of today's NDJSON `file` / `remove` frames; removes precede files, as now).
- **Framing/library are open** — a compact JSON-RPC message can ride the existing
  NDJSON line framing (keeps `readline` streaming), or move to LSP-style
  `Content-Length`. Hand-rolled vs. a library (StreamJsonRpc on .NET,
  vscode-jsonrpc on Node) is undecided.

## To investigate further

- **In-process stage escape hatch (zero-copy fast-path).** _(Explicitly flagged
  for exploration; no concrete solution now.)_ How to let a local in-process
  transform bypass IR serialization while everything else stays uniform JSON-RPC
  — e.g. a loopback channel that hands the IR by reference, a capability the
  channel advertises, or a two-tier stage seam (in-proc fast path vs. RPC path).
  Goal: keep the uniform mental model without paying serialization where it buys
  nothing. Details to work through:
  - **Shape: an explicit mode switch, not auto-detection.** A compiler option
    selects fast-path vs. the serialized default. The host already knows whether a
    run is interactive or automation, so a coarse, explicit switch beats the
    compiler guessing per stage (explicit over magic). Apt for
    automation / pipeline / CI / tooling runs that are guaranteed non-interactive,
    where per-stage IR materialization buys nothing.
  - **Default is serialized** (observable, traceable, web/interactive-ready);
    fast-path is the opt-in for guaranteed-non-interactive runs.
  - **Name the real axis.** The switch is "materialize intermediate IR: on/off,"
    not "fast/slow" — the speed difference is a *consequence* of whether the
    intermediate IR is materialized, not the thing being toggled.
  - **Payoff is bounded to in-process hops.** `emit` and `generate` are separate
    processes, so the IR serializes crossing *those* boundaries regardless of mode.
    Fast-path only skips serialization along the **in-process transform chain**, so
    the win scales with the number of transforms; a bare `emit → generate` (no
    transforms) sees essentially no difference.
  - **Mutually exclusive with capture / trace.** Fast-path materializes nothing
    between stages, so `--capture-ir` and any diff-between-stages debugging force
    the serialized path. Rule: asking to capture ⇒ serialized mode.
  - **Invariant — both modes MUST produce identical output.** Serialization here is
    purely observability/materialization, never semantic. This leans on faithful,
    canonical IR (de)serialization (see the next item) and is cheaply guarded by a
    differential test: run fast-path vs. serialized on the same input and assert the
    same generated files. The modes must never be allowed to drift.
  - A compiler option to switch between fast-path and default. Good for automation/pipeline/tooling scenario where non-interactive guaranteed
- **Canonical / deterministic IR serialization** — stable key/array ordering so
  between-stage diffs are clean and snapshots are reproducible/comparable.
- **Session / state model** for future interactive hosts — stateless
  (client ships the IR back per call) vs. stateful (server holds a snapshot
  session). Ties to whether stage endpoints are persistent.
- **Framing + library choice** (NDJSON line vs. `Content-Length`; hand-rolled vs.
  StreamJsonRpc / vscode-jsonrpc).
- **Protocol + IR version negotiation** across independently deployed endpoints.

## Deferred scenarios (recorded, not designed now)

These become natural once stages are JSON-RPC endpoints over host-bound channels,
which is a large part of why the direction is worth recording even while deferring
the refactor:

- **Interactive / wizard host.** A web app where the user click-picks transforms,
  inspects the resulting IR after each step, and chooses what to do next
  (branch / undo / redo; generate-as-preview from any IR state). Requires
  materialized IR per step (which this protocol provides) plus stepwise/resumable
  execution and a self-describing transform catalog. **Noted; considered later.**
- **Polyglot / remote / sandboxed stages.** Transforms or generators written in
  other languages, run out-of-process or as remote/isolated workers.
- **Warm worker pools.** Persistent stage endpoints that amortize process/JIT
  startup across many compiles (watch mode, a hosted multi-tenant compiler).

## Non-goals / invariants to preserve

- **The compiler stays pure.** Protocol + orchestration only; transport is always
  a host-injected channel.
- **IR *input* remains one-shot** for now (the request carries the whole IR); this
  document targets the *stage-to-stage* and *output* paths.
- **Do not force serialization onto hot in-process paths beyond the accepted
  default** — that is exactly what the escape-hatch investigation is for.

## Why defer, and how we'd migrate

- **Right-sized today.** Two external stages (emit, generate) + in-process
  transforms. NDJSON + fork-IPC are adequate and freshly streaming.
- **Localized, reversible adoption.** Because the `Runtime`/stage seam already
  isolates transport, moving to JSON-RPC swaps the runtime/channel implementation
  and the stage tool descriptors — orchestration is untouched.
- **Revisit when a concrete trigger appears:** a web/interactive host; a polyglot
  or sandboxed stage; warm pools for a hosted service; or editor/LSP integration
  (LSP *is* JSON-RPC).
