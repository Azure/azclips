import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { cpSync, existsSync, mkdirSync, readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const directory = dirname(fileURLToPath(import.meta.url));
export const upstream = JSON.parse(readFileSync(join(directory, "upstream.json"), "utf8"));
if (!/^[a-f0-9]{40}$/.test(upstream.revision)) throw new Error("Upstream must be pinned to a full commit.");
export const checkout = resolve(directory, "..", "..", "..", "artifacts", "typespec-bicep", upstream.revision);
export const packageRoot = join(checkout, "src", "typespec-bicep");
export const buildRoot = join(directory, "dist", upstream.revision,
  createHash("sha256").update(readFileSync(join(directory, "integration.patch"))).digest("hex").slice(0, 16));

function git(args, capture = false) {
  return execFileSync("git", ["-c", "core.autocrlf=false", "-c", "core.longpaths=true", ...args], {
    cwd: checkout,
    env: { ...process.env, GIT_TERMINAL_PROMPT: "0" },
    encoding: "utf8",
    stdio: capture ? ["ignore", "pipe", "inherit"] : "inherit",
    timeout: 600_000,
  });
}

function npm(args) {
  if (!process.env.npm_execpath) throw new Error("Invoke setup/build through this package's npm scripts.");
  execFileSync(process.execPath, [process.env.npm_execpath, ...args], {
    cwd: directory,
    stdio: "inherit",
    timeout: 600_000,
  });
}

export function verifySource() {
  if (!existsSync(join(checkout, ".git"))) throw new Error("Upstream checkout missing; run npm run setup.");
  if (git(["rev-parse", "HEAD"], true).trim() !== upstream.revision) {
    throw new Error("Unexpected upstream HEAD; refusing to modify or build this checkout.");
  }
  const expected = readFileSync(join(directory, "integration.patch"), "utf8");
  const actual = git(["diff", "--binary", "--no-ext-diff", "--no-textconv", "HEAD"], true);
  if (actual !== expected) throw new Error("Upstream changes do not match the reviewed integration patch.");
  if (git(["ls-files", "--others", "--exclude-standard", "--", "src/typespec-bicep/src"], true).trim()) {
    throw new Error("Unexpected untracked upstream source files; refusing to build this checkout.");
  }
}

function prepare(mode) {
  if (!existsSync(checkout)) {
    mkdirSync(checkout, { recursive: true });
    git(["init", "--quiet"]);
    git(["remote", "add", "origin", upstream.repository]);
    git(["sparse-checkout", "set", "--cone", "src/typespec-bicep"]);
    git(["fetch", "--no-tags", "--depth", "1", "--filter=tree:0", "origin", upstream.revision]);
    git(["checkout", "--detach", "FETCH_HEAD"]);
  }
  if (git(["rev-parse", "HEAD"], true).trim() !== upstream.revision) {
    throw new Error("Unexpected upstream HEAD; refusing to apply the integration patch.");
  }
  const patch = resolve(directory, "integration.patch");
  if (git(["diff", "--binary", "--no-ext-diff", "--no-textconv", "HEAD"], true) === "") {
    git(["apply", "--check", "--whitespace=error", patch]);
    git(["apply", "--whitespace=error", patch]);
  }
  verifySource();
  if (mode === "--install") npm(["ci", "--ignore-scripts", "--no-audit", "--no-fund"]);
  mkdirSync(buildRoot, { recursive: true });
  for (const name of ["src", "package.json", "tsconfig.json"]) {
    cpSync(join(packageRoot, name), join(buildRoot, name), { recursive: true });
  }
  cpSync(join(checkout, "LICENSE"), join(buildRoot, "LICENSE"));
  const require = createRequire(import.meta.url);
  execFileSync(process.execPath, [require.resolve("typescript/bin/tsc"), "-p", buildRoot], {
    cwd: directory,
    stdio: "inherit",
    timeout: 120_000,
  });
  verifySource();
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [mode, ...rest] = process.argv.slice(2);
  if (!["--install", "--build"].includes(mode) || rest.length !== 0) {
    throw new Error("Use npm run setup (restore/build) or npm run build (without restore).");
  }
  prepare(mode);
}
