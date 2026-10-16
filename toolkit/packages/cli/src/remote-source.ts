/**
 * Remote sources: spec folders on GitHub, fetched so that staging can copy them.
 *
 * `tsp-location` and each `additional-directories` entry may be a GitHub URL:
 * `https://github.com/<owner>/<repo>/tree/<ref>/<path>` names a folder, and
 * `.../blob/<ref>/<path>` a file, as GitHub's own links do. Each repo and ref is
 * fetched once: one commit, holding only the folders listed for it (a cone-mode
 * sparse checkout), in a temporary folder that staging copies from as from a local
 * checkout.
 *
 * A remote `tsp-location`'s root is its repo at its ref, so the fetch it is in stands
 * for the repo root, and an entry in that fetch lands at its repo-relative path. Every
 * other fetch holds external folders, which land by name.
 *
 * git does the fetching, with the user's own configuration and credentials. It never
 * prompts in the terminal, so a repo it cannot read fails instead of waiting.
 */

import { statSync } from "node:fs";
import { mkdir, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, isAbsolute, join, posix, relative, sep } from "node:path";

import { runCommand, timeoutFromEnv } from "./subprocess.js";

const GITHUB = "https://github.com/";

/** The URL form, for messages. */
export const GITHUB_URL_FORM = `${GITHUB}<owner>/<repo>/tree/<ref>/<path>`;

/** Hang guard for each git command, overridable for slow networks and large specs. */
export const FETCH_TIMEOUT_VARIABLE = "ACE_FETCH_TIMEOUT_MS";
const DEFAULT_FETCH_TIMEOUT_MS = 10 * 60_000;

/** A ref that pins its files: a full commit. */
const FULL_COMMIT = /^[0-9a-f]{40}$/i;

/** A folder or file in a GitHub repo, at a ref. */
export interface GitHubLocation {
  readonly owner: string;
  readonly repo: string;
  /** A commit, branch or tag, as written. */
  readonly ref: string;
  /** The path in the repo, `/`-separated; empty for the repo root. */
  readonly path: string;
  /** True for a file: a `blob` URL, or a path ending in `.tsp`. */
  readonly isFile: boolean;
}

/** True when `value` is a URL, not a path: it starts with a scheme, such as `https://`. */
export function isUrl(value: string): boolean {
  return /^[a-z][a-z0-9+.-]*:\/\//i.test(value);
}

/**
 * Read a GitHub URL, or say what is wrong with it: the problem is a clause that
 * follows the URL in a message.
 */
export function parseGitHubUrl(url: string): { location: GitHubLocation } | { problem: string } {
  if (!url.toLowerCase().startsWith(GITHUB)) {
    return { problem: `is not on ${GITHUB}, the only host ACE fetches from` };
  }
  if (/[?#]/.test(url)) return { problem: "has a query or fragment; drop it" };

  let segments: string[];
  try {
    // One trailing slash is allowed, as browsers write folder links.
    segments = url.slice(GITHUB.length).replace(/\/$/, "").split("/").map(decodeURIComponent);
  } catch {
    return { problem: "has malformed percent-encoding" };
  }
  const [owner, repo, kind, ref, ...path] = segments;
  if (owner === undefined || repo === undefined || kind === undefined || ref === undefined) {
    return { problem: `names no ref: write ${GITHUB_URL_FORM}` };
  }
  if (kind !== "tree" && kind !== "blob") {
    return { problem: `has '/${kind}/' where '/tree/' (a folder) or '/blob/' (a file) goes` };
  }
  if (segments.some((segment) => segment === "" || segment === "." || segment === "..")) {
    return { problem: "has an empty, '.' or '..' segment" };
  }
  if (segments.some((segment) => segment.includes("/") || segment.includes("\\"))) {
    return { problem: "has an encoded '/' or '\\' in a segment" };
  }
  // GitHub's own rules, which also keep the repo name safe as a folder name.
  if (!/^[A-Za-z0-9-]+$/.test(owner) || !/^[A-Za-z0-9._-]+$/.test(repo)) {
    return { problem: `names '${owner}/${repo}', which is not a GitHub owner and repository` };
  }
  // The ref is passed to git as an argument of its own.
  if (ref.startsWith("-")) return { problem: `names the ref '${ref}', which no branch, tag or commit can be` };
  if (kind === "blob" && path.length === 0) return { problem: "names a file but no path" };

  return {
    location: {
      owner,
      repo,
      ref,
      path: path.join("/"),
      isFile: kind === "blob" || path[path.length - 1]?.endsWith(".tsp") === true,
    },
  };
}

/** The GitHub URL of `path` (`/`-separated) in a repo at a ref. */
export function gitHubUrl(location: Omit<GitHubLocation, "path" | "isFile">, path: string, isFile: boolean): string {
  const tail = path === "" ? "" : `/${path.split("/").map(encodeURIComponent).join("/")}`;
  const kind = isFile ? "blob" : "tree";
  return `${GITHUB}${location.owner}/${location.repo}/${kind}/${encodeURIComponent(location.ref)}${tail}`;
}

/** One repo and ref fetched for a run. */
export interface FetchedSource {
  readonly owner: string;
  readonly repo: string;
  /** The ref, as written. */
  readonly ref: string;
  /** The commit fetched. */
  readonly commit: string;
  /** The folders fetched, as repo paths; empty for the whole repo. */
  readonly folders: readonly string[];
}

/** A staging request's sources, with each GitHub URL replaced by what was fetched for it. */
export interface FetchedSources {
  /** `tsp-location`: the local one, or its counterpart in its fetched repo. */
  readonly tspLocation: string;
  /** The additional directories: a local one as written, a fetched one as an absolute path. */
  readonly additionalDirectories: readonly string[];
  /** The folder standing for a remote `tsp-location`'s repo root; absent for a local one. */
  readonly repoRoot?: string;
  /** What was fetched, one entry per repo and ref; empty when nothing was remote. */
  readonly fetched: readonly FetchedSource[];
  /** A path for messages: the GitHub URL of a fetched path, else the path itself. */
  readonly show: (path: string) => string;
  /** Delete what was fetched. Best effort. */
  readonly dispose: () => Promise<void>;
}

type Report = (code: string, message: string) => void;

/** A temporary checkout of one commit, holding the folders asked for. */
interface Checkout {
  /** It stands for the repo root, and is named after the repo. */
  readonly root: string;
  readonly commit: string;
}

/** One repo at one ref to fetch, shared by every URL that names them. */
interface Fetch {
  /** The first location naming it. */
  readonly location: GitHubLocation;
  /** That location's URL, as written. */
  readonly url: string;
  /** Repo paths of the folders to fetch. */
  readonly folders: Set<string>;
  checkout?: Checkout;
}

/**
 * Fetch every GitHub URL among `tsp-location` and the additional directories, and
 * say where each landed. Every problem is reported; returns `undefined` when there
 * was any, having deleted whatever was fetched. Nothing is fetched for local paths.
 */
export async function fetchSources(
  tspLocation: string,
  additionalDirectories: readonly string[],
  fail: Report,
  warn: Report,
): Promise<FetchedSources | undefined> {
  let failed = false;
  const report: Report = (code, message) => {
    failed = true;
    fail(code, message);
  };

  const fetches: Fetch[] = [];
  const fetchOf = (location: GitHubLocation, url: string): Fetch => {
    let fetch = fetches.find((other) => sameRepoAndRef(other.location, location));
    if (fetch === undefined) {
      fetch = { location, url, folders: new Set() };
      fetches.push(fetch);
    }
    return fetch;
  };

  let root: { fetch: Fetch; location: GitHubLocation; folder: string } | undefined;
  if (isUrl(tspLocation)) {
    const parsed = parseGitHubUrl(tspLocation);
    if ("problem" in parsed) {
      fail("invalid-tsp-location", `tsp-location '${tspLocation}' ${parsed.problem}.`);
      return undefined;
    }
    const { location } = parsed;
    // A file stands for its folder, which is fetched whole.
    const folder = location.isFile ? parentPath(location.path) : location.path;
    root = { fetch: fetchOf(location, tspLocation), location, folder };
    root.fetch.folders.add(folder);
  }

  // A fetched entry is known by its fetch and repo path until it is fetched.
  const entries: (string | { fetch: Fetch; path: string })[] = [];
  for (const written of additionalDirectories) {
    const named = `The additional directory '${written}'`;
    if (isUrl(written)) {
      const parsed = parseGitHubUrl(written);
      if ("problem" in parsed) {
        report("invalid-additional-directory", `${named} ${parsed.problem}.`);
        continue;
      }
      if (parsed.location.isFile) {
        report("invalid-additional-directory", `${named} names a file; a folder's URL has '/tree/'.`);
        continue;
      }
      // In tsp-location's repo at its ref, it shares that fetch and is under the root;
      // any other URL is fetched on its own, as an external folder.
      const fetch = fetchOf(parsed.location, written);
      fetch.folders.add(parsed.location.path);
      entries.push({ fetch, path: parsed.location.path });
    } else if (root !== undefined && !isAbsolute(written)) {
      // Relative to tsp-location within its repo, whose root is as far up as it goes.
      const path = posix.join(root.folder, written.replaceAll("\\", "/")).replace(/\/$/, "");
      if (path === ".." || path.startsWith("../")) {
        const { owner, repo } = root.location;
        report(
          "invalid-additional-directory",
          `${named} climbs above the root of ${owner}/${repo}, the repo of tsp-location '${tspLocation}'.`,
        );
        continue;
      }
      const repoPath = path === "." ? "" : path;
      root.fetch.folders.add(repoPath);
      entries.push({ fetch: root.fetch, path: repoPath });
    } else {
      // A local folder, which staging resolves and places.
      entries.push(written);
    }
  }
  if (failed) return undefined;

  const dispose = async (): Promise<void> => {
    await Promise.all(fetches.map((fetch) => (fetch.checkout ? removeCheckout(fetch.checkout.root) : undefined)));
  };
  for (const fetch of fetches) {
    try {
      fetch.checkout = await checkOut(fetch.location, [...fetch.folders]);
    } catch (error) {
      report("remote-fetch-failed", fetchFailure(fetch, error));
    }
  }
  if (failed) {
    await dispose();
    return undefined;
  }

  for (const fetch of fetches) {
    const { location } = fetch;
    if (FULL_COMMIT.test(location.ref)) continue;
    const commit = checkoutOf(fetch).commit;
    const pinned = gitHubUrl({ ...location, ref: commit }, location.path, location.isFile);
    warn(
      "unpinned-ref",
      `'${fetch.url}' names '${location.ref}', not a commit, so another run may fetch other files. ` +
        `This one fetched ${commit}; pin it with '${pinned}'.`,
    );
  }

  const inCheckout = (fetch: Fetch, path: string): string =>
    join(checkoutOf(fetch).root, ...path.split("/").filter((segment) => segment !== ""));
  const show = (path: string): string => {
    if (!isAbsolute(path)) return path;
    for (const fetch of fetches) {
      const rel = relative(checkoutOf(fetch).root, path);
      if (rel === ".." || rel.startsWith(`..${sep}`) || isAbsolute(rel)) continue;
      const repoPath = rel.split(sep).join("/");
      const stat = statSync(path, { throwIfNoEntry: false });
      return gitHubUrl(fetch.location, repoPath, stat === undefined ? repoPath.endsWith(".tsp") : stat.isFile());
    }
    return path;
  };

  return {
    tspLocation: root !== undefined ? inCheckout(root.fetch, root.location.path) : tspLocation,
    additionalDirectories: entries.map((entry) =>
      typeof entry === "string" ? entry : inCheckout(entry.fetch, entry.path),
    ),
    ...(root !== undefined ? { repoRoot: checkoutOf(root.fetch).root } : {}),
    fetched: fetches.map((fetch) => ({
      owner: fetch.location.owner,
      repo: fetch.location.repo,
      ref: fetch.location.ref,
      commit: checkoutOf(fetch).commit,
      folders: [...fetch.folders],
    })),
    show,
    dispose,
  };
}

/** GitHub ignores the case of owner and repo names; a ref is compared as written. */
function sameRepoAndRef(a: GitHubLocation, b: GitHubLocation): boolean {
  return (
    a.owner.toLowerCase() === b.owner.toLowerCase() &&
    a.repo.toLowerCase() === b.repo.toLowerCase() &&
    a.ref === b.ref
  );
}

/** The folder holding a repo path; empty for the repo root. */
function parentPath(path: string): string {
  const parent = posix.dirname(path);
  return parent === "." ? "" : parent;
}

function checkoutOf(fetch: Fetch): Checkout {
  if (fetch.checkout === undefined) throw new Error("A source was used before it was fetched.");
  return fetch.checkout;
}

/** Why a fetch failed, with a hint when git found no such ref. */
function fetchFailure({ location: { owner, repo, ref }, url }: Fetch, error: unknown): string {
  const detail = (error instanceof Error ? error.message : String(error)).replace(/[^.!?]$/, "$&.");
  let message = `Cannot fetch '${ref}' of ${owner}/${repo}, named by '${url}': ${detail}`;
  if (/couldn't find remote ref/i.test(detail)) {
    message += /^[0-9a-f]{7,39}$/i.test(ref)
      ? " A commit is named by all 40 of its hex digits."
      : " A branch or tag whose name holds '/' cannot be named in the URL; name its commit instead.";
  }
  return message;
}

/**
 * Fetch one commit of a GitHub repo, holding only `folders`, into a new temporary
 * folder. Throws with git's own message when git cannot be run or fails.
 */
async function checkOut({ owner, repo, ref }: GitHubLocation, folders: readonly string[]): Promise<Checkout> {
  // Named after the repo, so that a fetched repo root lands under its own name.
  const root = join(await mkdtemp(join(tmpdir(), "ace-fetch-")), repo);
  try {
    await mkdir(root);
    await git(root, ["init", "--quiet"]);
    // A spec's paths can outgrow Windows' MAX_PATH under the temporary folder.
    await git(root, ["config", "core.longpaths", "true"]);
    await git(root, ["remote", "add", "origin", `${GITHUB}${owner}/${repo}.git`]);
    // The whole repo needs no sparse checkout, and is better fetched in one piece
    // than tree by tree.
    const whole = folders.includes("");
    if (!whole) await git(root, ["sparse-checkout", "set", "--cone", "--stdin"], folders.join("\n") + "\n");
    await git(root, [
      "fetch",
      "--quiet",
      "--no-tags",
      "--depth",
      "1",
      ...(whole ? [] : ["--filter=tree:0"]),
      "origin",
      ref,
    ]);
    await git(root, ["-c", "advice.detachedHead=false", "checkout", "--quiet", "FETCH_HEAD"]);
    return { root, commit: (await git(root, ["rev-parse", "HEAD"])).trim() };
  } catch (error) {
    await removeCheckout(root);
    throw error;
  }
}

/** Delete a checkout with the temporary folder holding it. Best effort. */
async function removeCheckout(root: string): Promise<void> {
  await rm(dirname(root), { recursive: true, force: true, maxRetries: 5, retryDelay: 200 }).catch(() => undefined);
}

/** Run git in `cwd` and resolve with its stdout; throws with git's message when it fails. */
async function git(cwd: string, args: readonly string[], input?: string): Promise<string> {
  let result;
  try {
    result = await runCommand("git", args, {
      label: `git ${args.join(" ")}`,
      timeoutMs: timeoutFromEnv(FETCH_TIMEOUT_VARIABLE, DEFAULT_FETCH_TIMEOUT_MS),
      cwd,
      // Never wait at a prompt: a repo git cannot read fails instead.
      env: { ...process.env, GIT_TERMINAL_PROMPT: "0" },
      input,
    });
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") {
      throw new Error("git is not on PATH, and fetching from GitHub needs it.");
    }
    if (error instanceof Error && error.name === "TimeoutError") {
      throw new Error(`${error.message} ${FETCH_TIMEOUT_VARIABLE} sets the limit, in milliseconds.`);
    }
    throw error;
  }
  if (result.code !== 0) {
    const message = result.stderr
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line !== "")
      .join(" ");
    throw new Error(message !== "" ? message : `git ${args[0]} exited with code ${result.code ?? "null"}.`);
  }
  return result.stdout;
}
