/**
 * Host runtime: the CLI's implementation of the compiler's `Runtime` seam.
 *
 * The compiler owns the pipeline catalog and, for each external stage, *what*
 * tool to run and the JSON protocol it speaks; it reaches those tools only
 * through this one impure seam. The host's job — everything the backbone stays
 * free of — is to resolve a logical tool id to a real command and execute it:
 *
 *   - `tsp-emit`        -> resolve the TypeSpec toolchain + fork the compile worker.
 *   - `csharp-generate` -> resolve `dotnet` + the realm's generator host dll and spawn it.
 *
 * Injected via `CompileOptions.runtime`, so a CLI and a web service can back the
 * same built-in stages with different effect implementations.
 */

import {
  CSHARP_GENERATE_TOOL,
  TSP_EMIT_TOOL,
  type EmitToolRequest,
  type Runtime,
  type RuntimeContext,
  type ToolInvocation,
  type ToolResult,
  type ToolStreamResult,
} from "@azure-client-engine/compiler";

import { runTspEmit } from "./tsp-compile.js";
import { runCsharpGenerateStreaming } from "./dotnet-generate.js";

/** Build the CLI host runtime backing the compiler's built-in external stages. */
export function createHostRuntime(): Runtime {
  return {
    // Buffered one-shot: used by the built-in emit stage (a single JSON reply).
    async spawn({ tool, input }: ToolInvocation, context: RuntimeContext): Promise<ToolResult> {
      const signal = context.signal;
      switch (tool) {
        case TSP_EMIT_TOOL: {
          const request = JSON.parse(input) as EmitToolRequest;
          const response = await runTspEmit(request, signal);
          // The compiler's built-in emit stage parses this JSON reply.
          return { stdout: JSON.stringify(response), stderr: "", code: 0 };
        }
        default:
          return { stdout: "", stderr: `tool '${tool}' does not support buffered spawn`, code: 1 };
      }
    },

    // Streamed: used by the built-in csharp generator so its (potentially large)
    // NDJSON output is delivered line by line and never buffered.
    async spawnStreaming(
      { tool, input }: ToolInvocation,
      context: RuntimeContext,
      onLine: (line: string) => void | Promise<void>,
    ): Promise<ToolStreamResult> {
      const signal = context.signal;
      switch (tool) {
        case CSHARP_GENERATE_TOOL:
          return runCsharpGenerateStreaming(input, onLine, signal);
        default:
          return { stderr: `tool '${tool}' does not support streaming spawn`, code: 1 };
      }
    },
  };
}
