/**
 * Generate stage: IR -> output files.
 *
 * The backbone runs exactly one `Generator` (defaulting to a no-op). The
 * future Roslyn-based C# back end implements this interface and is injected
 * via `CompileOptions.generator`; the backbone itself stays host-agnostic and
 * never assumes what a generator produces.
 */

import type { IR } from "@azure-client-engine/ir";
import type { CompileConfig, CompilerDiagnostic } from "./types.js";

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
}

export interface GeneratorOutput {
  /** Generated files keyed by path relative to the output root. */
  files: Map<string, string>;
}

export interface Generator {
  readonly name: string;
  generate(ir: IR, context: GeneratorContext): GeneratorOutput | Promise<GeneratorOutput>;
}

/** Default generator: emits nothing. */
export const noopGenerator: Generator = {
  name: "noop",
  generate: () => ({ files: new Map<string, string>() }),
};
