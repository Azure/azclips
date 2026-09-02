/**
 * Subprocess-backed C# generator.
 *
 * Wraps the `Ace.Generator.Host` process as a `Generator` the orchestrator can resolve through
 * the registry. The backbone is .NET-free, so the Roslyn generator is reached the same way the
 * emitter is: spawn it, hand it JSON, read JSON back.
 */

import { spawn } from "node:child_process";

import type { Generator, GeneratorContext, GeneratorOutput } from "@azure-client-engine/compiler";
import type { IR } from "@azure-client-engine/ir";

/** Registry id for the C# generator. */
export const CSHARP_GENERATOR_ID = "csharp";

const KNOWN_OPTIONS = new Set(["hostPath", "command", "args", "rootNamespace"]);

interface HostResponse {
  files?: Record<string, string>;
  diagnostics?: { severity?: string; code?: string; message?: string }[];
  success?: boolean;
}

/**
 * Build the C# generator. Point it at the built host with `hostPath`, or supply `command` +
 * `args` directly for a self-contained executable or a wrapper.
 *
 * Options are read in `generate`, not here: the backbone turns a throw from `generate` into a
 * generate-stage diagnostic, whereas a throw from this factory would escape generator resolution.
 */
export function createDotnetGenerator(options: Record<string, unknown> = {}): Generator {
  return {
    name: CSHARP_GENERATOR_ID,
    async generate(ir: IR, context: GeneratorContext): Promise<GeneratorOutput> {
      const { command, args, rootNamespace } = readOptions(options);
      const request = JSON.stringify({ ir, ...(rootNamespace === undefined ? {} : { rootNamespace }) });
      const response = await invokeHost(command, args, request, context.signal);

      for (const diagnostic of response.diagnostics ?? []) {
        context.reportDiagnostic({
          severity: diagnostic.severity === "error" ? "error" : "warning",
          code: `@azure-client-engine/generator/${diagnostic.code ?? "unknown"}`,
          message: diagnostic.message ?? "",
        });
      }

      return { files: new Map(Object.entries(response.files ?? {})) };
    },
  };
}

/** A `.dll` needs the `dotnet` muxer; anything else is assumed directly executable. */
export function resolveHostCommand(hostPath: string): { command: string; args: string[] } {
  return hostPath.endsWith(".dll")
    ? { command: "dotnet", args: [hostPath] }
    : { command: hostPath, args: [] };
}

function readOptions(options: Record<string, unknown>): {
  command: string;
  args: string[];
  rootNamespace: string | undefined;
} {
  for (const key of Object.keys(options)) {
    if (!KNOWN_OPTIONS.has(key)) {
      throw new Error(
        `${CSHARP_GENERATOR_ID}: unknown option '${key}'. Expected one of ${[...KNOWN_OPTIONS].join(", ")}.`,
      );
    }
  }

  const rootNamespace = optionalString(options, "rootNamespace");
  const explicit = optionalString(options, "command");
  if (explicit !== undefined) {
    const args = options["args"];
    if (args !== undefined && (!Array.isArray(args) || args.some((a) => typeof a !== "string"))) {
      throw new Error(`${CSHARP_GENERATOR_ID}: option 'args' must be an array of strings.`);
    }
    return { command: explicit, args: (args as string[] | undefined) ?? [], rootNamespace };
  }

  const hostPath = optionalString(options, "hostPath");
  if (hostPath === undefined) {
    throw new Error(
      `${CSHARP_GENERATOR_ID}: needs 'hostPath' (the built Ace.Generator.Host) or an explicit 'command'.`,
    );
  }
  return { ...resolveHostCommand(hostPath), rootNamespace };
}

function optionalString(options: Record<string, unknown>, key: string): string | undefined {
  const value = options[key];
  if (value === undefined) return undefined;
  if (typeof value !== "string") {
    throw new Error(
      `${CSHARP_GENERATOR_ID}: option '${key}' must be a string, got ${typeof value}.`,
    );
  }
  return value;
}

/** Spawn the host, write the request to stdin, resolve with its parsed reply. */
function invokeHost(
  command: string,
  args: string[],
  request: string,
  signal?: AbortSignal,
): Promise<HostResponse> {
  return new Promise<HostResponse>((resolve, reject) => {
    if (signal?.aborted) {
      reject(new Error("aborted"));
      return;
    }

    const child = spawn(command, args, { stdio: ["pipe", "pipe", "pipe"] });
    const stdout: string[] = [];
    const stderr: string[] = [];
    child.stdout.on("data", (chunk: Buffer) => stdout.push(String(chunk)));
    child.stderr.on("data", (chunk: Buffer) => stderr.push(String(chunk)));

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

    child.on("error", (err) => finish(() => reject(err)));
    child.on("close", (code) =>
      finish(() => {
        const text = stdout.join("");
        // The host reports failure in the response, so a non-zero exit is only fatal when it
        // left nothing parseable behind.
        try {
          resolve(JSON.parse(text) as HostResponse);
        } catch {
          reject(
            new Error(
              `${CSHARP_GENERATOR_ID}: host exited (code ${code ?? "null"}) without a JSON response.\n${stderr.join("")}`,
            ),
          );
        }
      }),
    );

    child.stdin.on("error", () => {
      /* the close handler reports what actually went wrong */
    });
    child.stdin.end(request);
  });
}
