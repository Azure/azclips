/**
 * The PowerShell transforms packaged as compiler transform steps.
 *
 * `deriveAttributes` and `applyDirectives` stay plain functions so they can be
 * unit-tested and driven from scripts; these wrap them in the backbone's
 * `TransformStep` contract, reading and writing `IR.ps`.
 */

import type { IR } from "@azure-client-engine/ir";
import type { TransformContext, TransformStep } from "@azure-client-engine/compiler";
import { deriveAttributes, type DeriveAttributesOptions } from "./derive.js";
import { applyDirectives } from "./apply-directives.js";
import { PSDirectivesSchema, type PSDirective } from "./directives.js";

/** Registry id for the PowerShell derivation step. */
export const PS_DERIVE_STEP_ID = "ps.derive";

/** Registry id for the PowerShell directive step. */
export const PS_DIRECTIVES_STEP_ID = "ps.directives";

const KNOWN_OPTIONS = new Set(["modulePrefix", "subjectPrefix"]);

const KNOWN_DIRECTIVE_OPTIONS = new Set(["directives"]);

/**
 * Build the `ps.derive` step.
 *
 * Options are read in `run`, not here: the backbone catches a throw from `run`
 * and reports it as a transform-stage diagnostic, whereas a throw from this
 * factory would escape pipeline resolution as an unhandled error.
 */
export function createPSDeriveStep(options: Record<string, unknown> = {}): TransformStep {
  return {
    name: PS_DERIVE_STEP_ID,
    run(ir: IR, _context: TransformContext): IR {
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

/**
 * Build the `ps.directives` step, which overrides the derived surface with a
 * module's authoring directives. Requires `ps.derive` to have run first.
 */
export function createPSDirectivesStep(options: Record<string, unknown> = {}): TransformStep {
  return {
    name: PS_DIRECTIVES_STEP_ID,
    run(ir: IR, context: TransformContext): IR {
      if (ir.ps === undefined) {
        context.reportDiagnostic({
          severity: "error",
          code: "@azure-client-engine/transformers/ps-surface-missing",
          message: `${PS_DIRECTIVES_STEP_ID} needs a document that already carries the PowerShell surface; run '${PS_DERIVE_STEP_ID}' before it.`,
        });
        return ir;
      }

      const { attributes, diagnostics } = applyDirectives(ir.ps, readDirectives(options));

      // A directive that matched nothing is reported, never dropped: silently
      // ignored overrides are how modules end up with directives their authors
      // believe are working.
      for (const diagnostic of diagnostics) {
        context.reportDiagnostic({
          severity: diagnostic.severity,
          code: `@azure-client-engine/transformers/${diagnostic.code}`,
          message: `directive[${diagnostic.directiveIndex}]: ${diagnostic.message}`,
        });
      }

      return { ...ir, ps: attributes };
    },
  };
}

function readDirectives(options: Record<string, unknown>): PSDirective[] {
  for (const key of Object.keys(options)) {
    if (!KNOWN_DIRECTIVE_OPTIONS.has(key)) {
      throw new Error(
        `${PS_DIRECTIVES_STEP_ID}: unknown option '${key}'. Expected one of ${[...KNOWN_DIRECTIVE_OPTIONS].join(", ")}.`,
      );
    }
  }

  const parsed = PSDirectivesSchema.safeParse(options["directives"] ?? []);
  if (!parsed.success) {
    throw new Error(
      `${PS_DIRECTIVES_STEP_ID}: invalid 'directives' option: ${parsed.error.issues
        .map((issue) => `${issue.path.join(".") || "(root)"}: ${issue.message}`)
        .join("; ")}`,
    );
  }
  return parsed.data;
}
