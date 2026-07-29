/**
 * @azure-client-engine/compiler — orchestration backbone.
 *
 * Pure, host-agnostic, TypeSpec-free library that sequences the Azure Client
 * Engine compilation pipeline end to end:
 *
 *   1. emit        source -> raw IR (an injected producer; the host supplies it,
 *                  e.g. the CLI's out-of-process tsp-compile — there is no
 *                  built-in default, so the backbone carries no TypeSpec deps)
 *   2. transform   IR -> IR (an injectable pipeline; identity by default)
 *   3. generate    IR -> output files (an injectable back end; no-op by default)
 *
 * Design contract (so a CLI *and* a web service can both consume it):
 *   - Inputs are data / an entrypoint, never assumed to be on disk.
 *   - Outputs are RETURNED (files as a map, IR as an object), never written.
 *   - Diagnostics are structured data, never printed.
 *   - No process side effects (no process.exit, no console.*).
 *   - Cancellable via AbortSignal; per-run state stays per-run.
 */

export { compile } from "./compile.js";

export type {
  DiagnosticSeverity,
  CompilerDiagnostic,
  CompileConfig,
  CompileInput,
  CompileOptions,
  CompileResult,
  PipelineStepConfig,
  GeneratorConfig,
} from "./types.js";

export type {
  IRSnapshot,
  EmitArtifact,
  OutputDelegate,
} from "./output.js";

export {
  createRegistry,
  isValidStepId,
  type CompilerRegistry,
  type TransformStepFactory,
  type GeneratorFactory,
} from "./registry.js";

export {
  createTransformPipeline,
  identityPipeline,
  type TransformPipeline,
  type TransformStep,
  type TransformContext,
} from "./transform.js";

export type {
  EmitProducer,
  EmitContext,
  EmitProduct,
} from "./emit-producer.js";

export {
  noopGenerator,
  type Generator,
  type GeneratorContext,
  type GeneratorOutput,
} from "./generate.js";
