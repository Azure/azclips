import { describe, expect, it } from "vitest";
import { emptyIR, parseIR, parseTransformedIR, type IR, type IRId } from "../src/index.js";

const id = (name: string): IRId => ({ module: "Service", namespace: ["Service"], name });

function document(): IR {
  const apiVersion = { value: "2024-01-01", location: "query" as const, parameterName: "api-version" };
  return {
    ...emptyIR(),
    servers: [{ id: id("server"), endpoint: { kind: "literal", url: "https://management.azure.com" } }],
    sources: [{ id: "Service", entrypoint: "service.tsp", rootNamespace: ["Service"], apiVersion, server: id("server") }],
    models: [{
      id: id("Widget"), name: "Widget", source: "Service",
      properties: [{
        id: id("Widget.value"), name: "Value", wireName: "value", typeKind: "collection",
        type: { kind: "array", element: { kind: "primitive", name: "string" } }, optional: true, nullable: false,
      }],
    }],
    operations: [{
      id: id("create"), name: "create", source: "Service", method: "PUT", pathTemplate: "/providers/Service/widgets/{name}",
      pathParameters: [{ id: id("create.name"), name: "name", wireName: "name", type: { kind: "primitive", name: "string" }, optional: false, nullable: false }],
      queryParameters: [], headerParameters: [],
      requestBody: { type: { kind: "model", id: id("Widget") }, contentType: "application/json" },
      responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }], apiVersion, server: id("server"),
    }],
    deployment: {
      schemaVersion: "1.0.0",
      resources: [{
        operation: id("create"), resourceType: "Service/widgets", apiVersion: apiVersion.value,
        writableScope: "tenant", name: [{ kind: "parameter", parameter: id("create.name") }], scopeParameters: [],
      }],
      facets: [{ target: id("Widget.value"), typePath: ["element"], minLength: 2, sensitive: true }],
      provenance: { sourceHash: "a".repeat(64), tools: [{ name: "emitter", version: "1.0.0" }] },
    },
  };
}

describe("deployment metadata", () => {
  it("preserves absent metadata and distinguishes enabled empty discovery", () => {
    const ir = document();
    delete ir.deployment;
    expect(parseIR(JSON.parse(JSON.stringify(ir)))).toEqual(ir);
    expect(parseIR(ir)).not.toHaveProperty("deployment");
    ir.deployment = { ...document().deployment!, resources: [], facets: [] };
    expect(parseIR(JSON.parse(JSON.stringify(ir))).deployment?.resources).toEqual([]);
  });

  it("round-trips descriptors and nested facets through both IR boundaries", () => {
    const ir = document();
    ir.deployment!.selection = ["Service/widgets@2024-01-01"];
    expect(parseIR(JSON.parse(JSON.stringify(ir)))).toEqual(ir);
    const transformed = { ...ir, commands: [], target: { rootNamespace: ["Service"], projectName: "Service" } };
    expect(parseTransformedIR(JSON.parse(JSON.stringify(transformed))).deployment).toEqual(ir.deployment);
  });

  it.each(["missing", "duplicate", "unselected"] as const)("rejects %s explicit selection", (kind) => {
    const ir = document();
    ir.deployment!.selection = kind === "missing" ? ["Service/missing@2024-01-01"]
      : kind === "duplicate" ? ["Service/widgets@2024-01-01", "service/WIDGETS@2024-01-01"]
      : ["Service/widgets@2024-01-01"];
    if (kind === "unselected") {
      ir.operations.push({ ...ir.operations[0]!, id: id("another") });
      ir.deployment!.resources.push({ ...ir.deployment!.resources[0]!, operation: id("another"), resourceType: "Service/others" });
    }
    expect(() => parseIR(ir)).toThrow(/Deployment|deployment/);
  });

  it.each(["operation", "parameter", "model", "facet", "typePath", "version", "ambiguous"] as const)(
    "rejects a malformed %s reference",
    (caseName) => {
      const ir = document();
      const resource = ir.deployment!.resources[0]!;
      switch (caseName) {
        case "operation": resource.operation = id("missing"); break;
        case "parameter": resource.name = [{ kind: "parameter", parameter: id("missing") }]; break;
        case "model": ir.models = []; break;
        case "facet": ir.deployment!.facets[0]!.target = id("missing"); break;
        case "typePath": ir.deployment!.facets[0]!.typePath = ["value"]; break;
        case "version": resource.apiVersion = "2025-01-01"; break;
        case "ambiguous": ir.operations.push(ir.operations[0]!); break;
      }
      expect(() => parseIR(ir)).toThrow(/Deployment/);
    },
  );
});
