import { describe, expect, it } from "vitest";
import { emptyIR, IRSchema, type IROperation } from "@azure-client-engine/ir";
import type { TransformContext } from "@azure-client-engine/compiler";
import { createPSDeriveStep, PS_DERIVE_STEP_ID, transformSteps } from "../../src/index.js";

const context: TransformContext = { reportDiagnostic: () => {}, config: {} };

const serverId = { module: "Microsoft.Test", namespace: [], name: "defaultServer" };

const operation: IROperation = {
  id: { module: "Microsoft.Test", namespace: ["Things"], name: "get" },
  name: "get",
  method: "GET",
  source: "Microsoft.Test",
  pathTemplate: "/things/{thingName}",
  pathParameters: [],
  queryParameters: [],
  headerParameters: [],
  responses: [
    {
      statusCode: { kind: "exact", code: 200 },
      isError: false,
      type: { kind: "model", id: { module: "Microsoft.Test", namespace: [], name: "Thing" } },
    },
  ],
  apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
  server: serverId,
};

const ir = {
  ...emptyIR(),
  servers: [{ id: serverId, endpoint: { kind: "literal" as const, url: "https://management.azure.com" } }],
  sources: [
    {
      id: "Microsoft.Test",
      entrypoint: "main.tsp",
      apiVersion: { value: "2025-04-01", location: "query" as const, parameterName: "api-version" },
      server: serverId,
      rootNamespace: ["Microsoft", "Test"],
    },
  ],
  operations: [operation],
};

describe("ps.derive step", () => {
  it("is registered under its id", () => {
    expect(transformSteps[PS_DERIVE_STEP_ID]).toBe(createPSDeriveStep);
    expect(createPSDeriveStep().name).toBe(PS_DERIVE_STEP_ID);
  });

  it("puts the derived surface on IR.ps without mutating the input", () => {
    const out = createPSDeriveStep().run(ir, context);

    expect(out.ps).toBeDefined();
    expect(out.operations).toBe(ir.operations);
    expect(ir).not.toHaveProperty("ps");
  });

  it("produces a document that still validates", () => {
    const out = createPSDeriveStep({ modulePrefix: "Az" }).run(ir, context);
    expect(IRSchema.safeParse(out).success).toBe(true);
  });

  it("threads options through to the derivation", () => {
    const out = createPSDeriveStep({ modulePrefix: "Az", subjectPrefix: "Test" }).run(ir, context);
    expect(out.ps!.commands).toHaveLength(1);
    expect(out.ps!.commands[0]!.noun).toBe("AzTestThing");
  });

  it("rejects an unknown option", () => {
    expect(() => createPSDeriveStep({ modulPrefix: "Az" }).run(ir, context)).toThrow(
      /unknown option 'modulPrefix'/,
    );
  });

  it("rejects a non-string option", () => {
    expect(() => createPSDeriveStep({ modulePrefix: 42 }).run(ir, context)).toThrow(
      /must be a string, got number/,
    );
  });
});
