/**
 * Built-in emit via the host runtime (design Part II, P2.3).
 *
 * Proves the full out-of-process emit chain now driven by the compiler's
 * built-in `tsp-emit` stage over `CompileOptions.runtime`: the host runtime
 * resolves the toolchain and forks the `tsp-compile` worker, whose IR + already
 * translated diagnostics are threaded back through the orchestrator. pets.tsp
 * lives inside the workspace, so its libraries resolve without staging.
 */

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import { compile, CSHARP_STAGE, CREATE_COMMAND_STAGE, FLATTEN_MODEL_STAGE, TWEAK_IR_STAGE, TSP_EMIT_STAGE } from "@azure-client-engine/compiler";

// These tests fork the real (compiled) worker process, so they must exercise the
// built artifacts under dist — Node cannot execute the .ts worker directly, and
// under vitest `import.meta.url` would otherwise point at the source tree.
import { createHostRuntime } from "../dist/src/runtime.js";

/**
 * Emit-only runs: turn off every optional stage after emit so these tests exercise
 * the real out-of-process `tsp-emit` stage. Only the mandatory `seed-ir` still runs,
 * and it just adds the target. `create-command` can be turned off only together
 * with the generate stage that `requires` it.
 */
const EMIT_ONLY: Record<string, boolean> = {
  [TWEAK_IR_STAGE]: false,
  [FLATTEN_MODEL_STAGE]: false,
  [CREATE_COMMAND_STAGE]: false,
  [CSHARP_STAGE]: false,
};

const PETS_TSP = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "ir",
  "examples",
  "pets.tsp",
);

describe("built-in emit (host runtime tsp-emit)", () => {
  it("produces schema-valid IR out of process and threads it through compile()", async () => {
    // Emit-only check: every optional stage after emit is off, so nothing is generated.
    const result = await compile(
      { entrypoint: PETS_TSP, config: { pipeline: EMIT_ONLY } },
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

  it("forwards config.emitter options to the emitter (sourceId overrides the module)", async () => {
    const result = await compile(
      { entrypoint: PETS_TSP, config: { pipeline: EMIT_ONLY, emitter: { sourceId: "CustomSource" } } },
      { runtime: createHostRuntime() },
    );

    expect(result.success).toBe(true);
    const pet = result.ir?.models.find((m) => m.name === "Pet");
    expect(pet?.source).toBe("CustomSource");
    expect(pet?.id.module).toBe("CustomSource");
  });

  it("forwards config.apiVersion through to TCGC's version selector", async () => {
    // pets.tsp only declares "2025-01-01"; a version outside that list proves the
    // selector *received* the value (TCGC warns + falls back).
    const result = await compile(
      { entrypoint: PETS_TSP, config: { pipeline: EMIT_ONLY, apiVersion: "1999-01-01" } },
      { runtime: createHostRuntime() },
    );

    expect(result.success).toBe(true);
    expect(result.diagnostics.some((d) => d.code.includes("invalid-schema"))).toBe(false);
    expect(result.diagnostics.some((d) => d.code.includes("api-version-undefined"))).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("surfaces emitter diagnostics across the process boundary", async () => {
    const result = await compile(
      { entrypoint: PETS_TSP, config: { pipeline: EMIT_ONLY } },
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
      { entrypoint: PETS_TSP, config: { pipeline: EMIT_ONLY } },
      { runtime: createHostRuntime(), captureIR: true },
    );

    // Snapshots are labeled by stage id, which is unique in the catalog.
    expect(result.snapshots?.[0]?.step).toBe(TSP_EMIT_STAGE);
    expect(result.snapshots?.[0]?.ir.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("reports a structured error when the entrypoint does not exist", async () => {
    const result = await compile(
      { entrypoint: resolve(dirname(PETS_TSP), "no-such-file.tsp"), config: { pipeline: EMIT_ONLY } },
      { runtime: createHostRuntime() },
    );

    expect(result.success).toBe(false);
    expect(result.diagnostics.some((d) => d.severity === "error")).toBe(true);
  });
});
