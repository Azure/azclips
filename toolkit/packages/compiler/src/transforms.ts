/**
 * The compiler's transform implementations, keyed by stage id.
 *
 * `STAGE_CATALOG` says where each transform runs; this map says which step runs
 * there. Both are compiler-owned, so a host supplies no stage code.
 */

import {
  createCommandStep,
  flattenModelStep,
  tweakIrStep,
  type TransformStep,
} from "@azure-client-engine/transformers";

import { CREATE_COMMAND_STAGE, FLATTEN_MODEL_STAGE, TWEAK_IR_STAGE } from "./stages.js";

/** Builds a transform step from the stage's configured options. */
export type TransformStepFactory = (options: Record<string, unknown>) => TransformStep;

export const TRANSFORMS: ReadonlyMap<string, TransformStepFactory> = new Map<
  string,
  TransformStepFactory
>([
  [TWEAK_IR_STAGE, tweakIrStep],
  [FLATTEN_MODEL_STAGE, flattenModelStep],
  [CREATE_COMMAND_STAGE, createCommandStep],
]);
