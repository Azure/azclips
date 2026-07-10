/**
 * The backbone orchestrator: compile -> emit IR -> transform -> generate.
 *
 * Host-agnostic and side-effect free (no disk writes, no `console.*`, no
 * `process.exit`). Outputs are returned; diagnostics are structured data.
 * Cancellable via `AbortSignal`, checked at every stage boundary.
 *
 * Today the transform stage defaults to identity and the generate stage to a
 * no-op, so a caller gets "TypeSpec in, IR out". Both stages are real,
 * injectable extension points (`CompileOptions.transform` / `.generator`), so
 * wiring future passes and the C# generator is additive, not a rewrite.
 */

import type { IR } from "@azure-client-engine/ir";

import { emitIR } from "./emit.js";
import { toCompilerDiagnostic } from "./diagnostics.js";
import { identityPipeline, type TransformContext } from "./transform.js";
import { noopGenerator, type GeneratorContext } from "./generate.js";
import { isAbortError, throwIfAborted } from "./abort.js";
import type {
  CompileInput,
  CompileOptions,
  CompileResult,
  CompilerDiagnostic,
} from "./types.js";

export async function compile(
  input: CompileInput,
  options: CompileOptions = {},
): Promise<CompileResult> {
  const { signal } = options;
  throwIfAborted(signal);

  const diagnostics: CompilerDiagnostic[] = [];
  const reportDiagnostic = (diagnostic: CompilerDiagnostic): void => {
    diagnostics.push(diagnostic);
  };

  const config = input.config ?? {};
  const extensions = input.extensions ?? {};

  // ── Stage 1 + 2: compile TypeSpec and emit IR ──────────────────────────
  const emitted = await emitIR({
    entrypoint: input.entrypoint,
    ...(config.emitter !== undefined ? { emitterOptions: config.emitter } : {}),
    ...(config.additionalImports !== undefined
      ? { additionalImports: config.additionalImports }
      : {}),
    ...(config.warnAsError !== undefined ? { warnAsError: config.warnAsError } : {}),
    ...(config.trace !== undefined ? { trace: config.trace } : {}),
    ...(config.nostdlib !== undefined ? { nostdlib: config.nostdlib } : {}),
  });
  for (const d of emitted.diagnostics) diagnostics.push(toCompilerDiagnostic(d));

  if (emitted.ir === undefined || diagnostics.some((d) => d.severity === "error")) {
    return { success: false, files: new Map(), diagnostics };
  }

  // ── Stage 3: transform (identity unless a pipeline is injected) ─────────
  throwIfAborted(signal);
  const pipeline = options.transform ?? identityPipeline;
  const transformContext: TransformContext = {
    reportDiagnostic,
    extensions,
    ...(signal !== undefined ? { signal } : {}),
  };
  let ir: IR;
  try {
    ir = await pipeline.run(emitted.ir, transformContext);
  } catch (error) {
    if (isAbortError(error)) throw error;
    reportDiagnostic(stageError("transform", pipeline.name, error));
    return { success: false, files: new Map(), diagnostics };
  }

  // ── Stage 4: generate (no-op unless a generator is injected) ────────────
  throwIfAborted(signal);
  const generator = options.generator ?? noopGenerator;
  const generatorContext: GeneratorContext = {
    reportDiagnostic,
    extensions,
    ...(signal !== undefined ? { signal } : {}),
  };
  let files: Map<string, string>;
  try {
    ({ files } = await generator.generate(ir, generatorContext));
  } catch (error) {
    if (isAbortError(error)) throw error;
    reportDiagnostic(stageError("generate", generator.name, error));
    return { success: false, ir, files: new Map(), diagnostics };
  }

  const failed =
    diagnostics.some((d) => d.severity === "error") ||
    (options.diagnosticsAsErrors === true &&
      diagnostics.some((d) => d.severity === "warning"));

  return { success: !failed, ir, files, diagnostics };
}

function stageError(stage: string, name: string, error: unknown): CompilerDiagnostic {
  const detail = error instanceof Error ? error.message : String(error);
  return {
    severity: "error",
    code: `@azure-client-engine/compiler/${stage}-stage-failed`,
    message: `${stage} stage '${name}' threw: ${detail}`,
  };
}
