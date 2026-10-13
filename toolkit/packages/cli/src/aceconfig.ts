/**
 * Loading `aceconfig.yaml`, the gen-time config of one generated extension.
 *
 * The CLI owns the file format. It finds the file, parses it, and takes the keys it
 * uses itself: `schema-version` and the run inputs (`tsp-location`, `output-dir`,
 * `entrypoints`). It checks them the way every stage checks its stage config: a key
 * it does not know is a warning, and an invalid or missing value is an error that
 * stops the run before anything is staged. Every other key goes to the compiler
 * untouched; the compiler takes `stages`, and the rest are document facts for
 * `seed-ir`.
 *
 * Relative paths in the file resolve against the file's own folder, which is also
 * where output goes unless the file says otherwise, so the same file works from any
 * working directory.
 */

import { statSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

import { parseDocument } from "yaml";

import {
  describeValue,
  isMap,
  warnUnknownKeys,
  type CompileConfig,
  type CompilerDiagnostic,
} from "@azure-client-engine/compiler";

/** The file `ace compile` reads from the current directory when `--config` is not given. */
export const CONFIG_FILE_NAME = "aceconfig.yaml";

/** The only `schema-version` this CLI reads. */
const SCHEMA_VERSION = 1;

const DIAGNOSTIC_PREFIX = "@azure-client-engine/cli";

const ENTRYPOINT_KEYS: ReadonlySet<string> = new Set(["path", "api-version"]);

/** The entrypoint to compile, as the file names it. */
export interface ConfigEntrypoint {
  /** `.tsp` file or folder, relative to `tsp-location` or absolute. */
  readonly path: string;
  /** API version to compile it at; `latest` when absent. */
  readonly apiVersion?: string;
}

/** The run inputs the CLI takes from the file, with paths made absolute. */
export interface ConfigInputs {
  /** The folder, or `.tsp` file, to copy. */
  readonly tspLocation?: string;
  /** Where output goes: the file's folder unless the file says otherwise. */
  readonly outputDir: string;
  readonly entrypoint?: ConfigEntrypoint;
}

/** A loaded file: the CLI's run inputs, and everything else for the compiler. */
export interface AceConfig {
  /** Absolute path of the file. */
  readonly file: string;
  readonly inputs: ConfigInputs;
  /** Every key the CLI does not take, untouched: `stages` and the document facts. */
  readonly config: CompileConfig;
}

type Report = (
  severity: "error" | "warning",
  code: string,
  message: string,
  at?: { line: number; column: number },
) => void;

/**
 * The file to load: `--config` resolved against `cwd`, else `aceconfig.yaml` in `cwd`
 * when it exists, else none.
 */
export function findConfigFile(configArg: string | undefined, cwd: string): string | undefined {
  if (configArg !== undefined) return resolve(cwd, configArg);
  const candidate = resolve(cwd, CONFIG_FILE_NAME);
  return statSync(candidate, { throwIfNoEntry: false })?.isFile() === true ? candidate : undefined;
}

/**
 * Read, parse and check `file`. Every problem is reported; the result is `undefined`
 * when any of them is an error. Warnings never block.
 */
export async function loadAceConfig(
  file: string,
  reportDiagnostic: (diagnostic: CompilerDiagnostic) => void,
): Promise<AceConfig | undefined> {
  let failed = false;
  const report: Report = (severity, code, message, at) => {
    if (severity === "error") failed = true;
    reportDiagnostic({
      severity,
      code: `${DIAGNOSTIC_PREFIX}/${code}`,
      message,
      target: { file, ...(at !== undefined ? { line: at.line, column: at.column } : {}) },
    });
  };

  let text: string;
  try {
    text = await readFile(file, "utf8");
  } catch (error) {
    const detail = error instanceof Error ? error.message : String(error);
    report("error", "unreadable-config", `Cannot read the config file: ${detail}`);
    return undefined;
  }

  // YAML 1.2, so an unquoted `2024-05-01` stays a string, and a JSON file parses too.
  const document = parseDocument(text);
  for (const problem of document.errors) report("error", "invalid-yaml", ...yamlProblem(problem));
  for (const problem of document.warnings) report("warning", "invalid-yaml", ...yamlProblem(problem));
  if (failed) return undefined;

  const value: unknown = document.toJS();
  if (!isMap(value)) {
    report("error", "invalid-setting", `The config must be a map of settings, not ${describeValue(value)}.`);
    return undefined;
  }

  const folder = dirname(file);
  const {
    "schema-version": schemaVersion,
    "tsp-location": tspLocationValue,
    "output-dir": outputDirValue,
    entrypoints,
    ...config
  } = value;

  checkSchemaVersion(schemaVersion, report);
  const tspLocation = readPath("tsp-location", tspLocationValue, folder, report);
  const outputDir = readPath("output-dir", outputDirValue, folder, report) ?? folder;
  const entrypoint = readEntrypoints(entrypoints, report);
  if (failed) return undefined;

  return {
    file,
    inputs: {
      ...(tspLocation !== undefined ? { tspLocation } : {}),
      outputDir,
      ...(entrypoint !== undefined ? { entrypoint } : {}),
    },
    config,
  };
}

function checkSchemaVersion(value: unknown, report: Report): void {
  if (value === undefined || value === null) {
    report(
      "error",
      "missing-setting",
      `'schema-version' is required: an aceconfig file starts with 'schema-version: ${SCHEMA_VERSION}'.`,
    );
  } else if (value !== SCHEMA_VERSION) {
    report(
      "error",
      "invalid-setting",
      `'schema-version' must be ${SCHEMA_VERSION}, not ${describeValue(value)}.`,
    );
  }
}

/** A path from the file, resolved against the file's folder. */
function readPath(key: string, value: unknown, folder: string, report: Report): string | undefined {
  if (value === undefined || value === null) return undefined;
  if (typeof value === "string" && value.length > 0) return resolve(folder, value);
  report("error", "invalid-setting", `'${key}' must be a path, not ${describeValue(value)}.`);
  return undefined;
}

/** The one entrypoint the file lists; several are refused until they can be compiled together. */
function readEntrypoints(value: unknown, report: Report): ConfigEntrypoint | undefined {
  if (value === undefined || value === null) return undefined;
  if (!Array.isArray(value)) {
    report(
      "error",
      "invalid-setting",
      `'entrypoints' must be a list holding one entrypoint, not ${describeValue(value)}.`,
    );
    return undefined;
  }
  if (value.length !== 1) {
    report(
      "error",
      "invalid-setting",
      value.length === 0
        ? "'entrypoints' is empty. List the entrypoint to compile, or leave the key out to compile 'tsp-location' itself."
        : `'entrypoints' lists ${value.length} entrypoints; only one is supported until several can be compiled together.`,
    );
    return undefined;
  }

  const item: unknown = value[0];
  if (!isMap(item)) {
    report(
      "error",
      "invalid-setting",
      `'entrypoints[0]' must be a map of 'path' and 'api-version', not ${describeValue(item)}.`,
    );
    return undefined;
  }
  warnUnknownKeys(item, ENTRYPOINT_KEYS, "setting", report, "entrypoints[0].");

  const path = item["path"];
  const apiVersion = item["api-version"];
  let valid = true;
  if (path === undefined || path === null) {
    report("error", "missing-setting", "'entrypoints[0].path' is required.");
    valid = false;
  } else if (typeof path !== "string" || path.length === 0) {
    report("error", "invalid-setting", `'entrypoints[0].path' must be a path, not ${describeValue(path)}.`);
    valid = false;
  }
  if (
    apiVersion !== undefined &&
    apiVersion !== null &&
    (typeof apiVersion !== "string" || apiVersion.length === 0)
  ) {
    report(
      "error",
      "invalid-setting",
      "'entrypoints[0].api-version' must be a version such as '2024-05-01', 'latest' or 'all', " +
        `not ${describeValue(apiVersion)}.`,
    );
    valid = false;
  }
  if (!valid || typeof path !== "string") return undefined;

  return { path, ...(typeof apiVersion === "string" ? { apiVersion } : {}) };
}

/**
 * A YAML problem as a message and a location. The parser's message ends with the
 * location and a code frame, which the diagnostic's target already carries.
 */
function yamlProblem(problem: {
  message: string;
  linePos?: [{ line: number; col: number }] | [{ line: number; col: number }, { line: number; col: number }];
}): [string, { line: number; column: number } | undefined] {
  const message = (problem.message.split("\n")[0] ?? problem.message).replace(
    / at line \d+, column \d+:?$/,
    "",
  );
  const at = problem.linePos?.[0];
  return [message, at !== undefined ? { line: at.line, column: at.col } : undefined];
}
