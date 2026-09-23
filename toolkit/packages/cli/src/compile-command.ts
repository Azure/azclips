/**
 * `ace compile` command.
 *
 * Owns everything about running a compile from the CLI: flag parsing, loading
 * `--config`, staging the spec, invoking the backbone's `compile()` with the host
 * runtime + a filesystem output sink, rendering diagnostics, and post-run cleanup
 * of the transient `source/` and `ir/` artifacts. `cli.ts` only discovers the
 * command and hands argv here.
 *
 * No pipeline logic lives here — that stays in @azure-client-engine/compiler so a
 * future web app can reuse it unchanged; this module is the host-side wiring.
 */

import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, isAbsolute, relative, resolve, sep } from "node:path";
import { parseArgs } from "node:util";

import {
  compile,
  createRegistry,
  CSHARP_STAGE,
  CREATE_COMMAND_STAGE,
  FLATTEN_MODEL_STAGE,
  PS_DERIVE_STAGE,
  PS_DIRECTIVES_STAGE,
  TWEAK_IR_STAGE,
  type CompileConfig,
  type CompilerDiagnostic,
  type OutputDelegate,
} from "@azure-client-engine/compiler";
import {
  createCommandStep,
  createPSDeriveStep,
  createPSDirectivesStep,
  flattenModelStep,
  tweakIrStep,
} from "@azure-client-engine/transformers";

import {
  stageLocalProject,
  removeDir,
  type StagedProject,
} from "./staging.js";
import { createHostRuntime } from "./runtime.js";

/**
 * Implementations this host supplies for the compiler's declared stages.
 *
 * This is fulfilment, not orchestration: the compiler's catalog owns where
 * `flatten-model`/`ps.derive`/`ps.directives` sit and whether each runs by
 * default, and the host only provides the code behind each id. `tsp-emit` and
 * `csharp` need no entry — they are external stages driven through the host
 * runtime.
 */
const STAGE_IMPLEMENTATIONS = createRegistry({
  transforms: {
    [TWEAK_IR_STAGE]: tweakIrStep,
    [FLATTEN_MODEL_STAGE]: flattenModelStep,
    [CREATE_COMMAND_STAGE]: createCommandStep,
    [PS_DERIVE_STAGE]: createPSDeriveStep,
    [PS_DIRECTIVES_STAGE]: createPSDirectivesStep,
  },
});

/** Usage/help text for `ace compile`. */
export function usage(): string {
  return [
    "ace — TypeSpec -> C# extension compiler",
    "",
    "Usage:",
    "  ace compile --tsp-location <path> [--config <path>] [--api-version <v>] [--out-dir <path>] [--generator <id>] [--root-namespace <ns>] [--capture-ir] [--capture-source]",
    "",
    "Options:",
    "  --tsp-location <path>  Path to the TypeSpec project dir or entry .tsp file (required)",
    "  --config <path>        Path to a JSON file of generation-time settings,",
    "                         loaded as CompileInput.config (CompileConfig) (optional).",
    "                         Its 'pipeline' flips compiler-declared stages on or off,",
    "                         e.g. { \"pipeline\": { \"flatten-model\": false } } or",
    "                         { \"pipeline\": { \"ps.derive\": true, \"ps.directives\": true } },",
    "                         and 'stageOptions' configures them by stage id. 'ps.directives'",
    "                         requires 'ps.derive' to also be enabled.",
    "  --api-version <v>      API version to compile at: a specific version,",
    "                         'latest' (default), or 'all'. Overrides config.apiVersion.",
    "  --out-dir <path>       Directory to write generated files to (default: cwd).",
    "                         The spec is staged under <out-dir>/source before compiling.",
    "  --generator <id>       Back end to run, e.g. 'csharp' (optional; default: 'csharp').",
    "  --root-namespace <ns>  Root C# namespace for the generator (generator option).",
    "  --capture-ir           Keep each pipeline IR under <out-dir>/ir; without it,",
    "                         a stale ir/ is removed (optional)",
    "  --capture-source       Keep the staged spec under <out-dir>/source instead of",
    "                         deleting it after compiling (optional)",
    "",
  ].join("\n");
}

/**
 * `ace compile --tsp-location <path> [--config <path>] [--api-version <v>] ...`.
 *
 * Parses the flags, hands them to the backbone's `compile()`, then maps the
 * structured result onto stdout/stderr and an exit code. No pipeline logic lives
 * here — that stays in the backbone so a future web app can reuse it.
 */
export async function runCompile(args: string[]): Promise<number> {
  let tspLocation: string | undefined;
  let configPath: string | undefined;
  let apiVersionArg: string | undefined;
  let outDirArg: string | undefined;
  let generatorArg: string | undefined;
  let rootNamespaceArg: string | undefined;
  let captureIr = false;
  let captureSource = false;
  try {
    const { values } = parseArgs({
      args,
      options: {
        "tsp-location": { type: "string" },
        config: { type: "string" },
        "api-version": { type: "string" },
        "out-dir": { type: "string" },
        generator: { type: "string" },
        "root-namespace": { type: "string" },
        "capture-ir": { type: "boolean" },
        "capture-source": { type: "boolean" },
      },
      allowPositionals: false,
    });
    tspLocation = values["tsp-location"];
    configPath = values.config;
    apiVersionArg = values["api-version"];
    outDirArg = values["out-dir"];
    generatorArg = values.generator;
    rootNamespaceArg = values["root-namespace"];
    captureIr = values["capture-ir"] === true;
    captureSource = values["capture-source"] === true;
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
  // the file here as CompileInput.config (a CompileConfig). --api-version is
  // sugar for config.apiVersion and takes precedence when both are given.
  let config: CompileConfig = {};
  if (configPath !== undefined) {
    const loaded = await loadConfig(configPath);
    if (loaded === undefined) return 2; // diagnostic already written
    config = loaded as CompileConfig;
  }
  if (apiVersionArg !== undefined) {
    config = { ...config, apiVersion: apiVersionArg };
  }
  // Orchestration stays compiler-owned, so these flags do not assemble a
  // pipeline: --generator turns a generator *stage* on, and --root-namespace
  // configures it through that stage's options.
  const generatorStage = generatorArg ?? CSHARP_STAGE;
  if (generatorArg !== undefined) {
    config = { ...config, pipeline: { ...config.pipeline, [generatorStage]: true } };
  }
  if (rootNamespaceArg !== undefined) {
    config = {
      ...config,
      stageOptions: {
        ...config.stageOptions,
        [generatorStage]: {
          ...config.stageOptions?.[generatorStage],
          rootNamespace: rootNamespaceArg,
        },
      },
    };
  }
  // The backbone streams every artifact (generated files, and each IR snapshot
  // when --capture-ir is set) to this filesystem sink as it is produced, so the
  // compiler never touches disk itself.
  const outDir = outDirArg ?? process.cwd();

  // Host responsibility (design Part II §18): acquire the source before the
  // TypeSpec-free orchestrator runs. Stage a pristine copy under <out-dir>/source.
  // Tool resolution (the emit toolchain, the generator host) now happens lazily
  // inside the host runtime, per external stage, so ace itself loads neither
  // @typespec/compiler nor the C# generator.
  let staged: StagedProject;
  try {
    staged = await stageLocalProject(tspLocation, outDir);
  } catch (err) {
    process.stderr.write(`compile: ${err instanceof Error ? err.message : String(err)}\n`);
    return 1;
  }

  const result = await compile(
    {
      entrypoint: staged.entrypoint,
      config,
    },
    {
      runtime: createHostRuntime(),
      registry: STAGE_IMPLEMENTATIONS,
      captureIR: captureIr,
      output: createFsSink(outDir),
    },
  );

  for (const d of result.diagnostics) {
    const sink = d.severity === "error" ? process.stderr : process.stdout;
    sink.write(formatDiagnostic(d) + "\n");
  }

  // The staged copy under <out-dir>/source is a transient build input; remove it
  // unless --capture-source was given. Best-effort: a failure (e.g. a lock from a
  // subprocess that hasn't fully exited) must not turn an otherwise-successful
  // compile into a failure — warn, keep the exit code, and let the next compile's
  // staging step reclaim it.
  if (!captureSource) {
    const { removed, error } = await removeDir(staged.root);
    if (!removed) {
      process.stderr.write(
        `compile: warning: could not remove staged source '${staged.root}': ` +
          `${error?.message ?? "unknown error"}. It will be cleaned on the next compile.\n`,
      );
    }
  }

  // The captured-IR directory follows the same model as source: keep it only
  // with --capture-ir, otherwise remove any (possibly stale) <out-dir>/ir so it
  // never reflects a previous run. Same non-fatal cleanup.
  if (!captureIr) {
    const irDir = resolve(outDir, "ir");
    const { removed, error } = await removeDir(irDir);
    if (!removed) {
      process.stderr.write(
        `compile: warning: could not remove '${irDir}': ${error?.message ?? "unknown error"}.\n`,
      );
    }
  }

  return result.success ? 0 : 1;
}

/** Render a diagnostic for the terminal. */
function formatDiagnostic(d: CompilerDiagnostic): string {
  const loc = d.target?.file
    ? `${d.target.file}:${d.target.line ?? 0}:${d.target.column ?? 0} `
    : "";
  return `${d.severity}: ${loc}${d.code}: ${d.message}`;
}

/**
 * Read and parse the `--config` JSON file into a `CompileConfig` object.
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
    // Remove a generator-owned scope (a dir or file) before the fresh files land.
    // The app owns the removal detail: reuse removeDir, but refuse anything at or
    // outside the output root so a stray scope can never wipe unrelated content.
    if (artifact.kind === "remove") {
      const target = resolve(outDir, artifact.path);
      const rel = relative(outDir, target);
      if (rel === "" || rel === ".." || rel.startsWith(".." + sep) || isAbsolute(rel)) {
        process.stderr.write(
          `compile: warning: refusing to remove scope '${artifact.path}' (outside the output directory).\n`,
        );
        return;
      }
      await removeDir(target);
      return;
    }

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
