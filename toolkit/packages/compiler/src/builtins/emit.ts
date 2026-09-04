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

import type { EmitContext, EmitProducer, EmitProduct } from "../emit-producer.js";
import type { CompileInput, CompilerDiagnostic } from "../types.js";
import { TSP_EMIT_TOOL, type Runtime } from "../runtime.js";
import type { EmitToolRequest, EmitToolResponse } from "./protocol.js";

/** Build the emit producer backed by the host runtime's `tsp-emit` tool. */
export function createBuiltinEmit(runtime: Runtime): EmitProducer {
  return {
    name: TSP_EMIT_TOOL,
    async produce(input: CompileInput, context: EmitContext): Promise<EmitProduct> {
      const config = input.config ?? {};
      const request: EmitToolRequest = {
        entrypoint: input.entrypoint,
        ...(config.emitter !== undefined ? { emitterOptions: config.emitter } : {}),
        ...(config.apiVersion !== undefined ? { apiVersion: config.apiVersion } : {}),
        ...(config.additionalImports !== undefined
          ? { additionalImports: config.additionalImports }
          : {}),
        ...(config.warnAsError !== undefined ? { warnAsError: config.warnAsError } : {}),
        ...(config.trace !== undefined ? { trace: config.trace } : {}),
        ...(config.nostdlib !== undefined ? { nostdlib: config.nostdlib } : {}),
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
