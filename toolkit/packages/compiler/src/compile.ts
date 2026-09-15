/**
 * The backbone orchestrator: compile -> emit IR -> transform -> generate.
 *
 * Host-agnostic and free of direct runtime coupling (no disk writes, no
 * `console.*`, no `process.exit`). Artifacts leave through an injected output
 * sink (`CompileOptions.output`); with no sink they are collected and returned.
 * Diagnostics are structured data. Cancellable via `AbortSignal`, checked at
 * every stage boundary.
 *
 * The pipeline is configurable, not hard-coded: the emit producer, transform
 * steps, and the generator are all injectable seams. Emit has no built-in
 * default — the orchestrator is TypeSpec-free, so the host supplies a producer
 * (`CompileOptions.emit`, e.g. the CLI's out-of-process tsp-compile). Transforms
 * and the generator resolve from `CompileConfig.pipeline` / `.generator` against
 * a `CompilerRegistry`, or are overridden via `CompileOptions`. The backbone
 * owns the step loop, so with `captureIR` it emits a labeled `IRSnapshot` after
 * emit and after each transform step.
 */

import type { IR } from "@azure-client-engine/ir";

import type { EmitContext, EmitProducer } from "./emit-producer.js";
import {
  createTransformPipeline,
  identityPipeline,
  type TransformContext,
  type TransformPipeline,
  type TransformStep,
} from "./transform.js";
import type { Generator, GeneratorContext } from "./generate.js";
import { createBuiltinEmit } from "./builtins/emit.js";
import { createCsharpGenerator, CSHARP_GENERATOR_ID } from "./builtins/generate-csharp.js";
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

  // ── Stage 1 + 2: emit IR (built-in tsp-emit via the host runtime, or an
  //    injected producer as the escape hatch) ──
  const emitProducer = resolveEmit(options, reportDiagnostic);
  if (emitProducer === undefined) return finish(false);
  const emitContext: EmitContext = {
    reportDiagnostic,
    ...(signal !== undefined ? { signal } : {}),
  };
  let emittedIr: IR | undefined;
  try {
    ({ ir: emittedIr } = await emitProducer.produce(input, emitContext));
  } catch (error) {
    if (isAbortError(error)) throw error;
    reportDiagnostic(stageError("emit", emitProducer.name, error));
    return finish(false);
  }

  if (emittedIr === undefined || hasError()) {
    return finish(false);
  }

  // Snapshot 0: the IR straight from the emitter.
  throwIfAborted(signal);
  if (!(await captureSnapshot(0, "emit", emittedIr))) return finish(false);

  // ── Stage 3: transform (identity unless configured/injected) ────────────
  const pipeline = resolvePipeline(options, config, reportDiagnostic);
  if (pipeline === undefined) return finish(false);

  const transformContext: TransformContext = {
    reportDiagnostic,
    config,
    ...(signal !== undefined ? { signal } : {}),
  };
  let ir: IR = emittedIr;
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

  let outputFailed = false;
  const generatorContext: GeneratorContext = {
    reportDiagnostic,
    config,
    ...(signal !== undefined ? { signal } : {}),
    // The generator streams each artifact here the moment it is produced; we
    // forward it straight to the sink (nothing buffered). Removes arrive before
    // files, so stale scopes are wiped before fresh output lands.
    emit: async (artifact) => {
      if (outputFailed) return;
      try {
        if (artifact.kind === "file") {
          await sink({ kind: "generated-file", path: artifact.path, contents: artifact.contents });
        } else {
          await sink({ kind: "remove", path: artifact.path });
        }
      } catch (error) {
        if (isAbortError(error)) throw error;
        outputFailed = true;
        reportDiagnostic(
          outputError(artifact.kind === "file" ? "generated-file" : "remove", artifact.path, error),
        );
      }
    },
  };

  try {
    await generator.generate(ir, generatorContext);
  } catch (error) {
    if (isAbortError(error)) throw error;
    reportDiagnostic(stageError("generate", generator.name, error));
    return finish(false, ir);
  }
  if (outputFailed) return finish(false, ir);

  const failed =
    hasError() ||
    (options.diagnosticsAsErrors === true &&
      diagnostics.some((d) => d.severity === "warning"));

  return finish(!failed, ir);
}

/** Resolve the emit producer: explicit override, then the runtime-backed built-in. */
function resolveEmit(
  options: CompileOptions,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): EmitProducer | undefined {
  // Explicit injection wins — the escape hatch for tests and bespoke embedding.
  if (options.emit !== undefined) return options.emit;
  // Otherwise the compiler owns emit: its built-in tsp-emit tool, driven through
  // the host runtime (which resolves the toolchain and spawns the worker).
  if (options.runtime !== undefined) return createBuiltinEmit(options.runtime);
  reportDiagnostic({
    severity: "error",
    code: "@azure-client-engine/compiler/no-emit-producer",
    message:
      "No emit stage available: provide a host runtime (CompileOptions.runtime) so the " +
      "built-in tsp-emit tool can run, or inject an emit producer (CompileOptions.emit).",
  });
  return undefined;
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

/**
 * Resolve the generator: explicit override, else the config-selected generator,
 * else the compiler's built-in `csharp` default. The pipeline always ends in a
 * generator — generated code is the product — so there is no no-op default; a
 * caller that deliberately wants no output injects `noopGenerator` through
 * `CompileOptions.generator` (the escape hatch).
 */
function resolveGenerator(
  options: CompileOptions,
  config: CompileConfig,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): Generator | undefined {
  if (options.generator !== undefined) return options.generator;

  const spec = config.generator ?? { use: CSHARP_GENERATOR_ID };

  // Compiler-owned built-in catalog: the C# generator runs out of process
  // through the host runtime — the compiler owns the stage, the host owns the spawn.
  if (spec.use === CSHARP_GENERATOR_ID) {
    if (options.runtime === undefined) {
      reportDiagnostic({
        severity: "error",
        code: "@azure-client-engine/compiler/generator-requires-runtime",
        message:
          `The '${CSHARP_GENERATOR_ID}' generator runs out of process and needs a ` +
          "host runtime (CompileOptions.runtime).",
      });
      return undefined;
    }
    return createCsharpGenerator(options.runtime, spec.options ?? {});
  }

  // Host-registered custom (in-process) generators — the registry extension seam.
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
