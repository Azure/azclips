/**
 * Host runtime: the CLI's implementation of the compiler's `Runtime` seam.
 *
 * The compiler owns the pipeline catalog and, for each external stage, *what*
 * tool to run and the JSON protocol it speaks; it reaches those tools only
 * through this one impure seam. The host decides what runs for each logical tool id
 * and where it is, and leaves running it to `subprocess.ts`:
 *
 *   - `tsp-emit`        -> the emitter package's `tsp-emit` worker, driving the
 *                          TypeSpec toolchain resolved for the entrypoint.
 *   - `csharp-generate` -> `dotnet`, running the generator host dll staged next to
 *                          the CLI.
 *
 * Injected via `CompileOptions.runtime`, so a CLI and a web service can back the
 * same built-in stages with different effect implementations.
 */

import {
  CSHARP_GENERATE_TOOL,
  TSP_EMIT_TOOL,
  type EmitToolRequest,
  type EmitToolResponse,
  type Runtime,
  type RuntimeContext,
  type ToolInvocation,
  type ToolResult,
  type ToolStreamResult,
} from "@azure-client-engine/compiler";
import type {
  TspEmitReply,
  TspEmitWorkerRequest,
} from "@azure-client-engine/typespec-emitter/tsp-emit-protocol";

import { resolveGeneratorHost } from "./generator-host.js";
import { runWorker, spawnStream, timeoutFromEnv } from "./subprocess.js";
import { resolveToolchain, resolveTspEmitWorker } from "./toolchain.js";

/**
 * Hang guard for each tool, overridable through its environment variable for
 * pathological specs. Generous, because large specs legitimately take a while.
 */
const DEFAULT_TIMEOUT_MS = 10 * 60_000;

/** Build the CLI host runtime backing the compiler's built-in external stages. */
export function createHostRuntime(): Runtime {
  return {
    // Buffered one-shot: used by the built-in emit stage (a single JSON reply).
    async spawn({ tool, input }: ToolInvocation, context: RuntimeContext): Promise<ToolResult> {
      switch (tool) {
        case TSP_EMIT_TOOL: {
          // The request goes to the worker untouched; its entrypoint is where the
          // TypeSpec toolchain is looked up.
          const request = JSON.parse(input) as EmitToolRequest;
          const toolchain = resolveToolchain(request.entrypoint);
          const reply: EmitToolResponse = await runWorker<TspEmitWorkerRequest, TspEmitReply>(
            resolveTspEmitWorker(),
            { compilerPath: toolchain.compilerPath, emitterPath: toolchain.emitterPath, request },
            {
              label: "tsp-emit worker",
              timeoutMs: timeoutFromEnv("ACE_EMIT_TIMEOUT_MS", DEFAULT_TIMEOUT_MS),
              signal: context.signal,
            },
          );
          // The compiler's built-in emit stage parses this JSON reply.
          return { stdout: JSON.stringify(reply), stderr: "", code: 0 };
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
      switch (tool) {
        case CSHARP_GENERATE_TOOL:
          return spawnStream("dotnet", [resolveGeneratorHost()], input, onLine, {
            label: "generator host",
            timeoutMs: timeoutFromEnv("ACE_GENERATE_TIMEOUT_MS", DEFAULT_TIMEOUT_MS),
            signal: context.signal,
          });
        default:
          return { stderr: `tool '${tool}' does not support streaming spawn`, code: 1 };
      }
    },
  };
}