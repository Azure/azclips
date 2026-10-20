/**
 * Emit stage seam: produce the initial IR from a source entrypoint.
 *
 * Emit is the pipeline's *producer* — it turns a non-IR input (a TypeSpec spec)
 * into the first IR, mirroring how `Generator` is the terminal consumer. The
 * compiler's built-in emit stage (`builtins/emit.ts`) implements this seam by
 * running the `tsp-emit` tool through the host `Runtime` (the CLI forks the
 * emitter package's worker); a producer injected through `CompileOptions.emit`
 * replaces it, for tests and bespoke embedding. Either way the orchestrator stays
 * host-agnostic and imports no TypeSpec type.
 *
 * The seam is intentionally free of any TypeSpec type: the input is the
 * serializable `CompileInput`; the output is `IR` + structured
 * `CompilerDiagnostic`s (reported via the context).
 */

import type { IR } from "@azure-client-engine/ir";
import type { CompileInput, CompilerDiagnostic } from "./types.js";

/** Per-run services handed to an emit producer. */
export interface EmitContext {
  /** Record a diagnostic without throwing; collected into the final result. */
  reportDiagnostic(diagnostic: CompilerDiagnostic): void;
  /**
   * The emit stage's own stage config, as written: its `stages.<id>.options` map.
   * The compiler routes it by stage id and checks nothing; the producer checks it.
   */
  readonly config: Readonly<Record<string, unknown>>;
  /** Cooperative cancellation shared with the enclosing compile run. */
  readonly signal?: AbortSignal;
}

export interface EmitProduct {
  /** The emitted IR, or undefined if emit produced none (e.g. compile errors). */
  ir?: IR;
}

/** Produces the initial IR from a `CompileInput`. The pipeline's source stage. */
export interface EmitProducer {
  readonly name: string;
  produce(input: CompileInput, context: EmitContext): EmitProduct | Promise<EmitProduct>;
}
