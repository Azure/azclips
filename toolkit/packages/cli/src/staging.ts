/**
 * Host staging + provisioning (design Part II, §18).
 *
 * ace compile is TypeSpec-free and does zero git/npm/filesystem work — the host
 * (this CLI) acquires the source and provisions its dependencies *before*
 * invoking the orchestrator. This module implements the local, bundled-branch
 * increment (P2.4):
 *
 *   1. Acquire — copy the spec tree into `<out>/source/` (never mutate the user's
 *      tree), excluding `node_modules` / `.git`.
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
import { basename, dirname, join, resolve } from "node:path";

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
 * `tspLocation` may be a project directory or a single entry `.tsp` file; either
 * way the whole containing tree is copied (to preserve sibling files and
 * relative imports) and the returned `entrypoint` points inside the staged copy.
 */
export async function stageLocalProject(
  tspLocation: string,
  outDir: string,
): Promise<StagedProject> {
  const stagedRoot = resolve(outDir, "source");

  // Fresh staging each run so a prior compile never leaks into this one. Retry
  // on transient locks (a prior run's worker that hasn't fully released the
  // tree yet) so a leftover staged copy is reclaimed self-healingly.
  await rm(stagedRoot, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  await mkdir(stagedRoot, { recursive: true });

  const isDir = statSync(tspLocation).isDirectory();
  const sourceDir = isDir ? tspLocation : dirname(tspLocation);

  await cp(sourceDir, stagedRoot, {
    recursive: true,
    filter: (src) => !EXCLUDED_FROM_COPY.has(basename(src)),
  });

  await provisionBundled(stagedRoot);

  const entrypoint = isDir ? stagedRoot : join(stagedRoot, basename(tspLocation));
  return { entrypoint, root: stagedRoot };
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
