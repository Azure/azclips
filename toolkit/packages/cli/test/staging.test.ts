/**
 * `stageLocalProject` staging + bundled-provisioning tests (design Part II, P2.4).
 *
 * Proves the host's acquire + provision step: the spec tree is copied into
 * `<out>/source` (excluding `node_modules` / `.git`), a `node_modules` junction
 * to ace's hoisted install is dropped beside it (so the compiler + libraries +
 * emitter all resolve), and the staged entrypoint points inside the copy. The
 * user's original tree is never touched.
 */

import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { stageLocalProject, removeDir } from "../src/staging.js";
import { resolveToolchain } from "../src/toolchain.js";

let sourceDir: string;
let outRoot: string;

beforeEach(() => {
  // A fake spec tree: an entry file, a nested file, and node_modules/.git that
  // provisioning must NOT copy.
  sourceDir = mkdtempSync(join(tmpdir(), "ace-stage-src-"));
  writeFileSync(join(sourceDir, "main.tsp"), "// fake spec\n", "utf8");
  mkdirSync(join(sourceDir, "sub"));
  writeFileSync(join(sourceDir, "sub", "extra.tsp"), "// nested\n", "utf8");
  mkdirSync(join(sourceDir, "node_modules"));
  writeFileSync(join(sourceDir, "node_modules", "junk.txt"), "stale", "utf8");
  mkdirSync(join(sourceDir, ".git"));
  writeFileSync(join(sourceDir, ".git", "config"), "[core]", "utf8");

  outRoot = mkdtempSync(join(tmpdir(), "ace-stage-out-"));
});

afterEach(() => {
  // rmSync unlinks the node_modules junction rather than recursing into ace's
  // real install (verified Node behavior on Windows junctions).
  rmSync(sourceDir, { recursive: true, force: true });
  rmSync(outRoot, { recursive: true, force: true });
});

describe("stageLocalProject", () => {
  it("copies the spec tree into <out>/source and points the entrypoint inside it", async () => {
    const staged = await stageLocalProject(join(sourceDir, "main.tsp"), outRoot);

    expect(staged.root).toBe(resolve(outRoot, "source"));
    expect(staged.entrypoint).toBe(join(staged.root, "main.tsp"));
    expect(existsSync(join(staged.root, "main.tsp"))).toBe(true);
    expect(existsSync(join(staged.root, "sub", "extra.tsp"))).toBe(true);
  });

  it("excludes node_modules and .git from the copy", async () => {
    const staged = await stageLocalProject(join(sourceDir, "main.tsp"), outRoot);

    // The source's own node_modules content is never copied...
    expect(existsSync(join(staged.root, "node_modules", "junk.txt"))).toBe(false);
    // ...and .git is skipped entirely.
    expect(existsSync(join(staged.root, ".git"))).toBe(false);
  });

  it("provisions a node_modules junction to ace's hoisted toolchain", async () => {
    const staged = await stageLocalProject(join(sourceDir, "main.tsp"), outRoot);

    // The junction resolves to ace's install, so @typespec/compiler is reachable
    // from the staged tree even though the spec brought no node_modules.
    expect(existsSync(join(staged.root, "node_modules", "@typespec", "compiler"))).toBe(true);

    // ...which is exactly what lets resolveToolchain find a toolchain for it.
    const tc = resolveToolchain(staged.entrypoint);
    expect(existsSync(tc.compilerPath)).toBe(true);
    expect(existsSync(tc.emitterPath)).toBe(true);
  });

  it("stages a directory entrypoint as the staged root", async () => {
    const staged = await stageLocalProject(sourceDir, outRoot);

    expect(staged.entrypoint).toBe(staged.root);
    expect(existsSync(join(staged.root, "main.tsp"))).toBe(true);
    expect(existsSync(join(staged.root, "node_modules", "@typespec", "compiler"))).toBe(true);
  });

  it("leaves the user's original tree untouched", async () => {
    await stageLocalProject(join(sourceDir, "main.tsp"), outRoot);

    // The source keeps its own (stale) node_modules; staging copied, not moved.
    expect(existsSync(join(sourceDir, "node_modules", "junk.txt"))).toBe(true);
  });
});

describe("removeDir", () => {
  it("removes the staged tree and unlinks the junction without touching ace's install", async () => {
    const staged = await stageLocalProject(join(sourceDir, "main.tsp"), outRoot);
    expect(existsSync(join(staged.root, "main.tsp"))).toBe(true);
    expect(existsSync(join(staged.root, "node_modules", "@typespec", "compiler"))).toBe(true);

    const { removed, error } = await removeDir(staged.root);

    expect(error).toBeUndefined();
    expect(removed).toBe(true);
    expect(existsSync(staged.root)).toBe(false);

    // The junction was unlinked, not recursed into: ace's real toolchain (the
    // junction's target) is untouched and still resolvable.
    const tc = resolveToolchain(join(sourceDir, "main.tsp"));
    expect(existsSync(tc.compilerPath)).toBe(true);
  });
});
