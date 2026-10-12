/**
 * The compiler's transform implementations, keyed by stage id.
 *
 * `STAGE_CATALOG` says where each transform runs; this map says which step runs
 * there. Both are compiler-owned, so a host supplies no stage code.
 */

import {
  createCommandStep,
  flattenModelStep,
  seedIrStep,
  tweakIrStep,
  type TransformStep,
} from "@azure-client-engine/transformers";

import {
  CREATE_COMMAND_STAGE,
  FLATTEN_MODEL_STAGE,
  SEED_IR_STAGE,
  TWEAK_IR_STAGE,
} from "./stages.js";

/** Builds a transform step from the stage's entry. */
export type TransformStepFactory = (entry: Record<string, unknown>) => TransformStep;

export const TRANSFORMS: ReadonlyMap<string, TransformStepFactory> = new Map<
  string,
  TransformStepFactory
>([
  [SEED_IR_STAGE, seedIrStep],
  [TWEAK_IR_STAGE, tweakIrStep],
  [FLATTEN_MODEL_STAGE, flattenModelStep],
  [CREATE_COMMAND_STAGE, createCommandStep],
]);
