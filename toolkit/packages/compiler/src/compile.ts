/**
 * The backbone orchestrator: run the compiler's stage sequence.
 *
 * Orchestration is compiler-owned. `STAGE_CATALOG` declares what stages exist,
 * in what order they run, and what runs by default; `CompileConfig.pipeline`
 * only flips optional stages on or off. The planner resolves that into a **flat
 * sequence**, and this module walks it start to finish.
 *
 * There is exactly **one IR per run**. Transforms hand it along — returning it
 * mutated, or returning a replacement — and each generate stage renders the IR
 * as it stands when its turn comes. Nothing here copies the IR, and nothing runs
 * concurrently, so a stage's view of the IR is precisely "everything enabled
 * before me, and nothing after".
 *
 * Stages run to the first failure. In a sequence every later stage would consume
 * the broken IR, so continuing could only produce more noise.
 *
 * Host-agnostic and free of direct runtime coupling (no disk writes, no
 * `console.*`, no `process.exit`). Artifacts leave through an injected output
 * sink; diagnostics are structured data; cancellation is checked at every stage
 * boundary.
 */

import type { IR } from "@azure-client-engine/ir";

import type { EmitContext, EmitProducer } from "./emit-producer.js";
import type { TransformContext, TransformStep } from "./transform.js";
import type { Generator, GeneratorContext } from "./generate.js";
import { createBuiltinEmit } from "./builtins/emit.js";
import { createCsharpGenerator } from "./builtins/generate-csharp.js";
import { createCollectorSink, type OutputDelegate } from "./output.js";
import { isAbortError, throwIfAborted } from "./abort.js";
import {
  CSHARP_STAGE,
  TSP_EMIT_STAGE,
  planStages,
  type PlannedStage,
  type StageDescriptor,
} from "./stages.js";
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

  // ── Plan: catalog defaults ⊕ config toggles → one flat sequence ─────────
  const plan = planStages(config.pipeline ?? {}, reportDiagnostic, options.catalog);
  if (plan === undefined) return finish(false);

  /** Paths already written, with the stage that wrote them. */
  const writtenBy = new Map<string, string>();

  const captureSnapshot = async (stage: PlannedStage, ir: IR): Promise<boolean> => {
    if (!captureIR) return true;
    try {
      // The IR is handed over live, and later stages keep writing to it. A sink
      // that serializes immediately (before its first `await`) needs no copy,
      // which is why capture is free on the filesystem path; a sink that
      // *retains* the object must copy it, or every snapshot it keeps will end
      // up showing the run's final state.
      await sink({ kind: "ir-snapshot", index: stage.index, step: stage.descriptor.id, ir });
      return true;
    } catch (error) {
      if (isAbortError(error)) throw error;
      reportDiagnostic(outputError("ir-snapshot", stage.descriptor.id, error));
      return false;
    }
  };

  // ── Emit: the sequence's first stage, and the run's only IR source ──────
  const emitProducer = resolveEmit(plan.emit.descriptor, options, config, reportDiagnostic);
  if (emitProducer === undefined) return finish(false);

  const emitContext: EmitContext = {
    reportDiagnostic,
    ...(signal !== undefined ? { signal } : {}),
  };
  let emitted: IR | undefined;
  try {
    ({ ir: emitted } = await emitProducer.produce(input, emitContext));
  } catch (error) {
    if (isAbortError(error)) throw error;
    reportDiagnostic(stageError("emit", emitProducer.name, error));
    return finish(false);
  }
  if (emitted === undefined || hasError()) return finish(false);

  // The emitted object is the run's only IR. Release the local the destructuring
  // bound it to, so a transform that returns a *new* object leaves the old one
  // collectable rather than pinned here for the rest of the run.
  let ir: IR = emitted;
  emitted = undefined;

  if (!(await captureSnapshot(plan.emit, ir))) return finish(false, ir);

  const transformContext: TransformContext = {
    reportDiagnostic,
    config,
    ...(signal !== undefined ? { signal } : {}),
  };

  /** Run one transform stage. Returns the IR to carry on with, or `undefined` on failure. */
  const runTransform = async (stage: PlannedStage, current: IR): Promise<IR | undefined> => {
    const id = stage.descriptor.id;
    const step = resolveTransform(stage.descriptor, options, config, reportDiagnostic);
    if (step === undefined) return undefined;
    try {
      return await step.run(current, transformContext);
    } catch (error) {
      if (isAbortError(error)) throw error;
      reportDiagnostic(stageError("transform", id, error));
      return undefined;
    }
  };

  /** Run one generate stage, streaming its artifacts straight to the sink. */
  const runGenerator = async (stage: PlannedStage, current: IR): Promise<boolean> => {
    const id = stage.descriptor.id;
    const generator = resolveGenerator(stage.descriptor, options, config, reportDiagnostic);
    if (generator === undefined) return false;

    let outputFailed = false;
    const generatorContext: GeneratorContext = {
      reportDiagnostic,
      config,
      ...(signal !== undefined ? { signal } : {}),
      emit: async (artifact) => {
        if (outputFailed) return;
        // Two generators writing the same path would silently clobber each other,
        // so a cross-stage collision is an error rather than last-writer-wins.
        const owner = writtenBy.get(artifact.path);
        if (owner !== undefined && owner !== id) {
          outputFailed = true;
          reportDiagnostic({
            severity: "error",
            code: "@azure-client-engine/compiler/output-path-collision",
            message:
              `Stages '${owner}' and '${id}' both write '${artifact.path}'. ` +
              "Scope one stage's output so generators cannot overwrite each other.",
          });
          return;
        }
        writtenBy.set(artifact.path, id);
        try {
          if (artifact.kind === "file") {
            await sink({
              kind: "generated-file",
              stage: id,
              path: artifact.path,
              contents: artifact.contents,
            });
          } else {
            await sink({ kind: "remove", stage: id, path: artifact.path });
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
      // Awaited in place, so the generator sees the IR exactly as it stands at
      // its position. Handing a live IR to something that runs concurrently
      // would let a later transform change what it renders.
      await generator.generate(current, generatorContext);
    } catch (error) {
      if (isAbortError(error)) throw error;
      reportDiagnostic(stageError("generate", id, error));
      return false;
    }
    return !outputFailed;
  };

  // ── Walk the rest of the sequence ──────────────────────────────────────
  for (const stage of plan.stages.slice(1)) {
    throwIfAborted(signal);

    if (stage.descriptor.kind === "generate") {
      if (!(await runGenerator(stage, ir))) return finish(false, ir);
      continue;
    }

    const next = await runTransform(stage, ir);
    if (next === undefined) return finish(false, ir);
    ir = next;
    if (!(await captureSnapshot(stage, ir))) return finish(false, ir);
  }

  const diagnosticsFailed =
    hasError() ||
    (options.diagnosticsAsErrors === true && diagnostics.some((d) => d.severity === "warning"));

  return finish(!diagnosticsFailed, ir);
}

/** Resolve the root emit stage: explicit override, registry, then the built-in. */
function resolveEmit(
  descriptor: StageDescriptor,
  options: CompileOptions,
  config: CompileConfig,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): EmitProducer | undefined {
  if (options.emit !== undefined) return options.emit;

  const factory = options.registry?.emitters.get(descriptor.id);
  if (factory !== undefined) return factory(stageOptions(config, descriptor.id));

  if (descriptor.id === TSP_EMIT_STAGE) {
    if (options.runtime === undefined) {
      reportDiagnostic({
        severity: "error",
        code: "@azure-client-engine/compiler/no-emit-producer",
        message:
          `The '${TSP_EMIT_STAGE}' stage runs out of process and needs a host runtime ` +
          "(CompileOptions.runtime), or an injected producer (CompileOptions.emit).",
      });
      return undefined;
    }
    return createBuiltinEmit(options.runtime);
  }

  reportDiagnostic(unimplemented(descriptor.id, "emit"));
  return undefined;
}

/** Resolve a transform stage. Transforms have no built-ins; the host must fulfil them. */
function resolveTransform(
  descriptor: StageDescriptor,
  options: CompileOptions,
  config: CompileConfig,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): TransformStep | undefined {
  const factory = options.registry?.transforms.get(descriptor.id);
  if (factory === undefined) {
    reportDiagnostic(unimplemented(descriptor.id, "transform"));
    return undefined;
  }
  return factory(stageOptions(config, descriptor.id));
}

/** Resolve a generate stage: a registry entry, else the compiler's built-in `csharp`. */
function resolveGenerator(
  descriptor: StageDescriptor,
  options: CompileOptions,
  config: CompileConfig,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): Generator | undefined {
  const resolved = stageOptions(config, descriptor.id);

  const factory = options.registry?.generators.get(descriptor.id);
  if (factory !== undefined) return factory(resolved);

  if (descriptor.id === CSHARP_STAGE) {
    if (options.runtime === undefined) {
      reportDiagnostic({
        severity: "error",
        code: "@azure-client-engine/compiler/generator-requires-runtime",
        message:
          `The '${CSHARP_STAGE}' stage runs out of process and needs a host runtime ` +
          "(CompileOptions.runtime).",
      });
      return undefined;
    }
    return createCsharpGenerator(options.runtime, resolved);
  }

  reportDiagnostic(unimplemented(descriptor.id, "generate"));
  return undefined;
}

function stageOptions(config: CompileConfig, id: string): Record<string, unknown> {
  return config.stageOptions?.[id] ?? {};
}

function unimplemented(id: string, kind: string): CompilerDiagnostic {
  return {
    severity: "error",
    code: "@azure-client-engine/compiler/unimplemented-stage",
    message:
      `The '${id}' ${kind} stage is enabled but no implementation was supplied. ` +
      "Register one through CompileOptions.registry, or disable the stage in config.pipeline.",
  };
}

function stageError(stage: string, name: string, error: unknown): CompilerDiagnostic {
  const detail = error instanceof Error ? error.message : String(error);
  return {
    severity: "error",
    code: `@azure-client-engine/compiler/${stage}-stage-failed`,
    message: `${stage} stage '${name}' threw: ${detail}`,
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
