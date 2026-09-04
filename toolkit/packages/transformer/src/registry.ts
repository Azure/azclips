/**
 * Registry contributions from this package: every transform step it provides,
 * keyed by the id used in `CompileConfig.pipeline[].use`. Hosts merge this into
 * the registry they hand to `compile()`, so adding a surface here is enough to
 * make it declarable in config.
 */

import type { TransformStepFactory } from "@azure-client-engine/compiler";
import { createPSDeriveStep, PS_DERIVE_STEP_ID } from "./ps/step.js";

export const transformSteps: Record<string, TransformStepFactory> = {
  [PS_DERIVE_STEP_ID]: createPSDeriveStep,
};
