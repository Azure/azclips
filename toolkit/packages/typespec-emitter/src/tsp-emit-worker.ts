/**
 * `tsp-emit` worker: the out-of-process end of the compiler's `tsp-emit` tool.
 *
 * Runs one TypeSpec compile + emit against the toolchain the host resolved, and
 * replies with the IR + structured diagnostics over IPC. It dynamic-imports
 * `@typespec/compiler` from the path the host resolved (`compilerPath`) and uses
 * *that* instance's `NodeHost` / `compile` / `getSourceLocation`, so a project-local
 * spec compiles against its own compiler, and the emitter (registered by its
 * resolved path) binds to the same instance. Nothing here statically imports
 * `@typespec/compiler`, so the worker carries no fixed TypeSpec version; that is why
 * it is a module of its own, apart from the emitter's library entry.
 *
 * What an entrypoint means is TypeSpec's call (a folder compiles its `main.tsp`), so
 * the worker is where an entrypoint TypeSpec cannot compile is explained: when
 * TypeSpec loads nothing from it, its location-less load errors become one
 * `invalid-tsp-source` error that names the entrypoint and quotes them.
 *
 * The host starts it with `child_process.fork`, sends one `TspEmitWorkerRequest`,
 * and reads one `TspEmitReply`.
 */

import { statSync } from "node:fs";
import { pathToFileURL } from "node:url";

import { EMITTER_NAME, type EmitterOptions } from "./options.js";
import type { TspEmitDiagnostic, TspEmitReply, TspEmitWorkerRequest } from "./tsp-emit-protocol.js";

/** Minimal surface of the dynamically-imported `@typespec/compiler` module. */
interface TypeSpecModule {
  NodeHost: Record<string, unknown>;
  compile: (host: unknown, mainFile: string, options: unknown) => Promise<CompiledProgram>;
  normalizePath: (path: string) => string;
  resolvePath: (path: string, ...paths: string[]) => string;
  getSourceLocation: (target: unknown) => SourceLocation | undefined;
  NoTarget: unknown;
}

/** The parts of TypeSpec's `Program` the worker reads, keyed by normalized path. */
interface CompiledProgram {
  diagnostics: readonly unknown[];
  sourceFiles: ReadonlyMap<string, unknown>;
  jsSourceFiles: ReadonlyMap<string, unknown>;
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

/** The file the emitter writes the IR to, captured in memory. */
const IR_OUTPUT_FILE = "ir.json";

/**
 * TypeSpec's errors for a main file it could not load: missing, unreadable, or not a
 * `.tsp` or `.js` file. It reports them with no source location.
 */
const LOAD_ERROR_CODES: ReadonlySet<string> = new Set(["file-not-found", "file-load", "invalid-main"]);

/** Translate a raw TypeSpec diagnostic into the protocol's structured shape. */
function toDiagnostic(ts: TypeSpecModule, d: RawDiagnostic): TspEmitDiagnostic {
  const mapped: TspEmitDiagnostic = { severity: d.severity, code: d.code, message: d.message };
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

/** Whether TypeSpec loaded any source from the entrypoint: the file itself, or a file in the folder. */
function loadedFrom(program: CompiledProgram, entrypoint: string): boolean {
  return [...program.sourceFiles.keys(), ...program.jsSourceFiles.keys()].some(
    (path) => path === entrypoint || path.startsWith(`${entrypoint}/`),
  );
}

/** The error for an entrypoint TypeSpec loaded nothing from, quoting TypeSpec's load errors. */
function invalidSource(entrypoint: string, loadErrors: readonly RawDiagnostic[]): TspEmitDiagnostic {
  const stat = statSync(entrypoint, { throwIfNoEntry: false });
  const reasons = loadErrors.map((d) => d.message).join(" ");
  const message =
    stat === undefined
      ? `The entrypoint '${entrypoint}' does not exist.`
      : stat.isDirectory()
        ? `The entrypoint '${entrypoint}' is a folder without a main file TypeSpec can load. ${reasons} ` +
          "TypeSpec compiles a folder's main.tsp; to compile another file, such as client.tsp, " +
          "name that file as the entrypoint."
        : `The entrypoint '${entrypoint}' is not a source TypeSpec can load. ${reasons}`;
  return { severity: "error", code: `${EMITTER_NAME}/invalid-tsp-source`, message };
}

async function run({ compilerPath, emitterPath, request }: TspEmitWorkerRequest): Promise<TspEmitReply> {
  const ts = (await import(pathToFileURL(compilerPath).href)) as unknown as TypeSpecModule;

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

  // What the run needs from the emitter: the IR as JSON in one file, at the
  // requested API version. These win over the forwarded options.
  const runOptions: EmitterOptions = {
    // TCGC reads the api-version selector from this exact (kebab) option key.
    ...(request.apiVersion !== undefined ? { "api-version": request.apiVersion } : {}),
    format: "json",
    outputFile: IR_OUTPUT_FILE,
    pretty: false,
  };

  // Register the emitter by its resolved path (forward slashes for TypeSpec's path
  // handling): a path works for project-local and bundled resolution alike, where a
  // bare name would fail for a spec with no `node_modules` of its own. TypeSpec files
  // emitter options under the emitter's library name, not the `emit` specifier.
  const emitterKey = emitterPath.replace(/\\/g, "/");
  const program = await ts.compile(host, request.entrypoint, {
    emit: [emitterKey],
    options: { [EMITTER_NAME]: { ...(request.emitterOptions ?? {}), ...runOptions } },
    ...(request.additionalImports !== undefined
      ? { additionalImports: request.additionalImports }
      : {}),
    ...(request.warnAsError !== undefined ? { warnAsError: request.warnAsError } : {}),
    ...(request.trace !== undefined ? { trace: request.trace } : {}),
    ...(request.nostdlib !== undefined ? { nostdlib: request.nostdlib } : {}),
  });

  // An entrypoint TypeSpec loaded nothing from is not a source it can compile. Its
  // location-less load errors are replaced by one error that says so; the program
  // must also have loaded nothing from the entrypoint, so a broken install that
  // reports the same codes is left as it is.
  const raw = program.diagnostics as RawDiagnostic[];
  const entrypoint = ts.resolvePath(request.entrypoint);
  const loadErrors = raw.filter((d) => d.target === ts.NoTarget && LOAD_ERROR_CODES.has(d.code));
  const unloadable = loadErrors.length > 0 && !loadedFrom(program, entrypoint);
  const diagnostics = (unloadable ? raw.filter((d) => !loadErrors.includes(d)) : raw).map((d) =>
    toDiagnostic(ts, d),
  );
  if (unloadable) diagnostics.unshift(invalidSource(entrypoint, loadErrors));

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

// If the host goes away, don't linger as an orphan holding handles on the staged
// spec. A host exit (even a hard kill) closes the IPC channel, which fires
// 'disconnect' here — self-terminate so the staged source can be cleaned.
process.on("disconnect", () => process.exit(1));

process.on("message", (message: TspEmitWorkerRequest) => {
  run(message)
    .then((reply) => process.send!(reply, () => process.exit(0)))
    .catch((err: unknown) => {
      const reply: TspEmitReply = {
        diagnostics: [
          {
            severity: "error",
            code: `${EMITTER_NAME}/tsp-emit-worker-failed`,
            message: String((err as { stack?: string })?.stack ?? err),
          },
        ],
      };
      process.send!(reply, () => process.exit(0));
    });
});