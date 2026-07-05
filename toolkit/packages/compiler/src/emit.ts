/**
 * Stage 1 + 2 of the backbone: compile TypeSpec and run the Azure Client Engine emitter,
 * capturing the emitted IR in memory (no disk writes).
 *
 * This module is the *single seam* coupling the backbone to the emitter. We
 * drive `@typespec/compiler`'s `compile()` with the emitter registered as an
 * emitter and a capture host, then read the emitted `ir.json` back and
 * validate it with `parseIR`. If the emitter later exposes a pure
 * `buildIR(program)`, only this file changes — the orchestrator, transforms
 * and generator stay put.
 */

import {
  NodeHost,
  compile as compileProgram,
  type CompilerHost,
  type Diagnostic,
} from "@typespec/compiler";
import { parseIR, type IR } from "@azure-client-engine/ir";

import { createCaptureHost } from "./host.js";

/** Registered name of the Azure Client Engine emitter (matches its `$lib.name`). */
export const EMITTER_NAME = "@azure-client-engine/typespec-emitter";

/** The emitter is asked to produce JSON for a lossless in-memory round-trip. */
const IR_OUTPUT_FILE = "ir.json";

export interface EmitIRInput {
  /** Local path to the TypeSpec project directory or an entry `.tsp` file. */
  entrypoint: string;
  /** Base host to wrap; defaults to NodeHost. */
  base?: CompilerHost;
  /** Options forwarded to the Azure Client Engine emitter (validated by its own schema). */
  emitterOptions?: Record<string, unknown>;
  /** Extra imports layered on top of the entrypoint's own imports. */
  additionalImports?: string[];
  /** Promote warning-severity TypeSpec diagnostics to errors. */
  warnAsError?: boolean;
  /** TypeSpec trace areas to enable. */
  trace?: string[];
  /** Skip loading the TypeSpec standard library. */
  nostdlib?: boolean;
}

export interface EmitIRResult {
  /** Parsed, schema-valid IR. Undefined if the emitter produced none. */
  ir?: IR;
  /** Raw TypeSpec diagnostics from the compile + emit. */
  diagnostics: readonly Diagnostic[];
  /** Everything the emitter "wrote" (debugging / future multi-file emit). */
  outputs: Map<string, string>;
}

export async function emitIR(input: EmitIRInput): Promise<EmitIRResult> {
  const { host, outputs } = createCaptureHost(input.base ?? NodeHost);

  const program = await compileProgram(host, input.entrypoint, {
    emit: [EMITTER_NAME],
    options: {
      // App-provided emitter options first; the serialization keys below are
      // backbone-owned (we capture IR in memory) and intentionally win.
      [EMITTER_NAME]: {
        ...(input.emitterOptions ?? {}),
        format: "json",
        outputFile: IR_OUTPUT_FILE,
        pretty: false,
      },
    },
    ...(input.additionalImports !== undefined
      ? { additionalImports: input.additionalImports }
      : {}),
    ...(input.warnAsError !== undefined ? { warnAsError: input.warnAsError } : {}),
    ...(input.trace !== undefined ? { trace: input.trace } : {}),
    ...(input.nostdlib !== undefined ? { nostdlib: input.nostdlib } : {}),
  });

  const diagnostics = program.diagnostics;

  const emitted = [...outputs.entries()].find(([path]) => path.endsWith(IR_OUTPUT_FILE));
  if (emitted === undefined) {
    return { diagnostics, outputs };
  }

  try {
    const ir = parseIR(JSON.parse(emitted[1]));
    return { ir, diagnostics, outputs };
  } catch {
    // The emitter wrote something that is not valid IR. It reports its own
    // `ir-validation-failed` diagnostic in that case; we surface no IR.
    return { diagnostics, outputs };
  }
}
