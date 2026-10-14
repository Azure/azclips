/**
 * Resolve the document facts — what the spec cannot provide — into the IR.
 *
 * The facts are the gen-time config left at the top level once the host and the
 * compiler have taken their own keys: the root namespace, the project name, the
 * output directories, and the extension's service name, version and description.
 * This step is the one place they become IR, so every later stage reads them from
 * the document rather than from config, and a captured IR explains the output's
 * identity.
 *
 * Today they all land in `target`. A missing fact gets its default: the root
 * namespace is the first source's own namespace (else `Generated`), the project is
 * named after the root namespace, and the directories are `Models` and
 * `Processors`. The service name, version and description stay unset.
 *
 * ## Checking its config
 *
 * The facts arrive as written in config, with kebab-case keys, and unchecked. A
 * fact with the wrong type or an empty value is an error, and the stage fails: every
 * fact is still checked, so all such errors surface together, but no target is
 * written and the compiler stops the run. A default stands in only for a missing
 * fact, never for an invalid one. A key this step does not know is a warning: its
 * config is everything nobody else took at the top level, so that is how a
 * misspelled top-level key surfaces. A key with no value (`null`) counts as absent.
 *
 * ## What it must not do
 *
 * It writes only top-level IR nodes. It never renames anything and never touches
 * ids or spec-derived content, so `tweak-ir` stays the only renaming stage.
 */

import type { TransformContext, TransformStep } from "./transform.js";
import { describeValue, isMap, warnUnknownKeys } from "./stage-config.js";
import type { IR, IRTarget, IRTargetDirectories } from "@azure-client-engine/ir";

const STEP_NAME = "seed-ir";
const DIAGNOSTIC_PREFIX = `@azure-client-engine/transformers/${STEP_NAME}`;

/** Fallback when a document declares no source to take a namespace from. */
const DEFAULT_ROOT_NAMESPACE: readonly string[] = ["Generated"];

const DEFAULT_DIRECTORIES: Readonly<IRTargetDirectories> = {
  models: "Models",
  processors: "Processors",
};

// ─── Facts ──────────────────────────────────────────────────────────────

/** The facts this step reads, keyed as written in config. */
export interface SeedIrFacts {
  /** The service the extension is generated for (`ElasticSan`). */
  "service-name"?: string;
  /** Dotted root namespace of the generated code. Defaults to the first source's. */
  "root-namespace"?: string;
  /** Generated project (and assembly) name. Defaults to the root namespace. */
  "project-name"?: string;
  /** Version of the generated extension (`0.1.0`). */
  version?: string;
  /** Description of the generated extension. */
  description?: string;
  /** Relative output directories. Default to `Models` and `Processors`. */
  directories?: Partial<IRTargetDirectories>;
}

const FACT_KEYS: ReadonlySet<string> = new Set<keyof SeedIrFacts>([
  "service-name",
  "root-namespace",
  "project-name",
  "version",
  "description",
  "directories",
]);

const DIRECTORY_KEYS: ReadonlySet<string> = new Set<keyof IRTargetDirectories>([
  "models",
  "processors",
]);

type Report = (severity: "error" | "warning", code: string, message: string) => void;

/** The `seed-ir` stage. Its stage config is the document facts. */
export const seedIrStep: TransformStep = {
  name: STEP_NAME,
  run(ir: IR, context: TransformContext): IR {
    let failed = false;
    const report: Report = (severity, code, message) => {
      if (severity === "error") failed = true;
      context.reportDiagnostic({ severity, code: `${DIAGNOSTIC_PREFIX}/${code}`, message });
    };

    const target = resolveTarget(ir, context.config, report);
    // An invalid fact fails the stage. No default stands in for a value the config
    // got wrong, so nothing is written, and the compiler stops the run.
    if (!failed) ir.target = target;
    return ir;
  },
};

// ─── Target ─────────────────────────────────────────────────────────────

function resolveTarget(ir: IR, facts: Readonly<Record<string, unknown>>, report: Report): IRTarget {
  warnUnknownKeys(facts, FACT_KEYS, "fact", report);
  const read = factReader(facts, "", report);

  const rootNamespace = read("root-namespace", namespaceSegments, DOTTED_NAMESPACE) ?? sourceNamespace(ir);
  const projectName = read("project-name", nonEmptyString, NON_EMPTY_STRING) ?? rootNamespace.join(".");
  const directories = resolveDirectories(facts, report);
  const serviceName = read("service-name", nonEmptyString, NON_EMPTY_STRING);
  const version = read("version", nonEmptyString, NON_EMPTY_STRING);
  const description = read("description", nonEmptyString, NON_EMPTY_STRING);

  return {
    rootNamespace,
    projectName,
    directories,
    ...(serviceName !== undefined ? { serviceName } : {}),
    ...(version !== undefined ? { version } : {}),
    ...(description !== undefined ? { description } : {}),
  };
}

/** The primary source's own namespace, so a document generates somewhere sensible unconfigured. */
function sourceNamespace(ir: IR): string[] {
  const source = ir.sources[0];
  return source !== undefined && source.rootNamespace.length > 0
    ? [...source.rootNamespace]
    : [...DEFAULT_ROOT_NAMESPACE];
}

function resolveDirectories(facts: Record<string, unknown>, report: Report): IRTargetDirectories {
  const value = facts["directories"];
  if (value === undefined || value === null) return { ...DEFAULT_DIRECTORIES };
  if (!isMap(value)) {
    report(
      "error",
      "invalid-fact",
      `'directories' must be a map of 'models' and 'processors', not ${describeValue(value)}.`,
    );
    return { ...DEFAULT_DIRECTORIES };
  }

  warnUnknownKeys(value, DIRECTORY_KEYS, "fact", report, "directories.");
  const read = factReader(value, "directories.", report);
  return {
    models: read("models", nonEmptyString, NON_EMPTY_STRING) ?? DEFAULT_DIRECTORIES.models,
    processors: read("processors", nonEmptyString, NON_EMPTY_STRING) ?? DEFAULT_DIRECTORIES.processors,
  };
}

// ─── Reading ────────────────────────────────────────────────────────────

const NON_EMPTY_STRING = "a non-empty string";
const DOTTED_NAMESPACE = "a dotted namespace such as 'Contoso.Widgets'";

/**
 * Reads one fact of `facts`. A missing or `null` value is absent. An invalid one is
 * reported, which fails the stage, and is then read as absent so that the remaining
 * facts are still checked.
 */
type ReadFact = <T>(key: string, parse: (value: unknown) => T | undefined, expected: string) => T | undefined;

/** `prefix` places a nested map in the config (`directories.`), so messages name the full key. */
function factReader(facts: Record<string, unknown>, prefix: string, report: Report): ReadFact {
  return (key, parse, expected) => {
    const value = facts[key];
    if (value === undefined || value === null) return undefined;

    const parsed = parse(value);
    if (parsed === undefined) {
      report(
        "error",
        "invalid-fact",
        `'${prefix}${key}' must be ${expected}, not ${describeValue(value)}.`,
      );
    }
    return parsed;
  };
}

function nonEmptyString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function namespaceSegments(value: unknown): string[] | undefined {
  if (typeof value !== "string") return undefined;
  const segments = value.split(".");
  return segments.every((segment) => segment.length > 0) ? segments : undefined;
}
