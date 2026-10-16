/**
 * `ace compile` command.
 *
 * Owns everything about running a compile from the CLI: flag parsing, loading the
 * `aceconfig.yaml`, staging the spec, invoking the backbone's `compile()` with the
 * host runtime + a filesystem output sink, rendering diagnostics, and post-run
 * cleanup of the transient `source/` and `ir/` artifacts. `cli.ts` only discovers the
 * command and hands argv here.
 *
 * No pipeline logic lives here — that stays in @azure-client-engine/compiler so a
 * future web app can reuse it unchanged; this module is the host-side wiring.
 */

import { mkdir, writeFile } from "node:fs/promises";
import { dirname, isAbsolute, relative, resolve, sep } from "node:path";
import { parseArgs } from "node:util";

import {
  compile,
  type CompilerDiagnostic,
  type OutputDelegate,
} from "@azure-client-engine/compiler";

import { CONFIG_FILE_NAME, findConfigFile, loadAceConfig } from "./aceconfig.js";
import { GITHUB_URL_FORM, isUrl, parseGitHubUrl } from "./remote-source.js";
import {
  STANDALONE_SPEC_VARIABLE,
  stageProject,
  removeDir,
  type StagedProject,
} from "./staging.js";
import { createHostRuntime } from "./runtime.js";

/** Usage/help text for `ace compile`. */
export function usage(): string {
  return [
    "ace — TypeSpec -> C# extension compiler",
    "",
    "Usage:",
    "  ace compile [<config>] [--tsp-location <path>] [--entrypoint <path>] [--api-version <v>] [--out-dir <path>] [--capture-ir] [--capture-source]",
    "",
    "Arguments:",
    `  <config>               The ${CONFIG_FILE_NAME} to use, or a folder holding one`,
    `                         (default: ./${CONFIG_FILE_NAME}, which must exist). The`,
    "                         flags below override its run inputs (tsp-location,",
    "                         entrypoints, output-dir); its 'stages' and document facts",
    "                         (e.g. 'root-namespace') go to the compiler. No flag sets a",
    "                         stage or a fact.",
    "",
    "Options:",
    "  --tsp-location <path>  TypeSpec project dir to copy, or an entry .tsp file whose",
    "                         directory is copied (default: the config's tsp-location).",
    "                         It must be inside an azure-rest-api-specs checkout: the",
    "                         copy keeps the repo's layout, together with the config's",
    "                         'additional-directories'. Set ACE_ALLOW_STANDALONE_SPEC=1",
    "                         to copy a spec from outside a checkout as is.",
    "                         Or a GitHub URL, fetched with git, whose repo is the root:",
    `                           ${GITHUB_URL_FORM}`,
    "                         (name a commit as <ref> to pin it).",
    "  --entrypoint <path>    Entry .tsp file or dir to compile, under --tsp-location;",
    "                         a relative path resolves against the copied directory",
    "                         (default: the config's entrypoint, else --tsp-location).",
    "  --api-version <v>      API version to compile at: a specific version,",
    "                         'latest' (default), or 'all'.",
    "  --out-dir <path>       Directory to write generated files to (default: the config's",
    "                         output-dir, else the config file's folder).",
    "                         The spec is staged under <out-dir>/source before compiling,",
    "                         which must not overlap any copied directory.",
    "  --capture-ir           Keep each pipeline IR under <out-dir>/ir; without it,",
    "                         a stale ir/ is removed (optional)",
    "  --capture-source       Keep the staged spec under <out-dir>/source instead of",
    "                         deleting it after compiling (optional)",
    "",
  ].join("\n");
}

/** Where a run happens, for anything that reads the environment. */
export interface CompileCommandOptions {
  /** The directory relative argument paths and `./aceconfig.yaml` are found in. */
  cwd?: string;
}

/**
 * `ace compile [<config>] [--tsp-location <path>] ...`.
 *
 * Loads the config file, applies flag overrides to its run inputs, hands the rest to
 * the backbone's `compile()`, then maps the structured result onto stdout/stderr and
 * an exit code. No pipeline logic lives here — that stays in the backbone so a
 * future web app can reuse it.
 */
export async function runCompile(
  args: string[],
  { cwd = process.cwd() }: CompileCommandOptions = {},
): Promise<number> {
  let tspLocationArg: string | undefined;
  let entrypointArg: string | undefined;
  let configPath: string | undefined;
  let apiVersionArg: string | undefined;
  let outDirArg: string | undefined;
  let captureIr = false;
  let captureSource = false;
  try {
    const { values, positionals } = parseArgs({
      args,
      options: {
        "tsp-location": { type: "string" },
        entrypoint: { type: "string" },
        "api-version": { type: "string" },
        "out-dir": { type: "string" },
        "capture-ir": { type: "boolean" },
        "capture-source": { type: "boolean" },
      },
      // The config file is the one positional argument.
      allowPositionals: true,
    });
    if (positionals.length > 1) {
      process.stderr.write(
        `compile: expected one config file, got ${positionals.length}: ${positionals.join(", ")}\n`,
      );
      return 2;
    }
    configPath = positionals[0];
    tspLocationArg = values["tsp-location"];
    entrypointArg = values.entrypoint;
    apiVersionArg = values["api-version"];
    outDirArg = values["out-dir"];
    captureIr = values["capture-ir"] === true;
    captureSource = values["capture-source"] === true;
  } catch (err) {
    process.stderr.write(`compile: ${err instanceof Error ? err.message : String(err)}\n`);
    return 2;
  }

  // The backbone is host-agnostic and never reads from disk, so the CLI loads the
  // file. It takes its own keys (the run inputs) and passes every other key on
  // untouched: the compiler takes `stages`, and the rest are document facts. A run
  // always has a file: without a config argument, the current directory's.
  const configFile = findConfigFile(configPath, cwd);
  if (configFile === undefined) {
    process.stderr.write(
      `compile: no ${CONFIG_FILE_NAME} in the current directory '${cwd}'. ` +
        "Name the config file, or a folder holding one: ace compile <path>\n",
    );
    return 2;
  }
  const loaded = await loadAceConfig(configFile, printDiagnostic);
  if (loaded === undefined) return 2; // diagnostics already written

  // A flag beats the file, which beats the default. Flag paths resolve against the
  // current directory; the file's against its own folder (done by the loader). A
  // GitHub URL is kept as written, and staging fetches it.
  let tspLocation = loaded.inputs.tspLocation;
  if (tspLocationArg !== undefined && tspLocationArg.length > 0) {
    if (isUrl(tspLocationArg)) {
      const parsed = parseGitHubUrl(tspLocationArg);
      if ("problem" in parsed) {
        process.stderr.write(`compile: --tsp-location '${tspLocationArg}' ${parsed.problem}.\n`);
        return 2;
      }
      tspLocation = tspLocationArg;
    } else {
      tspLocation = resolve(cwd, tspLocationArg);
    }
  }
  if (tspLocation === undefined) {
    process.stderr.write(
      `compile: missing 'tsp-location': set it in '${configFile}', or pass --tsp-location <path>\n`,
    );
    return 2;
  }
  // The file's output-dir defaults to its own folder, never to the current directory:
  // the run wipes generated folders in its output directory.
  const outDir = outDirArg !== undefined ? resolve(cwd, outDirArg) : loaded.inputs.outputDir;
  // --entrypoint replaces the file's entrypoint, so the file's API version goes with it.
  const entrypoint = entrypointArg ?? loaded.inputs.entrypoint?.path;
  const apiVersion =
    apiVersionArg ?? (entrypointArg === undefined ? loaded.inputs.entrypoint?.apiVersion : undefined);

  // Host responsibility (design Part II §18): acquire the source before the
  // TypeSpec-free orchestrator runs. Fetch what GitHub URLs name, stage a pristine
  // copy of the tsp-location, and of each additional directory, under
  // <out-dir>/source in the specs repo's layout, and compile the entrypoint's
  // counterpart inside it.
  // Tool resolution (the emit toolchain, the generator host) now happens lazily
  // inside the host runtime, per external stage, so ace itself loads neither
  // @typespec/compiler nor the C# generator.
  let staged: StagedProject | undefined;
  try {
    staged = await stageProject(
      {
        tspLocation,
        outDir,
        entrypoint,
        additionalDirectories: loaded.inputs.additionalDirectories,
        allowStandaloneSpec: process.env[STANDALONE_SPEC_VARIABLE] === "1",
      },
      printDiagnostic,
    );
  } catch (err) {
    // Staging checks its inputs before touching anything, so this is an I/O failure.
    process.stderr.write(`compile: ${err instanceof Error ? err.message : String(err)}\n`);
    return 1;
  }
  if (staged === undefined) return 1; // diagnostics already written

  // Provenance: what was fetched, at which commit, so a run can be reproduced.
  for (const { owner, repo, commit, folders } of staged.fetched) {
    const fetched = folders.map((folder) => (folder === "" ? "(the whole repo)" : folder)).join(", ");
    process.stdout.write(`Fetched ${owner}/${repo}@${commit}: ${fetched}\n`);
  }

  // The backbone streams every artifact (generated files, and each IR snapshot
  // when --capture-ir is set) to this filesystem sink as it is produced, so the
  // compiler never touches disk itself.
  const result = await compile(
    {
      entrypoint: staged.entrypoint,
      // A run input: the API version travels with its entrypoint.
      ...(apiVersion !== undefined ? { apiVersion } : {}),
      config: loaded.config,
    },
    {
      runtime: createHostRuntime(),
      captureIR: captureIr,
      output: createFsSink(outDir),
    },
  );

  for (const d of result.diagnostics) printDiagnostic(d);

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

/** Print a diagnostic: errors to stderr, everything else to stdout. */
function printDiagnostic(d: CompilerDiagnostic): void {
  const sink = d.severity === "error" ? process.stderr : process.stdout;
  sink.write(formatDiagnostic(d) + "\n");
}

/** Render a diagnostic for the terminal. */
function formatDiagnostic(d: CompilerDiagnostic): string {
  const file = d.target?.file;
  const loc =
    file === undefined
      ? ""
      : d.target?.line === undefined
        ? `${file} `
        : `${file}:${d.target.line}:${d.target.column ?? 0} `;
  return `${d.severity}: ${loc}${d.code}: ${d.message}`;
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
