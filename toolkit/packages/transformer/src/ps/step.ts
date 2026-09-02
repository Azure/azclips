/**
 * The PowerShell derivation packaged as a compiler transform step.
 *
 * `deriveAttributes` stays a plain function so it can be unit-tested and driven
 * from scripts; this wraps it in the backbone's `TransformStep` contract and
 * parks the result on `IR.ps`.
 */

import type { IR } from "@azure-client-engine/ir";
import type { TransformStep } from "@azure-client-engine/compiler";
import { deriveAttributes, type DeriveAttributesOptions } from "./derive.js";

/** Registry id for the PowerShell derivation step. */
export const PS_DERIVE_STEP_ID = "ps.derive";

const KNOWN_OPTIONS = new Set(["modulePrefix", "subjectPrefix"]);

/**
 * Build the `ps.derive` step. Options are validated inside `run` rather than
 * here so a bad config surfaces as a transform-stage diagnostic instead of an
 * exception escaping pipeline resolution.
 */
export function createPSDeriveStep(options: Record<string, unknown> = {}): TransformStep {
  return {
    name: PS_DERIVE_STEP_ID,
    run(ir: IR): IR {
      return { ...ir, ps: deriveAttributes(ir, readOptions(options)) };
    },
  };
}

function readOptions(options: Record<string, unknown>): DeriveAttributesOptions {
  for (const key of Object.keys(options)) {
    if (!KNOWN_OPTIONS.has(key)) {
      throw new Error(
        `${PS_DERIVE_STEP_ID}: unknown option '${key}'. Expected one of ${[...KNOWN_OPTIONS].join(", ")}.`,
      );
    }
  }
  return {
    modulePrefix: optionalString(options, "modulePrefix"),
    subjectPrefix: optionalString(options, "subjectPrefix"),
  };
}

function optionalString(options: Record<string, unknown>, key: string): string | undefined {
  const value = options[key];
  if (value === undefined) return undefined;
  if (typeof value !== "string") {
    throw new Error(`${PS_DERIVE_STEP_ID}: option '${key}' must be a string, got ${typeof value}.`);
  }
  return value;
}
