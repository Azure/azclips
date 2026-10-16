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
import type { CompilerDiagnostic } from "./types.js";

/** One artifact a generator streams to the host: a file to write, or a scope to wipe. */
export type GeneratedArtifact =
  | { kind: "file"; path: string; contents: string }
  | { kind: "remove"; path: string };

/** Per-run services handed to a generator. */
export interface GeneratorContext {
  /** Record a diagnostic without throwing; collected into the final result. */
  reportDiagnostic(diagnostic: CompilerDiagnostic): void;
  /**
   * The generator's own stage config, as written: its `stages.<id>.options` map.
   * The compiler routes it by stage id and checks nothing; the generator checks it.
   */
  readonly config: Readonly<Record<string, unknown>>;
  /** Cooperative cancellation shared with the enclosing compile run. */
  readonly signal?: AbortSignal;
  /**
   * Stream one artifact to the host the moment it is produced — a generated file
   * or a scope removal (cleanup). Awaited before producing the next, so a slow
   * sink applies backpressure and nothing is buffered. A generator that cleans
   * emits its `remove` artifacts before its files, so stale output is wiped first.
   */
  emit(artifact: GeneratedArtifact): Promise<void>;
}

/**
 * What a generator will accept, and the outcome of checking a document against it.
 *
 * Deliberately the shape `safeParse` already returns, so a Zod schema *is* an
 * acceptance check with no adapter in between.
 */
export type GeneratorAcceptance<TIn extends IR> =
  | { readonly success: true; readonly data: TIn }
  | { readonly success: false; readonly error: { readonly issues: readonly GeneratorInputIssue[] } };

/** One reason a document was rejected, in the shape Zod reports it. */
export interface GeneratorInputIssue {
  readonly path: readonly (string | number)[];
  readonly message: string;
}

/**
 * A generate stage.
 *
 * `TIn` is what this generator actually renders. The parameter exists because the
 * pipeline carries one `IR` and a generate stage may sit anywhere in the sequence,
 * so the orchestrator cannot know statically that an earlier transform produced the
 * richer document a given backend needs.
 *
 * A generator that needs more than a bare `IR` says so with `accepts` rather than
 * re-checking inside {@link Generator.generate}. That keeps the signature honest — a
 * backend declaring `IR` and then rejecting most of them is a lie the type system
 * cannot catch — and lets the orchestrator produce one uniform diagnostic instead of
 * each backend hand-rolling its own.
 *
 * `accepts` is **required exactly when `TIn` is narrower than `IR`**, so a backend
 * cannot claim a richer input without supplying the check that establishes it. When
 * a bare `IR` is enough it stays optional, and the orchestrator passes the document
 * through untouched.
 */
export type Generator<TIn extends IR = IR> = {
  readonly name: string;
  /**
   * Generate from `ir`, streaming each artifact via `context.emit`. Only called with
   * a document `accepts` approved, so this may rely on `TIn` in full.
   */
  generate(ir: TIn, context: GeneratorContext): void | Promise<void>;
} & (IR extends TIn
  ? {
      /** Optional here: `TIn` is `IR`, so every document already qualifies. */
      readonly accepts?: (ir: IR) => GeneratorAcceptance<TIn>;
    }
  : {
      /** Narrow the pipeline's document to `TIn`, or explain why it cannot. */
      readonly accepts: (ir: IR) => GeneratorAcceptance<TIn>;
    });
