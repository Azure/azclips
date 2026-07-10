/**
 * Extension-seam tests.
 *
 * Prove that `compile()` threads the emitted IR through an injected
 * `TransformPipeline` and routes the transformed IR to an injected
 * `Generator`, returning the generator's files. These are the seams the
 * future transform passes and the Roslyn C# back end plug into.
 */

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import { normalizePath } from "@typespec/compiler";

import { compile, createTransformPipeline, type Generator } from "../src/index.js";

const PETS_TSP = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "ir", "examples", "pets.tsp"),
);

describe("compiler backbone: extension seams", () => {
  it("threads IR through an injected TransformPipeline", async () => {
    const renamePet = createTransformPipeline("test-rename-pet", [
      (ir) => ({
        ...ir,
        models: ir.models.map((m) => (m.name === "Pet" ? { ...m, name: "Animal" } : m)),
      }),
    ]);

    const result = await compile({ entrypoint: PETS_TSP }, { transform: renamePet });

    expect(result.success).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Animal")).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(false);
  });

  it("routes transformed IR to an injected Generator and returns its files", async () => {
    const generator: Generator = {
      name: "test-generator",
      generate: (ir) => ({
        files: new Map([
          ["summary.txt", `models=${ir.models.length};operations=${ir.operations.length}`],
        ]),
      }),
    };

    const result = await compile({ entrypoint: PETS_TSP }, { generator });

    expect(result.success).toBe(true);
    expect(result.files.get("summary.txt")).toMatch(/^models=\d+;operations=\d+$/);
  });

  it("forwards emitter options via config.emitter (sourceId overrides the module)", async () => {
    const result = await compile({
      entrypoint: PETS_TSP,
      config: { emitter: { sourceId: "CustomSource" } },
    });

    expect(result.success).toBe(true);
    const pet = result.ir?.models.find((m) => m.name === "Pet");
    expect(pet?.source).toBe("CustomSource");
    expect(pet?.id.module).toBe("CustomSource");
  });

  it("threads CompileInput.extensions into transform and generator contexts", async () => {
    const seen: Record<string, unknown> = {};
    const transform = createTransformPipeline("capture-extensions", [
      (ir, ctx) => {
        seen.marker = ctx.extensions.marker;
        return ir;
      },
    ]);
    const generator: Generator = {
      name: "capture-extensions-generator",
      generate: (ir, ctx) => ({ files: new Map([["ext.txt", String(ctx.extensions.marker)]]) }),
    };

    const result = await compile(
      { entrypoint: PETS_TSP, extensions: { marker: "hello" } },
      { transform, generator },
    );

    expect(result.success).toBe(true);
    expect(seen.marker).toBe("hello");
    expect(result.files.get("ext.txt")).toBe("hello");
  });
});
