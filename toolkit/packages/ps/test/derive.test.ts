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
  it("derives one command from a single operation, inferring the verb and subject from its group and action word", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "create" },
      method: "PUT",
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [operation] }, { subjectPrefix: "Az" });

    expect(commands).toHaveLength(1);
    expect(commands[0]!.verb).toBe("New");
    expect(commands[0]!.noun).toBe("AzResourceGroup");
    expect(commands[0]!.supportsShouldProcess).toBe(true);
    expect(commands[0]!.parameterSets).toHaveLength(1);
    expect(commands[0]!.outputTypes).toHaveLength(1);
    expect(commands[0]!.defaultParameterSetName).toBe("Create");
    expect(commands[0]!.sourceOperationIds).toEqual([operation.id]);
    expect(commands[0]!.parameters).toHaveLength(0);
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

  it("drops content-negotiation headers but keeps service-defined ones", () => {
    const header = (name: string, wireName: string, optional: boolean) => ({
      id: { module: "<root>", namespace: ["Volumes"], name: `delete.${wireName}` },
      name,
      wireName,
      type: { kind: "primitive" as const, name: "string" as const },
      optional,
      nullable: false,
    });
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["Volumes"], name: "delete" },
      method: "DELETE",
      responses: [],
      headerParameters: [
        header("accept", "Accept", false),
        header("contentType", "Content-Type", false),
        // Real ElasticSan ships this one as -ForceDelete.
        header("x-ms-force-delete", "x-ms-force-delete", true),
      ],
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [operation] });

    // The name autorest.powershell produces for this header -- ElasticSan's README renames it
    // with the selector `^XmsForceDelete$`.
    expect(commands[0]!.parameters.map((p) => p.name)).toEqual(["XmsForceDelete"]);
  });

  it("nests parameters under their command, omitting the set when a parameter spans all of them", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "create" },
      method: "PUT",
      pathParameters: [
        {
          id: { module: "<root>", namespace: ["ResourceGroups"], name: "create#name" },
          name: "resourceGroupName",
          wireName: "resourceGroupName",
          type: { kind: "primitive", name: "string" },
          optional: false,
          nullable: false,
        },
      ],
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [operation] });
    const parameters = commands[0]!.parameters;

    expect(parameters).toHaveLength(1);
    // PascalCase, and the redundant subject prefix is dropped with the full name kept as an alias.
    expect(parameters[0]!.name).toBe("Name");
    expect(parameters[0]!.alias).toEqual(["ResourceGroupName"]);
    expect(parameters[0]!.mandatory).toBe(true);
    // The command has a single set, so naming it would be noise.
    expect(parameters[0]!.parameterSetName).toBeUndefined();
    expect(parameters[0]!.sourceParameterId).toEqual(operation.pathParameters[0]!.id);
  });

  it("keeps a path parameter's full name when shortening would collide", () => {
    const param = (name: string) => ({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: `create#${name}` },
      name,
      wireName: name,
      type: { kind: "primitive" as const, name: "string" as const },
      optional: false,
      nullable: false,
    });
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "create" },
      method: "PUT",
      pathParameters: [param("resourceGroupName"), param("name")],
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [operation] });

    expect(commands[0]!.parameters.map((p) => p.name).sort()).toEqual(["Name", "ResourceGroupName"]);
  });

  it("emits one parameter per name across merged sets, recording which sets it belongs to", () => {
    const named = (operationName: string) => ({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: `${operationName}#name` },
      name: "resourceGroupName",
      wireName: "resourceGroupName",
      type: { kind: "primitive" as const, name: "string" as const },
      optional: false,
      nullable: false,
    });
    const get = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathParameters: [named("get")],
    });
    const list = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "list" },
      method: "GET",
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [get, list] });

    expect(commands).toHaveLength(1);
    const parameters = commands[0]!.parameters;
    // One property, not one per set -- and it only exists on the Get set.
    expect(parameters).toHaveLength(1);
    expect(parameters[0]!.parameterSetName).toBe("Get");
  });

  it("derives one module per IR source", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "create" },
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

    // Pageability drives the output type, but not SupportsPaging: that is opt-in per module via
    // the `clientside-pagination` directive, so a derived command never claims it.
    expect(commands[0]!.supportsPaging).toBeUndefined();
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
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "provision" },
      method: "PUT",
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [withBody, withoutBody] });

    // Both action words map to New, so they merge into one command with two sets.
    expect(commands).toHaveLength(1);
    expect(commands[0]!.defaultParameterSetName).toBe("Provision");
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

  it("attributes each command to its own source even when ids collide across sources", () => {
    // `IRId` is only unique within a source, so recovering the source by id lookup would put
    // both commands under whichever source happened to win the index.
    const id = { module: "<root>", namespace: ["ResourceGroups"], name: "get" };
    const sources = ["Microsoft.Resources", "Microsoft.Storage"].map((sourceId) => ({
      id: sourceId,
      entrypoint: "main.tsp",
      apiVersion: { value: "2025-04-01", location: "query" as const, parameterName: "api-version" },
      server: { kind: "literal" as const, url: "https://management.azure.com" },
      rootNamespace: [sourceId],
    }));
    const ir = {
      ...emptyIR(),
      sources,
      operations: [
        makeOperation({ id, method: "GET", source: "Microsoft.Resources" }),
        makeOperation({ id, method: "GET", source: "Microsoft.Storage" }),
      ],
    };

    const { modules } = deriveAttributes(ir);

    expect(modules.map((m) => m.sourceId).sort()).toEqual(["Microsoft.Resources", "Microsoft.Storage"]);
    for (const module of modules) {
      expect(module.cmdletsToExport).toEqual(["Get-ResourceGroup"]);
    }
  });

  it("omits httpPath on a merged command, which spans several endpoints", () => {
    const get = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathTemplate: "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
    });
    const list = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "list" },
      method: "GET",
      pathTemplate: "/subscriptions/{subscriptionId}/resourceGroups",
    });

    const merged = deriveAttributes({ ...emptyIR(), operations: [get, list] });
    expect(merged.commands).toHaveLength(1);
    expect(merged.commands[0]!.httpPath).toBeUndefined();

    const single = deriveAttributes({ ...emptyIR(), operations: [get] });
    expect(single.commands[0]!.httpPath?.path).toBe(
      "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
    );
  });

  it("does not unwrap a collection envelope whose value array is optional", () => {
    const envelope = makeEnvelope("MaybeListResult", "Item");
    envelope.properties[0] = makeProperty(
      "value",
      { kind: "array", element: modelRef("Item") },
      true,
    );
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["Things"], name: "list" },
      method: "GET",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, isError: false, type: modelRef("MaybeListResult") },
      ],
    });

    const { commands } = deriveAttributes({
      ...emptyIR(),
      models: [envelope],
      operations: [operation],
    });

    expect(commands[0]!.outputTypes).toEqual([
      { module: "<root>", namespace: [], name: "MaybeListResult" },
    ]);
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
    // The noun comes from the group either way; only the output type needs the envelope unwrapped.
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

    expect(commands[0]!.outputTypes).toEqual([
      { module: "<root>", namespace: [], name: "NetworkRuleSet" },
    ]);
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

    // Group-derived, so the bodiless DELETE cannot disagree with its sibling GET.
    const nouns = new Set(commands.map((c) => c.noun));
    expect(nouns).toEqual(new Set(["VolumeSnapshot"]));
    expect(commands.map((c) => `${c.verb}-${c.noun}`).sort()).toEqual([
      "Get-VolumeSnapshot",
      "Remove-VolumeSnapshot",
    ]);
  });

  it("resolves the verb from the leading action word, not a prefix of a short table key", () => {
    // `is` -> Test and `end` -> Stop are real entries; prefix matching would hijack these.
    const restore = makeOperation({
      id: { module: "<root>", namespace: ["Volumes"], name: "restoreVolume" },
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
    expect(verbs["IssueCredentialsCredential"]).toBe("Invoke");
  });

  it("names a root client's operations after its namespace, not its generated client name", () => {
    // TCGC gives the root client a "<Service>Client" name; that is a client, not a resource.
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["Microsoft", "ElasticSan", "ElasticSanClient"], name: "restoreVolume" },
      method: "POST",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, isError: false, type: modelRef("Volume") },
      ],
    });

    const { commands } = deriveAttributes(
      { ...emptyIR(), operations: [operation] },
      { modulePrefix: "Az", subjectPrefix: "ElasticSan" },
    );

    expect(`${commands[0]!.verb}-${commands[0]!.noun}`).toBe("Restore-AzElasticSanVolume");
  });

  it("leaves a resource group that merely ends in 'Client' alone", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["Microsoft", "Network", "VpnClients"], name: "get" },
      method: "GET",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, isError: false, type: modelRef("VpnClient") },
      ],
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [operation] });

    expect(commands[0]!.noun).toBe("VpnClient");
  });

  it("fans createOrUpdate out into New and Update commands from one operation", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
    });

    const { commands } = deriveAttributes({ ...emptyIR(), operations: [operation] });

    expect(commands.map((c) => `${c.verb}-${c.noun}`).sort()).toEqual([
      "New-ResourceGroup",
      "Update-ResourceGroup",
    ]);
    for (const command of commands) {
      expect(command.sourceOperationIds).toEqual([operation.id]);
    }
  });
});
