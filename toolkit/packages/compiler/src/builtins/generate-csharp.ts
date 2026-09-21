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

import type { IR } from "@azure-client-engine/ir";
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
  options: Record<string, unknown> = {},
): Generator {
  return {
    name: CSHARP_GENERATOR_ID,
    async generate(ir: IR, context: GeneratorContext): Promise<void> {
      // The generator consumes a *transformed* document — commands and a resolved
      // target, not bare operations. Validating here turns "the command stage never
      // ran" or "the projection is malformed" into a diagnostic at the boundary,
      // instead of a null dereference inside the out-of-process host.
      const document = safeParseTransformedIR(ir);
      if (!document.success) {
        context.reportDiagnostic(invalidInput(document.error.issues));
        return;
      }

      const rootNamespace =
        typeof options.rootNamespace === "string" ? options.rootNamespace : undefined;
      const request: GenerateToolRequest = {
        ir: document.data,
        options: {
          ...(rootNamespace !== undefined ? { rootNamespace } : {}),
          config: context.config,
        },
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

/** Diagnostic for a document that is not a valid transformed IR. */
function invalidInput(issues: readonly { path: (string | number)[]; message: string }[]): CompilerDiagnostic {
  const detail = issues
    .slice(0, 3)
    .map((issue) => `${issue.path.join(".") || "<root>"}: ${issue.message}`)
    .join("; ");
  const more = issues.length > 3 ? ` (+${issues.length - 3} more)` : "";
  return {
    severity: "error",
    code: "@azure-client-engine/compiler/invalid-generator-input",
    message:
      "The csharp generator requires a transformed IR (commands and a resolved target). " +
      `The document it received is not one — ${detail}${more}`,
  };
}
