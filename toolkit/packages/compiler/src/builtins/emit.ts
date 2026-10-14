/**
 * Built-in emit stage: TypeSpec spec -> IR, run out of process.
 *
 * The compiler owns *that* emit is stage 0 and *that* it is an external
 * subprocess speaking the `tsp-emit` protocol; it drives the tool through the
 * host `Runtime`. Everything host/spec-specific — resolving which
 * `@typespec/compiler` + emitter to load (the hybrid, spec-preferred toolchain),
 * and actually spawning the worker — lives behind `runtime.spawn`.
 *
 * This is a thin adapter onto the existing `EmitProducer` seam, so the
 * orchestrator's emit stage (snapshots, diagnostics, cancellation) is unchanged;
 * with a runtime present the backbone wires this in place of an injected
 * producer.
 */

import { describeValue, isMap, warnUnknownKeys } from "@azure-client-engine/transformers";

import type { EmitContext, EmitProducer, EmitProduct } from "../emit-producer.js";
import type { CompileInput, CompilerDiagnostic } from "../types.js";
import { TSP_EMIT_TOOL, type Runtime } from "../runtime.js";
import { TSP_EMIT_STAGE } from "../stages.js";
import type { EmitToolRequest, EmitToolResponse } from "./protocol.js";

const DIAGNOSTIC_PREFIX = `@azure-client-engine/compiler/${TSP_EMIT_STAGE}`;

/** The stage's options, as the `tsp-emit` tool takes them. */
type EmitSettings = Pick<
  EmitToolRequest,
  "emitterOptions" | "additionalImports" | "warnAsError" | "trace" | "nostdlib"
>;

const OPTION_KEYS: ReadonlySet<string> = new Set([
  "warnAsError",
  "trace",
  "nostdlib",
  "additionalImports",
  "emitter",
]);

type Report = (severity: "error" | "warning", code: string, message: string) => void;

/**
 * Build the emit producer backed by the host runtime's `tsp-emit` tool. Its stage
 * config arrives in the context: `warnAsError`, `trace`, `nostdlib`,
 * `additionalImports`, and `emitter`, the options forwarded verbatim to the Azure
 * Client Engine emitter.
 */
export function createBuiltinEmit(runtime: Runtime): EmitProducer {
  return {
    name: TSP_EMIT_TOOL,
    async produce(input: CompileInput, context: EmitContext): Promise<EmitProduct> {
      const report: Report = (severity, code, message) =>
        context.reportDiagnostic({ severity, code: `${DIAGNOSTIC_PREFIX}/${code}`, message });

      // An invalid option fails the stage before the tool is started.
      const settings = readOptions(context.config, report);
      if (settings === undefined) return {};

      const request: EmitToolRequest = {
        entrypoint: input.entrypoint,
        ...(input.apiVersion !== undefined ? { apiVersion: input.apiVersion } : {}),
        ...settings,
      };

      const result = await runtime.spawn(
        { tool: TSP_EMIT_TOOL, input: JSON.stringify(request) },
        { ...(context.signal !== undefined ? { signal: context.signal } : {}) },
      );

      let response: EmitToolResponse;
      try {
        response = JSON.parse(result.stdout) as EmitToolResponse;
      } catch {
        context.reportDiagnostic(toolFailure(result.code, result.stderr));
        return {};
      }

      for (const diagnostic of response.diagnostics ?? []) context.reportDiagnostic(diagnostic);
      if (response.ir === undefined) return {};
      return { ir: response.ir as NonNullable<EmitProduct["ir"]> };
    },
  };
}

/** Diagnostic for a `tsp-emit` run that produced no parseable reply. */
function toolFailure(code: number | null, stderr: string): CompilerDiagnostic {
  const detail = stderr.trim().length > 0 ? `: ${stderr.trim()}` : "";
  return {
    severity: "error",
    code: "@azure-client-engine/compiler/emit-tool-failed",
    message: `The tsp-emit tool exited (code ${code ?? "null"}) without a valid response${detail}`,
  };
}

/**
 * Read the stage's options. A key it does not know is a warning; an invalid value is
 * an error, and every option is still checked so one run reports them all. Returns
 * `undefined` when any value is invalid.
 */
function readOptions(options: Record<string, unknown>, report: Report): EmitSettings | undefined {
  warnUnknownKeys(options, OPTION_KEYS, "option", report);

  const settings: EmitSettings = {};
  let valid = true;
  const reject = (key: string, expected: string): void => {
    report("error", "invalid-option", `'${key}' must be ${expected}, not ${describeValue(options[key])}.`);
    valid = false;
  };
  const present = (key: string): boolean => options[key] !== undefined && options[key] !== null;
  const isStringList = (value: unknown): value is string[] =>
    Array.isArray(value) && value.every((item) => typeof item === "string");

  const { warnAsError, nostdlib, trace, additionalImports, emitter } = options;
  if (present("warnAsError")) {
    if (typeof warnAsError === "boolean") settings.warnAsError = warnAsError;
    else reject("warnAsError", "a boolean");
  }
  if (present("nostdlib")) {
    if (typeof nostdlib === "boolean") settings.nostdlib = nostdlib;
    else reject("nostdlib", "a boolean");
  }
  if (present("trace")) {
    if (isStringList(trace)) settings.trace = trace;
    else reject("trace", "a list of strings");
  }
  if (present("additionalImports")) {
    if (isStringList(additionalImports)) settings.additionalImports = additionalImports;
    else reject("additionalImports", "a list of strings");
  }
  if (present("emitter")) {
    if (isMap(emitter)) settings.emitterOptions = emitter;
    else reject("emitter", "a map of emitter options");
  }

  return valid ? settings : undefined;
}
