/**
 * @cirrus/compiler — orchestration backbone.
 *
 * Pure, host-agnostic library that sequences the cirrus compilation
 * pipeline end to end:
 *
 *   1. compile     TypeSpec source -> typespec-compiler program (in process)
 *   2. emit        program -> raw IR (via @cirrus/typespec-emitter)
 *   3. transform   IR -> IR (a series of passes)
 *   4. generate    IR -> C# source (Roslyn-based back end)
 *
 * Design contract (so a CLI *and* a web service can both consume it):
 *   - Inputs are data / a CompilerHost, never assumed to be on disk.
 *   - Outputs are RETURNED (files as a map, IR as an object), never written.
 *   - Diagnostics are structured data, never printed.
 *   - No process side effects (no process.exit, no console.*).
 *   - Cancellable via AbortSignal; per-run state stays per-run.
 *
 * This module is currently a skeleton: the type surface is defined and the
 * stages are stubbed so front ends (@cirrus/cli, future web app) can build
 * against a stable API while the pipeline is filled in.
 */

import type { IR } from "@cirrus/ir";

/** Severity levels mirror the TypeSpec compiler's diagnostic model. */
export type DiagnosticSeverity = "error" | "warning";

/** A structured diagnostic. Front ends decide how to surface these. */
export interface CompilerDiagnostic {
  severity: DiagnosticSeverity;
  /** Stable code, e.g. "@cirrus/typespec-emitter/streaming-body-dropped". */
  code: string;
  message: string;
  /** Optional source location for editor integration. */
  target?: {
    file?: string;
    line?: number;
    column?: number;
  };
}

/** One TypeSpec source file provided in memory. */
export interface SourceFile {
  /** Virtual or real path; used for diagnostics and import resolution. */
  path: string;
  content: string;
}

/**
 * Compilation input. Either in-memory sources (the web case) or an
 * entrypoint path resolved against the ambient filesystem (the CLI case).
 */
export interface CompileInput {
  /** Entry .tsp file path. */
  entrypoint: string;
  /**
   * Optional in-memory sources. When provided, the compiler resolves
   * imports from here instead of (or in addition to) disk.
   */
  sources?: SourceFile[];
}

/** Knobs for a single compile run. */
export interface CompileOptions {
  /** Override the IRSource.id / module string for spec-declared types. */
  sourceId?: string;
  /** Treat emitter warnings as failures. */
  diagnosticsAsErrors?: boolean;
  /** Cooperative cancellation (timeouts, web request abort). */
  signal?: AbortSignal;
}

/**
 * Result of a compile run. Fully serializable so a queue/sandbox can sit
 * between a front end and this backbone without changing either side.
 */
export interface CompileResult {
  /** True when no error-severity diagnostics were produced. */
  success: boolean;
  /** The IR after all transformer passes. Undefined if compile failed early. */
  ir?: IR;
  /** Generated output files, keyed by relative path. */
  files: Map<string, string>;
  /** All diagnostics collected across every stage. */
  diagnostics: CompilerDiagnostic[];
}

/**
 * Run the full pipeline: compile -> emit IR -> transform -> generate C#.
 *
 * NOTE: skeleton. Throws until the stages are wired up. The signature is
 * the stable contract front ends should target.
 */
export async function compile(
  input: CompileInput,
  options: CompileOptions = {},
): Promise<CompileResult> {
  void input;
  void options;
  throw new Error("@cirrus/compiler: compile() is not implemented yet.");
}
