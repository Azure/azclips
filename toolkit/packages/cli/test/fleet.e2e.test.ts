/**
 * Real-world coverage proof: compiles the actual Azure Compute Fleet
 * management API (`Microsoft.AzureFleet`, from the real `azure-rest-api-specs`
 * TypeSpec source, built on the real `@azure-tools/typespec-azure-resource-manager`
 * templates) through the real `create-ps-commands` + `derivePsCommands`/
 * `deriveModules` pipeline — unlike `resourcegroup.tsp` (hand-simplified,
 * avoids the ARM template library so its IR shape stays fully predictable),
 * this fixture exercises real ARM LRO operations, a 4-way-candidate
 * parameter-set merge, and a nested list operation whose response model
 * doesn't match its containing interface.
 *
 * Found this exact gap by compiling this fixture: `listVirtualMachineScaleSets`
 * is declared under the `Fleets` interface but returns `VirtualMachineScaleSetListResult`,
 * not a `Fleet` — noun inference must follow the response model, not the
 * containing namespace, or it wrongly merges into `Get-AzFleet`.
 */

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import { compile, createPsCommandsFactory, createTransformPipeline } from "@azure-client-engine/compiler";
import { derivePsCommands, deriveModules } from "@azure-client-engine/ps-commands";

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
});
