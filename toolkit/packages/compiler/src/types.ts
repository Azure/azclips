/**
 * Public, fully-serializable data types for the Azure Client Engine compiler backbone.
 *
 * These describe the contract between a front end (@azure-client-engine/cli, a future web
 * app) and the pipeline. Keeping them free of behaviour means a queue or
 * sandbox can sit between the two sides without either end changing.
 */

import type { IR } from "@azure-client-engine/ir";
import type { TransformPipeline } from "./transform.js";
import type { Generator } from "./generate.js";

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
}

/**
 * Compilation input: app-resolved *data* describing what to compile. The
 * entrypoint is always a local path — resolving the source (clone, download,
 * merge, ...) is the app's responsibility.
 */
export interface CompileInput {
  /** Local path to the TypeSpec project directory or an entry `.tsp` file. */
  entrypoint: string;
  /** Resolved compile configuration (see `CompileConfig`). */
  config?: CompileConfig;
  /**
   * Free-form, app-resolved settings threaded into the transform and generate
   * stages (and available for compile-time supplements to the source spec).
   * Deliberately a plain dictionary for now.
   */
  extensions?: Record<string, unknown>;
}

/** Knobs for a single compile run. */
export interface CompileOptions {
  /** Treat warning-severity diagnostics as failures. */
  diagnosticsAsErrors?: boolean;
  /** Cooperative cancellation (timeouts, web request abort). */
  signal?: AbortSignal;
  /**
   * IR -> IR transform stage. Defaults to an identity pipeline. Injecting a
   * pipeline lets a front end compose passes without forking the backbone.
   */
  transform?: TransformPipeline;
  /**
   * IR -> files back end (e.g. the future Roslyn C# generator). Defaults to a
   * no-op generator that emits nothing.
   */
  generator?: Generator;
}

/**
 * Result of a compile run. Fully serializable so a queue/sandbox can sit
 * between a front end and this backbone without changing either side.
 */
export interface CompileResult {
  /** True when no error-severity diagnostics were produced. */
  success: boolean;
  /** The IR after all transform passes. Undefined if compilation failed early. */
  ir?: IR;
  /** Generated output files, keyed by relative path. */
  files: Map<string, string>;
  /** All diagnostics collected across every stage. */
  diagnostics: CompilerDiagnostic[];
}
