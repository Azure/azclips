/**
 * Orchestration tests.
 *
 * Prove that `compile()` walks the compiler-owned stage sequence with the
 * compiler's own stage implementations: the emit stage produces the run's one IR,
 * the transforms hand it along in catalog order, and every stage is resolved by id
 * rather than supplied by the host.
 *
 * The orchestrator is TypeSpec-free, so emit is a stub producer backed by the
 * committed `pets.ir.json` fixture — the real compile+emit path is covered in
 * @azure-client-engine/cli's tests. The `csharp` stage runs out of process, so
 * these runs turn it off; its built-in path is covered in `runtime.test.ts`.
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
  type CompileConfig,
  type EmitProducer,
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

const NO_GENERATOR: CompileConfig = { pipeline: { [CSHARP_STAGE]: false } };

const errorsOf = (result: Awaited<ReturnType<typeof compile>>) =>
  result.diagnostics.filter((d) => d.severity === "error");

describe("orchestration: the emit stage", () => {
  it("fails when the emit stage has no implementation and no runtime", async () => {
    const result = await compile({ entrypoint: ENTRYPOINT });
    expect(result.success).toBe(false);
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/no-emit-producer",
    );
  });

  it("uses the injected producer and never touches the entrypoint", async () => {
    const result = await compile(
      { entrypoint: "does-not-exist.tsp", config: NO_GENERATOR },
      { emit: petsEmit },
    );
    expect(result.success).toBe(true);
    expect(errorsOf(result)).toEqual([]);
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
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

describe("orchestration: the stage sequence", () => {
  it("runs the catalog's transforms in order, snapshotting each IR-producing stage", async () => {
    const result = await compile(
      { entrypoint: ENTRYPOINT, config: NO_GENERATOR },
      { emit: petsEmit, captureIR: true },
    );

    expect(result.success).toBe(true);
    expect(result.snapshots?.map((s) => [s.index, s.step])).toEqual([
      [0, TSP_EMIT_STAGE],
      [1, TWEAK_IR_STAGE],
      [2, FLATTEN_MODEL_STAGE],
      [3, CREATE_COMMAND_STAGE],
    ]);
    // A run with no generate stage still succeeds; it is only warned about.
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/no-generator",
    );
  });

  it("skips a stage the config turns off, and the sequence closes up", async () => {
    const result = await compile(
      {
        entrypoint: ENTRYPOINT,
        config: { pipeline: { [FLATTEN_MODEL_STAGE]: false, [CSHARP_STAGE]: false } },
      },
      { emit: petsEmit, captureIR: true },
    );

    expect(result.success).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual([
      TSP_EMIT_STAGE,
      TWEAK_IR_STAGE,
      CREATE_COMMAND_STAGE,
    ]);
    expect(
      result.ir?.operations.every((o) => o.requestBody?.rendering?.parameters === undefined),
    ).toBe(true);
  });

  it("returns the IR in its final state, including a stage's replacement document", async () => {
    // create-command returns a new object rather than mutating, so its commands
    // reaching the result proves the orchestrator carries on with what a stage returns.
    const result = await compile(
      { entrypoint: ENTRYPOINT, config: NO_GENERATOR },
      { emit: petsEmit },
    );

    expect(result.success).toBe(true);
    expect((result.ir as TransformedIR | undefined)?.commands.length).toBeGreaterThan(0);
  });

  it("copies each snapshot, so in-place mutation cannot rewrite an earlier one", async () => {
    // flatten-model writes its hints into the run's one IR in place, so the retaining
    // collector sink owns the copy — without it every snapshot would show the final state.
    const result = await compile(
      { entrypoint: ENTRYPOINT, config: NO_GENERATOR },
      { emit: petsEmit, captureIR: true },
    );

    const createBody = (step: string) =>
      result.snapshots!.find((s) => s.step === step)!.ir.operations.find(
        (o) => o.name === "create",
      )!.requestBody!;

    expect(createBody(TSP_EMIT_STAGE).rendering?.parameters).toBeUndefined();
    expect(createBody(FLATTEN_MODEL_STAGE).rendering?.parameters?.length).toBeGreaterThan(0);
  });
});

describe("orchestration: stage resolution", () => {
  it("hands each stage its own options from config.stageOptions", async () => {
    const result = await compile(
      {
        entrypoint: ENTRYPOINT,
        config: {
          ...NO_GENERATOR,
          stageOptions: { [CREATE_COMMAND_STAGE]: { rootNamespace: "Contoso.Pets" } },
        },
      },
      { emit: petsEmit },
    );

    expect(result.success).toBe(true);
    expect((result.ir as TransformedIR | undefined)?.target.rootNamespace).toEqual([
      "Contoso",
      "Pets",
    ]);
  });

  it("fails when an enabled stage has no implementation", async () => {
    const result = await compile(
      { entrypoint: ENTRYPOINT },
      {
        emit: petsEmit,
        catalog: [
          { id: TSP_EMIT_STAGE, kind: "emit", default: "mandatory" },
          { id: "mystery", kind: "transform", default: "on" },
        ],
      },
    );

    expect(result.success).toBe(false);
    expect(result.diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/compiler/unimplemented-stage",
    );
  });
});