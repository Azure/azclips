/**
 * The PowerShell derivation packaged as a compiler transform step.
 *
 * `deriveDialect` stays a plain function so it can be unit-tested and driven from
 * scripts; this wraps it in the backbone's `TransformStep` contract. It runs after
 * `create-command` (declared via the stage catalog's `requires`), reads
 * `ir.commands`, and mutates each command's and parameter's `dialect.ps` in place --
 * there is no longer an `IR.ps` to park a result on.
 */

import type { TransformedIR } from "@azure-client-engine/ir";
import type { TransformContext, TransformStep } from "@azure-client-engine/compiler";
import { deriveDialect, type DeriveDialectOptions } from "./derive.js";

/** Registry id for the PowerShell derivation step. */
export const PS_DERIVE_STEP_ID = "ps.derive";

const KNOWN_OPTIONS = new Set(["modulePrefix", "subjectPrefix"]);

/**
 * Build the `ps.derive` step.
 *
 * Options are read in `run`, not here: the backbone catches a throw from `run`
 * and reports it as a transform-stage diagnostic, whereas a throw from this
 * factory would escape pipeline resolution as an unhandled error.
 */
export function createPSDeriveStep(
  options: Record<string, unknown> = {},
): TransformStep<TransformedIR, TransformedIR> {
  return {
    name: PS_DERIVE_STEP_ID,
    run(ir: TransformedIR, _context: TransformContext): TransformedIR {
      return deriveDialect(ir, readOptions(options));
    },
  };
}

function readOptions(options: Record<string, unknown>): DeriveDialectOptions {
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
