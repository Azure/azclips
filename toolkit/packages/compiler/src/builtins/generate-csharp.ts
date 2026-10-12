/**
 * Built-in C# generator stage: IR -> C# files, run out of process.
 *
 * The compiler owns *that* the `csharp` generator is an external subprocess
 * speaking the `csharp-generate` protocol; it drives the tool through the host
 * `Runtime`. Resolving the `dotnet` command and the realm-relative generator
 * host dll, and spawning it, live behind `runtime.spawn` (the host's job).
 *
 * A thin adapter onto the existing `Generator` seam: it serializes the request,
 * runs the tool, and folds the NDJSON reply (a `file` frame per generated file,
 * then a `result` frame) into the `files` map + diagnostics the orchestrator
 * already knows how to stream and surface.
 */

import type { TransformedIR } from "@azure-client-engine/ir";
import { safeParseTransformedIR } from "@azure-client-engine/ir";
import type { Generator, GeneratorContext } from "../generate.js";
import type { CompilerDiagnostic } from "../types.js";
import { CSHARP_GENERATE_TOOL, type Runtime } from "../runtime.js";
import type { GenerateToolFrame, GenerateToolRequest } from "./protocol.js";

/** Registered id of the compiler's built-in C# generator (see `CompileConfig.generator`). */
export const CSHARP_GENERATOR_ID = "csharp";

/** Build the C# generator backed by the host runtime's `csharp-generate` tool. */
export function createCsharpGenerator(
  runtime: Runtime,
  _options: Record<string, unknown> = {},
): Generator<TransformedIR> {
  return {
    name: CSHARP_GENERATOR_ID,
    // This backend renders commands and a resolved target, not bare operations, and
    // says so in its contract: the orchestrator checks the document before calling
    // `generate`, so "the command stage never ran" or "the projection is malformed"
    // becomes a diagnostic at the boundary instead of a null dereference inside the
    // out-of-process host it is about to be serialized to.
    accepts: safeParseTransformedIR,
    async generate(ir: TransformedIR, context: GeneratorContext): Promise<void> {
      // Placement travels in the document (`ir.target`), so the request carries no
      // namespace of its own.
      const request: GenerateToolRequest = {
        ir,
        options: { config: context.config },
      };

      // Stream the tool's NDJSON stdout line by line, forwarding each artifact to
      // the host as it arrives (removes precede files) — nothing is buffered.
      let sawResult = false;
      const result = await runtime.spawnStreaming(
        { tool: CSHARP_GENERATE_TOOL, input: JSON.stringify(request) },
        { ...(context.signal !== undefined ? { signal: context.signal } : {}) },
        async (line) => {
          if (line.trim().length === 0) return;
          let frame: GenerateToolFrame;
          try {
            frame = JSON.parse(line) as GenerateToolFrame;
          } catch {
            return; // ignore non-JSON noise on stdout
          }
          if (frame.kind === "file") {
            await context.emit({ kind: "file", path: frame.path, contents: frame.contents });
          } else if (frame.kind === "remove") {
            await context.emit({ kind: "remove", path: frame.path });
          } else if (frame.kind === "result") {
            sawResult = true;
            for (const diagnostic of frame.diagnostics ?? []) context.reportDiagnostic(diagnostic);
          }
        },
      );

      // No terminal result frame means the tool crashed mid-run; surface it.
      if (!sawResult) context.reportDiagnostic(toolFailure(result.code, result.stderr));
    },
  };
}

/** Diagnostic for a `csharp-generate` run that produced no terminal result frame. */
function toolFailure(code: number | null, stderr: string): CompilerDiagnostic {
  const detail = stderr.trim().length > 0 ? `: ${stderr.trim()}` : "";
  return {
    severity: "error",
    code: "@azure-client-engine/compiler/generate-tool-failed",
    message: `The csharp-generate tool exited (code ${code ?? "null"}) without a result${detail}`,
  };
}
