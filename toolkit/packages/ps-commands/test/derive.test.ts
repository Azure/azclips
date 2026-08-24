import { describe, expect, it } from "vitest";
import {
  emptyIR,
  type IRModel,
  type IROperation,
  type IRParameter,
  type IRProperty,
  type IRSource,
} from "@azure-client-engine/ir";

import { derivePsCommands, deriveModules, deriveFormatViews, resolveParameterHintForSet } from "../src/derive.js";

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
    expect(command!.defaultParameterSetName).toBe("createOrUpdate");
  });

  it("copies Milestone 1's operation-level fields (alias, hidden, summary, lifecycleTag, etc.) onto the derived command", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: {
        powershell: {
          verb: "New",
          noun: "ResourceGroup",
          subjectPrefix: "Az",
          confirmImpact: "Medium",
          alias: ["nrg"],
          hidden: false,
          summary: "Creates a resource group.",
          description: "Creates a resource group in the specified location.",
          examples: ["New-AzResourceGroup -Name rg1 -Location eastus"],
          lifecycleTag: "GA",
          httpPath: { path: "/subscriptions/{subscriptionId}/resourcegroups/{name}", apiVersion: "2025-04-01" },
          supportsPaging: false,
          helpUri: "https://learn.microsoft.com/powershell/module/az.resources/new-azresourcegroup",
        },
      },
    });

    const [command] = derivePsCommands({ ...emptyIR(), operations: [operation] });

    expect(command!.alias).toEqual(["nrg"]);
    expect(command!.hidden).toBe(false);
    expect(command!.summary).toBe("Creates a resource group.");
    expect(command!.description).toBe("Creates a resource group in the specified location.");
    expect(command!.examples).toEqual(["New-AzResourceGroup -Name rg1 -Location eastus"]);
    expect(command!.lifecycleTag).toBe("GA");
    expect(command!.httpPath).toEqual({
      path: "/subscriptions/{subscriptionId}/resourcegroups/{name}",
      apiVersion: "2025-04-01",
    });
    expect(command!.supportsPaging).toBe(false);
    expect(command!.helpUri).toBe(
      "https://learn.microsoft.com/powershell/module/az.resources/new-azresourcegroup",
    );
  });

  it("merges Get + List sharing verb+noun into one PsCommand with two parameter sets, defaulting to the more general List (fewer required parameters), matching GetResourceGroupCmdlet's real precedent", () => {
    const get = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathParameters: [
        {
          id: { module: "<root>", namespace: ["ResourceGroups"], name: "resourceGroupName" },
          name: "resourceGroupName",
          wireName: "resourceGroupName",
          type: { kind: "primitive", name: "string" },
          optional: false,
          nullable: false,
        },
      ],
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
    expect(commands[0]!.defaultParameterSetName).toBe("List");
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
    expect(command!.defaultParameterSetName).toBe("CreateOrUpdateExpanded");
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
    expect(command!.defaultParameterSetName).toBe("CreateOrUpdate");
  });
});

describe("resolveParameterHintForSet", () => {
  function makeParameter(overrides: Partial<IRParameter> = {}): IRParameter {
    return {
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "subscriptionId" },
      name: "subscriptionId",
      wireName: "subscriptionId",
      type: { kind: "primitive", name: "string" },
      optional: true,
      nullable: false,
      ...overrides,
    };
  }

  it("mirrors the real SubscriptionId-across-Get/List-sets case: mandatory differs per set", () => {
    // Real precedent: SubscriptionId is mandatory when contributed by a Get-shaped
    // operation stacking multiple [Parameter(ParameterSetName=...)] attributes, but
    // optional (falls back to ambient context) for a List-shaped operation.
    const parameter = makeParameter({
      rendering: {
        powershell: {
          mandatory: false,
          parameterSets: {
            Get: { mandatory: true, position: 0 },
            List: { mandatory: false },
          },
        },
      },
    });

    expect(resolveParameterHintForSet(parameter, "Get")).toEqual({
      mandatory: true,
      position: 0,
      helpMessage: undefined,
    });
    expect(resolveParameterHintForSet(parameter, "List")).toEqual({
      mandatory: false,
      position: undefined,
      helpMessage: undefined,
    });
  });

  it("falls back to the flat value when a set has no override entry", () => {
    const parameter = makeParameter({
      rendering: {
        powershell: { mandatory: true, position: 2, helpMessage: "The subscription id." },
      },
    });

    expect(resolveParameterHintForSet(parameter, "AnySet")).toEqual({
      mandatory: true,
      position: 2,
      helpMessage: "The subscription id.",
    });
  });

  it("resolves to all-undefined for a parameter with no powershell hint at all", () => {
    expect(resolveParameterHintForSet(makeParameter(), "Get")).toEqual({
      mandatory: undefined,
      position: undefined,
      helpMessage: undefined,
    });
  });
});

describe("deriveModules", () => {
  function makeSource(overrides: Partial<IRSource> = {}): IRSource {
    return {
      id: "<root>",
      entrypoint: "resourcegroup.tsp",
      apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
      server: { kind: "literal", url: "https://management.azure.com" },
      rootNamespace: ["ResourceGroups"],
      ...overrides,
    };
  }

  it("groups every derived command under its source into one PsModule with cmdletsToExport", () => {
    const createOrUpdate = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: { powershell: { verb: "New", noun: "ResourceGroup", subjectPrefix: "Az", confirmImpact: "Medium" } },
    });
    const get = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      rendering: { powershell: { verb: "Get", noun: "ResourceGroup", subjectPrefix: "Az", parameterSetName: "Get" } },
    });

    const source = makeSource({
      rendering: { powershell: { description: "Resource group management cmdlets." } },
    });

    const modules = deriveModules({
      ...emptyIR(),
      sources: [source],
      operations: [createOrUpdate, get],
    });

    expect(modules).toHaveLength(1);
    expect(modules[0]!.sourceId).toBe("<root>");
    expect(modules[0]!.description).toBe("Resource group management cmdlets.");
    expect(modules[0]!.cmdletsToExport.sort()).toEqual(["Get-AzResourceGroup", "New-AzResourceGroup"]);
  });

  it("omits description when the source has no rendering hint", () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: { powershell: { verb: "New", noun: "ResourceGroup", subjectPrefix: "Az" } },
    });

    const modules = deriveModules({ ...emptyIR(), sources: [makeSource()], operations: [operation] });

    expect(modules[0]!.description).toBeUndefined();
  });

  it("groups operations from different sources into separate modules", () => {
    const fromRoot = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      source: "<root>",
      rendering: { powershell: { verb: "New", noun: "ResourceGroup", subjectPrefix: "Az" } },
    });
    const fromOther = makeOperation({
      id: { module: "Other", namespace: ["Widgets"], name: "createOrUpdate" },
      method: "PUT",
      source: "Other",
      rendering: { powershell: { verb: "New", noun: "Widget", subjectPrefix: "Az" } },
    });

    const modules = deriveModules({
      ...emptyIR(),
      sources: [makeSource(), makeSource({ id: "Other" })],
      operations: [fromRoot, fromOther],
    });

    expect(modules.map((m) => m.sourceId).sort()).toEqual(["<root>", "Other"]);
  });
});

describe("deriveFormatViews", () => {
  function makeProperty(overrides: Partial<IRProperty> & Pick<IRProperty, "name" | "typeKind">): IRProperty {
    const base = {
      id: { module: "<root>", namespace: ["ResourceGroups"], name: overrides.name },
      wireName: overrides.name,
      optional: true,
      nullable: false,
    };
    switch (overrides.typeKind) {
      case "object":
        return { ...base, type: { kind: "model", id: base.id }, ...overrides } as IRProperty;
      case "collection":
        return {
          ...base,
          type: { kind: "array", element: { kind: "primitive", name: "string" } },
          ...overrides,
        } as IRProperty;
      default:
        return { ...base, type: { kind: "primitive", name: "string" }, ...overrides } as IRProperty;
    }
  }

  function makeModel(overrides: Partial<IRModel> & Pick<IRModel, "properties">): IRModel {
    return {
      id: { module: "<root>", namespace: [], name: "ResourceGroup" },
      name: "ResourceGroup",
      source: "<root>",
      ...overrides,
    };
  }

  it("default view: simple-scalar properties only, inherited before owned, then declaration order", () => {
    const model = makeModel({
      properties: [
        makeProperty({ name: "Tags", typeKind: "object" }), // complex — excluded
        makeProperty({ name: "Name", typeKind: "primitive" }),
        makeProperty({ name: "Id", typeKind: "primitive", inheritedFrom: { module: "<root>", namespace: [], name: "Resource" } }),
        makeProperty({ name: "Servers", typeKind: "collection" }), // complex — excluded
        makeProperty({ name: "Location", typeKind: "primitive" }),
      ],
    });

    const [view] = deriveFormatViews({ ...emptyIR(), models: [model] });

    expect(view).toBeDefined();
    expect(view!.sourceModelId).toEqual(model.id);
    // Id is inherited (Origin: Inherited) so it sorts first; Name/Location keep declaration order.
    expect(view!.columns.map((c) => c.propertyName)).toEqual(["Id", "Name", "Location"]);
  });

  it("formatIndex/formatLabel/formatWidth override the default position/label/width", () => {
    const model = makeModel({
      properties: [
        makeProperty({ name: "Name", typeKind: "primitive" }),
        makeProperty({
          name: "Location",
          typeKind: "primitive",
          rendering: { powershell: { formatIndex: 0, formatLabel: "Region", formatWidth: 15 } },
        }),
      ],
    });

    const [view] = deriveFormatViews({ ...emptyIR(), models: [model] });

    expect(view!.columns).toEqual([
      { propertyName: "Location", label: "Region", width: 15 },
      { propertyName: "Name", label: undefined, width: undefined },
    ]);
  });

  it("excludeFromFormat removes an otherwise-eligible simple-scalar property", () => {
    const model = makeModel({
      properties: [
        makeProperty({ name: "Name", typeKind: "primitive" }),
        makeProperty({
          name: "InternalId",
          typeKind: "primitive",
          rendering: { powershell: { excludeFromFormat: true } },
        }),
      ],
    });

    const [view] = deriveFormatViews({ ...emptyIR(), models: [model] });

    expect(view!.columns.map((c) => c.propertyName)).toEqual(["Name"]);
  });

  it("a format override forces an otherwise-excluded complex property into the view", () => {
    const model = makeModel({
      properties: [
        makeProperty({
          name: "Tags",
          typeKind: "object",
          rendering: { powershell: { formatIndex: 0 } },
        }),
      ],
    });

    const [view] = deriveFormatViews({ ...emptyIR(), models: [model] });

    expect(view!.columns.map((c) => c.propertyName)).toEqual(["Tags"]);
  });

  it("produces no view for a model with zero eligible columns", () => {
    const model = makeModel({
      properties: [makeProperty({ name: "Tags", typeKind: "object" })],
    });

    expect(deriveFormatViews({ ...emptyIR(), models: [model] })).toHaveLength(0);
  });
});
