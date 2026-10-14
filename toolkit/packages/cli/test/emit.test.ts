/**
 * Built-in emit via the host runtime (design Part II, P2.3).
 *
 * Proves the full out-of-process emit chain now driven by the compiler's
 * built-in `tsp-emit` stage over `CompileOptions.runtime`: the host runtime
 * resolves the toolchain and forks the emitter package's `tsp-emit` worker, whose
 * IR + already translated diagnostics are threaded back through the orchestrator.
 * pets.tsp lives inside the workspace, so its libraries resolve without staging.
 */

import { copyFileSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import {
  compile,
  CSHARP_STAGE,
  CREATE_COMMAND_STAGE,
  FLATTEN_MODEL_STAGE,
  TWEAK_IR_STAGE,
  TSP_EMIT_STAGE,
  type StageSection,
} from "@azure-client-engine/compiler";

// The worker these tests fork is the emitter package's built module, found through
// its `./tsp-emit-worker` export, so the emitter must be built first.
import { createHostRuntime } from "../src/runtime.js";

/**
 * Emit-only runs: disable every optional stage after emit so these tests exercise
 * the real out-of-process `tsp-emit` stage. Only the mandatory `seed-ir` still runs,
 * and it just adds the target. `create-command` can be disabled only together with
 * the generate stage that `requires` it.
 */
const EMIT_ONLY: Record<string, StageSection> = {
  [TWEAK_IR_STAGE]: { disabled: true },
  [FLATTEN_MODEL_STAGE]: { disabled: true },
  [CREATE_COMMAND_STAGE]: { disabled: true },
  [CSHARP_STAGE]: { disabled: true },
};

const PETS_TSP = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "ir",
  "examples",
  "pets.tsp",
);

/** The worker's error for an entrypoint TypeSpec cannot load. */
const INVALID_TSP_SOURCE = "@azure-client-engine/typespec-emitter/invalid-tsp-source";

/** Emit `entrypoint` alone and return the run's errors. */
async function emitErrors(entrypoint: string) {
  const result = await compile(
    { entrypoint, config: { stages: EMIT_ONLY } },
    { runtime: createHostRuntime() },
  );
  expect(result.success).toBe(false);
  return result.diagnostics.filter((d) => d.severity === "error");
}

describe("built-in emit (host runtime tsp-emit)", () => {
  it("produces schema-valid IR out of process and threads it through compile()", async () => {
    // Emit-only check: every optional stage after emit is off, so nothing is generated.
    const result = await compile(
      { entrypoint: PETS_TSP, config: { stages: EMIT_ONLY } },
      { runtime: createHostRuntime() },
    );

    expect(result.success).toBe(true);
    expect(typeof result.ir?.schemaVersion).toBe("string");
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
    expect(result.ir?.operations.some((o) => o.name === "list")).toBe(true);
    expect(result.ir?.operations.some((o) => o.name === "rename")).toBe(true);
    // No generate stage ran, so no files are produced.
    expect(result.files.size).toBe(0);
  });

  it("forwards tsp-emit's emitter option to the emitter (sourceId overrides the module)", async () => {
    const result = await compile(
      {
        entrypoint: PETS_TSP,
        config: {
          stages: {
            ...EMIT_ONLY,
            [TSP_EMIT_STAGE]: { options: { emitter: { sourceId: "CustomSource" } } },
          },
        },
      },
      { runtime: createHostRuntime() },
    );

    expect(result.success).toBe(true);
    const pet = result.ir?.models.find((m) => m.name === "Pet");
    expect(pet?.source).toBe("CustomSource");
    expect(pet?.id.module).toBe("CustomSource");
  });

  it("forwards the input's API version through to TCGC's version selector", async () => {
    // pets.tsp only declares "2025-01-01"; a version outside that list proves the
    // selector *received* the value (TCGC warns + falls back).
    const result = await compile(
      { entrypoint: PETS_TSP, apiVersion: "1999-01-01", config: { stages: EMIT_ONLY } },
      { runtime: createHostRuntime() },
    );

    expect(result.success).toBe(true);
    expect(result.diagnostics.some((d) => d.code.includes("invalid-schema"))).toBe(false);
    expect(result.diagnostics.some((d) => d.code.includes("api-version-undefined"))).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("surfaces emitter diagnostics across the process boundary", async () => {
    const result = await compile(
      { entrypoint: PETS_TSP, config: { stages: EMIT_ONLY } },
      { runtime: createHostRuntime() },
    );

    // pets.tsp has no @versioned api-version param -> emitter warns.
    expect(
      result.diagnostics.some(
        (d) =>
          d.severity === "warning" &&
          d.code === "@azure-client-engine/typespec-emitter/missing-api-version",
      ),
    ).toBe(true);
  });

  it("captures the subprocess IR when --capture-ir style snapshots are on", async () => {
    const result = await compile(
      { entrypoint: PETS_TSP, config: { stages: EMIT_ONLY } },
      { runtime: createHostRuntime(), captureIR: true },
    );

    // Snapshots are labeled by stage id, which is unique in the catalog.
    expect(result.snapshots?.[0]?.step).toBe(TSP_EMIT_STAGE);
    expect(result.snapshots?.[0]?.ir.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("explains an entrypoint that does not exist", async () => {
    const errors = await emitErrors(resolve(dirname(PETS_TSP), "no-such-file.tsp"));

    // One error that says what is wrong, in place of TypeSpec's file-not-found.
    expect(errors.map((d) => d.code)).toEqual([INVALID_TSP_SOURCE]);
    expect(errors[0]?.message).toMatch(/^The entrypoint '.*no-such-file\.tsp' does not exist\.$/);
  });

  it("explains a folder without main.tsp, quoting TypeSpec and naming what to do", async () => {
    const folder = mkdtempSync(join(tmpdir(), "ace-emit-folder-"));
    try {
      copyFileSync(PETS_TSP, join(folder, "client.tsp"));
      const errors = await emitErrors(folder);

      expect(errors.map((d) => d.code)).toEqual([INVALID_TSP_SOURCE]);
      expect(errors[0]?.message).toContain("is a folder without a main file TypeSpec can load.");
      expect(errors[0]?.message).toMatch(/File .*main\.tsp not found\./);
      expect(errors[0]?.message).toContain("to compile another file, such as client.tsp, name that file");
    } finally {
      rmSync(folder, { recursive: true, force: true });
    }
  });

  it("explains an entrypoint that is not a TypeSpec source", async () => {
    const folder = mkdtempSync(join(tmpdir(), "ace-emit-file-"));
    try {
      const notes = join(folder, "notes.txt");
      writeFileSync(notes, "not TypeSpec\n", "utf8");
      const errors = await emitErrors(notes);

      expect(errors.map((d) => d.code)).toEqual([INVALID_TSP_SOURCE]);
      expect(errors[0]?.message).toContain("is not a source TypeSpec can load.");
      expect(errors[0]?.message).toContain("Main file must either be a .tsp file or a .js file.");
    } finally {
      rmSync(folder, { recursive: true, force: true });
    }
  });
});
