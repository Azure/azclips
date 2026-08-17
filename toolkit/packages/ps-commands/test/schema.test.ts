/**
 * Zod validation smoke tests for the thin `Command`/`ParameterSet` aggregate.
 */

import { describe, expect, it } from "vitest";

import { CommandSchema, ParameterSetSchema, type Command, type ParameterSet } from "../src/index.js";

const OPERATION_ID = {
  module: "<root>",
  namespace: ["ResourceGroups"],
  name: "createOrUpdate",
};

describe("ParameterSet", () => {
  it("parses a valid parameter set", () => {
    const parameterSet: ParameterSet = { name: "Get", sourceOperationId: OPERATION_ID };

    expect(ParameterSetSchema.parse(parameterSet)).toEqual(parameterSet);
  });

  it("rejects a parameter set with no source operation id", () => {
    expect(() => ParameterSetSchema.parse({ name: "Get" })).toThrow();
  });
});

describe("Command", () => {
  const validParameterSet: ParameterSet = { name: "Get", sourceOperationId: OPERATION_ID };

  it("parses a valid command", () => {
    const command: Command = {
      verb: "New",
      noun: "AzResourceGroup",
      subjectPrefix: "Az",
      supportsShouldProcess: true,
      confirmImpact: "Medium",
      outputTypes: [],
      parameterSets: [validParameterSet],
      sourceOperationIds: [OPERATION_ID],
    };

    expect(CommandSchema.parse(command)).toEqual(command);
  });

  it("rejects a command with no parameter sets", () => {
    expect(() =>
      CommandSchema.parse({
        verb: "New",
        noun: "AzResourceGroup",
        supportsShouldProcess: true,
        outputTypes: [],
        parameterSets: [],
        sourceOperationIds: [OPERATION_ID],
      }),
    ).toThrow();
  });

  it("rejects a command with no source operation ids", () => {
    expect(() =>
      CommandSchema.parse({
        verb: "New",
        noun: "AzResourceGroup",
        supportsShouldProcess: true,
        outputTypes: [],
        parameterSets: [validParameterSet],
        sourceOperationIds: [],
      }),
    ).toThrow();
  });
});
