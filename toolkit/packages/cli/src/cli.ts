#!/usr/bin/env node
/**
 * `ace` command-line front end.
 *
 * This is a THIN shell over @azure-client-engine/compiler: it parses argv, invokes the
 * backbone, then maps the structured result onto stdout/stderr and a process
 * exit code. All pipeline logic lives in the backbone so the future web app
 * can reuse it unchanged.
 *
 * Only `ace compile` is implemented today. Subcommands like `emit`, `check`,
 * and `watch` slot in alongside it later.
 */

import { mkdir, readFile, writeFile } from "node:fs/promises";
import { realpathSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import {
  compile,
  type CompilerDiagnostic,
  type OutputDelegate,
} from "@azure-client-engine/compiler";

/** Render a diagnostic for the terminal. */
function formatDiagnostic(d: CompilerDiagnostic): string {
  const loc = d.target?.file
    ? `${d.target.file}:${d.target.line ?? 0}:${d.target.column ?? 0} `
    : "";
  return `${d.severity}: ${loc}${d.code}: ${d.message}`;
}

/**
 * Run the CLI. Returns a process exit code (0 = success) instead of calling
 * process.exit directly, so it stays testable.
 */
export async function run(argv: string[]): Promise<number> {
  const [command, ...rest] = argv;

  if (!command || command === "help" || command === "--help" || command === "-h") {
    process.stdout.write(usage());
    return 0;
  }

  if (command !== "compile") {
    process.stderr.write(`Unknown command: ${command}\n`);
    return 2;
  }

  return runCompile(rest);
}

/** Usage/help text for the `ace` command. */
function usage(): string {
  return [
    "ace — TypeSpec -> C# extension compiler",
    "",
    "Usage:",
    "  ace compile --tsp-location <path> [--config <path>] [--out-dir <path>] [--capture-ir]",
    "",
    "Options:",
    "  --tsp-location <path>  Path to the TypeSpec project dir or entry .tsp file (required)",
    "  --config <path>        Path to a JSON file of generation-time settings,",
    "                         passed through as CompileInput.extensions (optional)",
    "  --out-dir <path>       Directory to write generated files to (default: cwd)",
    "  --capture-ir           Also write each pipeline IR to <out-dir>/ir (optional)",
    "",
  ].join("\n");
}

/**
 * `ace compile --tsp-location <path> [--config <path>]`.
 *
 * Parses the flags, hands them to the backbone's `compile()`, then maps the
 * structured result onto stdout/stderr and an exit code. No pipeline logic
 * lives here — that stays in the backbone so a future web app can reuse it.
 */
async function runCompile(args: string[]): Promise<number> {
  let tspLocation: string | undefined;
  let configPath: string | undefined;
  let outDirArg: string | undefined;
  let captureIr = false;
  try {
    const { values } = parseArgs({
      args,
      options: {
        "tsp-location": { type: "string" },
        config: { type: "string" },
        "out-dir": { type: "string" },
        "capture-ir": { type: "boolean" },
      },
      allowPositionals: false,
    });
    tspLocation = values["tsp-location"];
    configPath = values.config;
    outDirArg = values["out-dir"];
    captureIr = values["capture-ir"] === true;
  } catch (err) {
    process.stderr.write(`compile: ${err instanceof Error ? err.message : String(err)}\n`);
    return 2;
  }

  if (tspLocation === undefined || tspLocation.length === 0) {
    process.stderr.write("compile: missing required --tsp-location <path>\n");
    return 2;
  }

  // --config points at a JSON file of app-resolved, generation-time settings.
  // The backbone is host-agnostic and never reads from disk, so the CLI loads
  // the file here and threads it in as CompileInput.extensions.
  let extensions: Record<string, unknown> | undefined;
  if (configPath !== undefined) {
    const loaded = await loadConfig(configPath);
    if (loaded === undefined) return 2; // diagnostic already written
    extensions = loaded;
  }

  // The backbone streams every artifact (generated files, and each IR snapshot
  // when --capture-ir is set) to this filesystem sink as it is produced, so the
  // compiler never touches disk itself.
  const outDir = outDirArg ?? process.cwd();
  const result = await compile(
    {
      entrypoint: tspLocation,
      ...(extensions !== undefined ? { extensions } : {}),
    },
    {
      captureIR: captureIr,
      output: createFsSink(outDir),
    },
  );

  for (const d of result.diagnostics) {
    const sink = d.severity === "error" ? process.stderr : process.stdout;
    sink.write(formatDiagnostic(d) + "\n");
  }

  return result.success ? 0 : 1;
}

/**
 * Read and parse the `--config` JSON file into an extensions dictionary.
 * Returns `undefined` (after writing a diagnostic to stderr) when the file
 * cannot be read, is not valid JSON, or is not a JSON object.
 */
async function loadConfig(path: string): Promise<Record<string, unknown> | undefined> {
  let raw: string;
  try {
    raw = await readFile(path, "utf8");
  } catch (err) {
    process.stderr.write(
      `compile: cannot read --config '${path}': ${err instanceof Error ? err.message : String(err)}\n`,
    );
    return undefined;
  }

  let parsed: unknown;
  try {
    // Strip a leading UTF-8 BOM (common in Windows-authored files) so JSON.parse
    // doesn't choke on it.
    parsed = JSON.parse(raw.charCodeAt(0) === 0xfeff ? raw.slice(1) : raw);
  } catch (err) {
    process.stderr.write(
      `compile: --config '${path}' is not valid JSON: ${err instanceof Error ? err.message : String(err)}\n`,
    );
    return undefined;
  }

  if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
    process.stderr.write(`compile: --config '${path}' must be a JSON object\n`);
    return undefined;
  }

  return parsed as Record<string, unknown>;
}

/**
 * Filesystem output sink handed to `compile()` as `options.output`. Writes
 * generated files under `outDir`, and captured IR snapshots under
 * `outDir/ir/<index>-<step>.json`, creating parent directories as needed. The
 * backbone streams artifacts here so it never touches the runtime itself.
 */
export function createFsSink(outDir: string): OutputDelegate {
  return async (artifact) => {
    let target: string;
    let contents: string;
    if (artifact.kind === "generated-file") {
      target = resolve(outDir, artifact.path);
      contents = artifact.contents;
    } else {
      const step = sanitizeSegment(artifact.step);
      target = resolve(outDir, "ir", `${artifact.index}-${step}.json`);
      contents = JSON.stringify(artifact.ir, null, 2) + "\n";
    }
    await mkdir(dirname(target), { recursive: true });
    await writeFile(target, contents, "utf8");
  };
}

/** Reduce a step label to a safe single path segment for a filename. */
function sanitizeSegment(name: string): string {
  const cleaned = name.replace(/[^a-zA-Z0-9._-]+/g, "-").replace(/^-+|-+$/g, "");
  return cleaned.length > 0 ? cleaned : "step";
}

/**
 * True when this module is the process entrypoint (i.e. launched as the `ace`
 * bin), false when imported. Compares realpaths so it still fires when launched
 * through npm's bin shim, which passes a symlinked, non-normalized path in
 * `process.argv[1]` rather than the module's own resolved path.
 */
function isMainModule(): boolean {
  const entry = process.argv[1];
  if (!entry) return false;
  try {
    return realpathSync(entry) === fileURLToPath(import.meta.url);
  } catch {
    return false;
  }
}

// Entrypoint guard: only run when invoked as the bin, not when imported.
if (isMainModule()) {
  run(process.argv.slice(2))
    .then((code) => process.exit(code))
    .catch((err) => {
      process.stderr.write(String(err?.stack ?? err) + "\n");
      process.exit(1);
    });
}
