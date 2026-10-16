/**
 * The backbone orchestrator: run the compiler's stage sequence.
 *
 * Orchestration is compiler-owned. `STAGE_CATALOG` declares what stages exist,
 * in what order they run, and what runs by default; `CompileConfig.stages` can
 * only disable optional stages. The planner resolves that into a **flat
 * sequence**, and this module walks it start to finish.
 *
 * There is exactly **one IR per run**. Transforms hand it along — returning it
 * mutated, or returning a replacement — and each generate stage renders the IR
 * as it stands when its turn comes. Nothing here copies the IR, and nothing runs
 * concurrently, so a stage's view of the IR is precisely "everything enabled
 * before me, and nothing after".
 *
 * Stages run to the first failure. An emit or transform stage fails when it throws
 * or reports an error: an error is the stage's own verdict that its output cannot be
 * trusted. In a sequence every later stage would consume the broken IR, so
 * continuing could only produce more noise.
 *
 * Host-agnostic and free of direct runtime coupling (no disk writes, no
 * `console.*`, no `process.exit`). Artifacts leave through an injected output
 * sink; diagnostics are structured data; cancellation is checked at every stage
 * boundary.
 */

import type { IR } from "@azure-client-engine/ir";
import type { TransformContext, TransformStep } from "@azure-client-engine/transformers";

import type { EmitContext, EmitProducer } from "./emit-producer.js";
import type { Generator, GeneratorContext, GeneratorInputIssue } from "./generate.js";
import { createBuiltinEmit } from "./builtins/emit.js";
import { createCsharpGenerator } from "./builtins/generate-csharp.js";
import { createCollectorSink, type OutputDelegate } from "./output.js";
import { isAbortError, throwIfAborted } from "./abort.js";
import {
  CSHARP_STAGE,
  SEED_IR_STAGE,
  TSP_EMIT_STAGE,
  planStages,
  readStages,
  type PlannedStage,
  type StageDescriptor,
} from "./stages.js";
import { TRANSFORMS } from "./transforms.js";
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

  // ── Read `stages`, the compiler's own setting ────────────────────────────
  const sections = readStages(config.stages, reportDiagnostic);
  if (sections === undefined) return finish(false);

  /** A stage's config, untouched: its section's `options`, or the facts for `seed-ir`. */
  const configOf = (id: string): Record<string, unknown> =>
    id === SEED_IR_STAGE ? documentFacts(config) : (sections.options.get(id) ?? {});

  // ── Plan: catalog defaults ⊕ config toggles → one flat sequence ─────────
  const plan = planStages(sections.toggles, reportDiagnostic, options.catalog);
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
  const emitProducer = resolveEmit(plan.emit.descriptor, options, reportDiagnostic);
  if (emitProducer === undefined) return finish(false);

  const emitContext: EmitContext = {
    reportDiagnostic,
    config: configOf(plan.emit.descriptor.id),
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

  /**
   * Run one transform stage. Returns the IR to carry on with, or `undefined` when the
   * stage failed: it threw, or it reported an error.
   */
  const runTransform = async (stage: PlannedStage, current: IR): Promise<IR | undefined> => {
    const id = stage.descriptor.id;
    const step = resolveTransform(stage.descriptor, reportDiagnostic);
    if (step === undefined) return undefined;
    const context: TransformContext = {
      reportDiagnostic,
      config: configOf(id),
      ...(signal !== undefined ? { signal } : {}),
    };
    const reportedBefore = diagnostics.length;
    try {
      const next = await step.run(current, context);
      // The step reports every problem it finds and returns; one error among them
      // fails the stage, as a failed emit does.
      const failed = diagnostics.slice(reportedBefore).some((d) => d.severity === "error");
      return failed ? undefined : next;
    } catch (error) {
      if (isAbortError(error)) throw error;
      reportDiagnostic(stageError("transform", id, error));
      return undefined;
    }
  };

  /** Run one generate stage, streaming its artifacts straight to the sink. */
  const runGenerator = async (stage: PlannedStage, current: IR): Promise<boolean> => {
    const id = stage.descriptor.id;
    const generator = resolveGenerator(stage.descriptor, options, reportDiagnostic);
    if (generator === undefined) return false;

    // A generator declares what it renders; the orchestrator checks it. The pipeline
    // carries one `IR` and a generate stage may sit anywhere, so this is the only
    // point where "the document this backend needs" can actually be established.
    // Doing it here rather than inside each generator keeps the failure uniform and
    // keeps a backend's signature honest about its input.
    const accepted = generator.accepts?.(current) ?? { success: true as const, data: current };
    if (!accepted.success) {
      reportDiagnostic(unacceptableInput(id, generator.name, accepted.error.issues));
      return false;
    }

    let outputFailed = false;
    const generatorContext: GeneratorContext = {
      reportDiagnostic,
      config: configOf(id),
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
      await generator.generate(accepted.data, generatorContext);
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

/** Resolve the root emit stage: an injected producer, else the built-in. */
function resolveEmit(
  descriptor: StageDescriptor,
  options: CompileOptions,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): EmitProducer | undefined {
  if (options.emit !== undefined) return options.emit;

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

/** Resolve a transform stage from the compiler's own implementations. */
function resolveTransform(
  descriptor: StageDescriptor,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): TransformStep | undefined {
  const step = TRANSFORMS.get(descriptor.id);
  if (step === undefined) reportDiagnostic(unimplemented(descriptor.id, "transform"));
  return step;
}

/** Resolve a generate stage: the compiler's built-in `csharp`. */
function resolveGenerator(
  descriptor: StageDescriptor,
  options: CompileOptions,
  reportDiagnostic: (d: CompilerDiagnostic) => void,
): Generator | undefined {
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
    return createCsharpGenerator(options.runtime);
  }

  reportDiagnostic(unimplemented(descriptor.id, "generate"));
  return undefined;
}

/** The keys a type declares, without the ones its index signature admits. */
type DeclaredKeys<T> = keyof {
  [K in keyof T as string extends K ? never : number extends K ? never : K]: T[K];
};

/**
 * The compiler's own settings: the `CompileConfig` keys it reads itself. Every other
 * key is gen-time config for the stages. `satisfies` keeps this in step with
 * `CompileConfig`, so a setting declared there but not listed here fails the build.
 */
const COMPILER_SETTINGS = new Set(
  Object.keys({
    stages: true,
  } satisfies Record<DeclaredKeys<CompileConfig>, true>),
);

/** The document facts: every config key that is not one of the compiler's settings. */
function documentFacts(config: CompileConfig): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(config).filter(([key]) => !COMPILER_SETTINGS.has(key)),
  );
}

function unimplemented(id: string, kind: string): CompilerDiagnostic {
  return {
    severity: "error",
    code: "@azure-client-engine/compiler/unimplemented-stage",
    message:
      `The '${id}' ${kind} stage is enabled, but the compiler has no implementation for it. ` +
      "Disable it with 'disabled: true' in its section under 'stages'.",
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

/**
 * A document the generate stage's backend will not render.
 *
 * Reported by the orchestrator rather than the generator so every backend fails the
 * same way, and so the message can name the stage whose position produced the
 * document — which is what a reader needs to know to fix it (usually an earlier
 * transform that did not run).
 */
function unacceptableInput(
  stage: string,
  name: string,
  issues: readonly GeneratorInputIssue[],
): CompilerDiagnostic {
  const detail = issues
    .slice(0, 3)
    .map((issue) => `${issue.path.join(".") || "<root>"}: ${issue.message}`)
    .join("; ");
  const more = issues.length > 3 ? ` (+${issues.length - 3} more)` : "";
  return {
    severity: "error",
    code: "@azure-client-engine/compiler/invalid-generator-input",
    message:
      `Generator '${name}' (stage '${stage}') cannot render the document it was given ` +
      `— ${detail}${more}`,
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
