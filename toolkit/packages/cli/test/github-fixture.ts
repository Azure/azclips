/**
 * A stand-in for github.com in tests: bare repos in a temporary folder, and git's
 * `url.<base>.insteadOf` set through `GIT_CONFIG_*` environment variables, so that
 * every git command the CLI runs reads `https://github.com/<owner>/<repo>.git` from
 * them. Nothing else changes: the fetch runs as it would against GitHub.
 */

import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { pathToFileURL } from "node:url";

/** True when git can run here; the tests that fetch are skipped otherwise. */
export const gitAvailable = spawnSync("git", ["--version"]).status === 0;

const REDIRECT_VARIABLES = ["GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_0", "GIT_CONFIG_VALUE_0"] as const;

export interface GitHubFixture {
  /**
   * Commit `files` (repo path to contents) as the whole of `<owner>/<repo>` on
   * `branch`, and return the commit.
   */
  publish(owner: string, repo: string, files: Record<string, string>, branch?: string): string;
  /** Stop redirecting github.com, and delete the repos. */
  dispose(): void;
}

/** Redirect `https://github.com/` to a fresh set of local repos until `dispose()`. */
export function createGitHubFixture(): GitHubFixture {
  const root = mkdtempSync(join(tmpdir(), "ace-github-"));
  const saved = REDIRECT_VARIABLES.map((name) => process.env[name]);
  process.env.GIT_CONFIG_COUNT = "1";
  process.env.GIT_CONFIG_KEY_0 = `url.${pathToFileURL(root).href}/.insteadOf`;
  process.env.GIT_CONFIG_VALUE_0 = "https://github.com/";

  return {
    publish(owner, repo, files, branch = "main") {
      const bare = join(root, owner, `${repo}.git`);
      if (!existsSync(bare)) {
        git(root, "init", "--quiet", "--bare", bare);
        // What github.com allows: partial fetches, and fetching a commit by its id.
        git(root, `--git-dir=${bare}`, "config", "uploadpack.allowFilter", "true");
        git(root, `--git-dir=${bare}`, "config", "uploadpack.allowAnySHA1InWant", "true");
      }
      const work = mkdtempSync(join(root, "work-"));
      try {
        git(work, "init", "--quiet", "-b", branch);
        for (const [path, contents] of Object.entries(files)) {
          mkdirSync(dirname(join(work, path)), { recursive: true });
          writeFileSync(join(work, path), contents, "utf8");
        }
        git(work, "add", "--all");
        git(
          work,
          ...["-c", "user.name=ace", "-c", "user.email=ace@example.invalid", "-c", "commit.gpgsign=false"],
          ...["commit", "--quiet", "--no-verify", "-m", "publish"],
        );
        git(work, "push", "--quiet", "--force", bare, `HEAD:refs/heads/${branch}`);
        return git(work, "rev-parse", "HEAD");
      } finally {
        rmSync(work, { recursive: true, force: true });
      }
    },
    dispose() {
      REDIRECT_VARIABLES.forEach((name, index) => {
        const value = saved[index];
        if (value === undefined) delete process.env[name];
        else process.env[name] = value;
      });
      rmSync(root, { recursive: true, force: true });
    },
  };
}

function git(cwd: string, ...args: string[]): string {
  return execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}
