/**
 * Transform stage: an ordered sequence of named IR -> IR steps.
 *
 * The backbone owns the loop over `TransformPipeline.steps` (defaulting to the
 * empty identity pipeline), so it can label and capture the IR at each step
 * boundary. Front ends compose steps with `createTransformPipeline` (injected
 * via `CompileOptions.transform`) or declare them as data in
 * `CompileConfig.pipeline`, resolved against a `CompilerRegistry`.
 */

import type { IR } from "@azure-client-engine/ir";
import type { CompileConfig, CompilerDiagnostic } from "./types.js";

/** Per-run services handed to every step. */
export interface TransformContext {
  /** Record a diagnostic without throwing; collected into the final result. */
  reportDiagnostic(diagnostic: CompilerDiagnostic): void;
  /** Cooperative cancellation shared with the enclosing compile run. */
  readonly signal?: AbortSignal;
  /**
   * App-resolved compile configuration (`CompileInput.config`); `{}` if none.
   * Steps read their own named fields off it (there is no free-form bag).
   */
  readonly config: CompileConfig;
}

/**
 * A single named IR -> IR transformation. The `name` labels the step's captured
 * IR snapshot (`ir/<index>-<name>.json`), so it should be filename-safe.
 */
export interface TransformStep {
  readonly name: string;
  run(ir: IR, context: TransformContext): IR | Promise<IR>;
}

/**
 * An ordered, named sequence of steps. The backbone iterates `steps` itself
 * (running each, then capturing a snapshot), so step boundaries stay observable.
 */
export interface TransformPipeline {
  readonly name: string;
  readonly steps: readonly TransformStep[];
}

/** Compose ordered steps into a pipeline. */
export function createTransformPipeline(
  name: string,
  steps: readonly TransformStep[],
): TransformPipeline {
  return { name, steps: [...steps] };
}

/** Default pipeline: no steps, so the IR passes through untouched. */
export const identityPipeline: TransformPipeline = {
  name: "identity",
  steps: [],
};
