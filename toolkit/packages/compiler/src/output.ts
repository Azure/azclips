/**
 * Output port: the single seam through which the backbone hands artifacts to a
 * host.
 *
 * The backbone decides *when* to emit each artifact; the host-provided
 * `OutputDelegate` decides *where/how* it lands (CLI -> filesystem, web app ->
 * blob/stream, tests -> in-memory). This keeps the backbone free of any direct
 * runtime coupling: it never imports `node:fs`, never touches `process`.
 *
 * When no delegate is supplied, `createCollectorSink` provides an in-memory
 * default that populates `CompileResult.files` / `.snapshots`, preserving the
 * serializable "return everything" contract for hosts behind a queue/sandbox
 * and for tests.
 */

import type { IR } from "@azure-client-engine/ir";

/** A labeled IR snapshot taken at a stage boundary. */
export interface IRSnapshot {
  /**
   * Position in the compiler's stage sequence; 0 = the emit stage. Indices can
   * have gaps: generate stages take a position but produce no IR to snapshot.
   */
  index: number;
  /** Producing stage id — unique by construction, so it needs no de-duplication. */
  step: string;
  /** The IR as it stood immediately after `step`. */
  ir: IR;
}

/**
 * An artifact the backbone emits during a compile run.
 *
 * Generated files carry the `stage` that produced them: with several generate
 * stages in one sequence, a host needs that to scope output (and the backbone
 * uses it to detect two stages writing the same path).
 */
export type EmitArtifact =
  | { kind: "generated-file"; stage: string; path: string; contents: string }
  | { kind: "remove"; stage: string; path: string }
  | ({ kind: "ir-snapshot" } & IRSnapshot);

/**
 * Host-provided output sink. The backbone awaits it once per artifact.
 *
 * An `ir-snapshot` carries the **live** IR, not a copy. A sink that serializes
 * it immediately (before its first `await`) needs no copy, which is why capture
 * is free on the filesystem path. A sink that *retains* the object **must** copy
 * it first.
 *
 * That contract is load-bearing: a run has exactly one IR, mutated in place from
 * the emit stage onward, so a retaining sink that skips the copy does not get a
 * slightly stale snapshot — it gets N references to the run's *final* state,
 * silently.
 */
export type OutputDelegate = (artifact: EmitArtifact) => void | Promise<void>;

/** In-memory sink plus the buffers it fills. */
export interface CollectorSink {
  write: OutputDelegate;
  files: Map<string, string>;
  snapshots: IRSnapshot[];
}

/**
 * Default sink used when the caller provides no `output`. Collects generated
 * files and IR snapshots into buffers that the backbone returns on the result.
 */
export function createCollectorSink(): CollectorSink {
  const files = new Map<string, string>();
  const snapshots: IRSnapshot[] = [];
  const write: OutputDelegate = (artifact) => {
    if (artifact.kind === "generated-file") {
      files.set(artifact.path, artifact.contents);
    } else if (artifact.kind === "ir-snapshot") {
      // This sink retains, so it owns the copy: the orchestrator hands over the
      // live IR, which later stages keep writing to.
      snapshots.push({
        index: artifact.index,
        step: artifact.step,
        ir: structuredClone(artifact.ir),
      });
    }
    // "remove" artifacts are host filesystem effects; the in-memory collector has
    // nothing to remove (removes precede writes within a run), so it ignores them.
  };
  return { write, files, snapshots };
}
