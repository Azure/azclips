import { describe, expect, it } from "vitest";
import { emptyIR, type IROperation } from "@azure-client-engine/ir";

import { derivePsCommands } from "../src/derive.js";

function makeOperation(overrides: Partial<IROperation> & Pick<IROperation, "id" | "method">): IROperation {
  return {
    name: overrides.id.name,
    source: "<root>",
    pathTemplate: "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
    pathParameters: [],
    queryParameters: [],
    headerParameters: [],
    responses: [
      {
        statusCode: { kind: "exact", code: 200 },
        isError: false,
        type: { kind: "model", id: { module: "<root>", namespace: [], name: "ResourceGroup" } },
      },
    ],
    apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
    server: { kind: "literal", url: "https://management.azure.com" },
    ...overrides,
  };
}

describe("derivePsCommands", () => {
  it("derives one PsCommand from a single enriched operation", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: {
        powershell: { verb: "New", noun: "ResourceGroup", subjectPrefix: "Az", confirmImpact: "Medium" },
      },
    });

    const [command] = derivePsCommands({ ...emptyIR(), operations: [operation] });

    expect(command).toBeDefined();
    expect(command!.verb).toBe("New");
    expect(command!.noun).toBe("AzResourceGroup");
    expect(command!.supportsShouldProcess).toBe(true);
    expect(command!.parameterSets).toHaveLength(1);
    expect(command!.outputTypes).toHaveLength(1);
  });

  it("merges Get + List sharing verb+noun into one PsCommand with two parameter sets", () => {
    const get = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      rendering: { powershell: { verb: "Get", noun: "ResourceGroup", parameterSetName: "Get" } },
    });
    const list = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "list" },
      method: "GET",
      rendering: { powershell: { verb: "Get", noun: "ResourceGroup", parameterSetName: "List" } },
    });

    const commands = derivePsCommands({ ...emptyIR(), operations: [get, list] });

    expect(commands).toHaveLength(1);
    expect(commands[0]!.parameterSets.map((p) => p.name).sort()).toEqual(["Get", "List"]);
    expect(commands[0]!.supportsShouldProcess).toBe(false);
  });

  it("skips operations create-ps-commands hasn't enriched yet", () => {
    const enriched = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      rendering: { powershell: { verb: "Get", noun: "ResourceGroup" } },
    });
    const notEnriched = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "delete" },
      method: "DELETE",
    });

    const commands = derivePsCommands({ ...emptyIR(), operations: [enriched, notEnriched] });

    expect(commands).toHaveLength(1);
    expect(commands[0]!.verb).toBe("Get");
  });

  it("fans out base + Expanded when the request body's FlattenHint says to flatten", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: { powershell: { verb: "New", noun: "ResourceGroup", parameterSetName: "CreateOrUpdate" } },
      requestBody: {
        type: { kind: "model", id: { module: "<root>", namespace: [], name: "ResourceGroup" } },
        contentType: "application/json",
        rendering: { flatten: { into: "self" } },
      },
    });

    const [command] = derivePsCommands({ ...emptyIR(), operations: [operation] });

    expect(command!.parameterSets.map((p) => p.name).sort()).toEqual([
      "CreateOrUpdate",
      "CreateOrUpdateExpanded",
    ]);
  });

  it("does not fan out when the request body has no flatten-into-self hint", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: { powershell: { verb: "New", noun: "ResourceGroup", parameterSetName: "CreateOrUpdate" } },
      requestBody: {
        type: { kind: "model", id: { module: "<root>", namespace: [], name: "ResourceGroup" } },
        contentType: "application/json",
      },
    });

    const [command] = derivePsCommands({ ...emptyIR(), operations: [operation] });

    expect(command!.parameterSets.map((p) => p.name)).toEqual(["CreateOrUpdate"]);
  });
});
