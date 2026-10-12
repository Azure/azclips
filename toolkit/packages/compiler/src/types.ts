/**
 * Public, fully-serializable data types for the Azure Client Engine compiler backbone.
 *
 * These describe the contract between a front end (@azure-client-engine/cli, a future web
 * app) and the pipeline. Keeping them free of behaviour means a queue or
 * sandbox can sit between the two sides without either end changing.
 */

import type { IR } from "@azure-client-engine/ir";
import type { EmitProducer } from "./emit-producer.js";
import type { Runtime } from "./runtime.js";
import type { IRSnapshot, OutputDelegate } from "./output.js";
import type { StageDescriptor } from "./stages.js";

/** Severity levels mirror the TypeSpec compiler's diagnostic model. */
export type DiagnosticSeverity = "error" | "warning";

/** A structured diagnostic. Front ends decide how to surface these. */
export interface CompilerDiagnostic {
  severity: DiagnosticSeverity;
  /** Stable code, e.g. "@azure-client-engine/typespec-emitter/streaming-body-dropped". */
  code: string;
  message: string;
  /** Optional source location for editor integration. */
  target?: {
    file?: string;
    /** 1-based line number. */
    line?: number;
    /** 1-based column number. */
    column?: number;
  };
}

/**
 * One stage's section under `stages`. Read by the compiler, which checks its shape:
 * the values arrive from a file, so the types below are what a valid section
 * holds, not a guarantee. `disabled` is the compiler's switch; `options` is the
 * stage config.
 */
export interface StageSection {
  /**
   * Skip the stage. The only switch: every catalog stage runs unless disabled, and
   * a mandatory one cannot be.
   */
  disabled?: boolean;
  /** The stage config, handed to the stage untouched; ignored while it is disabled. */
  options?: Record<string, unknown>;
}

/**
 * Resolved gen-time configuration, delivered by the app once it has taken its own
 * keys (the run inputs) out of the file.
 *
 * The compiler takes `stages`, its only setting, and passes every other key on
 * without interpreting it. The backbone NEVER reads `tspconfig.yaml` or any other
 * file from disk.
 */
export interface CompileConfig {
  /**
   * Per-stage sections, keyed by the compiler's stage id. Orchestration — which
   * stages exist, in what order they run — is owned by the compiler's catalog, so a
   * section can only disable its stage or configure it; it cannot append, insert, or
   * reorder.
   *
   * Naming an unknown stage, disabling a mandatory one, or disabling a stage that an
   * enabled stage `requires` is an error rather than a silent no-op.
   */
  stages?: Record<string, StageSection>;
  /**
   * Every other key is gen-time config the compiler does not interpret: the
   * document facts, which the compiler hands untouched to the `seed-ir` stage, keyed
   * as written in config (kebab-case, e.g. `"root-namespace"`). `seed-ir` checks
   * them and warns about any key it does not know.
   */
  [fact: string]: unknown;
}

/**
 * Compilation input: app-resolved *data* describing what to compile. The
 * entrypoint is always a local path — resolving the source (clone, download,
 * merge, ...) is the app's responsibility.
 */
export interface CompileInput {
  /** Local path to the TypeSpec project directory or an entry `.tsp` file. */
  entrypoint: string;
  /**
   * API version to compile the entrypoint at: a specific version (e.g.
   * `"2025-09-01"`), `"latest"` (default), or `"all"`. Threaded to the emitter as
   * the `api-version` option, which TCGC uses to project the `@versioned` program
   * to that version before the IR is built. A run input, not a setting: it travels
   * with its entrypoint.
   */
  apiVersion?: string;
  /**
   * Resolved gen-time config (see `CompileConfig`). Each stage receives only its
   * own stage config: `stages.<id>.options`, or for `seed-ir` every key that is not
   * `stages`.
   */
  config?: CompileConfig;
}

/** Knobs for a single compile run. */
export interface CompileOptions {
  /** Treat warning-severity diagnostics as failures. */
  diagnosticsAsErrors?: boolean;
  /** Cooperative cancellation (timeouts, web request abort). */
  signal?: AbortSignal;
  /**
   * Host runtime backing the compiler's built-in **external** stages (the
   * `tsp-emit` tool and the `csharp` generator). The backbone owns *which* tool
   * runs and its protocol; the runtime resolves the logical tool id to a command
   * and executes it. Required for the built-in emit (unless `emit` is injected)
   * and for the built-in `csharp` generator.
   */
  runtime?: Runtime;
  /**
   * Source -> IR emit stage. **Escape hatch**: normally emit is the compiler's
   * built-in `tsp-emit` tool run through `runtime`. Inject a producer to bypass
   * that (tests, bespoke embedding). With neither `emit` nor `runtime`,
   * `compile()` fails with a `no-emit-producer` diagnostic.
   */
  emit?: EmitProducer;
  /**
   * Stage catalog to plan against. **Escape hatch** for embedders and tests —
   * orchestration is compiler-owned, so production callers leave this unset and
   * get `STAGE_CATALOG`. Array order is execution order.
   */
  catalog?: readonly StageDescriptor[];
  /**
   * Capture the IR after emit (index 0) and after each transform stage as
   * `IRSnapshot`s. Snapshots flow to `output`; with the default collector they
   * also appear on `CompileResult.snapshots`.
   */
  captureIR?: boolean;
  /**
   * Host output sink for every generated file and (when `captureIR`) every IR
   * snapshot. Defaults to an in-memory collector that fills
   * `CompileResult.files` / `.snapshots`. A custom sink takes ownership, leaving
   * those result buffers empty.
   */
  output?: OutputDelegate;
}

/**
 * Result of a compile run. Fully serializable so a queue/sandbox can sit
 * between a front end and this backbone without changing either side.
 */
export interface CompileResult {
  /** True when no error-severity diagnostics were produced. */
  success: boolean;
  /**
   * The run's IR. A run has exactly one, so this is its final state — after
   * every enabled transform. Undefined only if emit itself failed.
   */
  ir?: IR;
  /** Generated output files, keyed by relative path. */
  files: Map<string, string>;
  /**
   * Per-step IR snapshots (emit + each transform step), present when `captureIR`
   * is set and the default collector sink is used. Empty/omitted otherwise.
   */
  snapshots?: IRSnapshot[];
  /** All diagnostics collected across every stage. */
  diagnostics: CompilerDiagnostic[];
}
