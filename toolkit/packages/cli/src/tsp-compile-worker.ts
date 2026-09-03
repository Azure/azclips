/**
 * `tsp-compile` worker (child process).
 *
 * Runs one TypeSpec compile + emit against the *resolved* toolchain and returns
 * the IR + structured diagnostics over IPC. It dynamic-imports `@typespec/compiler`
 * from the path the runner resolved (`request.compilerPath`) and uses *that*
 * instance's `NodeHost` / `compile` / `getSourceLocation` — so a project-local
 * spec compiles against its own compiler, and the emitter (registered by its
 * resolved path) binds to the same instance. Nothing here statically imports
 * `@typespec/compiler`, so the worker carries no fixed TypeSpec version.
 *
 * Started via `child_process.fork`; receives a `TspCompileRequest` message and
 * replies with a `TspCompileResponse`.
 */

import { pathToFileURL } from "node:url";

import type { CompilerDiagnostic } from "@azure-client-engine/compiler";
import type { TspCompileRequest, TspCompileResponse } from "./tsp-compile-protocol.js";

/** Minimal surface of the dynamically-imported `@typespec/compiler` module. */
interface TypeSpecModule {
  NodeHost: Record<string, unknown>;
  compile: (host: unknown, mainFile: string, options: unknown) => Promise<{ diagnostics: unknown[] }>;
  normalizePath: (path: string) => string;
  getSourceLocation: (target: unknown) => SourceLocation | undefined;
  NoTarget: unknown;
}

interface SourceLocation {
  pos: number;
  file: { path: string; getLineAndCharacterOfPosition: (pos: number) => { line: number; character: number } };
}

interface RawDiagnostic {
  severity: "error" | "warning";
  code: string;
  message: string;
  target: unknown;
}

const IR_OUTPUT_FILE = "ir.json";

/**
 * The emitter's registered library name. TypeSpec keys emitter *options* by the
 * emitter's `$lib` name (not by the `emit` specifier), so options must use this
 * even though we register the emitter by its resolved *path* (a path works for
 * both project-local and bundled resolution; a bare name would fail for an
 * external spec that has no local `node_modules`).
 */
const EMITTER_NAME = "@azure-client-engine/typespec-emitter";

/** Translate a raw TypeSpec diagnostic into the backbone's structured shape. */
function toCompilerDiagnostic(ts: TypeSpecModule, d: RawDiagnostic): CompilerDiagnostic {
  const mapped: CompilerDiagnostic = { severity: d.severity, code: d.code, message: d.message };
  if (d.target !== ts.NoTarget) {
    try {
      const loc = ts.getSourceLocation(d.target);
      if (loc !== undefined) {
        const { line, character } = loc.file.getLineAndCharacterOfPosition(loc.pos);
        // TypeSpec reports 0-based; expose 1-based for editors.
        mapped.target = { file: loc.file.path, line: line + 1, column: character + 1 };
      }
    } catch {
      // Best-effort location; omit on failure.
    }
  }
  return mapped;
}

async function run(request: TspCompileRequest): Promise<TspCompileResponse> {
  const ts = (await import(pathToFileURL(request.compilerPath).href)) as unknown as TypeSpecModule;

  // Capture host: intercept writes so the emitter's IR is captured in memory
  // instead of hitting disk. Built from the *resolved* NodeHost.
  const outputs = new Map<string, string>();
  const host = {
    ...ts.NodeHost,
    writeFile: async (path: string, content: string): Promise<void> => {
      outputs.set(ts.normalizePath(path), content);
    },
    mkdirp: async (path: string): Promise<string> => path,
  };

  // Register the emitter by its resolved path (forward slashes for TypeSpec's
  // path handling); key the options by the emitter's library NAME, which is how
  // TypeSpec matches emitter options.
  const emitterKey = request.emitterPath.replace(/\\/g, "/");
  const program = await ts.compile(host, request.entrypoint, {
    emit: [emitterKey],
    options: {
      [EMITTER_NAME]: {
        ...(request.emitterOptions ?? {}),
        // TCGC reads the api-version selector from this exact (kebab) option key.
        ...(request.apiVersion !== undefined ? { "api-version": request.apiVersion } : {}),
        format: "json",
        outputFile: IR_OUTPUT_FILE,
        pretty: false,
      },
    },
    ...(request.additionalImports !== undefined
      ? { additionalImports: request.additionalImports }
      : {}),
    ...(request.warnAsError !== undefined ? { warnAsError: request.warnAsError } : {}),
    ...(request.trace !== undefined ? { trace: request.trace } : {}),
    ...(request.nostdlib !== undefined ? { nostdlib: request.nostdlib } : {}),
  });

  const diagnostics = (program.diagnostics as RawDiagnostic[]).map((d) =>
    toCompilerDiagnostic(ts, d),
  );

  const emitted = [...outputs.entries()].find(([path]) => path.endsWith(IR_OUTPUT_FILE));
  let ir: unknown;
  if (emitted !== undefined) {
    try {
      ir = JSON.parse(emitted[1]);
    } catch {
      // The emitter wrote non-JSON; it reports its own diagnostic, we surface no IR.
    }
  }
  return ir !== undefined ? { ir, diagnostics } : { diagnostics };
}

// If the parent goes away, don't linger as an orphan holding handles on the
// staged spec. A parent exit (even a hard kill) closes the IPC channel, which
// fires 'disconnect' here — self-terminate so the staged source can be cleaned.
process.on("disconnect", () => process.exit(1));

process.on("message", (request: TspCompileRequest) => {
  run(request)
    .then((response) => process.send!(response, () => process.exit(0)))
    .catch((err: unknown) => {
      const response: TspCompileResponse = {
        diagnostics: [
          {
            severity: "error",
            code: "@azure-client-engine/cli/tsp-compile-worker-failed",
            message: String((err as { stack?: string })?.stack ?? err),
          },
        ],
      };
      process.send!(response, () => process.exit(0));
    });
});
