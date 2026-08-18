/**
 * Milestone 0 proof: `IR -> (enriched IR) -> PsCommand[]` end-to-end, via a
 * real dynamic TypeSpec compile.
 *
 * Lives in `cli` (not `compiler`) because the compiler orchestrator is
 * TypeSpec-free (see `pipeline.test.ts`'s emit-seam tests) — the only way to
 * turn a real `.tsp` fixture into IR is the CLI's out-of-process
 * `createSubprocessEmitProducer`, exactly as `tsp-compile.test.ts` does for
 * `pets.tsp`. Compiles the hand-authored `resourcegroup.tsp` fixture
 * (chosen because a real hand-written comparison exists — see the fixture's
 * own header comment) through the real `compile()` backbone with
 * `create-ps-commands` in the transform pipeline, then calls the separate
 * `derivePsCommands()` (see design.md Decision 8) on the result.
 */

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import { compile, createPsCommandsFactory, createTransformPipeline } from "@azure-client-engine/compiler";
import { derivePsCommands } from "@azure-client-engine/ps-commands";
import type { IR } from "@azure-client-engine/ir";

// These tests fork the real (compiled) worker process, so they must exercise the
// built artifacts under dist — Node cannot execute the .ts worker directly, and
// under vitest `import.meta.url` would otherwise point at the source tree.
import { resolveToolchain } from "../dist/src/toolchain.js";
import { createSubprocessEmitProducer } from "../dist/src/tsp-compile.js";

const RESOURCE_GROUP_TSP = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "compiler",
  "test",
  "fixtures",
  "resource-group",
  "resourcegroup.tsp",
);

describe("create-ps-commands + derivePsCommands: resourcegroup.tsp end-to-end", () => {
  it("produces a New-AzResourceGroup command from ResourceGroups.createOrUpdate", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("m0", [createPsCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );

    expect(result.success).toBe(true);
    expect(result.ir).toBeDefined();

    const commands = derivePsCommands(result.ir!);
    const createOrUpdate = commands.find((c) => c.verb === "New" && c.noun === "AzResourceGroup");

    expect(createOrUpdate).toBeDefined();
    expect(createOrUpdate!.supportsShouldProcess).toBe(true);
    expect(createOrUpdate!.confirmImpact).toBe("Medium");
  });

  it("merges get + list into one Get-AzResourceGroup command with two parameter sets", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("m0", [createPsCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );
    const commands = derivePsCommands(result.ir!);

    const get = commands.find((c) => c.verb === "Get" && c.noun === "AzResourceGroup");
    expect(get).toBeDefined();
    expect(get!.supportsShouldProcess).toBe(false);
    expect(get!.parameterSets.map((p) => p.name).sort()).toEqual(["Get", "List"]);
  });

  it("produces a Remove-AzResourceGroup command from ResourceGroups.delete", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("m0", [createPsCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );
    const commands = derivePsCommands(result.ir!);

    const del = commands.find((c) => c.verb === "Remove" && c.noun === "AzResourceGroup");
    expect(del).toBeDefined();
    expect(del!.supportsShouldProcess).toBe(true);
  });

  it("Expanded variant flattens ResourceGroupProperties (Milestone 1 fan-out, exercised end-to-end)", async () => {
    // No flatten-decision TransformStep exists yet in this codebase (a real,
    // separate gap — see design.md's Non-Goals discussion on FlattenHint reuse).
    // This test-only step simulates what that future transform would produce,
    // so the *plumbing* between create-ps-commands and derivePsCommands' fan-out
    // (Milestone 1) is proven end-to-end rather than only in derive.test.ts's
    // isolated unit tests.
    const simulateFlattenDecision = {
      name: "simulate-flatten-decision",
      run: (ir: IR): IR => ({
        ...ir,
        operations: ir.operations.map((operation) =>
          operation.id.name === "createOrUpdate" && operation.requestBody
            ? {
                ...operation,
                requestBody: {
                  ...operation.requestBody,
                  rendering: { flatten: { into: "self" as const } },
                },
              }
            : operation,
        ),
      }),
    };

    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("m1", [
      createPsCommandsFactory({ subjectPrefix: "Az" }),
      simulateFlattenDecision,
    ]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );
    const commands = derivePsCommands(result.ir!);

    const createOrUpdate = commands.find((c) => c.verb === "New" && c.noun === "AzResourceGroup");
    expect(createOrUpdate!.parameterSets.map((p) => p.name).sort()).toEqual([
      "CreateOrUpdate",
      "CreateOrUpdateExpanded",
    ]);
  });
});
