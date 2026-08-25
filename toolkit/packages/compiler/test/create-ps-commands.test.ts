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
});
