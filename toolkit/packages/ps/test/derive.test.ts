import { describe, expect, it } from "vitest";
import { emptyIR, type IRModel, type IROperation, type IRTypeRef } from "@azure-client-engine/ir";

import { deriveAttributes } from "../src/derive.js";

const modelRef = (name: string): IRTypeRef => ({
  kind: "model",
  id: { module: "<root>", namespace: [], name },
});

function makeProperty(
  name: string,
  type: IRTypeRef,
  optional: boolean,
): IRModel["properties"][number] {
  const common = {
    id: { module: "<root>", namespace: [], name: `prop#${name}` },
    name,
    wireName: name,
    optional,
    nullable: false,
  };
  if (type.kind === "array") return { ...common, typeKind: "collection", type };
  if (type.kind === "model") return { ...common, typeKind: "object", type };
  return { ...common, typeKind: "primitive", type: { kind: "primitive", name: "string" } };
}

/** ARM collection envelope: required `value` array + optional `nextLink`. */
function makeEnvelope(name: string, itemName: string): IRModel {
  return {
    id: { module: "<root>", namespace: [], name },
    name,
    source: "<root>",
    properties: [
      makeProperty("value", { kind: "array", element: modelRef(itemName) }, false),
      makeProperty("nextLink", { kind: "primitive", name: "string" }, true),
    ],
  };
}

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

  it("uses a pageable operation's item type as the output type, not the page wrapper", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "list" },
      method: "GET",
      responses: [
        {
          statusCode: { kind: "exact", code: 200 },
          isError: false,
          type: { kind: "model", id: { module: "<root>", namespace: [], name: "ResourceGroupListResult" } },
        },
      ],
      semantics: {
        kind: "pageable",
        itemType: { kind: "model", id: { module: "<root>", namespace: [], name: "ResourceGroup" } },
        itemsPath: "value",
        nextLinkPath: "nextLink",
      },
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [operation] });

    expect(commands[0]!.supportsPaging).toBe(true);
    expect(commands[0]!.noun).toBe("ResourceGroup");
    // The page wrapper would be inconsistent with the inferred noun.
    expect(commands[0]!.outputTypes).toEqual([
      { module: "<root>", namespace: [], name: "ResourceGroup" },
    ]);
  });

  it("counts a request body as a required parameter when picking the default parameter set", () => {
    const withBody = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "create" },
      method: "PUT",
      requestBody: {
        type: { kind: "model", id: { module: "<root>", namespace: [], name: "ResourceGroup" } },
        contentType: "application/json",
      },
    });
    const withoutBody = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [withBody, withoutBody] });

    expect(commands).toHaveLength(1);
    expect(commands[0]!.defaultParameterSetName).toBe("CreateOrUpdate");
  });

  it("keeps operations from different IR sources in separate commands", () => {
    // Same IRId (module + namespace + name) on purpose: only `source` distinguishes these, so
    // this fails if the grouping key relies on `id.module` alone.
    const id = { module: "<root>", namespace: ["ResourceGroups"], name: "get" };
    const first = makeOperation({ id, method: "GET", source: "Microsoft.Resources" });
    const second = makeOperation({ id, method: "GET", source: "Microsoft.Storage" });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [first, second] });

    expect(commands).toHaveLength(2);
  });

  it("unwraps a non-pageable collection envelope for both the noun and the output type", () => {
    // Real ElasticSan case: PrivateLinkResources.listByElasticSan carries no pageable semantics,
    // so only the envelope's shape distinguishes it from a single-resource read.
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["PrivateLinkResources"], name: "listByElasticSan" },
      method: "GET",
      responses: [
        {
          statusCode: { kind: "exact", code: 200 },
          isError: false,
          type: modelRef("PrivateLinkResourceListResult"),
        },
      ],
    });
    const ir = {
      ...emptyIR(),
      models: [makeEnvelope("PrivateLinkResourceListResult", "PrivateLinkResource")],
      operations: [operation],
    };

    const { commands } = deriveAttributes(ir);

    expect(commands[0]!.noun).toBe("PrivateLinkResource");
    expect(commands[0]!.outputTypes).toEqual([
      { module: "<root>", namespace: [], name: "PrivateLinkResource" },
    ]);
  });

  it("does not unwrap an ordinary model that merely holds one array", () => {
    const ruleSet: IRModel = {
      id: { module: "<root>", namespace: [], name: "NetworkRuleSet" },
      name: "NetworkRuleSet",
      source: "<root>",
      properties: [
        makeProperty(
          "virtualNetworkRules",
          { kind: "array", element: modelRef("VirtualNetworkRule") },
          false,
        ),
      ],
    };
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["NetworkRuleSets"], name: "get" },
      method: "GET",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, isError: false, type: modelRef("NetworkRuleSet") },
      ],
    });

    const { commands } = deriveAttributes({ ...emptyIR(), models: [ruleSet], operations: [operation] });

    expect(commands[0]!.noun).toBe("NetworkRuleSet");
  });

  it("gives every operation on a resource the same noun, including bodiless DELETEs", () => {
    // Real ElasticSan case: VolumeSnapshots.get returns `Snapshot` while VolumeSnapshots.delete
    // has no response at all, which previously yielded Get-Snapshot next to Remove-VolumeSnapshot.
    const get = makeOperation({
      id: { module: "<root>", namespace: ["VolumeSnapshots"], name: "get" },
      method: "GET",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, isError: false, type: modelRef("Snapshot") },
      ],
    });
    const remove = makeOperation({
      id: { module: "<root>", namespace: ["VolumeSnapshots"], name: "delete" },
      method: "DELETE",
      responses: [],
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [get, remove] });

    const nouns = new Set(commands.map((c) => c.noun));
    expect(nouns).toEqual(new Set(["Snapshot"]));
    expect(commands.map((c) => `${c.verb}-${c.noun}`).sort()).toEqual([
      "Get-Snapshot",
      "Remove-Snapshot",
    ]);
  });

  it("resolves the verb from the leading action word, not a prefix of a short table key", () => {
    // `is` -> Test and `end` -> Stop are real entries; prefix matching would hijack these.
    const restore = makeOperation({
      id: { module: "<root>", namespace: ["ElasticSanClient"], name: "restoreVolume" },
      method: "POST",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, isError: false, type: modelRef("Volume") },
      ],
    });
    const issue = makeOperation({
      id: { module: "<root>", namespace: ["Credentials"], name: "issueCredential" },
      method: "POST",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, isError: false, type: modelRef("Credential") },
      ],
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [restore, issue] });

    const verbs = Object.fromEntries(commands.map((c) => [c.noun, c.verb]));
    expect(verbs["Volume"]).toBe("Restore");
    expect(verbs["Credential"]).toBe("Invoke");
  });
});
