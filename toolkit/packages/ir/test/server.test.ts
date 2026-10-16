import { describe, expect, it } from "vitest";
import { emptyIR, parseIR, safeParseIR, type IR, type IRServer } from "../src/index.js";

function documentWithServer(endpoint: IRServer = { kind: "literal", url: "https://service.example/" }): IR {
  const id = { module: "Service", namespace: ["Service"], name: "server" };
  const apiVersion = { value: "2025-01-01", location: "query" as const, parameterName: "api-version" };
  return {
    ...emptyIR(),
    servers: [{ id, endpoint }],
    sources: [{
      id: "Service",
      entrypoint: "service.tsp",
      rootNamespace: ["Service"],
      apiVersion,
      server: id,
    }],
    operations: [{
      id: { module: "Service", namespace: ["Service"], name: "get" },
      name: "get",
      source: "Service",
      method: "GET",
      pathTemplate: "/items",
      pathParameters: [],
      queryParameters: [],
      headerParameters: [],
      responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
      apiVersion,
      server: id,
    }],
  };
}

describe("shared server definitions", () => {
  const endpoints: IRServer[] = [
    { kind: "literal", url: "https://service.example/" },
    {
      kind: "parameterized",
      template: "https://{region}.example/",
      parameters: [{
        name: "region",
        wireName: "region",
        type: { kind: "primitive", name: "string" },
        defaultValue: { kind: "string", value: "west" },
      }],
    },
    {
      kind: "oneOf",
      servers: [
        { kind: "literal", url: "https://primary.example/" },
        { kind: "literal", url: "https://secondary.example/" },
      ],
    },
  ];

  it.each(endpoints)("preserves a $kind endpoint under its shared definition", (endpoint) => {
    const ir = documentWithServer(endpoint);
    ir.operations.push({ ...ir.operations[0]!, name: "list", id: { ...ir.operations[0]!.id, name: "list" } });
    const parsed = parseIR(ir);
    expect(parsed.servers).toHaveLength(1);
    expect(parsed.servers[0]!.endpoint).toEqual(endpoint);
    expect(parsed.sources[0]!.server).toEqual(parsed.servers[0]!.id);
    for (const operation of parsed.operations) expect(operation.server).toEqual(parsed.servers[0]!.id);
  });

  it("supports distinct server definitions and per-operation selections", () => {
    const ir = documentWithServer();
    const second = { module: "OtherService", namespace: ["OtherService"], name: "server" };
    ir.servers.push({ id: second, endpoint: { kind: "literal", url: "https://other.example/" } });
    ir.sources.push({ ...ir.sources[0]!, id: "OtherService", rootNamespace: ["OtherService"], server: second });
    ir.operations.push({ ...ir.operations[0]!, source: "OtherService", id: { ...second, name: "get" }, server: second });
    expect(parseIR(ir).operations.map((operation) => operation.server)).toEqual([ir.servers[0]!.id, second]);
  });

  it("requires a server reference on every operation", () => {
    const ir = documentWithServer();
    const { server, ...withoutServer } = ir.operations[0]!;
    expect(server).toBeDefined();
    expect(safeParseIR({ ...ir, operations: [withoutServer] }).success).toBe(false);
  });

  it.each(["sources", "operations"] as const)("rejects unresolved %s server references", (collection) => {
    const ir = documentWithServer();
    ir[collection][0]!.server = { ...ir.servers[0]!.id, name: "missing" };
    const result = safeParseIR(ir);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: [collection, 0, "server"] }),
      ]));
    }
  });

  it("rejects ambiguous duplicate server IDs", () => {
    const ir = documentWithServer();
    ir.servers.push({ ...ir.servers[0]!, endpoint: { kind: "literal", url: "https://different.example/" } });
    const result = safeParseIR(ir);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["servers", 1, "id"] }),
      ]));
    }
  });

  it("rejects the old inline-operation-server shape", () => {
    const ir = documentWithServer();
    const operation = { ...ir.operations[0]!, server: ir.servers[0]!.endpoint };
    expect(safeParseIR({ ...ir, operations: [operation] }).success).toBe(false);
  });
});
