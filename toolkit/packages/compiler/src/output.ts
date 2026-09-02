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

/** A labeled IR snapshot taken at a pipeline step boundary. */
export interface IRSnapshot {
  /** Ordered position in the pipeline; 0 = emit, then 1..N per transform step. */
  index: number;
  /** Producing step name: "emit", then each transform step's (unique) name. */
  step: string;
  /** The IR as it stood immediately after `step`. */
  ir: IR;
}

/** An artifact the backbone emits during a compile run. */
export type EmitArtifact =
  | { kind: "generated-file"; path: string; contents: string }
  | { kind: "remove"; path: string }
  | ({ kind: "ir-snapshot" } & IRSnapshot);

/**
 * Host-provided output sink. The backbone awaits it once per artifact, in
 * pipeline order. Implementations own destination + serialization.
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
      snapshots.push({ index: artifact.index, step: artifact.step, ir: artifact.ir });
    }
    // "remove" artifacts are host filesystem effects; the in-memory collector has
    // nothing to remove (removes precede writes within a run), so it ignores them.
  };
  return { write, files, snapshots };
}
