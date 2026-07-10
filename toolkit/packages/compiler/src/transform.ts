/**
 * Transform stage: an ordered IR -> IR pipeline.
 *
 * The backbone runs exactly one `TransformPipeline` (defaulting to the
 * identity pipeline). Front ends compose the passes they want with
 * `createTransformPipeline` and inject the result via `CompileOptions.transform`.
 * This keeps the backbone stable while future rename/flatten/normalize passes
 * are added purely on the caller side.
 */

import type { IR } from "@azure-client-engine/ir";
import type { CompilerDiagnostic } from "./types.js";
import { throwIfAborted } from "./abort.js";

/** Per-run services handed to every step. */
export interface TransformContext {
  /** Record a diagnostic without throwing; collected into the final result. */
  reportDiagnostic(diagnostic: CompilerDiagnostic): void;
  /** Cooperative cancellation shared with the enclosing compile run. */
  readonly signal?: AbortSignal;
  /** App-resolved free-form settings (`CompileInput.extensions`); `{}` if none. */
  readonly extensions: Record<string, unknown>;
}

/** A single IR -> IR transformation. The building block of a pipeline. */
export type TransformStep = (ir: IR, context: TransformContext) => IR | Promise<IR>;

/** An ordered, named sequence of IR -> IR steps. */
export interface TransformPipeline {
  readonly name: string;
  run(ir: IR, context: TransformContext): IR | Promise<IR>;
}

/** Compose ordered steps into a pipeline, threading the IR through each. */
export function createTransformPipeline(
  name: string,
  steps: readonly TransformStep[],
): TransformPipeline {
  return {
    name,
    async run(ir, context) {
      let current = ir;
      for (const step of steps) {
        throwIfAborted(context.signal);
        current = await step(current, context);
      }
      return current;
    },
  };
}

/** Default pipeline: returns the IR untouched. */
export const identityPipeline: TransformPipeline = {
  name: "identity",
  run: (ir) => ir,
};
