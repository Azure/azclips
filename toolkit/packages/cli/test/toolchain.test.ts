/**
 * `resolveToolchain` runner tests (design Part II, P2.2), and the `tsp-emit`
 * worker lookup.
 *
 * Proves the hybrid path-resolve: a spec inside the workspace resolves the
 * compiler + emitter from its own tree (project-local); a spec with no reachable
 * `node_modules` falls back to ace's bundled toolchain. Resolution returns real,
 * existing paths and never loads the compiler.
 */

import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { resolveToolchain, resolveTspEmitWorker } from "../src/toolchain.js";

const PETS_TSP = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "ir",
  "examples",
  "pets.tsp",
);

describe("resolveToolchain", () => {
  it("resolves compiler + emitter from a spec inside the workspace (project-local)", () => {
    const tc = resolveToolchain(PETS_TSP);
    expect(tc.source).toBe("project-local");
    expect(existsSync(tc.compilerPath)).toBe(true);
    expect(existsSync(tc.emitterPath)).toBe(true);
    expect(tc.compilerPath).toMatch(/@typespec[\\/]compiler/);
    expect(tc.emitterPath).toMatch(/typespec-emitter/);
    expect(tc.entrypoint).toBe(PETS_TSP);
  });

  it("falls back to the bundled toolchain when the spec has no node_modules", () => {
    const dir = mkdtempSync(join(tmpdir(), "ace-toolchain-"));
    try {
      const tc = resolveToolchain(join(dir, "main.tsp"));
      expect(tc.source).toBe("bundled");
      expect(existsSync(tc.compilerPath)).toBe(true);
      expect(existsSync(tc.emitterPath)).toBe(true);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("accepts a directory entrypoint", () => {
    const tc = resolveToolchain(dirname(PETS_TSP));
    expect(existsSync(tc.compilerPath)).toBe(true);
    expect(existsSync(tc.emitterPath)).toBe(true);
  });
});

describe("resolveTspEmitWorker", () => {
  it("finds ace's own tsp-emit worker in the emitter package", () => {
    const worker = resolveTspEmitWorker();
    expect(existsSync(worker)).toBe(true);
    expect(worker).toMatch(/typespec-emitter[\\/]dist[\\/]src[\\/]tsp-emit-worker\.js$/);
  });
});
