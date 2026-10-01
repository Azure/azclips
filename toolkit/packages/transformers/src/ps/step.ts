import type { TransformedIR } from "@azure-client-engine/ir";
import type { TransformContext, TransformStep } from "@azure-client-engine/compiler";
import { deriveDialect, type DeriveDialectOptions } from "./derive.js";
import { applyDirectives } from "./apply-directives.js";
import { PSDirectivesSchema, type PSDirective } from "./directives.js";

export const PS_DERIVE_STEP_ID = "ps.derive";
export const PS_DIRECTIVES_STEP_ID = "ps.directives";

const KNOWN_OPTIONS = new Set(["modulePrefix", "subjectPrefix"]);
const KNOWN_DIRECTIVE_OPTIONS = new Set(["directives"]);

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

export function createPSDirectivesStep(
  options: Record<string, unknown> = {},
): TransformStep<TransformedIR, TransformedIR> {
  return {
    name: PS_DIRECTIVES_STEP_ID,
    run(ir: TransformedIR, context: TransformContext): TransformedIR {
      const missing = ir.commands.filter((command) => command.dialect?.ps === undefined);
      if (missing.length > 0) {
        context.reportDiagnostic({
          severity: "error",
          code: "@azure-client-engine/transformers/ps-command-dialect-missing",
          message:
            `${PS_DIRECTIVES_STEP_ID} needs every command to carry dialect.ps; ` +
            `run '${PS_DERIVE_STEP_ID}' before it. Missing: ${missing.map((command) => command.name).join(", ")}.`,
        });
        return ir;
      }

      const { ir: transformed, diagnostics } = applyDirectives(ir, readDirectives(options));
      for (const diagnostic of diagnostics) {
        context.reportDiagnostic({
          severity: diagnostic.severity,
          code: `@azure-client-engine/transformers/${diagnostic.code}`,
          message: `directive[${diagnostic.directiveIndex}]: ${diagnostic.message}`,
        });
      }
      return transformed;
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