/**
 * Generate stage: IR -> output artifacts, streamed.
 *
 * The backbone runs exactly one `Generator`; the pipeline always ends in one
 * (default: the built-in `csharp`), since generated code is the product. A
 * generator *streams* each artifact — a file to write or a scope to wipe — to the host via
 * `context.emit` the moment it is produced, awaiting it before the next, so
 * nothing is buffered end to end. The backbone stays host-agnostic and never
 * assumes what a generator produces.
 */

import type { IR } from "@azure-client-engine/ir";
import type { CompileConfig, CompilerDiagnostic } from "./types.js";

/** One artifact a generator streams to the host: a file to write, or a scope to wipe. */
export type GeneratedArtifact =
  | { kind: "file"; path: string; contents: string }
  | { kind: "remove"; path: string };

/** Per-run services handed to a generator. */
export interface GeneratorContext {
  /** Record a diagnostic without throwing; collected into the final result. */
  reportDiagnostic(diagnostic: CompilerDiagnostic): void;
  /** Cooperative cancellation shared with the enclosing compile run. */
  readonly signal?: AbortSignal;
  /**
   * App-resolved compile configuration (`CompileInput.config`); `{}` if none.
   * The generator reads its own named fields off it (there is no free-form bag).
   */
  readonly config: CompileConfig;
  /**
   * Stream one artifact to the host the moment it is produced — a generated file
   * or a scope removal (cleanup). Awaited before producing the next, so a slow
   * sink applies backpressure and nothing is buffered. A generator that cleans
   * emits its `remove` artifacts before its files, so stale output is wiped first.
   */
  emit(artifact: GeneratedArtifact): Promise<void>;
}

export interface Generator {
  readonly name: string;
  /** Generate from `ir`, streaming each artifact via `context.emit`. */
  generate(ir: IR, context: GeneratorContext): void | Promise<void>;
}

/**
 * Escape-hatch no-op generator: emits nothing. No longer the pipeline default —
 * inject it via `CompileOptions.generator` for the rare case of a deliberate
 * no-output run (e.g. a compiler unit test exercising only emit/transform).
 */
export const noopGenerator: Generator = {
  name: "noop",
  generate: () => {},
};
