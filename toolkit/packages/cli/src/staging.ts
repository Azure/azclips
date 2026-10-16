/**
 * Host staging + provisioning (design Part II, §18).
 *
 * ace compile is TypeSpec-free and does zero git/npm/filesystem work — the host
 * (this CLI) acquires the source and provisions its dependencies *before*
 * invoking the orchestrator. This module implements the bundled branch (P2.4):
 *
 *   1. Fetch — a GitHub URL, as `tsp-location` or an additional directory, is
 *      fetched into a temporary checkout first (`remote-source.ts`), which the
 *      copy then reads as it reads a local checkout. A remote `tsp-location`'s
 *      checkout stands for its repo root.
 *   2. Acquire — copy the spec into `<out>/source/` (never mutate the user's tree),
 *      keeping the specs repo's layout. `<out>/source` stands for the repo root:
 *      `tsp-location` and each additional directory land at their paths relative to
 *      it, so a spec's import of a sibling folder (`../Shared`) keeps working. A
 *      folder outside the repo lands directly under `<out>/source`, by name.
 *      `node_modules`, `.git` and `tsp-output` are never copied, even when listed.
 *      The entrypoint maps onto its counterpart through the copied folder that
 *      carries it. Staging treats it as a path: what TypeSpec makes of it (a
 *      folder compiles its `main.tsp`) is the emit side's concern.
 *   3. Provision (bundled) — junction ace's own hoisted `node_modules` at
 *      `<out>/source/node_modules`, above every copied folder, so the spec's
 *      library imports *and* our emitter all resolve from ace's install without the
 *      spec needing its own `npm install`.
 *
 * ACE reads no `tspconfig.yaml`: the folders to copy come from the config file. The
 * staged entrypoint is then handed to `resolveToolchain` (path-resolve) and the
 * subprocess emit producer. Project-local provisioning (`npm install` + emitter
 * delivery, W3) is a later increment.
 */

import { cp, mkdir, rm, symlink } from "node:fs/promises";
import { existsSync, statSync } from "node:fs";
import { createRequire } from "node:module";
import { basename, dirname, isAbsolute, join, relative, resolve, sep } from "node:path";

import type { CompilerDiagnostic } from "@azure-client-engine/compiler";

import { fetchSources, isUrl, type FetchedSource, type FetchedSources } from "./remote-source.js";

/** Names never copied into the staging dir: provisioning supplies deps, and tsp-output is emitter output. */
const EXCLUDED_FROM_COPY = new Set(["node_modules", ".git", "tsp-output"]);

/** The folders that mark the root of an azure-rest-api-specs checkout (AzDev's `IsSpecRoot`). */
const SPECS_REPO_MARKERS = ["specification", ".github", ".azure-pipelines"] as const;

/** Set to `1` to stage a spec from outside any specs repo, copied flat as before. */
export const STANDALONE_SPEC_VARIABLE = "ACE_ALLOW_STANDALONE_SPEC";

const DIAGNOSTIC_PREFIX = "@azure-client-engine/cli";

/** What to stage. */
export interface StagingRequest {
  /**
   * The folder to copy, or a `.tsp` file whose folder is copied: a local path, or a
   * GitHub URL (`https://github.com/<owner>/<repo>/tree/<ref>/<path>`).
   */
  readonly tspLocation: string;
  /** The output folder; the copy goes to `<outDir>/source`. */
  readonly outDir: string;
  /**
   * What to compile: a `.tsp` file or folder under the copied folder, relative to it
   * or absolute. The `tsp-location` itself when absent.
   */
  readonly entrypoint?: string | undefined;
  /**
   * Folders the compile needs that are not under `tsp-location`: relative to the
   * copied folder, absolute, or GitHub URLs.
   */
  readonly additionalDirectories?: readonly string[] | undefined;
  /**
   * Stage a local `tsp-location` outside any specs repo by copying it flat into
   * `<outDir>/source`, as before the repo's layout was kept. For tests and ad-hoc
   * specs; it has no effect inside a specs repo, or on a GitHub URL.
   */
  readonly allowStandaloneSpec?: boolean | undefined;
}

export interface StagedProject {
  /** The staged entrypoint (a `.tsp` file or the staged dir) to compile. */
  entrypoint: string;
  /** The staging root (`<outDir>/source`). */
  root: string;
  /** What was fetched from GitHub, one entry per repo and ref; empty for local sources. */
  fetched: readonly FetchedSource[];
}

/**
 * Stage a spec into `<outDir>/source` and provision it with ace's bundled TypeSpec
 * toolchain. GitHub URLs are fetched first, into temporary checkouts that are deleted
 * once copied.
 *
 * Every input is checked, and every problem reported, before `<outDir>/source` is
 * wiped, so a bad input fails without touching the filesystem. Returns `undefined`
 * when any problem was reported.
 */
export async function stageProject(
  request: StagingRequest,
  reportDiagnostic: (diagnostic: CompilerDiagnostic) => void,
): Promise<StagedProject | undefined> {
  const report =
    (severity: CompilerDiagnostic["severity"]) =>
    (code: string, message: string): void =>
      reportDiagnostic({ severity, code: `${DIAGNOSTIC_PREFIX}/${code}`, message });

  const sources = await fetchSources(
    request.tspLocation,
    request.additionalDirectories ?? [],
    report("error"),
    report("warning"),
  );
  if (sources === undefined) return undefined;
  try {
    const stagedRoot = resolve(request.outDir, "source");
    const local: StagingRequest = {
      ...request,
      tspLocation: sources.tspLocation,
      additionalDirectories: sources.additionalDirectories,
    };
    const plan = planStaging(local, stagedRoot, sources, report("error"));
    if (plan === undefined) return undefined;

    // Fresh staging each run so a prior compile never leaks into this one. Retry
    // on transient locks (a prior run's worker that hasn't fully released the
    // tree yet) so a leftover staged copy is reclaimed self-healingly.
    await rm(stagedRoot, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
    await mkdir(stagedRoot, { recursive: true });

    for (const folder of plan.folders) {
      await mkdir(dirname(folder.target), { recursive: true });
      await cp(folder.source, folder.target, {
        recursive: true,
        filter: (src) => !EXCLUDED_FROM_COPY.has(basename(src)),
      });
    }

    await provisionBundled(stagedRoot);

    return { entrypoint: plan.entrypoint, root: stagedRoot, fetched: sources.fetched };
  } finally {
    await sources.dispose();
  }
}

/**
 * The root of the azure-rest-api-specs checkout holding `path`: the first folder at
 * or above it with `specification/`, `.github/` and `.azure-pipelines/`. No `.git` is
 * needed, so zip downloads and worktrees count.
 */
export function findSpecsRepoRoot(path: string): string | undefined {
  let dir = resolve(path);
  for (;;) {
    if (SPECS_REPO_MARKERS.every((marker) => isDirectory(join(dir, marker)))) return dir;
    const parent = dirname(dir);
    if (parent === dir) return undefined;
    dir = parent;
  }
}

/** One folder to copy, and where it lands. */
interface CopiedFolder {
  readonly source: string;
  readonly target: string;
}

interface StagingPlan {
  /** The folders to copy; no two of them, or of their targets, overlap. */
  readonly folders: readonly CopiedFolder[];
  /** The entrypoint's counterpart in the copy. */
  readonly entrypoint: string;
}

type Fail = (code: string, message: string) => void;

/** A path as messages name it: a fetched one by its GitHub URL. */
type Show = (path: string) => string;

/**
 * Resolve and check what to copy, where each folder lands, and what to compile,
 * without side effects. `sources` knows a fetched tsp-location's repo root, which no
 * marker folder shows. Every problem is reported; returns `undefined` when there was
 * any.
 */
function planStaging(
  request: StagingRequest,
  stagedRoot: string,
  sources: Pick<FetchedSources, "repoRoot" | "show">,
  report: Fail,
): StagingPlan | undefined {
  let failed = false;
  const fail: Fail = (code, message) => {
    failed = true;
    report(code, message);
  };
  const { show } = sources;

  const location = resolve(request.tspLocation);
  const stat = statSync(location, { throwIfNoEntry: false });
  if (stat === undefined) {
    fail("invalid-tsp-location", `tsp-location '${show(location)}' does not exist.`);
    return undefined;
  }
  const copyRoot = stat.isDirectory() ? location : dirname(location);
  const entrypoint = checkEntrypoint(request.entrypoint, location, copyRoot, show, fail);
  const additional = request.additionalDirectories ?? [];

  let folders: CopiedFolder[];
  const repoRoot = sources.repoRoot ?? findSpecsRepoRoot(copyRoot);
  if (repoRoot === undefined) {
    if (request.allowStandaloneSpec !== true) {
      fail(
        "not-in-specs-repo",
        `tsp-location '${copyRoot}' is not inside an azure-rest-api-specs checkout: no folder at ` +
          "or above it holds specification/, .github/ and .azure-pipelines/. " +
          `Set ${STANDALONE_SPEC_VARIABLE}=1 to stage a spec from outside one, copied flat.`,
      );
    } else if (additional.length > 0) {
      fail(
        "invalid-additional-directory",
        `additional-directories need a specs repo to be placed against, and tsp-location '${copyRoot}' ` +
          "is outside any.",
      );
    }
    // Standalone: copied flat, as before the repo's layout was kept.
    folders = [{ source: copyRoot, target: stagedRoot }];
  } else {
    // A folder inside the repo lands at its path relative to the repo root, which
    // keeps its position relative to tsp-location; that is what imports rely on.
    const mirrored = (path: string): string => join(stagedRoot, relative(repoRoot, path));
    const candidates: CopiedFolder[] = [{ source: copyRoot, target: mirrored(copyRoot) }];
    for (const written of additional) {
      const folder = checkAdditionalDirectory(written, copyRoot, show, fail);
      if (folder === undefined) continue;
      // Outside the repo there is no position to keep: it lands at the copy's root, by name.
      const target = isWithin(repoRoot, folder) ? mirrored(folder) : join(stagedRoot, basename(folder));
      candidates.push({ source: folder, target });
    }
    folders = outermost(candidates);
    checkTargets(folders, show, fail);
  }

  // Copying a tree into itself fails part-way, and wiping a staging root that holds
  // a copied folder would delete it, so neither may contain the other.
  for (const folder of folders) {
    if (isWithin(folder.source, stagedRoot) || isWithin(stagedRoot, folder.source)) {
      fail(
        "staging-overlap",
        `The output folder stages into '${stagedRoot}', which overlaps '${show(folder.source)}' ` +
          "(neither may contain the other). Choose a different output folder.",
      );
    }
  }

  const stagedEntrypoint =
    entrypoint === undefined ? undefined : stageEntrypoint(entrypoint, folders, show, fail);
  return failed || stagedEntrypoint === undefined
    ? undefined
    : { folders, entrypoint: stagedEntrypoint };
}

/**
 * The entrypoint, checked: under the copied folder, and present. Staging treats it
 * as a path; what TypeSpec makes of it is the emit side's concern.
 */
function checkEntrypoint(
  entrypoint: string | undefined,
  location: string,
  copyRoot: string,
  show: Show,
  fail: Fail,
): string | undefined {
  if (entrypoint !== undefined && isUrl(entrypoint)) {
    fail(
      "invalid-entrypoint",
      `The entrypoint '${entrypoint}' is a URL; name it by its path under tsp-location, relative to it.`,
    );
    return undefined;
  }
  // A relative entrypoint resolves against the copied folder, not the cwd.
  const target = entrypoint === undefined ? location : resolve(copyRoot, entrypoint);
  if (!isWithin(copyRoot, target)) {
    fail(
      "invalid-entrypoint",
      `The entrypoint '${show(target)}' is not under the tsp-location folder '${show(copyRoot)}'.`,
    );
    return undefined;
  }
  if (!existsSync(target)) {
    fail(
      "invalid-entrypoint",
      `The entrypoint '${show(target)}' does not exist (a relative path resolves against '${show(copyRoot)}').`,
    );
    return undefined;
  }
  return target;
}

/**
 * The entrypoint's counterpart in the copy, through the copied folder that carries
 * it: tsp-location's folder, or a listed folder that holds it.
 */
function stageEntrypoint(
  entrypoint: string,
  folders: readonly CopiedFolder[],
  show: Show,
  fail: Fail,
): string | undefined {
  for (const folder of folders) {
    if (!isWithin(folder.source, entrypoint)) continue;
    const path = relative(folder.source, entrypoint);
    // The copy skips these names wherever they are, the copied folder's own included.
    if ([basename(folder.source), ...path.split(sep)].some((segment) => EXCLUDED_FROM_COPY.has(segment))) {
      fail(
          "invalid-entrypoint",
          `The entrypoint '${show(entrypoint)}' is inside a folder staging does not copy ` +
            `(${[...EXCLUDED_FROM_COPY].join(", ")}).`,
      );
      return undefined;
    }
    return join(folder.target, path);
  }
  // Unreachable: the entrypoint is under tsp-location's folder, which is copied itself
  // or as part of a listed folder that holds it.
  throw new Error(`No copied folder holds the entrypoint '${entrypoint}'.`);
}

/**
 * An additional directory, checked: an existing folder the compile needs that is not
 * under `tsp-location`. Returns its absolute path, or `undefined` after reporting.
 */
function checkAdditionalDirectory(written: string, copyRoot: string, show: Show, fail: Fail): string | undefined {
  // resolve() keeps an absolute path as is; a relative one is relative to tsp-location.
  const folder = resolve(copyRoot, written);
  const named = `The additional directory '${show(written)}'`;
  const stat = statSync(folder, { throwIfNoEntry: false });
  if (stat === undefined) {
    fail(
      "invalid-additional-directory",
      `${named} does not exist (looked for '${show(folder)}'; a relative path resolves against ` +
        `'${show(copyRoot)}').`,
    );
    return undefined;
  }
  if (!stat.isDirectory()) {
    fail("invalid-additional-directory", `${named} is not a folder: '${show(folder)}'.`);
    return undefined;
  }
  if (isWithin(copyRoot, folder)) {
    fail(
      "invalid-additional-directory",
      `${named} is tsp-location '${show(copyRoot)}' or under it, and tsp-location is copied whole. ` +
        "List only folders the compile needs outside it.",
    );
    return undefined;
  }
  return folder;
}

/**
 * Drop each folder another one contains: it is copied as part of the outer folder.
 * Of two equal folders, the first is kept.
 */
function outermost(folders: readonly CopiedFolder[]): CopiedFolder[] {
  return folders.filter(
    (folder, index) =>
      !folders.some(
        (other, otherIndex) =>
          otherIndex !== index &&
          isWithin(other.source, folder.source) &&
          (!isWithin(folder.source, other.source) || otherIndex < index),
      ),
  );
}

/**
 * Every folder needs a place of its own in the copy. Two whose places nest collide:
 * two external folders with one name, or one named after a folder the repo's layout
 * already uses. Copied together, one's files would silently overwrite the other's.
 */
function checkTargets(folders: readonly CopiedFolder[], show: Show, fail: Fail): void {
  for (const [index, folder] of folders.entries()) {
    for (const other of folders.slice(index + 1)) {
      if (isWithin(folder.target, other.target) || isWithin(other.target, folder.target)) {
        fail(
          "staging-collision",
          `'${show(folder.source)}' and '${show(other.source)}' would land in the same place in the copy ` +
            `('${folder.target}' and '${other.target}').`,
        );
      }
    }
  }
}

function isDirectory(path: string): boolean {
  return statSync(path, { throwIfNoEntry: false })?.isDirectory() === true;
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
