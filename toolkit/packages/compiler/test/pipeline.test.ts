/**
 * Extension-seam tests.
 *
 * Prove that `compile()` requires an injected emit producer, threads the emitted
 * IR through an injected `TransformPipeline`, and routes the transformed IR to an
 * injected `Generator`. The orchestrator is TypeSpec-free, so these tests feed IR
 * from a stub emit producer backed by the committed `pets.ir.json` fixture rather
 * than running a real TypeSpec compile — the real compile+emit path is covered in
 * @azure-client-engine/cli's tsp-compile tests.
 */

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  compile,
  createRegistry,
  createTransformPipeline,
  noopGenerator,
  type EmitProducer,
  type Generator,
  type OutputDelegate,
  type TransformStep,
} from "../src/index.js";

import { emptyIR, parseIR, type IR } from "@azure-client-engine/ir";

// A real, schema-valid IR fixture (models incl. "Pet"; operations incl. "list" /
// "rename"). Feeding it through a stub producer keeps the orchestrator tests free
// of any TypeSpec dependency while still exercising realistic IR.
const PETS_IR: IR = parseIR(
  JSON.parse(
    readFileSync(
      resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "ir", "examples", "pets.ir.json"),
      "utf8",
    ),
  ),
);

// Fresh clone per call so a mutating transform in one test can't leak into
// another test that shares this producer.
const petsEmit: EmitProducer = {
  name: "stub-pets",
  produce: () => ({ ir: structuredClone(PETS_IR) }),
};

// The stub ignores the entrypoint; this is just a readable placeholder.
const ENTRYPOINT = "pets.tsp";

describe("compiler backbone: emit seam", () => {
  const stubIR = (id: string): IR => ({
    ...emptyIR(),
    sources: [
      {
        id,
        entrypoint: "stub.tsp",
        apiVersion: { value: "2024-01-01", location: "query", parameterName: "api-version" },
        server: { module: id, namespace: [id], name: "server" },
        rootNamespace: [id],
      },
    ],
    servers: [{
      id: { module: id, namespace: [id], name: "server" },
      endpoint: { kind: "literal", url: "https://stub.example/" },
    }],
  });

  it("fails with no-emit-producer when no emit seam is injected", async () => {
    // The orchestrator is TypeSpec-free: with no injected producer there is no
    // source -> IR step, so it reports a structured error instead of running one.
    const result = await compile({ entrypoint: ENTRYPOINT });

    expect(result.success).toBe(false);
    expect(
      result.diagnostics.some((d) => d.code === "@azure-client-engine/compiler/no-emit-producer"),
    ).toBe(true);
  });

  it("uses the injected EmitProducer and never touches the entrypoint", async () => {
    // A stub producer returns IR directly — no TypeSpec compile — proving emit
    // is an injectable seam. The entrypoint is never read.
    const stub: EmitProducer = {
      name: "stub",
      produce: () => ({ ir: stubIR("Stub") }),
    };

    const result = await compile({ entrypoint: "does-not-exist.tsp" }, { emit: stub, generator: noopGenerator });

    expect(result.success).toBe(true);
    expect(result.ir?.sources[0]?.id).toBe("Stub");
    expect(result.diagnostics).toEqual([]);
  });

  it("reports a stage-failed diagnostic when the producer throws", async () => {
    const boom: EmitProducer = {
      name: "boom",
      produce: () => {
        throw new Error("kaboom");
      },
    };

    const result = await compile({ entrypoint: "x.tsp" }, { emit: boom });

    expect(result.success).toBe(false);
    expect(
      result.diagnostics.some((d) => d.code === "@azure-client-engine/compiler/emit-stage-failed"),
    ).toBe(true);
  });

  it("feeds the injected producer's IR through transform and capture", async () => {
    const stub: EmitProducer = {
      name: "stub",
      produce: () => ({ ir: stubIR("Stub") }),
    };
    const rename = createTransformPipeline("t", [
      {
        name: "tag",
        run: (ir) => ({ ...ir, sources: ir.sources.map((s) => ({ ...s, id: `${s.id}!` })) }),
      },
    ]);

    const result = await compile(
      { entrypoint: "x.tsp" },
      { emit: stub, transform: rename, captureIR: true, generator: noopGenerator },
    );

    expect(result.success).toBe(true);
    expect(result.ir?.sources[0]?.id).toBe("Stub!");
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "tag"]);
    expect(result.snapshots?.[0]?.ir.sources[0]?.id).toBe("Stub");
  });
});

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

    const result = await compile(
      { entrypoint: ENTRYPOINT },
      { emit: petsEmit, transform: renamePet, generator: noopGenerator },
    );

    expect(result.success).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Animal")).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(false);
  });

  it("routes transformed IR to an injected Generator and returns its files", async () => {
    const generator: Generator = {
      name: "test-generator",
      generate: async (ir, ctx) => {
        await ctx.emit({
          kind: "file",
          path: "summary.txt",
          contents: `models=${ir.models.length};operations=${ir.operations.length}`,
        });
      },
    };

    const result = await compile({ entrypoint: ENTRYPOINT }, { emit: petsEmit, generator });

    expect(result.success).toBe(true);
    expect(result.files.get("summary.txt")).toMatch(/^models=\d+;operations=\d+$/);
  });

  it("threads CompileInput.config into transform and generator contexts", async () => {
    const seen: Record<string, unknown> = {};
    const transform = createTransformPipeline("capture-config", [
      {
        name: "capture",
        run: (ir, ctx) => {
          seen.apiVersion = ctx.config.apiVersion;
          return ir;
        },
      },
    ]);
    const generator: Generator = {
      name: "capture-config-generator",
      generate: async (ir, ctx) => {
        await ctx.emit({ kind: "file", path: "cfg.txt", contents: String(ctx.config.apiVersion) });
      },
    };

    const result = await compile(
      { entrypoint: ENTRYPOINT, config: { apiVersion: "2025-09-01" } },
      { emit: petsEmit, transform, generator },
    );

    expect(result.success).toBe(true);
    expect(seen.apiVersion).toBe("2025-09-01");
    expect(result.files.get("cfg.txt")).toBe("2025-09-01");
  });
});

describe("compiler backbone: IR capture", () => {
  it("captures emit as snapshot 0 (default collector)", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT }, { emit: petsEmit, captureIR: true, generator: noopGenerator });
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
      { entrypoint: ENTRYPOINT },
      { emit: petsEmit, captureIR: true, transform: renamePet, generator: noopGenerator },
    );
    expect(result.success).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "rename-pet"]);
    // Emit snapshot is isolated (structuredClone) from the later rename.
    expect(result.snapshots?.[0]?.ir.models.some((m) => m.name === "Pet")).toBe(true);
    expect(result.snapshots?.[1]?.ir.models.some((m) => m.name === "Animal")).toBe(true);
  });

  it("omits snapshots when captureIR is not set", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT }, { emit: petsEmit, generator: noopGenerator });
    expect(result.snapshots).toBeUndefined();
  });

  it("routes artifacts to a custom sink, leaving result buffers empty", async () => {
    const seen: string[] = [];
    const output: OutputDelegate = (a) => {
      seen.push(a.kind === "ir-snapshot" ? `ir:${a.index}-${a.step}` : `file:${a.path}`);
    };
    const result = await compile(
      { entrypoint: ENTRYPOINT },
      { emit: petsEmit, captureIR: true, output, generator: noopGenerator },
    );
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
        entrypoint: ENTRYPOINT,
        config: { pipeline: [{ use: "rename", options: { from: "Pet", to: "Animal" } }] },
      },
      { emit: petsEmit, registry, captureIR: true, generator: noopGenerator },
    );
    expect(result.success).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Animal")).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "rename"]);
  });

  it("de-duplicates repeated step labels (rename, rename-2)", async () => {
    const registry = createRegistry({ transforms: { rename: renameFactory } });
    const result = await compile(
      {
        entrypoint: ENTRYPOINT,
        config: {
          pipeline: [
            { use: "rename", options: { from: "Pet", to: "Animal" } },
            { use: "rename", options: { from: "Animal", to: "Creature" } },
          ],
        },
      },
      { emit: petsEmit, registry, captureIR: true, generator: noopGenerator },
    );
    expect(result.success).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Creature")).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "rename", "rename-2"]);
  });

  it("fails with a diagnostic when a step id is not registered", async () => {
    const result = await compile(
      { entrypoint: ENTRYPOINT, config: { pipeline: [{ use: "does-not-exist" }] } },
      { emit: petsEmit, registry: createRegistry() },
    );
    expect(result.success).toBe(false);
    expect(
      result.diagnostics.some(
        (d) => d.code === "@azure-client-engine/compiler/unknown-transform-step",
      ),
    ).toBe(true);
  });
});
