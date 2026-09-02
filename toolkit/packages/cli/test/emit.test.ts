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
import { compile, noopGenerator } from "@azure-client-engine/compiler";

// These tests fork the real (compiled) worker process, so they must exercise the
// built artifacts under dist — Node cannot execute the .ts worker directly, and
// under vitest `import.meta.url` would otherwise point at the source tree.
import { createHostRuntime } from "../dist/src/runtime.js";

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
    // Emit-only check: inject the no-op generator so the default csharp back end
    // does not run (the pipeline now always ends in a generator).
    const result = await compile(
      { entrypoint: PETS_TSP },
      { runtime: createHostRuntime(), generator: noopGenerator },
    );

    expect(result.success).toBe(true);
    expect(typeof result.ir?.schemaVersion).toBe("string");
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
    expect(result.ir?.operations.some((o) => o.name === "list")).toBe(true);
    expect(result.ir?.operations.some((o) => o.name === "rename")).toBe(true);
    // The injected no-op generator writes nothing, so no files are produced.
    expect(result.files.size).toBe(0);
  });

  it("forwards config.emitter options to the emitter (sourceId overrides the module)", async () => {
    const result = await compile(
      { entrypoint: PETS_TSP, config: { emitter: { sourceId: "CustomSource" } } },
      { runtime: createHostRuntime(), generator: noopGenerator },
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
      { entrypoint: PETS_TSP, config: { apiVersion: "1999-01-01" } },
      { runtime: createHostRuntime(), generator: noopGenerator },
    );

    expect(result.success).toBe(true);
    expect(result.diagnostics.some((d) => d.code.includes("invalid-schema"))).toBe(false);
    expect(result.diagnostics.some((d) => d.code.includes("api-version-undefined"))).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("surfaces emitter diagnostics across the process boundary", async () => {
    const result = await compile(
      { entrypoint: PETS_TSP },
      { runtime: createHostRuntime(), generator: noopGenerator },
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
      { entrypoint: PETS_TSP },
      { runtime: createHostRuntime(), captureIR: true, generator: noopGenerator },
    );

    expect(result.snapshots?.[0]?.step).toBe("emit");
    expect(result.snapshots?.[0]?.ir.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("reports a structured error when the entrypoint does not exist", async () => {
    const result = await compile(
      { entrypoint: resolve(dirname(PETS_TSP), "no-such-file.tsp") },
      { runtime: createHostRuntime(), generator: noopGenerator },
    );

    expect(result.success).toBe(false);
    expect(result.diagnostics.some((d) => d.severity === "error")).toBe(true);
  });
});
