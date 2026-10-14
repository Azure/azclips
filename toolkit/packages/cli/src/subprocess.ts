/**
 * Running a tool in a child process, generic over what runs.
 *
 * The host runtime decides what runs for each tool and where it is (`runtime.ts`);
 * these helpers only run it. They deliver the input, collect the output, and stop the
 * child on cancellation or past a hang guard, so a wedged tool can neither hang the
 * run nor hold the staged spec open. Cancellation rejects with an `AbortError`, which
 * the compiler tells apart from a failing tool.
 */

import { fork, spawn, type Serializable } from "node:child_process";
import { createInterface } from "node:readline";

import type { ToolStreamResult } from "@azure-client-engine/compiler";

/** How to run one child process. */
export interface SubprocessOptions {
  /** Names the child in errors, e.g. `tsp-emit worker`. */
  readonly label: string;
  /** Hang guard: the child is killed past this many milliseconds. */
  readonly timeoutMs: number;
  /** Cooperative cancellation: kills the child and rejects with an `AbortError`. */
  readonly signal?: AbortSignal | undefined;
}

/**
 * Fork the Node module at `modulePath`, send it `request` over IPC, and resolve with
 * its first reply. Rejects if the child fails to start, exits without replying, runs
 * past the hang guard, or is cancelled.
 */
export function runWorker<Request extends Serializable, Reply>(
  modulePath: string,
  request: Request,
  { label, timeoutMs, signal }: SubprocessOptions,
): Promise<Reply> {
  return new Promise<Reply>((resolve, reject) => {
    if (signal?.aborted) {
      reject(abortError());
      return;
    }

    const child = fork(modulePath, [], { silent: true });
    const stderr: string[] = [];
    child.stderr?.on("data", (chunk: Buffer | string) => stderr.push(String(chunk)));

    let settled = false;
    const finish = (fn: () => void): void => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      signal?.removeEventListener("abort", onAbort);
      fn();
    };
    const onAbort = (): void => {
      child.kill();
      finish(() => reject(abortError()));
    };
    signal?.addEventListener("abort", onAbort, { once: true });

    const timer = setTimeout(() => {
      child.kill();
      finish(() => reject(new Error(`${label} timed out after ${timeoutMs}ms and was terminated.`)));
    }, timeoutMs);

    child.on("message", (message) => finish(() => resolve(message as Reply)));
    child.on("error", (error) => finish(() => reject(error)));
    child.on("exit", (code) =>
      finish(() =>
        reject(new Error(`${label} exited (code ${code ?? "null"}) without a result.\n${stderr.join("")}`)),
      ),
    );

    child.send(request);
  });
}

/**
 * Spawn `command`, write `input` to its stdin, and hand each stdout line to `onLine`
 * as it arrives (awaited, so a slow consumer applies backpressure and the child
 * throttles). Resolves with stderr and the exit code once the stream ends; rejects if
 * the child fails to start, runs past the hang guard, or is cancelled.
 */
export async function spawnStream(
  command: string,
  args: readonly string[],
  input: string,
  onLine: (line: string) => void | Promise<void>,
  { label, timeoutMs, signal }: SubprocessOptions,
): Promise<ToolStreamResult> {
  if (signal?.aborted) throw abortError();

  const child = spawn(command, args, { stdio: ["pipe", "pipe", "pipe"] });
  child.stderr.setEncoding("utf8");
  const err: string[] = [];
  child.stderr.on("data", (chunk: string) => err.push(chunk));

  // A spawn error (e.g. the command is not found), a cancellation, or the hang guard
  // stops the run: record why, kill the child, and end the line stream so the read
  // loop unwinds and rethrows.
  let stopError: Error | undefined;
  const stop = (error: Error): void => {
    stopError ??= error;
    child.kill();
    child.stdout.destroy();
  };
  child.on("error", (e) => stop(e));
  const onAbort = (): void => stop(abortError());
  signal?.addEventListener("abort", onAbort, { once: true });
  const timer = setTimeout(
    () => stop(new Error(`${label} timed out after ${timeoutMs}ms and was terminated.`)),
    timeoutMs,
  );

  // Ignore EPIPE if the child exits before consuming all of stdin.
  child.stdin.on("error", () => {});
  child.stdin.end(input, "utf8");

  const rl = createInterface({ input: child.stdout, crlfDelay: Infinity });
  try {
    for await (const line of rl) {
      if (stopError !== undefined) break;
      await onLine(line);
    }
  } catch (error) {
    stopError ??= error instanceof Error ? error : new Error(String(error));
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener("abort", onAbort);
    rl.close();
  }

  if (stopError !== undefined) throw stopError;

  const code = child.exitCode ?? (await new Promise<number | null>((r) => child.once("close", r)));
  return { stderr: err.join(""), code };
}

/** A hang guard from the environment variable `name`, else `fallbackMs`. */
export function timeoutFromEnv(name: string, fallbackMs: number): number {
  const raw = process.env[name];
  const parsed = raw !== undefined ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallbackMs;
}

/** A cancellation error the compiler recognises (`name` is `AbortError`). */
function abortError(): Error {
  const error = new Error("aborted");
  error.name = "AbortError";
  return error;
}