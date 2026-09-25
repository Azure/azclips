/**
 * Transform stage: one IR -> IR step in the compiler's stage sequence.
 *
 * A step has no say in where it runs. The compiler's `STAGE_CATALOG` declares
 * its id, its position, and whether it is on by default; a host only supplies
 * the implementation behind the id via `CompileOptions.registry`. There is no
 * host-authored pipeline object — the catalog is the pipeline.
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
   * Per-stage options arrive through the step's factory, not from here.
   */
  readonly config: CompileConfig;
}

/**
 * A single named IR -> IR transformation. The `name` is conventionally the
 * stage id, which labels the captured IR snapshot (`ir/<index>-<stage>.json`).
 *
 * A run has exactly one IR, so returning the same object mutated in place is the
 * expected shape; returning a replacement is equally valid, and the orchestrator
 * carries on with whatever comes back.
 *
 * `In` and `Out` let a step state the **document phase** it works in. Most steps
 * leave both as `IR`; a step that projects commands declares
 * `TransformStep<IR, TransformedIR>`, and one that annotates them declares
 * `TransformStep<TransformedIR, TransformedIR>` — so it reads `ir.commands`
 * without a cast.
 *
 * The orchestrator erases the parameters: it threads the widest document type, and
 * *ordering* is what actually guarantees a phase, via `StageDescriptor.requires`.
 * Nothing forces a step's declared phase and its catalog `requires` to agree —
 * that stays review discipline, like the additive-after-generate rule.
 */
export interface TransformStep<In extends IR = IR, Out extends IR = In> {
  readonly name: string;
  run(ir: In, context: TransformContext): Out | Promise<Out>;
}
