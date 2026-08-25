/**
 * Milestone 0 proof: `IR -> (enriched IR) -> PsCommand[]` end-to-end, via a
 * real dynamic TypeSpec compile.
 *
 * Lives in `cli` (not `compiler`) because the compiler orchestrator is
 * TypeSpec-free (see `pipeline.test.ts`'s emit-seam tests) — the only way to
 * turn a real `.tsp` fixture into IR is the CLI's out-of-process
 * `createSubprocessEmitProducer`, exactly as `tsp-compile.test.ts` does for
 * `pets.tsp`. Compiles the real, official `Microsoft.Resources` TypeSpec
 * (from `azure-rest-api-specs`, unmodified aside from being vendored
 * locally) through the real `compile()` backbone with `create-ps-commands`
 * in the transform pipeline, then calls the separate `derivePsCommands()`
 * (see design.md Decision 8) on the result. Spans `Operations`/
 * `ResourceGroups`/`Resources`/`Tags`/`Providers`, 24 derived commands
 * total, exercising the noun-inference fix (`listByResourceGroup`
 * correctly separates from `Get-AzResourceGroup`) against the exact
 * real-world case that inspired it — an earlier, separate real fixture
 * (the actual Azure Compute Fleet spec) found the same gap first and was
 * removed once this fixture was confirmed to cover it too.
 */


import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import { compile, createPsCommandsFactory, createTransformPipeline } from "@azure-client-engine/compiler";
import { derivePsCommands, deriveModules } from "@azure-client-engine/ps-commands";
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
  "typespec-emitter",
  "test",
  "fixtures",
  "resource-group",
  "main.tsp",
);

describe("create-ps-commands + derivePsCommands: real Microsoft.Resources spec end-to-end", () => {
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
    // Real precedent: GetResourceGroupCmdlet.DefaultParameterSetName = "List" — List has
    // fewer required parameters (no resourceGroupName) than Get, so it's more general.
    expect(get!.defaultParameterSetName).toBe("List");
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

  it("Milestone 1 parameter hints survive a real compile unclobbered (create-ps-commands infers no parameter-level fields)", async () => {
    // hints.powershell has no TypeSpec-decorator authoring path yet — this test-only step
    // stands in for that, simulating an author setting parameter-level hints inline before
    // create-ps-commands runs, on the real `resourceGroupName` path parameter shared by
    // get/createOrUpdate/delete.
    const simulateAuthoring = {
      name: "simulate-authoring",
      run: (ir: IR): IR => ({
        ...ir,
        operations: ir.operations.map((operation) => ({
          ...operation,
          pathParameters: operation.pathParameters.map((parameter) =>
            parameter.name === "resourceGroupName"
              ? {
                  ...parameter,
                  rendering: {
                    powershell: {
                      mandatory: true,
                      helpMessage: "The name of the resource group.",
                      position: 0,
                    },
                  },
                }
              : parameter,
          ),
        })),
      }),
    };

    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("m1", [
      simulateAuthoring,
      createPsCommandsFactory({ subjectPrefix: "Az" }),
    ]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );
    expect(result.success).toBe(true);

    const get = result.ir!.operations.find((o) => o.id.name === "get");
    const resourceGroupName = get!.pathParameters.find((p) => p.name === "resourceGroupName");

    // Real precedent: azclips's hand-written cmdlets require the resource group name.
    expect(resourceGroupName!.rendering!.powershell).toEqual({
      mandatory: true,
      helpMessage: "The name of the resource group.",
      position: 0,
    });
  });

  it("Milestone 2 directives apply against real ResourceGroups operations", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("m2", [
      createPsCommandsFactory({
        subjectPrefix: "Az",
        directives: [
          // Command-level: hide Remove-AzResourceGroup.
          { select: "command", where: { verb: "Remove", subject: "ResourceGroup" }, set: { hidden: true } },
          // Parameter-level: alias the real resourceGroupName parameter across every command.
          { where: { parameterName: "resourceGroupName" }, set: { alias: ["Name"] } },
        ],
      }),
    ]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );
    expect(result.success).toBe(true);

    const commands = derivePsCommands(result.ir!);
    const del = commands.find((c) => c.verb === "Remove" && c.noun === "AzResourceGroup");
    expect(del!.hidden).toBe(true);

    const get = result.ir!.operations.find((o) => o.id.name === "get");
    const resourceGroupName = get!.pathParameters.find((p) => p.name === "resourceGroupName");
    expect(resourceGroupName!.rendering!.powershell!.aliases).toEqual(["Name"]);
  });

  it("deriveModules groups every real Microsoft.Resources command into one PsModule, ResourceGroups cmdlets included", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("module-level", [
      createPsCommandsFactory({ subjectPrefix: "Az" }),
    ]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );
    expect(result.success).toBe(true);

    const modules = deriveModules(result.ir!);

    expect(modules).toHaveLength(1);
    expect(modules[0]!.cmdletsToExport).toEqual(
      expect.arrayContaining(["New-AzResourceGroup", "Get-AzResourceGroup", "Remove-AzResourceGroup"]),
    );
    // Real spec spans Operations/ResourceGroups/Resources/Tags/Providers, not just
    // ResourceGroups — proves the module groups the whole real RP, not a hand-picked subset.
    expect(modules[0]!.cmdletsToExport.length).toBeGreaterThan(20);
  });

  it("listByResourceGroup gets its own command instead of merging into Get-AzResourceGroup (the real case the noun-inference fix was built for)", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(RESOURCE_GROUP_TSP));
    const pipeline = createTransformPipeline("noun-inference", [
      createPsCommandsFactory({ subjectPrefix: "Az" }),
    ]);

    const result = await compile(
      { entrypoint: RESOURCE_GROUP_TSP },
      { emit: producer, transform: pipeline },
    );
    const commands = derivePsCommands(result.ir!);

    const get = commands.find((c) => c.verb === "Get" && c.noun === "AzResourceGroup");
    expect(get!.parameterSets.map((p) => p.name).sort()).toEqual(["Get", "List"]);

    // listByResourceGroup returns ResourceListResult (GenericResourceExpanded), not
    // ResourceGroup — real evidence the noun must follow the response model.
    const genericResourceList = commands.find((c) => c.parameterSets.some((p) => p.name === "ListByResourceGroup"));
    expect(genericResourceList).toBeDefined();
    expect(genericResourceList!.noun).not.toBe("AzResourceGroup");
  });
});
