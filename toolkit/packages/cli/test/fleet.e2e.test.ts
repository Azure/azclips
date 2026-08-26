/**
 * Real-world coverage proof: compiles the actual Azure Compute Fleet management API
 * (`Microsoft.AzureFleet`, from the real `azure-rest-api-specs` TypeSpec source, built on the
 * real `@azure-tools/typespec-azure-resource-manager` templates) through the real
 * `create-ps-commands` + `derivePsCommands`/`deriveModules` pipeline. Exercises real ARM LRO
 * operations, a multi-operation parameter-set merge, and a nested list operation whose response
 * model doesn't match its containing interface.
 *
 * Chosen over a ResourceGroup-based fixture because it has a real, currently-generated
 * counterpart to diff against: `azure-powershell/generated/ComputeFleet/ComputeFleet.Autorest/`
 * (built via the real `@azure-tools/typespec-powershell` emitter, confirmed by its
 * `tspconfig.yaml`). Diffing our derived commands against that real output found two real gaps
 * fixed alongside this fixture:
 * - Noun inference must follow the response model, not the containing namespace, or
 *   `listVirtualMachineScaleSets` (declared under `Fleets`, returns
 *   `VirtualMachineScaleSetListResult`) wrongly merges into `Get-AzFleet`.
 * - `derivePsCommands`'s noun-prefix combining must skip the bare noun when it's already a
 *   suffix of the subject prefix (`ps-commands/src/derive.ts`'s `combineNoun`), matching the
 *   real module's own `Get-AzComputeFleet` (not `Get-AzComputeFleetFleet`) for its own
 *   eponymous `Fleet` resource — covered directly in `derive.test.ts`, not re-tested here since
 *   this fixture uses a plain `"Az"` prefix with no suffix collision.
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

const FLEET_TSP = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "typespec-emitter",
  "test",
  "fixtures",
  "fleet",
  "main.tsp",
);

describe("create-ps-commands + derivePsCommands/deriveModules: real Azure Fleet spec end-to-end", () => {
  it("merges get/listByResourceGroup/listBySubscription into one Get-AzFleet command, defaulting to the most general set", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(FLEET_TSP));
    const pipeline = createTransformPipeline("fleet", [createPsCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile({ entrypoint: FLEET_TSP }, { emit: producer, transform: pipeline });
    expect(result.success).toBe(true);

    const commands = derivePsCommands(result.ir!);
    const get = commands.find((c) => c.verb === "Get" && c.noun === "AzFleet");

    expect(get).toBeDefined();
    expect(get!.supportsShouldProcess).toBe(false);
    expect(get!.parameterSets.map((p) => p.name).sort()).toEqual(["Get", "ListByResourceGroup", "ListBySubscription"]);
    // ListBySubscription needs neither resourceGroupName nor fleetName — the most general set.
    // Real precedent: the real Get-AzComputeFleet.ps1's own DefaultParameterSetName ("List1")
    // is the same subscription-wide list operation, just under an abbreviated set name.
    expect(get!.defaultParameterSetName).toBe("ListBySubscription");
  });

  it("keeps New-AzFleet (createOrUpdate/PUT) and Update-AzFleet (update/PATCH) as separate commands", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(FLEET_TSP));
    const pipeline = createTransformPipeline("fleet", [createPsCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile({ entrypoint: FLEET_TSP }, { emit: producer, transform: pipeline });
    const commands = derivePsCommands(result.ir!);

    const create = commands.find((c) => c.verb === "New" && c.noun === "AzFleet");
    const update = commands.find((c) => c.verb === "Update" && c.noun === "AzFleet");
    const remove = commands.find((c) => c.verb === "Remove" && c.noun === "AzFleet");

    expect(create).toBeDefined();
    expect(create!.supportsShouldProcess).toBe(true);
    expect(update).toBeDefined();
    expect(update!.supportsShouldProcess).toBe(true);
    expect(remove).toBeDefined();
    expect(remove!.supportsShouldProcess).toBe(true);
  });

  it("gives listVirtualMachineScaleSets its own Get-AzVirtualMachineScaleSet command instead of merging into Get-AzFleet", async () => {
    // Real gap, found by compiling this exact spec: without noun inference following the
    // response model, this operation (declared under Fleets, returns
    // VirtualMachineScaleSetListResult) would wrongly become a 4th Get-AzFleet parameter set.
    // Real precedent: the real module exports this as its own Get-AzComputeFleetVirtualMachineScaleSet.
    const producer = createSubprocessEmitProducer(resolveToolchain(FLEET_TSP));
    const pipeline = createTransformPipeline("fleet", [createPsCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile({ entrypoint: FLEET_TSP }, { emit: producer, transform: pipeline });
    const commands = derivePsCommands(result.ir!);

    const vmss = commands.find((c) => c.verb === "Get" && c.noun === "AzVirtualMachineScaleSet");
    const fleet = commands.find((c) => c.verb === "Get" && c.noun === "AzFleet");

    expect(vmss).toBeDefined();
    expect(fleet!.parameterSets.map((p) => p.name)).not.toContain("ListVirtualMachineScaleSets");
  });

  it("also derives Get-AzOperation from the real ARM common Operations interface, and groups everything into one PsModule", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(FLEET_TSP));
    const pipeline = createTransformPipeline("fleet", [createPsCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile({ entrypoint: FLEET_TSP }, { emit: producer, transform: pipeline });
    const commands = derivePsCommands(result.ir!);
    const modules = deriveModules(result.ir!);

    expect(commands.some((c) => c.verb === "Get" && c.noun === "AzOperation")).toBe(true);
    expect(modules.length).toBeGreaterThan(0);
    const cmdletsToExport = modules.flatMap((m) => m.cmdletsToExport);
    expect(cmdletsToExport).toEqual(expect.arrayContaining(["New-AzFleet", "Get-AzFleet", "Remove-AzFleet"]));
  });

  it("Expanded variant flattens the Fleet request body (variant fan-out, exercised end-to-end)", async () => {
    // No flatten-decision TransformStep exists yet in this codebase (a real, separate gap —
    // see design.md's Non-Goals discussion on FlattenHint reuse). This test-only step simulates
    // what that future transform would produce, so the *plumbing* between create-ps-commands
    // and derivePsCommands' fan-out is proven end-to-end rather than only in derive.test.ts's
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

    const producer = createSubprocessEmitProducer(resolveToolchain(FLEET_TSP));
    const pipeline = createTransformPipeline("variant-fan-out", [
      createPsCommandsFactory({ subjectPrefix: "Az" }),
      simulateFlattenDecision,
    ]);

    const result = await compile({ entrypoint: FLEET_TSP }, { emit: producer, transform: pipeline });
    const commands = derivePsCommands(result.ir!);

    const createOrUpdate = commands.find((c) => c.verb === "New" && c.noun === "AzFleet");
    expect(createOrUpdate!.parameterSets.map((p) => p.name).sort()).toEqual([
      "CreateOrUpdate",
      "CreateOrUpdateExpanded",
    ]);
  });

  it("parameter hints survive a real compile unclobbered (an author-set value wins over create-ps-commands' own mandatory/helpMessage inference)", async () => {
    // hints.powershell has no TypeSpec-decorator authoring path yet — this test-only step
    // stands in for that, simulating an author setting parameter-level hints inline before
    // create-ps-commands runs, on the real `fleetName` path parameter shared by
    // get/createOrUpdate/delete.
    const simulateAuthoring = {
      name: "simulate-authoring",
      run: (ir: IR): IR => ({
        ...ir,
        operations: ir.operations.map((operation) => ({
          ...operation,
          pathParameters: operation.pathParameters.map((parameter) =>
            parameter.name === "fleetName"
              ? {
                  ...parameter,
                  rendering: {
                    powershell: { mandatory: true, helpMessage: "The name of the Fleet.", position: 0 },
                  },
                }
              : parameter,
          ),
        })),
      }),
    };

    const producer = createSubprocessEmitProducer(resolveToolchain(FLEET_TSP));
    const pipeline = createTransformPipeline("parameter-hints", [
      simulateAuthoring,
      createPsCommandsFactory({ subjectPrefix: "Az" }),
    ]);

    const result = await compile({ entrypoint: FLEET_TSP }, { emit: producer, transform: pipeline });
    expect(result.success).toBe(true);

    const get = result.ir!.operations.find((o) => o.id.name === "get" && o.id.namespace.includes("Fleets"));
    const fleetName = get!.pathParameters.find((p) => p.name === "fleetName");

    expect(fleetName!.rendering!.powershell).toEqual({
      mandatory: true,
      helpMessage: "The name of the Fleet.",
      position: 0,
    });
  });

  it("directives apply against real Fleets operations", async () => {
    const producer = createSubprocessEmitProducer(resolveToolchain(FLEET_TSP));
    const pipeline = createTransformPipeline("directives", [
      createPsCommandsFactory({
        subjectPrefix: "Az",
        directives: [
          // Command-level: hide Remove-AzFleet.
          { select: "command", where: { verb: "Remove", subject: "Fleet" }, set: { hidden: true } },
          // Parameter-level: alias the real fleetName parameter across every command.
          { where: { parameterName: "fleetName" }, set: { alias: ["Name"] } },
        ],
      }),
    ]);

    const result = await compile({ entrypoint: FLEET_TSP }, { emit: producer, transform: pipeline });
    expect(result.success).toBe(true);

    const commands = derivePsCommands(result.ir!);
    const del = commands.find((c) => c.verb === "Remove" && c.noun === "AzFleet");
    expect(del!.hidden).toBe(true);

    const get = result.ir!.operations.find((o) => o.id.name === "get" && o.id.namespace.includes("Fleets"));
    const fleetName = get!.pathParameters.find((p) => p.name === "fleetName");
    expect(fleetName!.rendering!.powershell!.aliases).toEqual(["Name"]);
  });
});
