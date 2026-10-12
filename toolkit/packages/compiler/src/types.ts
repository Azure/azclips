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
 * Resolved compile configuration, delivered by the app.
 *
 * The backbone NEVER reads `tspconfig.yaml` from disk — resolving and merging
 * config (potentially from several tspconfig sources) is the app's job. An
 * Azure ARM tspconfig mostly targets other emitters and linting, none of
 * which Azure Client Engine consumes; the fields here are the small subset that actually
 * affects an Azure Client Engine compile.
 */
export interface CompileConfig {
  /**
   * API version to compile the spec at. A specific version string
   * (e.g. `"2025-09-01"`), `"latest"` (default), or `"all"`. Threaded to the
   * emitter as the `api-version` option, which TCGC uses to project the
   * `@versioned` program to that version before the IR is built.
   */
  apiVersion?: string;
  /** Extra files/libraries to import, on top of what the entrypoint imports. */
  additionalImports?: string[];
  /** Promote warning-severity TypeSpec diagnostics to errors. Default false. */
  warnAsError?: boolean;
  /** TypeSpec trace areas to enable. */
  trace?: string[];
  /** Skip loading the TypeSpec standard library. */
  nostdlib?: boolean;
  /**
   * Options forwarded verbatim to the Azure Client Engine emitter
   * (`@azure-client-engine/typespec-emitter`) — e.g. `{ sourceId: "..." }`. Keys must be
   * valid emitter options (validated at emit time), so the emitter can grow
   * new options without a backbone change. The serialization keys `format` /
   * `outputFile` / `pretty` are owned by the backbone (it captures IR in
   * memory) and are ignored if supplied here.
   */
  emitter?: Record<string, unknown>;
  /**
   * Stage enablement toggles, keyed by the compiler's stage id. Orchestration —
   * which stages exist, in what order they run, and what runs by default — is
   * owned by the compiler's catalog; a config's only verb is to flip an optional
   * stage on or off. It cannot append, insert, or reorder.
   *
   * Omitted ids keep their catalog default. Naming an unknown stage, disabling a
   * mandatory one, or disabling a stage that an enabled stage `requires` is an
   * error rather than a silent no-op.
   */
  pipeline?: Record<string, boolean>;
  /**
   * Per-stage options, keyed by stage id (e.g.
   * `{ csharp: { rootNamespace: "Contoso" } }`). Separate from `pipeline`
   * because options are not orchestration: they configure a stage that the
   * catalog already placed.
   */
  stageOptions?: Record<string, Record<string, unknown>>;
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
   * Resolved compile configuration (see `CompileConfig`). The emit stage reads
   * compiler/emitter fields, each transform receives its own `stageOptions` entry,
   * `seed-ir` receives every key that is not one of the compiler's own settings,
   * and generate steps read their fields off the same `config` (exposed read-only
   * on their context).
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
