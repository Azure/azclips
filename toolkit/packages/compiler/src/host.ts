/**
 * A `CompilerHost` wrapper that captures emitter/generator output in memory
 * instead of writing it to disk.
 *
 * Rationale (backbone contract): "outputs are RETURNED, never written" and
 * "no process side effects". We intercept `writeFile`/`mkdirp` so nothing
 * touches disk and expose whatever was written via `outputs`; every other
 * operation (including reads and library resolution) delegates to the base
 * host. The compile entrypoint is always a real local path, so no in-memory
 * source overlay is needed — the app materializes sources before calling us.
 */

import { NodeHost, normalizePath, type CompilerHost } from "@typespec/compiler";

export interface CaptureHost {
  host: CompilerHost;
  /** Files written by emitters/generators, keyed by normalized path. */
  outputs: Map<string, string>;
}

export function createCaptureHost(base: CompilerHost = NodeHost): CaptureHost {
  const outputs = new Map<string, string>();

  const host: CompilerHost = {
    ...base,
    writeFile: async (path, content) => {
      outputs.set(normalizePath(path), content);
    },
    mkdirp: async (path) => path,
  };

  return { host, outputs };
}
