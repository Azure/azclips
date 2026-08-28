import { describe, expect, it } from "vitest";
import { emptyIR, type IROperation } from "@azure-client-engine/ir";

import { deriveAttributes } from "../src/derive.js";

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

describe("deriveAttributes", () => {
  it("derives one command from a single operation, inferring verb/noun from the action word and response type", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
    });

    const { commands, parameters } = deriveAttributes({ ...emptyIR(), operations: [operation] }, { subjectPrefix: "Az" });

    expect(commands).toHaveLength(1);
    expect(commands[0]!.verb).toBe("New");
    expect(commands[0]!.noun).toBe("AzResourceGroup");
    expect(commands[0]!.supportsShouldProcess).toBe(true);
    expect(commands[0]!.parameterSets).toHaveLength(1);
    expect(commands[0]!.outputTypes).toHaveLength(1);
    expect(commands[0]!.defaultParameterSetName).toBe("CreateOrUpdate");
    expect(commands[0]!.sourceOperationIds).toEqual([operation.id]);
    expect(parameters).toHaveLength(0);
  });

  it("merges operations sharing verb+noun into one command with one parameter set per source operation", () => {
    const get = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathParameters: [
        {
          id: { module: "<root>", namespace: ["ResourceGroups"], name: "get#name" },
          name: "resourceGroupName",
          wireName: "resourceGroupName",
          type: { kind: "primitive", name: "string" },
          optional: false,
          nullable: false,
        },
      ],
    });
    const list = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "list" },
      method: "GET",
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [get, list] });

    expect(commands).toHaveLength(1);
    expect(commands[0]!.parameterSets).toHaveLength(2);
    // "List" requires fewer parameters than "Get" -> wins as the default set (real
    // GetResourceGroupCmdlet precedent).
    expect(commands[0]!.defaultParameterSetName).toBe("List");
  });

  it("skips the bare noun when it's already a suffix of the subject prefix (real ComputeFleet precedent)", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["Fleets"], name: "get" },
      method: "GET",
      responses: [
        {
          statusCode: { kind: "exact", code: 200 },
          isError: false,
          type: { kind: "model", id: { module: "<root>", namespace: [], name: "Fleet" } },
        },
      ],
    });

    const { commands } = deriveAttributes(
      { ...emptyIR(), operations: [operation] },
      { subjectPrefix: "AzComputeFleet" },
    );

    expect(commands[0]!.noun).toBe("AzComputeFleet");
  });

  it("derives one PSParameterAttribute per operation parameter, tagged with its parameter set", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      pathParameters: [
        {
          id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate#name" },
          name: "resourceGroupName",
          wireName: "resourceGroupName",
          type: { kind: "primitive", name: "string" },
          optional: false,
          nullable: false,
        },
      ],
    });

    const { parameters } = deriveAttributes({ ...emptyIR(), operations: [operation] });

    expect(parameters).toHaveLength(1);
    expect(parameters[0]!.name).toBe("resourceGroupName");
    expect(parameters[0]!.mandatory).toBe(true);
    expect(parameters[0]!.parameterSetName).toBe("CreateOrUpdate");
    expect(parameters[0]!.sourceParameterId).toEqual(operation.pathParameters[0]!.id);
  });

  it("derives one module per IR source", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      source: "Microsoft.Resources",
    });
    const ir = {
      ...emptyIR(),
      sources: [
        {
          id: "Microsoft.Resources",
          entrypoint: "main.tsp",
          apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
          server: { kind: "literal" as const, url: "https://management.azure.com" },
          rootNamespace: ["Microsoft.Resources"],
        },
      ],
      operations: [operation],
    };

    const { modules } = deriveAttributes(ir);

    expect(modules).toHaveLength(1);
    expect(modules[0]!.sourceId).toBe("Microsoft.Resources");
    expect(modules[0]!.cmdletsToExport).toEqual(["New-ResourceGroup"]);
  });
});
