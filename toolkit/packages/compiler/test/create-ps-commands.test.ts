import { describe, expect, it } from "vitest";
import { emptyIR, type IROperation } from "@azure-client-engine/ir";

import { createPsCommandsFactory } from "../src/create-ps-commands.js";

const CONTEXT = { reportDiagnostic: () => undefined, extensions: {} };

function makeOperation(overrides: Partial<IROperation> & Pick<IROperation, "id" | "method">): IROperation {
  return {
    name: overrides.id.name,
    source: "<root>",
    pathTemplate: "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
    pathParameters: [],
    queryParameters: [],
    headerParameters: [],
    responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
    apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
    server: { kind: "literal", url: "https://management.azure.com" },
    ...overrides,
  };
}

describe("create-ps-commands (TransformStep)", () => {
  it("enriches ResourceGroups.createOrUpdate (PUT) to verb New, matching real precedent", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    const enriched = result.operations[0]!.rendering!.powershell!;
    expect(enriched.verb).toBe("New");
    expect(enriched.noun).toBe("ResourceGroup");
    expect(enriched.subjectPrefix).toBe("Az");
    expect(enriched.confirmImpact).toBe("Medium");
  });

  it("does not set confirmImpact for a GET operation", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    const enriched = result.operations[0]!.rendering!.powershell!;
    expect(enriched.verb).toBe("Get");
    expect(enriched.confirmImpact).toBeUndefined();
  });

  it("respects an author-provided inline hint instead of overwriting it", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: { powershell: { verb: "Set" } },
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    // Author explicitly set verb to "Set" — must not be clobbered by inference.
    expect(result.operations[0]!.rendering!.powershell!.verb).toBe("Set");
    // Noun was left unset by the author, so inference still fills it in (bare, no prefix).
    expect(result.operations[0]!.rendering!.powershell!.noun).toBe("ResourceGroup");
  });

  it("preserves pre-existing richness fields the transform never resolves itself", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: {
        powershell: {
          summary: "Creates or updates a resource group.",
          description: "Long description.",
          examples: ["New-AzResourceGroup -Name foo -Location westus"],
          lifecycleTag: "GA",
          httpPath: { path: "/subscriptions/{id}/resourceGroups/{name}", apiVersion: "2025-04-01" },
          supportsPaging: false,
          helpUri: "https://learn.microsoft.com/powershell/module/az.resources/new-azresourcegroup",
        },
      },
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    const enriched = result.operations[0]!.rendering!.powershell!;
    // The transform still resolves verb/noun/confirmImpact as usual...
    expect(enriched.verb).toBe("New");
    expect(enriched.noun).toBe("ResourceGroup");
    expect(enriched.confirmImpact).toBe("Medium");
    // ...without dropping fields it never itself resolves.
    expect(enriched.summary).toBe("Creates or updates a resource group.");
    expect(enriched.description).toBe("Long description.");
    expect(enriched.examples).toEqual(["New-AzResourceGroup -Name foo -Location westus"]);
    expect(enriched.lifecycleTag).toBe("GA");
    expect(enriched.httpPath).toEqual({ path: "/subscriptions/{id}/resourceGroups/{name}", apiVersion: "2025-04-01" });
    expect(enriched.supportsPaging).toBe(false);
    expect(enriched.helpUri).toBe(
      "https://learn.microsoft.com/powershell/module/az.resources/new-azresourcegroup",
    );
  });

  it("infers noun from the pageable item type, not the containing namespace, when they disagree (real Azure Fleet gap)", async () => {
    // Real gap found compiling the real Azure Fleet spec: listVirtualMachineScaleSets is
    // declared under the Fleets interface (namespace: ["Fleets"]) but is a pageable operation
    // over VirtualMachineScaleSet, not Fleet — the noun must follow the real item type (IR's
    // own computed IRPageableSemantics.itemType), or it wrongly merges into Get-AzFleet. The
    // response's own type (the page-wrapper model) is deliberately NOT what's asserted here —
    // real wrapper naming has no consistent convention across specs.
    const operation = makeOperation({
      id: { module: "Microsoft.AzureFleet", namespace: ["Fleets"], name: "listVirtualMachineScaleSets" },
      method: "GET",
      responses: [
        {
          statusCode: { kind: "exact", code: 200 },
          isError: false,
          type: { kind: "model", id: { module: "Microsoft.AzureFleet", namespace: [], name: "VirtualMachineScaleSetListResult" } },
        },
      ],
      semantics: {
        kind: "pageable",
        itemType: { kind: "model", id: { module: "Microsoft.AzureFleet", namespace: [], name: "VirtualMachineScaleSet" } },
        itemsPath: "value",
        nextLinkPath: "nextLink",
      },
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.noun).toBe("VirtualMachineScaleSet");
  });

  it("falls back to the containing namespace when the operation has no model response (e.g. delete)", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "delete" },
      method: "DELETE",
      responses: [{ statusCode: { kind: "exact", code: 204 }, isError: false }],
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.noun).toBe("ResourceGroup");
  });

  it("infers httpPath from the operation's own pathTemplate/apiVersion", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.httpPath).toEqual({
      path: "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
      apiVersion: "2025-04-01",
    });
  });

  it("infers supportsPaging from IR's own pageable semantics", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "list" },
      method: "GET",
      semantics: {
        kind: "pageable",
        itemType: { kind: "model", id: { module: "<root>", namespace: [], name: "ResourceGroup" } },
        itemsPath: "value",
        nextLinkPath: "nextLink",
      },
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.supportsPaging).toBe(true);
  });

  it("leaves supportsPaging unset for a non-pageable operation", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.supportsPaging).toBeUndefined();
  });

  it("infers summary/description from the operation's own @summary/@doc", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      summary: "Creates a resource group.",
      docs: "Creates a resource group at the specified location with the given name.",
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    const enriched = result.operations[0]!.rendering!.powershell!;
    expect(enriched.summary).toBe("Creates a resource group.");
    expect(enriched.description).toBe("Creates a resource group at the specified location with the given name.");
  });

  it("infers hidden from an @access(internal) operation", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      access: "internal",
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.hidden).toBe(true);
  });

  it("leaves hidden unset for a public (default-access) operation", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.hidden).toBeUndefined();
  });
});

function makeParameter(overrides: Partial<IROperation["pathParameters"][number]> & Pick<IROperation["pathParameters"][number], "name">): IROperation["pathParameters"][number] {
  return {
    id: { module: "<root>", namespace: ["ResourceGroups"], name: overrides.name },
    wireName: overrides.name,
    type: { kind: "primitive", name: "string" },
    optional: false,
    nullable: false,
    ...overrides,
  };
}

describe("create-ps-commands (TransformStep) — parameter-level inference", () => {
  it("infers mandatory = true for a required (non-optional) parameter", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathParameters: [makeParameter({ name: "resourceGroupName", optional: false })],
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.pathParameters[0]!.rendering!.powershell!.mandatory).toBe(true);
  });

  it("infers mandatory = false for an optional parameter", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "list" },
      method: "GET",
      queryParameters: [makeParameter({ name: "$filter", optional: true })],
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.queryParameters[0]!.rendering!.powershell!.mandatory).toBe(false);
  });

  it("does not overwrite an author-set mandatory value", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathParameters: [
        {
          ...makeParameter({ name: "resourceGroupName", optional: false }),
          rendering: { powershell: { mandatory: false } },
        },
      ],
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.pathParameters[0]!.rendering!.powershell!.mandatory).toBe(false);
  });

  it("infers helpMessage from the parameter's own @summary, falling back to @doc", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathParameters: [
        makeParameter({ name: "resourceGroupName", summary: "The resource group name." }),
        makeParameter({ name: "subscriptionId", docs: "The subscription identifier." }),
      ],
    });

    const step = createPsCommandsFactory({ subjectPrefix: "Az" });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.pathParameters[0]!.rendering!.powershell!.helpMessage).toBe(
      "The resource group name.",
    );
    expect(result.operations[0]!.pathParameters[1]!.rendering!.powershell!.helpMessage).toBe(
      "The subscription identifier.",
    );
  });
});
