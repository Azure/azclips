/**
 * Orchestration tests.
 *
 * Prove that `compile()` walks the compiler-owned stage sequence: the emit stage
 * produces the run's one IR, transforms hand it along in catalog order, each
 * generate stage renders it as it stands at that position, and every seam is
 * fulfilled by stage id rather than by a host-authored pipeline.
 *
 * The orchestrator is TypeSpec-free, so emit is a stub producer backed by the
 * committed `pets.ir.json` fixture — the real compile+emit path is covered in
 * @azure-client-engine/cli's tests.
 */

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  CSHARP_STAGE,
  CREATE_COMMAND_STAGE,
  FLATTEN_MODEL_STAGE,
  TWEAK_IR_STAGE,
  TSP_EMIT_STAGE,
  compile,
  createRegistry,
  noopGenerator,
  type CompileOptions,
  type EmitArtifact,
  type EmitProducer,
  type Generator,
  type StageDescriptor,
  type TransformStep,
} from "../src/index.js";

import { parseIR, type IR, type TransformedIR } from "@azure-client-engine/ir";

const PETS_IR: IR = parseIR(
  JSON.parse(
    readFileSync(
      resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "ir", "examples", "pets.ir.json"),
      "utf8",
    ),
  ),
);

// Fresh clone per call so a mutating stage in one test cannot leak into another.
const petsEmit: EmitProducer = {
  name: "stub-pets",
  produce: () => ({ ir: structuredClone(PETS_IR) }),
};

const ENTRYPOINT = "pets.tsp";

/** A transform that renames a model in place, so a stage's effect is observable. */
const renameModel = (from: string, to: string): TransformStep => ({
  name: `rename-${from}`,
  run: (ir) => {
    for (const model of ir.models) if (model.name === from) model.name = to;
    return ir;
  },
});

/** A generator that records what IR it saw, under its own file path. */
const reportingGenerator = (name: string): Generator => ({
  name,
  generate: async (ir, ctx) => {
    await ctx.emit({
      kind: "file",
      path: `${name}.txt`,
      contents: ir.models.map((m) => m.name).join(","),
    });
  },
});

/**
 * Fulfil the shipped catalog's optional stages so a test exercises orchestration
 * rather than tripping over unimplemented ones.
 *
 * Overrides are merged per kind, not substituted: a test that replaces one
 * transform still gets implementations for the others, so adding a stage to the
 * catalog does not silently break every test that names a different one.
 */
function defaults(overrides: Partial<Parameters<typeof createRegistry>[0]> = {}): CompileOptions {
  return {
    emit: petsEmit,
    registry: createRegistry({
      transforms: {
        [TWEAK_IR_STAGE]: () => ({ name: TWEAK_IR_STAGE, run: (ir) => ir }),
        [FLATTEN_MODEL_STAGE]: () => ({ name: FLATTEN_MODEL_STAGE, run: (ir) => ir }),
        [CREATE_COMMAND_STAGE]: () => ({ name: CREATE_COMMAND_STAGE, run: (ir) => ir }),
        ...overrides.transforms,
      },
      generators: { [CSHARP_STAGE]: () => noopGenerator, ...overrides.generators },
      ...(overrides.emitters !== undefined ? { emitters: overrides.emitters } : {}),
    }),
  };
}

describe("orchestration: the emit stage", () => {
  it("fails when the emit stage has no implementation and no runtime", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT });
    expect(result.success).toBe(false);
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/no-emit-producer",
    );
  });

  it("uses the injected producer and never touches the entrypoint", async () => {
    const result = await compile({ entrypoint: "does-not-exist.tsp" }, defaults());
    expect(result.success).toBe(true);
    expect(result.diagnostics).toEqual([]);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
  });

  it("resolves the emit stage from the registry by its catalog id", async () => {
    const result = await compile(
      { entrypoint: ENTRYPOINT },
      {
        registry: createRegistry({
          emitters: { [TSP_EMIT_STAGE]: () => petsEmit },
          transforms: {
            [TWEAK_IR_STAGE]: () => ({ name: "w", run: (ir) => ir }),
            [FLATTEN_MODEL_STAGE]: () => ({ name: "x", run: (ir) => ir }),
            [CREATE_COMMAND_STAGE]: () => ({ name: "y", run: (ir) => ir }),
          },
          generators: { [CSHARP_STAGE]: () => noopGenerator },
        }),
      },
    );
    expect(result.success).toBe(true);
    expect(result.diagnostics).toEqual([]);
  });

  it("reports a stage-failed diagnostic when the producer throws", async () => {
    const result = await compile(
      { entrypoint: "x.tsp" },
      { emit: { name: "boom", produce: () => { throw new Error("kaboom"); } } },
    );
    expect(result.success).toBe(false);
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/emit-stage-failed",
    );
  });
});

describe("orchestration: config toggles", () => {
  it("runs the catalog's default stages without any config", async () => {
    const seen: string[] = [];
    const result = await compile({ entrypoint: ENTRYPOINT }, {
      ...defaults({
        transforms: {
          [FLATTEN_MODEL_STAGE]: () => ({
            name: FLATTEN_MODEL_STAGE,
            run: (ir) => { seen.push(FLATTEN_MODEL_STAGE); return ir; },
          }),
        },
      }),
      captureIR: true,
    });
    expect(result.success).toBe(true);
    expect(seen).toEqual([FLATTEN_MODEL_STAGE]);
    // Every IR-producing stage snapshots; generate stages take a position but emit none.
    expect(result.snapshots?.map((s) => s.step)).toEqual([
      TSP_EMIT_STAGE,
      TWEAK_IR_STAGE,
      FLATTEN_MODEL_STAGE,
      CREATE_COMMAND_STAGE,
    ]);
  });

  it("skips a stage the config turns off, and the sequence closes up", async () => {
    let ran = false;
    const result = await compile(
      { entrypoint: ENTRYPOINT, config: { pipeline: { [FLATTEN_MODEL_STAGE]: false } } },
      {
        ...defaults({
          transforms: {
            [FLATTEN_MODEL_STAGE]: () => ({
              name: FLATTEN_MODEL_STAGE,
              run: (ir) => { ran = true; return ir; },
            }),
          },
          generators: { [CSHARP_STAGE]: () => reportingGenerator(CSHARP_STAGE) },
        }),
        captureIR: true,
      },
    );
    expect(result.success).toBe(true);
    expect(ran).toBe(false);
    // csharp still ran — it simply moved up the sequence.
    expect(result.files.has(`${CSHARP_STAGE}.txt`)).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual([
      TSP_EMIT_STAGE,
      TWEAK_IR_STAGE,
      CREATE_COMMAND_STAGE,
    ]);
  });

  it("fails when an enabled stage has no implementation", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT }, { emit: petsEmit });
    expect(result.success).toBe(false);
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/unimplemented-stage",
    );
  });

  it("hands each stage its own options from config.stageOptions", async () => {
    let seen: unknown;
    const result = await compile(
      {
        entrypoint: ENTRYPOINT,
        config: { stageOptions: { [FLATTEN_MODEL_STAGE]: { threshold: 7 } } },
      },
      defaults({
        transforms: {
          [FLATTEN_MODEL_STAGE]: (options) => ({
            name: FLATTEN_MODEL_STAGE,
            run: (ir) => { seen = options.threshold; return ir; },
          }),
        },
      }),
    );
    expect(result.success).toBe(true);
    expect(seen).toBe(7);
  });

  it("threads CompileInput.config into every stage context", async () => {
    let transformSaw: unknown;
    const result = await compile(
      { entrypoint: ENTRYPOINT, config: { apiVersion: "2025-09-01" } },
      defaults({
        transforms: {
          [FLATTEN_MODEL_STAGE]: () => ({
            name: FLATTEN_MODEL_STAGE,
            run: (ir, ctx) => { transformSaw = ctx.config.apiVersion; return ir; },
          }),
        },
        generators: {
          [CSHARP_STAGE]: () => ({
            name: CSHARP_STAGE,
            generate: async (_ir, ctx) => {
              await ctx.emit({ kind: "file", path: "cfg.txt", contents: String(ctx.config.apiVersion) });
            },
          }),
        },
      }),
    );
    expect(result.success).toBe(true);
    expect(transformSaw).toBe("2025-09-01");
    expect(result.files.get("cfg.txt")).toBe("2025-09-01");
  });
});

describe("orchestration: the linear sequence", () => {
  /** emit -> early -> gen-early -> late -> gen-late. */
  const SEQUENCE: readonly StageDescriptor[] = [
    { id: "tsp-emit", kind: "emit", default: "mandatory" },
    { id: "early", kind: "transform", default: "on" },
    { id: "gen-early", kind: "generate", default: "on" },
    { id: "late", kind: "transform", default: "on" },
    { id: "gen-late", kind: "generate", default: "on" },
  ];

  const sequenceOptions = (extra: Partial<CompileOptions> = {}): CompileOptions => ({
    emit: petsEmit,
    catalog: SEQUENCE,
    registry: createRegistry({
      transforms: {
        early: () => renameModel("Pet", "Early"),
        late: () => renameModel("Early", "Late"),
      },
      generators: {
        "gen-early": () => reportingGenerator("gen-early"),
        "gen-late": () => reportingGenerator("gen-late"),
      },
    }),
    ...extra,
  });

  it("renders each generate stage against the IR as it stands at that position", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT }, sequenceOptions());

    expect(result.success).toBe(true);
    expect(result.diagnostics).toEqual([]);
    // `gen-early` ran before `late`, so it cannot have seen that rename.
    expect(result.files.get("gen-early.txt")).toContain("Early");
    expect(result.files.get("gen-early.txt")).not.toContain("Late");
    expect(result.files.get("gen-late.txt")).toContain("Late");
  });

  it("carries one IR through the whole run, with no copy between stages", async () => {
    const seen: IR[] = [];
    const record = (name: string): TransformStep => ({
      name,
      run: (ir) => { seen.push(ir); return ir; },
    });

    const result = await compile(
      { entrypoint: ENTRYPOINT },
      {
        emit: petsEmit,
        catalog: SEQUENCE,
        registry: createRegistry({
          transforms: { early: () => record("early"), late: () => record("late") },
          generators: {
            "gen-early": () => reportingGenerator("gen-early"),
            "gen-late": () => reportingGenerator("gen-late"),
          },
        }),
      },
    );

    expect(result.success).toBe(true);
    expect(seen).toHaveLength(2);
    // Same object throughout — the sequence never clones.
    expect(new Set(seen).size).toBe(1);
    expect(result.ir).toBe(seen[0]);
  });

  it("returns the IR in its final state, after every transform", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT }, sequenceOptions());
    expect(result.ir?.models.some((m) => m.name === "Late")).toBe(true);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(false);
  });

  it("carries on with a replacement when a stage returns a new object", async () => {
    const result = await compile(
      { entrypoint: ENTRYPOINT },
      {
        emit: petsEmit,
        catalog: SEQUENCE,
        registry: createRegistry({
          transforms: {
            // Pure style: returns a fresh object rather than mutating.
            early: () => ({
              name: "early",
              run: (ir) => ({ ...ir, models: ir.models.map((m) => ({ ...m, name: `x:${m.name}` })) }),
            }),
            late: () => ({ name: "late", run: (ir) => ir }),
          },
          generators: {
            "gen-early": () => reportingGenerator("gen-early"),
            "gen-late": () => reportingGenerator("gen-late"),
          },
        }),
      },
    );
    expect(result.success).toBe(true);
    expect(result.files.get("gen-early.txt")).toContain("x:Pet");
  });

  it("stops at the first failing stage rather than feeding broken IR onward", async () => {
    let lateRan = false;
    const result = await compile(
      { entrypoint: ENTRYPOINT },
      {
        emit: petsEmit,
        catalog: SEQUENCE,
        registry: createRegistry({
          transforms: {
            early: () => ({ name: "early", run: () => { throw new Error("kaboom"); } }),
            late: () => ({ name: "late", run: (ir) => { lateRan = true; return ir; } }),
          },
          generators: {
            "gen-early": () => reportingGenerator("gen-early"),
            "gen-late": () => reportingGenerator("gen-late"),
          },
        }),
      },
    );

    expect(result.success).toBe(false);
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/transform-stage-failed",
    );
    expect(lateRan).toBe(false);
    expect(result.files.size).toBe(0);
  });

  it("tags every artifact with the stage that produced it", async () => {
    const artifacts: EmitArtifact[] = [];
    const result = await compile(
      { entrypoint: ENTRYPOINT },
      sequenceOptions({ output: (artifact) => { artifacts.push(artifact); } }),
    );

    expect(result.success).toBe(true);
    const stages = artifacts
      .filter((a) => a.kind === "generated-file")
      .map((a) => (a.kind === "generated-file" ? a.stage : ""));
    expect(new Set(stages)).toEqual(new Set(["gen-early", "gen-late"]));
    // A custom sink takes ownership, so the result buffers stay empty.
    expect(result.files.size).toBe(0);
  });

  it("errors when two generate stages write the same path", async () => {
    const collide: Generator = {
      name: "collide",
      generate: async (_ir, ctx) => {
        await ctx.emit({ kind: "file", path: "same.txt", contents: "x" });
      },
    };
    const result = await compile(
      { entrypoint: ENTRYPOINT },
      {
        emit: petsEmit,
        catalog: SEQUENCE,
        registry: createRegistry({
          transforms: {
            early: () => ({ name: "early", run: (ir) => ir }),
            late: () => ({ name: "late", run: (ir) => ir }),
          },
          generators: { "gen-early": () => collide, "gen-late": () => collide },
        }),
      },
    );

    expect(result.success).toBe(false);
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/output-path-collision",
    );
  });

  it("captures one snapshot per IR-producing stage, in sequence order", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT }, sequenceOptions({ captureIR: true }));

    expect(result.success).toBe(true);
    // Generate stages take a position but produce no IR, hence the index gap.
    expect(result.snapshots?.map((s) => [s.index, s.step])).toEqual([
      [0, "tsp-emit"],
      [1, "early"],
      [3, "late"],
    ]);
  });

  it("copies each snapshot, so in-place mutation cannot rewrite an earlier one", async () => {
    // The run has one IR mutated from end to end, so the retaining collector sink
    // owns the copy — without it every snapshot would show the final state.
    const result = await compile({ entrypoint: ENTRYPOINT }, sequenceOptions({ captureIR: true }));

    const names = (step: string): string[] =>
      result.snapshots!.find((s) => s.step === step)!.ir.models.map((m) => m.name);

    expect(names("tsp-emit")).toContain("Pet");
    expect(names("early")).toContain("Early");
    expect(names("late")).toContain("Late");
  });
});

describe("orchestration: what a generator accepts", () => {
  /** A generator that narrows, recording whether it ever ran. */
  const picky = (ran: { value: boolean }, accept: boolean): Generator<TransformedIR> => ({
    name: "picky",
    accepts: (ir) =>
      accept
        ? { success: true, data: ir as TransformedIR }
        : { success: false, error: { issues: [{ path: ["commands"], message: "Required" }] } },
    generate: () => { ran.value = true; },
  });

  it("rejects a document the generator cannot render, naming the stage", async () => {
    // The pipeline carries one `IR` and a generate stage may sit anywhere, so this is
    // the only point where a backend's real input requirement can be established.
    const ran = { value: false };
    const result = await compile({ entrypoint: ENTRYPOINT }, {
      ...defaults({ generators: { [CSHARP_STAGE]: () => picky(ran, false) } }),
    });

    expect(result.success).toBe(false);
    const diagnostic = result.diagnostics.find(
      (d) => d.code === "@azure-client-engine/compiler/invalid-generator-input",
    );
    expect(diagnostic).toBeDefined();
    // Names the backend and the stage whose position produced the document, since
    // that is what a reader needs to find the transform that did not run.
    expect(diagnostic!.message).toContain("'picky'");
    expect(diagnostic!.message).toContain(`'${CSHARP_STAGE}'`);
    expect(diagnostic!.message).toContain("commands: Required");
    // Rejected before dispatch, so the backend never sees the document.
    expect(ran.value).toBe(false);
  });

  it("runs the generator once its document is accepted", async () => {
    const ran = { value: false };
    const result = await compile({ entrypoint: ENTRYPOINT }, {
      ...defaults({ generators: { [CSHARP_STAGE]: () => picky(ran, true) } }),
    });

    expect(result.success).toBe(true);
    expect(result.diagnostics).toEqual([]);
    expect(ran.value).toBe(true);
  });

  it("passes the document through untouched when a generator declares no requirement", async () => {
    // A backend happy with a bare `IR` omits `accepts` entirely; nothing is checked
    // and nothing is copied on its behalf.
    let seen: unknown;
    const result = await compile({ entrypoint: ENTRYPOINT }, {
      ...defaults({
        generators: {
          [CSHARP_STAGE]: () => ({ name: "plain", generate: (ir) => { seen = ir; } }),
        },
      }),
    });

    expect(result.success).toBe(true);
    expect(seen).toBe(result.ir);
  });
});

describe("orchestration: a long sequence", () => {
  it("runs every stage in catalog order, however deep the sequence gets", async () => {
    const DEPTH = 8;
    const catalog: StageDescriptor[] = [{ id: "tsp-emit", kind: "emit", default: "mandatory" }];
    const transforms: Record<string, () => TransformStep> = {};
    const ran: string[] = [];

    for (let i = 0; i < DEPTH; i++) {
      const id = `t${i}`;
      catalog.push({ id, kind: "transform", default: "on" });
      transforms[id] = () => ({
        name: id,
        run: (ir) => { ran.push(id); return ir; },
      });
    }
    catalog.push({ id: "csharp", kind: "generate", default: "on" });

    const result = await compile(
      { entrypoint: ENTRYPOINT },
      {
        emit: petsEmit,
        catalog,
        registry: createRegistry({ transforms, generators: { csharp: () => noopGenerator } }),
      },
    );

    expect(result.success).toBe(true);
    expect(result.diagnostics).toEqual([]);
    expect(ran).toEqual(Array.from({ length: DEPTH }, (_, i) => `t${i}`));
  });
});
