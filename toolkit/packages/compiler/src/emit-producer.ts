/**
 * Emit stage seam: produce the initial IR from a source entrypoint.
 *
 * Emit is the pipeline's *producer* — it turns a non-IR input (a TypeSpec spec)
 * into the first IR, mirroring how `Generator` is the terminal consumer. Making
 * emit an injectable seam (like transform / generator) keeps the orchestrator
 * host-agnostic and TypeSpec-free: it declares no built-in emit, so a host
 * supplies one — e.g. the CLI's subprocess-backed producer that runs
 * `@typespec/compiler` out of process (design Part II) — without the
 * orchestrator importing any TypeSpec type.
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
