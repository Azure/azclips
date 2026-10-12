/**
 * Wire protocol for the compiler's built-in **external** stages.
 *
 * The compiler owns these shapes (it owns the catalog and how each tool is
 * driven); the host runtime only moves the bytes, and each out-of-process tool
 * implements the matching end by hand:
 *
 *   - `tsp-emit`  — stdin `EmitToolRequest`  -> stdout `EmitToolResponse` (one JSON object).
 *   - `csharp-generate` — stdin `GenerateToolRequest` -> stdout a stream of
 *      newline-delimited `GenerateToolFrame`s (NDJSON: one `file` per line, then
 *      a final `result`).
 *
 * These are the *spec-level* fields only. Host-resolved concerns (the resolved
 * `@typespec/compiler` / emitter paths for emit, the `dotnet` command + host
 * dll path for generate) are injected by the runtime when it launches the tool,
 * never carried here — that is exactly the resolution the host owns.
 */

import type { IR } from "@azure-client-engine/ir";
import type { CompilerDiagnostic } from "../types.js";

/** stdin payload for the `tsp-emit` tool. */
export interface EmitToolRequest {
  /** Staged entrypoint (project dir or `.tsp` file) to compile. */
  entrypoint: string;
  /** Options forwarded verbatim to the Azure Client Engine emitter. */
  emitterOptions?: Record<string, unknown>;
  /** API version to compile at (a specific version, `"latest"`, or `"all"`). */
  apiVersion?: string;
  /** Extra imports layered on top of the entrypoint's own imports. */
  additionalImports?: string[];
  /** Promote warning-severity TypeSpec diagnostics to errors. */
  warnAsError?: boolean;
  /** TypeSpec trace areas to enable. */
  trace?: string[];
  /** Skip loading the TypeSpec standard library. */
  nostdlib?: boolean;
}

/** stdout reply from the `tsp-emit` tool (a single JSON object). */
export interface EmitToolResponse {
  /** Parsed IR the emitter produced, or undefined if none was emitted. */
  ir?: unknown;
  /** Structured diagnostics from the compile + emit (already translated). */
  diagnostics: CompilerDiagnostic[];
}

/**
 * Host-resolved options for the `csharp-generate` tool. Placement (root namespace,
 * project name, folders) is not among them: it travels in the document's `target`.
 */
export interface GenerateToolOptions {
  /** The `csharp` stage's own options (`stages.csharp.options`), opaque to the generator for now. */
  config?: unknown;
}

/** stdin payload for the `csharp-generate` tool. */
export interface GenerateToolRequest {
  /** The IR document to generate from. */
  ir: IR;
  /** Host-resolved generation options. */
  options: GenerateToolOptions;
}

/** One newline-delimited frame emitted by the `csharp-generate` tool on stdout. */
export type GenerateToolFrame =
  | {
      kind: "file";
      /** Output path relative to the output root (`/` separators). */
      path: string;
      /** The generated file's full text. */
      contents: string;
    }
  | {
      kind: "remove";
      /** Output scope (relative dir or file) to wipe before writing, `/` separators. */
      path: string;
    }
  | {
      kind: "result";
      /** True when the generator reported no error-severity diagnostics. */
      success: boolean;
      /** All diagnostics collected during generation. */
      diagnostics: CompilerDiagnostic[];
    };
