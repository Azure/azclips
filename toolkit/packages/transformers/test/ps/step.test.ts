import { describe, expect, it } from "vitest";
import {
  emptyIR,
  parseTransformedIR,
  type IROperation,
  type TransformedIR,
} from "@azure-client-engine/ir";
import type { TransformContext } from "@azure-client-engine/compiler";
import { createCommandStep } from "../../src/create-command-step.js";
import { createPSDeriveStep, PS_DERIVE_STEP_ID } from "../../src/index.js";

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

/** `ps.derive` runs after `create-command`, so tests build a `TransformedIR` first. */
function transformed(): TransformedIR {
  return createCommandStep().run(ir, context);
}

describe("ps.derive step", () => {
  it("names itself after its stage id", () => {
    expect(createPSDeriveStep().name).toBe(PS_DERIVE_STEP_ID);
  });

  it("mutates the commands it is given, setting dialect.ps rather than a separate IR.ps", () => {
    const input = transformed();
    const out = createPSDeriveStep().run(input, context);

    expect(out).toBe(input);
    expect(out.commands[0]!.dialect?.ps).toBeDefined();
    expect(out).not.toHaveProperty("ps");
  });

  it("produces a document that still validates", () => {
    const out = createPSDeriveStep({ modulePrefix: "Az" }).run(transformed(), context);
    expect(() => parseTransformedIR(out)).not.toThrow();
  });

  it("threads options through to the derivation", () => {
    const out = createPSDeriveStep({ modulePrefix: "Az", subjectPrefix: "Test" }).run(
      transformed(),
      context,
    );
    expect(out.commands).toHaveLength(1);
    expect(out.commands[0]!.dialect?.ps?.noun).toBe("AzTestThings");
  });

  it("rejects an unknown option", () => {
    expect(() => createPSDeriveStep({ modulPrefix: "Az" }).run(transformed(), context)).toThrow(
      /unknown option 'modulPrefix'/,
    );
  });

  it("rejects a non-string option", () => {
    expect(() => createPSDeriveStep({ modulePrefix: 42 }).run(transformed(), context)).toThrow(
      /must be a string, got number/,
    );
  });
});
