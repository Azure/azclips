/**
 * The backbone orchestrator: compile -> emit IR -> transform -> generate.
 *
 * Host-agnostic and free of direct runtime coupling (no disk writes, no
 * `console.*`, no `process.exit`). Artifacts leave through an injected output
 * sink (`CompileOptions.output`); with no sink they are collected and returned.
 * Diagnostics are structured data. Cancellable via `AbortSignal`, checked at
 * every stage boundary.
 *
 * The pipeline is configurable, not hard-coded: transform steps and the
 * generator are resolved from `CompileConfig.pipeline` / `.generator` against a
 * `CompilerRegistry`, or overridden directly via `CompileOptions`. The backbone
 * owns the step loop, so with `captureIR` it emits a labeled `IRSnapshot` after
 * emit and after each transform step.
 */

import type { IR } from "@azure-client-engine/ir";

import { emitIR } from "./emit.js";
import { toCompilerDiagnostic } from "./diagnostics.js";
import {
  createTransformPipeline,
  identityPipeline,
  type TransformContext,
  type TransformPipeline,
  type TransformStep,
} from "./transform.js";
import { noopGenerator, type Generator, type GeneratorContext } from "./generate.js";
import { createCollectorSink, type OutputDelegate } from "./output.js";
import { isAbortError, throwIfAborted } from "./abort.js";
import type {
  CompileConfig,
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
  const captureIR = options.captureIR === true;

  // Output sink: a caller-provided delegate, or the default in-memory collector
  // that fills the result buffers. A custom sink takes ownership of artifacts.
  const collector = options.output ? undefined : createCollectorSink();
  const sink: OutputDelegate = options.output ?? collector!.write;

  const hasError = (): boolean => diagnostics.some((d) => d.severity === "error");
  const finish = (success: boolean, ir?: IR): CompileResult => ({
    success,
    ...(ir !== undefined ? { ir } : {}),
    files: collector?.files ?? new Map<string, string>(),
    ...(collector && captureIR ? { snapshots: collector.snapshots } : {}),
    diagnostics,
  });

  // Unique, filename-safe snapshot labels: "emit" is taken first, and a step
  // name reused within a run is de-duplicated ("flatten", then "flatten-2").
  const label = createLabeler();
  const captureSnapshot = async (index: number, name: string, ir: IR): Promise<boolean> => {
    if (!captureIR) return true;
    try {
      // Deep-clone at capture so a later mutating step can't corrupt this snapshot.
      await sink({ kind: "ir-snapshot", index, step: label(name), ir: structuredClone(ir) });
      return true;
    } catch (error) {
      if (isAbortError(error)) throw error;
      reportDiagnostic(outputError("ir-snapshot", name, error));
      return false;
    }
  };

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

  if (emitted.ir === undefined || hasError()) {
    return finish(false);
  }

  // Snapshot 0: the IR straight from the emitter.
  throwIfAborted(signal);
  if (!(await captureSnapshot(0, "emit", emitted.ir))) return finish(false);

  // ── Stage 3: transform (identity unless configured/injected) ────────────
  const pipeline = resolvePipeline(options, config, reportDiagnostic);
  if (pipeline === undefined) return finish(false);

  const transformContext: TransformContext = {
    reportDiagnostic,
    extensions,
    ...(signal !== undefined ? { signal } : {}),
  };
  let ir: IR = emitted.ir;
  let index = 1;
  for (const step of pipeline.steps) {
    throwIfAborted(signal);
    try {
      ir = await step.run(ir, transformContext);
    } catch (error) {
      if (isAbortError(error)) throw error;
      reportDiagnostic(stageError("transform", step.name, error));
      return finish(false, ir);
    }
    if (!(await captureSnapshot(index, step.name, ir))) return finish(false, ir);
    index++;
  }

  // ── Stage 4: generate (no-op unless configured/injected) ────────────────
  throwIfAborted(signal);
  const generator = resolveGenerator(options, config, reportDiagnostic);
  if (generator === undefined) return finish(false, ir);

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
    return finish(false, ir);
  }

  // Forward generated files to the sink (in-memory collector or host).
  for (const [path, contents] of files) {
    try {
      await sink({ kind: "generated-file", path, contents });
    } catch (error) {
      if (isAbortError(error)) throw error;
      reportDiagnostic(outputError("generated-file", path, error));
      return finish(false, ir);
    }
  }

  const failed =
    hasError() ||
    (options.diagnosticsAsErrors === true &&
      diagnostics.some((d) => d.severity === "warning"));

  return finish(!failed, ir);
}

/** Resolve the transform pipeline: explicit override, then config+registry, then identity. */
function resolvePipeline(
  options: CompileOptions,
  config: CompileConfig,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): TransformPipeline | undefined {
  if (options.transform !== undefined) return options.transform;

  const specs = config.pipeline;
  if (specs === undefined || specs.length === 0) return identityPipeline;

  const steps: TransformStep[] = [];
  for (const spec of specs) {
    const factory = options.registry?.transforms.get(spec.use);
    if (factory === undefined) {
      reportDiagnostic({
        severity: "error",
        code: "@azure-client-engine/compiler/unknown-transform-step",
        message: `No transform step registered for id '${spec.use}'.`,
      });
      return undefined;
    }
    steps.push(factory(spec.options ?? {}));
  }
  return createTransformPipeline("configured", steps);
}

/** Resolve the generator: explicit override, then config+registry, then no-op. */
function resolveGenerator(
  options: CompileOptions,
  config: CompileConfig,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): Generator | undefined {
  if (options.generator !== undefined) return options.generator;

  const spec = config.generator;
  if (spec === undefined) return noopGenerator;

  const factory = options.registry?.generators.get(spec.use);
  if (factory === undefined) {
    reportDiagnostic({
      severity: "error",
      code: "@azure-client-engine/compiler/unknown-generator",
      message: `No generator registered for id '${spec.use}'.`,
    });
    return undefined;
  }
  return factory(spec.options ?? {});
}

/** Build a labeler that returns unique names ("x", then "x-2", "x-3", ...). */
function createLabeler(): (name: string) => string {
  const counts = new Map<string, number>();
  return (name) => {
    const seen = counts.get(name) ?? 0;
    counts.set(name, seen + 1);
    return seen === 0 ? name : `${name}-${seen + 1}`;
  };
}

function stageError(stage: string, name: string, error: unknown): CompilerDiagnostic {
  const detail = error instanceof Error ? error.message : String(error);
  return {
    severity: "error",
    code: `@azure-client-engine/compiler/${stage}-stage-failed`,
    message: `${stage} step '${name}' threw: ${detail}`,
  };
}

function outputError(kind: string, name: string, error: unknown): CompilerDiagnostic {
  const detail = error instanceof Error ? error.message : String(error);
  return {
    severity: "error",
    code: "@azure-client-engine/compiler/output-write-failed",
    message: `writing ${kind} '${name}' failed: ${detail}`,
  };
}
