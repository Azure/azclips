/**
 * Host staging + provisioning (design Part II, §18).
 *
 * ace compile is TypeSpec-free and does zero git/npm/filesystem work — the host
 * (this CLI) acquires the source and provisions its dependencies *before*
 * invoking the orchestrator. This module implements the local, bundled-branch
 * increment (P2.4):
 *
 *   1. Acquire — copy the spec tree into `<out>/source/` (never mutate the user's
 *      tree), excluding `node_modules` / `.git`, and map the entrypoint to compile
 *      onto its counterpart inside the copy.
 *   2. Provision (bundled) — junction ace's own hoisted `node_modules` beside the
 *      staged spec, so the spec's library imports *and* our emitter all resolve
 *      from ace's install without the spec needing its own `npm install`.
 *
 * The staged entrypoint is then handed to `resolveToolchain` (path-resolve) and
 * the subprocess emit producer. Project-local provisioning (`npm install` +
 * emitter delivery, W3) and remote `git` sparse-clone are later increments.
 */

import { cp, mkdir, rm, symlink } from "node:fs/promises";
import { existsSync, statSync } from "node:fs";
import { createRequire } from "node:module";
import { basename, dirname, isAbsolute, join, relative, resolve, sep } from "node:path";

/** Names never copied into the staging dir — provisioning supplies deps. */
const EXCLUDED_FROM_COPY = new Set(["node_modules", ".git"]);

export interface StagedProject {
  /** The staged entrypoint (a `.tsp` file or the staged dir) to compile. */
  entrypoint: string;
  /** The staging root (`<outDir>/source`). */
  root: string;
}

/**
 * Stage a *local* spec into `<outDir>/source` and provision it with ace's
 * bundled TypeSpec toolchain.
 *
 * `tspLocation` is what gets copied: a project directory, or an entry `.tsp`
 * file whose containing directory is copied (preserving sibling files and
 * relative imports). `entrypoint` is what gets compiled: a `.tsp` file or a
 * directory under that copied directory, either absolute or relative to it, and
 * `tspLocation` itself when omitted. The returned `entrypoint` is its
 * counterpart inside the staged copy.
 *
 * Every input is validated before `<outDir>/source` is wiped, so a bad argument
 * fails without touching the filesystem.
 */
export async function stageLocalProject(
  tspLocation: string,
  outDir: string,
  entrypoint?: string,
): Promise<StagedProject> {
  const stagedRoot = resolve(outDir, "source");
  const plan = planStaging(tspLocation, entrypoint, stagedRoot);

  // Fresh staging each run so a prior compile never leaks into this one. Retry
  // on transient locks (a prior run's worker that hasn't fully released the
  // tree yet) so a leftover staged copy is reclaimed self-healingly.
  await rm(stagedRoot, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  await mkdir(stagedRoot, { recursive: true });

  await cp(plan.copyRoot, stagedRoot, {
    recursive: true,
    filter: (src) => !EXCLUDED_FROM_COPY.has(basename(src)),
  });

  await provisionBundled(stagedRoot);

  return { entrypoint: join(stagedRoot, plan.entry), root: stagedRoot };
}

interface StagingPlan {
  /** The directory copied into the staging root. */
  copyRoot: string;
  /** The entrypoint relative to `copyRoot`; `""` when it is `copyRoot` itself. */
  entry: string;
}

/** Resolve and validate what to copy and what to compile, without side effects. */
function planStaging(
  tspLocation: string,
  entrypoint: string | undefined,
  stagedRoot: string,
): StagingPlan {
  const location = resolve(tspLocation);
  const copyRoot = statSync(location).isDirectory() ? location : dirname(location);

  // A relative entrypoint resolves against the copied directory, not the cwd.
  const target = entrypoint === undefined ? location : resolve(copyRoot, entrypoint);
  if (!isWithin(copyRoot, target)) {
    throw new Error(
      `--entrypoint '${target}' is not under the --tsp-location directory '${copyRoot}'.`,
    );
  }
  if (!existsSync(target)) {
    throw new Error(
      `--entrypoint '${target}' does not exist (a relative path resolves against '${copyRoot}').`,
    );
  }
  const entry = relative(copyRoot, target);
  if (entry.split(sep).some((segment) => EXCLUDED_FROM_COPY.has(segment))) {
    throw new Error(
      `--entrypoint '${target}' is inside a directory staging does not copy ` +
        `(${[...EXCLUDED_FROM_COPY].join(", ")}).`,
    );
  }

  // Copying a tree into itself fails part-way, and wiping a staging root that
  // holds the spec would delete the spec, so neither may contain the other.
  if (isWithin(copyRoot, stagedRoot) || isWithin(stagedRoot, copyRoot)) {
    throw new Error(
      `--out-dir stages into '${stagedRoot}', which overlaps the --tsp-location ` +
        `directory '${copyRoot}' (neither may contain the other). Choose a different --out-dir.`,
    );
  }

  return { copyRoot, entry };
}

/** True when `target` is `root` itself or lies beneath it. */
function isWithin(root: string, target: string): boolean {
  const rel = relative(root, target);
  return rel === "" || (rel !== ".." && !rel.startsWith(`..${sep}`) && !isAbsolute(rel));
}

/**
 * Recursive directory removal for the transient compile artifacts
 * (`<out>/source`, `<out>/ir`). Non-fatal by design:
 *
 *   - Retries on transient locks. `fs.rm`'s built-in retry (on
 *     EBUSY/EPERM/ENOTEMPTY/...) rides out the brief window where a subprocess
 *     that just finished hasn't fully released its handles yet. (Only the emit
 *     worker touches `source/`, and it self-exits on parent disconnect / is
 *     killed past a timeout, so it never lingers to hold the tree open.)
 *   - `force: true` makes a missing directory a no-op (no throw), so callers can
 *     invoke it unconditionally to clear a possibly-absent stale artifact.
 *   - A Windows `node_modules` junction (as under `source/`) is unlinked, not
 *     recursed into — ace's real install is never touched (verified behavior).
 *   - Returns the outcome instead of throwing: a leftover artifact must never
 *     fail an otherwise-successful compile, and the next run reclaims it.
 */
export async function removeDir(dir: string): Promise<{ removed: boolean; error?: Error }> {
  try {
    await rm(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
    return { removed: true };
  } catch (error) {
    return { removed: false, error: error instanceof Error ? error : new Error(String(error)) };
  }
}

/**
 * Bundled provisioning: junction ace's hoisted `node_modules` into the staged
 * dir so the spec compiles against ace's compiler + libraries + emitter (a
 * single toolchain, one `@typespec/compiler` instance). A junction (not a copy)
 * is instant and, resolving to ace's real files, guarantees the single instance.
 *
 * Skips linking if the staged dir already has a `node_modules` (e.g. a future
 * project-local branch that installed its own), keeping this hybrid-friendly.
 */
async function provisionBundled(stagedRoot: string): Promise<void> {
  const link = join(stagedRoot, "node_modules");
  if (existsSync(link)) return;
  await symlink(aceNodeModulesRoot(), link, "junction");
}

/**
 * Locate ace's hoisted `node_modules` root — the one carrying `@typespec/compiler`
 * (and, by hoisting, every TypeSpec/Azure library plus the emitter). Resolves the
 * compiler's entry from this module's location and walks up to the enclosing
 * `node_modules`.
 */
function aceNodeModulesRoot(): string {
  const require = createRequire(import.meta.url);
  let dir = dirname(require.resolve("@typespec/compiler"));
  while (basename(dir) !== "node_modules") {
    const parent = dirname(dir);
    if (parent === dir) {
      throw new Error(
        "Cannot locate ace's node_modules (containing @typespec/compiler) to " +
          "provision the staged spec. Is the toolkit installed?",
      );
    }
    dir = parent;
  }
  return dir;
}
