/**
 * Subprocess-backed emit producer.
 *
 * Wraps the `tsp-compile` worker as an `EmitProducer` the orchestrator can inject
 * via `CompileOptions.emit`. Given a `ResolvedToolchain` (from `resolveToolchain`),
 * it forks the worker, sends a `TspCompileRequest`, and turns the reply back into
 * an `EmitProduct` (reporting diagnostics through the emit context). All TypeSpec
 * work happens in the child, so ace's process never loads `@typespec/compiler`.
 */

import { fork } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import type {
  CompileInput,
  EmitContext,
  EmitProducer,
  EmitProduct,
} from "@azure-client-engine/compiler";

import type { ResolvedToolchain } from "./toolchain.js";
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
 * Build an `EmitProducer` that runs tsp compile out of process against
 * `toolchain`. Inject the result as `CompileOptions.emit`.
 */
export function createSubprocessEmitProducer(toolchain: ResolvedToolchain): EmitProducer {
  return {
    name: `tsp-compile(${toolchain.source})`,
    async produce(input: CompileInput, context: EmitContext): Promise<EmitProduct> {
      const config = input.config ?? {};
      const request: TspCompileRequest = {
        compilerPath: toolchain.compilerPath,
        emitterPath: toolchain.emitterPath,
        entrypoint: input.entrypoint,
        ...(config.emitter !== undefined ? { emitterOptions: config.emitter } : {}),
        ...(config.apiVersion !== undefined ? { apiVersion: config.apiVersion } : {}),
        ...(config.additionalImports !== undefined
          ? { additionalImports: config.additionalImports }
          : {}),
        ...(config.warnAsError !== undefined ? { warnAsError: config.warnAsError } : {}),
        ...(config.trace !== undefined ? { trace: config.trace } : {}),
        ...(config.nostdlib !== undefined ? { nostdlib: config.nostdlib } : {}),
      };

      const response = await invokeWorker(request, context.signal);
      for (const diagnostic of response.diagnostics) context.reportDiagnostic(diagnostic);
      if (response.ir === undefined) return {};
      return { ir: response.ir as NonNullable<EmitProduct["ir"]> };
    },
  };
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
      if (signal !== undefined) signal.removeEventListener("abort", onAbort);
      fn();
    };
    const onAbort = (): void => {
      child.kill();
      finish(() => reject(new Error("aborted")));
    };
    signal?.addEventListener("abort", onAbort, { once: true });

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
