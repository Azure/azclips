/**
 * `tsp-emit` tool host: run TypeSpec compile + emit out of process.
 *
 * Backs the compiler's built-in emit stage. The compiler owns *that* emit is an
 * external subprocess speaking the `tsp-emit` protocol and drives it through
 * `runtime.spawn`; everything host/spec-specific lives here:
 *   - resolving which `@typespec/compiler` + emitter to load — the hybrid,
 *     project-local-preferred toolchain (see `resolveToolchain`), the documented
 *     exception to "tools live under the host realm"; and
 *   - forking the worker that actually loads them.
 *
 * The boundary stays data-only (an `EmitToolRequest` in, an `EmitToolResponse`
 * out), so ace's own process never loads `@typespec/compiler`.
 */

import { fork } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import type { EmitToolRequest, EmitToolResponse } from "@azure-client-engine/compiler";

import { resolveToolchain } from "./toolchain.js";
import type { TspCompileRequest, TspCompileResponse } from "./tsp-compile-protocol.js";

/**
 * Absolute path to the compiled worker. In production this module is
 * `dist/src/tsp-compile.js` and the worker is its sibling. Under vitest it runs
 * from `src/tsp-compile.ts`, but Node cannot `fork` a `.ts` file, so redirect to
 * the built worker under `dist` (the workflow builds before running tests).
 */
function resolveWorkerPath(): string {
  const self = fileURLToPath(import.meta.url);
  if (self.endsWith(".ts")) {
    const packageRoot = dirname(dirname(self)); // .../packages/cli/src -> .../packages/cli
    return join(packageRoot, "dist", "src", "tsp-compile-worker.js");
  }
  return fileURLToPath(new URL("./tsp-compile-worker.js", import.meta.url));
}

const WORKER_PATH = resolveWorkerPath();

/**
 * Hang guard for the emit worker. A wedged TypeSpec compile would otherwise
 * hang the whole CLI *and* keep handles on the staged spec (blocking cleanup),
 * so we kill it past this bound. Generous by default (emit of large specs is
 * legitimately slow); override with `ACE_EMIT_TIMEOUT_MS` for pathological specs.
 */
const DEFAULT_EMIT_TIMEOUT_MS = 10 * 60_000;

function emitTimeoutMs(): number {
  const raw = process.env.ACE_EMIT_TIMEOUT_MS;
  const parsed = raw !== undefined ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_EMIT_TIMEOUT_MS;
}

/**
 * Run the built-in `tsp-emit` tool: resolve the toolchain for the request's
 * entrypoint (hybrid — the spec's own install preferred, ace's bundled toolchain
 * as fallback), then compile + emit out of process and return the IR + structured
 * diagnostics. The compiler's built-in emit stage frames this over
 * `runtime.spawn`; resolution + the worker fork are the host's responsibility.
 */
export async function runTspEmit(
  request: EmitToolRequest,
  signal?: AbortSignal,
): Promise<EmitToolResponse> {
  const toolchain = resolveToolchain(request.entrypoint);
  const workerRequest: TspCompileRequest = {
    compilerPath: toolchain.compilerPath,
    emitterPath: toolchain.emitterPath,
    entrypoint: request.entrypoint,
    ...(request.emitterOptions !== undefined ? { emitterOptions: request.emitterOptions } : {}),
    ...(request.apiVersion !== undefined ? { apiVersion: request.apiVersion } : {}),
    ...(request.additionalImports !== undefined
      ? { additionalImports: request.additionalImports }
      : {}),
    ...(request.warnAsError !== undefined ? { warnAsError: request.warnAsError } : {}),
    ...(request.trace !== undefined ? { trace: request.trace } : {}),
    ...(request.nostdlib !== undefined ? { nostdlib: request.nostdlib } : {}),
  };
  return invokeWorker(workerRequest, signal);
}

/** Fork the worker, send the request over IPC, and resolve with its reply. */
function invokeWorker(
  request: TspCompileRequest,
  signal?: AbortSignal,
): Promise<TspCompileResponse> {
  return new Promise<TspCompileResponse>((resolve, reject) => {
    if (signal?.aborted) {
      reject(new Error("aborted"));
      return;
    }

    const child = fork(WORKER_PATH, [], { silent: true });
    const stderr: string[] = [];
    child.stderr?.on("data", (chunk: Buffer | string) => stderr.push(String(chunk)));

    let settled = false;
    const finish = (fn: () => void): void => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      if (signal !== undefined) signal.removeEventListener("abort", onAbort);
      fn();
    };
    const onAbort = (): void => {
      child.kill();
      finish(() => reject(new Error("aborted")));
    };
    signal?.addEventListener("abort", onAbort, { once: true });

    // Hang guard: kill a wedged worker so it can't hang the CLI or hold the
    // staged spec open forever.
    const timeoutMs = emitTimeoutMs();
    const timer = setTimeout(() => {
      child.kill();
      finish(() =>
        reject(new Error(`tsp-compile worker timed out after ${timeoutMs}ms and was terminated.`)),
      );
    }, timeoutMs);

    child.on("message", (message: TspCompileResponse) => finish(() => resolve(message)));
    child.on("error", (err) => finish(() => reject(err)));
    child.on("exit", (code) =>
      finish(() =>
        reject(
          new Error(
            `tsp-compile worker exited (code ${code ?? "null"}) without a result.\n${stderr.join("")}`,
          ),
        ),
      ),
    );

    child.send(request);
  });
}
