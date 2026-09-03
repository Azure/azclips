/**
 * Wire protocol between the ace process and the `tsp-compile` worker.
 *
 * Both shapes are plain JSON (no live TypeSpec objects): the request carries the
 * resolved toolchain paths + emit knobs; the response carries the emitted IR (as
 * parsed JSON) and already-translated structured diagnostics. Keeping the
 * boundary data-only is what lets the worker load a *different* `@typespec/compiler`
 * without any of its types leaking into the orchestrator.
 */

import type { CompilerDiagnostic } from "@azure-client-engine/compiler";

/** Sent from the producer (ace process) to the worker (child process). */
export interface TspCompileRequest {
  /** Resolved `@typespec/compiler` entry to dynamic-import in the worker. */
  compilerPath: string;
  /** Resolved emitter entry to register with the compiler. */
  emitterPath: string;
  /** Staged entrypoint (project dir or `.tsp` file) to compile. */
  entrypoint: string;
  /** Options forwarded verbatim to the Azure Client Engine emitter. */
  emitterOptions?: Record<string, unknown>;
  /**
   * API version to compile at (a specific version, `"latest"`, or `"all"`).
   * Delivered to the emitter as its `api-version` option, which TCGC reads to
   * project the `@versioned` program before the IR is built.
   */
  apiVersion?: string;
  /** Extra imports layered on top of the entrypoint's own imports. */
  additionalImports?: string[];
  /** Promote warning-severity TypeSpec diagnostics to errors. */
  warnAsError?: boolean;
  /** TypeSpec trace areas to enable. */
  trace?: string[];
  /** Skip loading the TypeSpec standard library. */
  nostdlib?: boolean;
}

/** Returned from the worker to the producer. */
export interface TspCompileResponse {
  /** Parsed `ir.json` the emitter produced, or undefined if none was emitted. */
  ir?: unknown;
  /** Structured diagnostics from the compile + emit (already translated). */
  diagnostics: CompilerDiagnostic[];
}
