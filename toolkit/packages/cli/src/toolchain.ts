/**
 * Toolchain runner: resolve which `@typespec/compiler` + emitter a spec should
 * compile against — **without loading them**.
 *
 * Hybrid resolution (design Part II, W1): prefer the spec's *project-local*
 * install; fall back to ace's *bundled* toolchain. It returns plain path strings
 * (a `ResolvedToolchain`) that the emit step later loads in its own process — so
 * resolution failures surface early and the step boundary carries data, not live
 * objects.
 *
 * Resolution != loading: this only walks `node_modules` (a filesystem read via
 * `createRequire().resolve`) and never imports the compiler. This is the
 * "path-resolve" the host owns (design §18), run before ace compile.
 */

import { statSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const COMPILER_PACKAGE = "@typespec/compiler";
/** The Azure Client Engine emitter, co-located with the compiler it binds to. */
const EMITTER_PACKAGE = "@azure-client-engine/typespec-emitter";

/** Where the toolchain was resolved from. */
export type ToolchainSource = "project-local" | "bundled";

export interface ResolvedToolchain {
  /** Whether the compiler/emitter came from the spec's install or ace's own. */
  source: ToolchainSource;
  /** Resolved `@typespec/compiler` entry path. */
  compilerPath: string;
  /** Resolved emitter entry path. */
  emitterPath: string;
  /** The staged entrypoint this toolchain will compile. */
  entrypoint: string;
}

/** Resolve a module specifier from `baseDir`, or `undefined` if not found. */
function resolveFrom(baseDir: string, specifier: string): string | undefined {
  try {
    // The sentinel file need not exist; only its directory roots resolution.
    const require = createRequire(join(baseDir, "__ace_resolver__.js"));
    return require.resolve(specifier);
  } catch {
    return undefined;
  }
}

/** Directory to root resolution at: the entry's own dir (file) or itself (dir). */
function baseDirOf(entrypoint: string): string {
  try {
    if (statSync(entrypoint).isDirectory()) return entrypoint;
  } catch {
    // Not stat-able (e.g. a not-yet-staged path); treat as a file path.
  }
  return dirname(entrypoint);
}

/**
 * Resolve the toolchain for `entrypoint`.
 *
 * Hybrid, anchored on the compiler:
 *   1. **project-local** — if the spec's tree resolves `@typespec/compiler`,
 *      use it, provided the emitter also resolves from the *same* root. (A
 *      project-local compiler with the emitter only reachable via ace would
 *      split the compiler instance — see W3 — so we do not mix.)
 *   2. **bundled** — otherwise resolve both from ace's own install.
 *
 * Throws if neither can supply a compiler + emitter (a broken install).
 */
export function resolveToolchain(entrypoint: string): ResolvedToolchain {
  const baseDir = baseDirOf(entrypoint);

  const projectCompiler = resolveFrom(baseDir, COMPILER_PACKAGE);
  if (projectCompiler !== undefined) {
    const projectEmitter = resolveFrom(baseDir, EMITTER_PACKAGE);
    if (projectEmitter !== undefined) {
      return {
        source: "project-local",
        compilerPath: projectCompiler,
        emitterPath: projectEmitter,
        entrypoint,
      };
    }
  }

  const bundledBase = dirname(fileURLToPath(import.meta.url));
  const bundledCompiler = resolveFrom(bundledBase, COMPILER_PACKAGE);
  const bundledEmitter = resolveFrom(bundledBase, EMITTER_PACKAGE);
  if (bundledCompiler !== undefined && bundledEmitter !== undefined) {
    return {
      source: "bundled",
      compilerPath: bundledCompiler,
      emitterPath: bundledEmitter,
      entrypoint,
    };
  }

  throw new Error(
    `Cannot resolve a TypeSpec toolchain for '${entrypoint}': neither the ` +
      `project nor ace provides ${COMPILER_PACKAGE} + ${EMITTER_PACKAGE}.`,
  );
}
