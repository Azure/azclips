/**
 * Milestone 0 proof: `IR -> (enriched IR) -> Command[]` end-to-end.
 *
 * Compiles the hand-authored `resourcegroup.tsp` fixture (chosen because a
 * real hand-written comparison exists — see the fixture's own header
 * comment) through the real `compile()` backbone with `create-commands` in
 * the transform pipeline, then calls the separate `deriveCommands()` (see
 * design.md Decision 8) on the result.
 */

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import { normalizePath } from "@typespec/compiler";
import { deriveCommands } from "@azure-client-engine/ps-commands";

import { compile, createTransformPipeline } from "../src/index.js";
import { createCommandsFactory } from "../src/create-commands.js";

const RESOURCE_GROUP_TSP = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "fixtures", "resource-group", "resourcegroup.tsp"),
);

describe("create-commands + deriveCommands: resourcegroup.tsp end-to-end", () => {
  it("produces a New-AzResourceGroup command from ResourceGroups.createOrUpdate", async () => {
    const pipeline = createTransformPipeline("m0", [createCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile({ entrypoint: RESOURCE_GROUP_TSP }, { transform: pipeline });

    expect(result.success).toBe(true);
    expect(result.ir).toBeDefined();

    const commands = deriveCommands(result.ir!);
    const createOrUpdate = commands.find((c) => c.verb === "New" && c.noun === "AzResourceGroup");

    expect(createOrUpdate).toBeDefined();
    expect(createOrUpdate!.supportsShouldProcess).toBe(true);
    expect(createOrUpdate!.confirmImpact).toBe("Medium");
  });

  it("merges get + list into one Get-AzResourceGroup command with two parameter sets", async () => {
    const pipeline = createTransformPipeline("m0", [createCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile({ entrypoint: RESOURCE_GROUP_TSP }, { transform: pipeline });
    const commands = deriveCommands(result.ir!);

    const get = commands.find((c) => c.verb === "Get" && c.noun === "AzResourceGroup");
    expect(get).toBeDefined();
    expect(get!.supportsShouldProcess).toBe(false);
    expect(get!.parameterSets.map((p) => p.name).sort()).toEqual(["Get", "List"]);
  });

  it("produces a Remove-AzResourceGroup command from ResourceGroups.delete", async () => {
    const pipeline = createTransformPipeline("m0", [createCommandsFactory({ subjectPrefix: "Az" })]);

    const result = await compile({ entrypoint: RESOURCE_GROUP_TSP }, { transform: pipeline });
    const commands = deriveCommands(result.ir!);

    const del = commands.find((c) => c.verb === "Remove" && c.noun === "AzResourceGroup");
    expect(del).toBeDefined();
    expect(del!.supportsShouldProcess).toBe(true);
  });
});
