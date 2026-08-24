/**
 * Zod validation smoke tests for the thin `PsCommand`/`PsParameterSet` aggregate.
 */

import { describe, expect, it } from "vitest";

import { PsCommandSchema, PsModuleSchema, PsParameterSetSchema, type PsCommand, type PsModule, type PsParameterSet } from "../src/index.js";

const OPERATION_ID = {
  module: "<root>",
  namespace: ["ResourceGroups"],
  name: "createOrUpdate",
};

describe("PsParameterSet", () => {
  it("parses a valid parameter set", () => {
    const parameterSet: PsParameterSet = { name: "Get", sourceOperationId: OPERATION_ID };

    expect(PsParameterSetSchema.parse(parameterSet)).toEqual(parameterSet);
  });

  it("rejects a parameter set with no source operation id", () => {
    expect(() => PsParameterSetSchema.parse({ name: "Get" })).toThrow();
  });
});

describe("PsCommand", () => {
  const validParameterSet: PsParameterSet = { name: "Get", sourceOperationId: OPERATION_ID };

  it("parses a valid command", () => {
    const command: PsCommand = {
      verb: "New",
      noun: "AzResourceGroup",
      subjectPrefix: "Az",
      supportsShouldProcess: true,
      confirmImpact: "Medium",
      outputTypes: [],
      parameterSets: [validParameterSet],
      defaultParameterSetName: "Get",
      sourceOperationIds: [OPERATION_ID],
      alias: ["nrg"],
      hidden: false,
      summary: "Creates a resource group.",
      description: "Creates a resource group in the specified location.",
      examples: ["New-AzResourceGroup -Name rg1 -Location eastus"],
      lifecycleTag: "GA",
      httpPath: { path: "/subscriptions/{subscriptionId}/resourcegroups/{name}", apiVersion: "2025-04-01" },
      supportsPaging: false,
      helpUri: "https://learn.microsoft.com/powershell/module/az.resources/new-azresourcegroup",
    };

    expect(PsCommandSchema.parse(command)).toEqual(command);
  });

  it("rejects a command with no parameter sets", () => {
    expect(() =>
      PsCommandSchema.parse({
        verb: "New",
        noun: "AzResourceGroup",
        supportsShouldProcess: true,
        outputTypes: [],
        parameterSets: [],
        defaultParameterSetName: "Get",
        sourceOperationIds: [OPERATION_ID],
      }),
    ).toThrow();
  });

  it("rejects a command with no source operation ids", () => {
    expect(() =>
      PsCommandSchema.parse({
        verb: "New",
        noun: "AzResourceGroup",
        supportsShouldProcess: true,
        outputTypes: [],
        parameterSets: [validParameterSet],
        defaultParameterSetName: "Get",
        sourceOperationIds: [],
      }),
    ).toThrow();
  });

  it("rejects a command with no defaultParameterSetName", () => {
    expect(() =>
      PsCommandSchema.parse({
        verb: "New",
        noun: "AzResourceGroup",
        supportsShouldProcess: true,
        outputTypes: [],
        parameterSets: [validParameterSet],
        sourceOperationIds: [OPERATION_ID],
      }),
    ).toThrow();
  });
});

describe("PsModule", () => {
  it("parses a valid module", () => {
    const module: PsModule = {
      sourceId: "Microsoft.Resources",
      description: "Resource group management cmdlets.",
      cmdletsToExport: ["New-AzResourceGroup", "Get-AzResourceGroup", "Remove-AzResourceGroup"],
    };

    expect(PsModuleSchema.parse(module)).toEqual(module);
  });

  it("accepts a module with no description", () => {
    const parsed = PsModuleSchema.parse({ sourceId: "Microsoft.Resources", cmdletsToExport: [] });

    expect(parsed.description).toBeUndefined();
  });

  it("rejects a module with no sourceId", () => {
    expect(() => PsModuleSchema.parse({ cmdletsToExport: [] })).toThrow();
  });
});
