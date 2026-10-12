/**
 * The transform contract: one IR -> IR step in the compiler's stage sequence.
 *
 * Defined next to the steps, so this package depends only on the IR. The compiler
 * depends on this package: its `STAGE_CATALOG` declares each step's id, position
 * and default, it maps each id to the step implementing it, and it builds the
 * context handed to the step. A step has no say in where it runs.
 */

import type { IR } from "@azure-client-engine/ir";

/** Per-run services the compiler hands every step. */
export interface TransformContext {
  /**
   * Record a diagnostic without throwing; the compiler collects it into the run's
   * result. An error fails the step: once the step returns, the compiler stops the
   * sequence, so a step reports every problem it finds before returning.
   *
   * A function-typed property rather than a method, so the reporter the compiler
   * passes in is checked contravariantly against this shape: the compiler stops
   * building if its diagnostic type no longer accepts what a step reports.
   */
  readonly reportDiagnostic: (diagnostic: {
    severity: "error" | "warning";
    code: string;
    message: string;
  }) => void;
  /** Cooperative cancellation shared with the enclosing compile run. */
  readonly signal?: AbortSignal;
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
