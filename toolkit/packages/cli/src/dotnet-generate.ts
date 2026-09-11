/**
 * `csharp-generate` tool host: run the C# generator out of process.
 *
 * Backs the compiler's built-in `csharp` generator. The compiler owns *that* the
 * generator is an external subprocess speaking the `csharp-generate` protocol
 * and drives it through `runtime.spawn`; resolving the `dotnet` command + the
 * generator host dll (staged next to the CLI by the build script) and spawning
 * it are the host's job.
 *
 * The boundary is byte-level: the request JSON goes in on stdin, the NDJSON reply
 * comes back on stdout. The compiler frames/parses the protocol on top.
 */

import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { createInterface } from "node:readline";
import { fileURLToPath } from "node:url";

import type { ToolStreamResult } from "@azure-client-engine/compiler";

/** Built assembly name of the generator host executable. */
const HOST_DLL = "Ace.Generator.Host.dll";

/**
 * Folder, relative to the CLI package root, where the build script stages the
 * published generator host (its dll + dependency dlls + runtime config). The CLI
 * resolves the host here at runtime and is deliberately build-config-agnostic:
 * choosing Debug/Release and gathering dependency dlls are the build script's
 * concern, not the CLI's.
 */
const HOST_DIR = "generator-host";

/**
 * Hang guard for the generator host. Generous by default (large specs generate
 * many files); override with `ACE_GENERATE_TIMEOUT_MS`.
 */
const DEFAULT_GENERATE_TIMEOUT_MS = 10 * 60_000;

function generateTimeoutMs(): number {
  const raw = process.env.ACE_GENERATE_TIMEOUT_MS;
  const parsed = raw !== undefined ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_GENERATE_TIMEOUT_MS;
}

/**
 * Resolve the generator host dll to spawn via `dotnet`.
 *
 * The CLI looks it up at a fixed location relative to its own package —
 * `<cli>/generator-host/` — which the build script populates by publishing the
 * host + its dependencies there. This resolves identically in local dev (relative
 * to the CLI source tree) and when installed from npm (relative to the installed
 * package), and keeps runtime code free of any build-config (Debug/Release)
 * knowledge. `ACE_GENERATOR_HOST_DLL` overrides for bespoke setups.
 */
export function resolveGeneratorHost(): string {
  const override = process.env.ACE_GENERATOR_HOST_DLL;
  if (override !== undefined && override.length > 0) {
    if (!existsSync(override)) {
      throw new Error(`ACE_GENERATOR_HOST_DLL points at a missing file: ${override}`);
    }
    return override;
  }

  const dll = join(cliPackageRoot(), HOST_DIR, HOST_DLL);
  if (!existsSync(dll)) {
    throw new Error(
      `Generator host not found at '${dll}'. Stage it by running the toolkit build ` +
        "script (./build.ps1), which publishes the C# generator host next to the CLI, " +
        "or set ACE_GENERATOR_HOST_DLL to a built Ace.Generator.Host.dll.",
    );
  }
  return dll;
}

/** The CLI package root: the nearest ancestor of this module that has a package.json. */
function cliPackageRoot(): string {
  let dir = dirname(fileURLToPath(import.meta.url));
  for (;;) {
    if (existsSync(join(dir, "package.json"))) return dir;
    const parent = dirname(dir);
    if (parent === dir) {
      throw new Error(`Cannot locate the CLI package root (no package.json above '${dir}').`);
    }
    dir = parent;
  }
}

/**
 * Run the built-in `csharp-generate` tool: spawn the generator host via `dotnet`,
 * pipe the request JSON to its stdin, and deliver its stdout (NDJSON) to
 * `onLine` a line at a time as it arrives — so large output is never buffered.
 */
export function runCsharpGenerateStreaming(
  input: string,
  onLine: (line: string) => void | Promise<void>,
  signal?: AbortSignal,
): Promise<ToolStreamResult> {
  const dll = resolveGeneratorHost();
  return spawnStream("dotnet", [dll], input, onLine, signal);
}

/**
 * Spawn a command, write `input` to its stdin, and hand each stdout line to
 * `onLine` as it arrives (awaited — so a slow consumer applies backpressure and
 * the child throttles). Resolves with stderr + exit code once the stream ends.
 */
async function spawnStream(
  command: string,
  args: string[],
  input: string,
  onLine: (line: string) => void | Promise<void>,
  signal?: AbortSignal,
): Promise<ToolStreamResult> {
  if (signal?.aborted) throw abortError();

  const child = spawn(command, args, { stdio: ["pipe", "pipe", "pipe"] });
  child.stderr.setEncoding("utf8");
  const err: string[] = [];
  child.stderr.on("data", (chunk: string) => err.push(chunk));

  // A spawn error (e.g. dotnet not found), an abort, or the hang-guard timeout
  // stops the run: record why, kill the child, and end the line stream so the
  // read loop unwinds and rethrows.
  let stopError: Error | undefined;
  const stop = (error: Error): void => {
    stopError ??= error;
    child.kill();
    child.stdout.destroy();
  };
  child.on("error", (e) => stop(e));
  const onAbort = (): void => stop(abortError());
  signal?.addEventListener("abort", onAbort, { once: true });
  const timeoutMs = generateTimeoutMs();
  const timer = setTimeout(
    () => stop(new Error(`generator host timed out after ${timeoutMs}ms and was terminated.`)),
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

/** A cancellation error recognised by the backbone's abort handling. */
function abortError(): Error {
  const error = new Error("aborted");
  error.name = "AbortError";
  return error;
}
