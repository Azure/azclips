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

import {
  compile,
  createRegistry,
  createTransformPipeline,
  type Generator,
  type OutputDelegate,
  type TransformStep,
} from "../src/index.js";

const PETS_TSP = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "ir", "examples", "pets.tsp"),
);

describe("compiler backbone: extension seams", () => {
  it("threads IR through an injected TransformPipeline", async () => {
    const renamePet = createTransformPipeline("test-rename-pet", [
      {
        name: "rename-pet",
        run: (ir) => ({
          ...ir,
          models: ir.models.map((m) => (m.name === "Pet" ? { ...m, name: "Animal" } : m)),
        }),
      },
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
      {
        name: "capture",
        run: (ir, ctx) => {
          seen.marker = ctx.extensions.marker;
          return ir;
        },
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

describe("compiler backbone: IR capture", () => {
  it("captures emit as snapshot 0 (default collector)", async () => {
    const result = await compile({ entrypoint: PETS_TSP }, { captureIR: true });
    expect(result.success).toBe(true);
    expect(result.snapshots?.[0]?.index).toBe(0);
    expect(result.snapshots?.[0]?.step).toBe("emit");
    expect(result.snapshots?.[0]?.ir.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("captures a snapshot per transform step, labeled by step name", async () => {
    const renamePet = createTransformPipeline("t", [
      {
        name: "rename-pet",
        run: (ir) => ({
          ...ir,
          models: ir.models.map((m) => (m.name === "Pet" ? { ...m, name: "Animal" } : m)),
        }),
      },
    ]);
    const result = await compile(
      { entrypoint: PETS_TSP },
      { captureIR: true, transform: renamePet },
    );
    expect(result.success).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "rename-pet"]);
    // Emit snapshot is isolated (structuredClone) from the later rename.
    expect(result.snapshots?.[0]?.ir.models.some((m) => m.name === "Pet")).toBe(true);
    expect(result.snapshots?.[1]?.ir.models.some((m) => m.name === "Animal")).toBe(true);
  });

  it("omits snapshots when captureIR is not set", async () => {
    const result = await compile({ entrypoint: PETS_TSP });
    expect(result.snapshots).toBeUndefined();
  });

  it("routes artifacts to a custom sink, leaving result buffers empty", async () => {
    const seen: string[] = [];
    const output: OutputDelegate = (a) => {
      seen.push(a.kind === "ir-snapshot" ? `ir:${a.index}-${a.step}` : `file:${a.path}`);
    };
    const result = await compile({ entrypoint: PETS_TSP }, { captureIR: true, output });
    expect(result.success).toBe(true);
    expect(seen).toContain("ir:0-emit");
    expect(result.snapshots).toBeUndefined();
    expect(result.files.size).toBe(0);
  });
});

describe("compiler backbone: configurable pipeline (registry + config)", () => {
  const renameFactory = (options: Record<string, unknown>): TransformStep => {
    const from = String(options.from ?? "Pet");
    const to = String(options.to ?? "Animal");
    return {
      name: "rename",
      run: (ir) => ({
        ...ir,
        models: ir.models.map((m) => (m.name === from ? { ...m, name: to } : m)),
      }),
    };
  };

  it("resolves config.pipeline steps against the registry, in order", async () => {
    const registry = createRegistry({ transforms: { rename: renameFactory } });
    const result = await compile(
      {
        entrypoint: PETS_TSP,
        config: { pipeline: [{ use: "rename", options: { from: "Pet", to: "Animal" } }] },
      },
      { registry, captureIR: true },
    );
    expect(result.success).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Animal")).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "rename"]);
  });

  it("de-duplicates repeated step labels (rename, rename-2)", async () => {
    const registry = createRegistry({ transforms: { rename: renameFactory } });
    const result = await compile(
      {
        entrypoint: PETS_TSP,
        config: {
          pipeline: [
            { use: "rename", options: { from: "Pet", to: "Animal" } },
            { use: "rename", options: { from: "Animal", to: "Creature" } },
          ],
        },
      },
      { registry, captureIR: true },
    );
    expect(result.success).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Creature")).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "rename", "rename-2"]);
  });

  it("fails with a diagnostic when a step id is not registered", async () => {
    const result = await compile(
      { entrypoint: PETS_TSP, config: { pipeline: [{ use: "does-not-exist" }] } },
      { registry: createRegistry() },
    );
    expect(result.success).toBe(false);
    expect(
      result.diagnostics.some(
        (d) => d.code === "@azure-client-engine/compiler/unknown-transform-step",
      ),
    ).toBe(true);
  });
});
