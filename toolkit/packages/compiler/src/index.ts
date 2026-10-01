/**
 * @azure-client-engine/compiler — orchestration backbone.
 *
 * Pure, host-agnostic, TypeSpec-free library that owns the compilation workflow.
 *
 * Orchestration is **compiler-owned**: `STAGE_CATALOG` declares what stages
 * exist, in what order they run, and what runs by default. The resolved plan is
 * a single **linear sequence** — one IR flows from emit through every enabled
 * transform, and each generate stage renders it as it stands at that point. A
 * host's only orchestration verb is flipping an optional stage on or off through
 * `CompileConfig.pipeline`; it supplies *implementations* for declared ids
 * through `CompileOptions.registry`.
 *
 * Design contract (so a CLI *and* a web service can both consume it):
 *   - Inputs are data / an entrypoint, never assumed to be on disk.
 *   - Outputs go to an injected sink (or are returned), never written directly.
 *   - Diagnostics are structured data, never printed.
 *   - No process side effects (no process.exit, no console.*).
 *   - Cancellable via AbortSignal; per-run state stays per-run.
 */

export { compile } from "./compile.js";

export {
  STAGE_CATALOG,
  TSP_EMIT_STAGE,
  TWEAK_IR_STAGE,
  FLATTEN_MODEL_STAGE,
  CREATE_COMMAND_STAGE,
  PS_DERIVE_STAGE,
  PS_DIRECTIVES_STAGE,
  CSHARP_STAGE,
  planStages,
  isStageEnabled,
  type StageDescriptor,
  type StageKind,
  type StageDefault,
  type PlannedStage,
  type StagePlan,
} from "./stages.js";

export type {
  DiagnosticSeverity,
  CompilerDiagnostic,
  CompileConfig,
  CompileInput,
  CompileOptions,
  CompileResult,
} from "./types.js";

export type {
  IRSnapshot,
  EmitArtifact,
  OutputDelegate,
} from "./output.js";

export {
  createRegistry,
  isValidStageId,
  type CompilerRegistry,
  type EmitProducerFactory,
  type TransformStepFactory,
  type GeneratorFactory,
} from "./registry.js";

export {
  type TransformStep,
  type TransformContext,
} from "./transform.js";

export type {
  EmitProducer,
  EmitContext,
  EmitProduct,
} from "./emit-producer.js";

export {
  TSP_EMIT_TOOL,
  CSHARP_GENERATE_TOOL,
  type Runtime,
  type ToolInvocation,
  type ToolResult,
  type ToolStreamResult,
  type RuntimeContext,
} from "./runtime.js";

export {
  createBuiltinEmit,
} from "./builtins/emit.js";

export {
  createCsharpGenerator,
} from "./builtins/generate-csharp.js";

export type {
  EmitToolRequest,
  EmitToolResponse,
  GenerateToolOptions,
  GenerateToolRequest,
  GenerateToolFrame,
} from "./builtins/protocol.js";

export {
  noopGenerator,
  type Generator,
  type GeneratorAcceptance,
  type GeneratorInputIssue,
  type GeneratorContext,
  type GeneratedArtifact,
} from "./generate.js";
