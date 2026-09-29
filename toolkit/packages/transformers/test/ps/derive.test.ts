/**
 * `ps.derive`'s pure function: annotating `TransformedIR` commands with `dialect.ps`.
 *
 * Built on top of `createCommandStep`, matching how the stages actually run --
 * `ps.derive` never sees a plain `IR`, only the `TransformedIR` `create-command`
 * produces.
 */

import { describe, expect, it } from "vitest";
import { IR_SCHEMA_VERSION, idKey, parseIR, parseTransformedIR, type IR, type TransformedIR } from "@azure-client-engine/ir";
import type { TransformContext } from "@azure-client-engine/compiler";
import { createCommandStep } from "../../src/create-command-step.js";
import { deriveDialect } from "../../src/ps/derive.js";

const MODULE = "M";
const SERVER = { module: MODULE, namespace: ["N"], name: "server" };
const API_VERSION = { value: "2026-01-01", location: "query" as const, parameterName: "api-version" };
const context: TransformContext = { reportDiagnostic: () => {}, config: {} };

const id = (name: string, iface?: string): Record<string, unknown> => ({
  module: MODULE,
  namespace: ["N"],
  ...(iface !== undefined ? { interface: iface } : {}),
  name,
});

const pathParam = (name: string, over: Record<string, unknown> = {}): Record<string, unknown> => ({
  id: id(`p.${name}`),
  name,
  wireName: name,
  type: { kind: "primitive", name: "string" },
  optional: false,
  nullable: false,
  ...over,
});

const operation = (over: Record<string, unknown> = {}): Record<string, unknown> => ({
  id: id("get", "Widgets"),
  name: "get",
  source: MODULE,
  method: "GET",
  pathTemplate: "/widgets",
  pathParameters: [],
  queryParameters: [],
  headerParameters: [],
  responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
  apiVersion: API_VERSION,
  server: SERVER,
  ...over,
});

function document(operations: Record<string, unknown>[]): IR {
  return parseIR({
    schemaVersion: IR_SCHEMA_VERSION,
    sources: [
      {
        id: MODULE,
        entrypoint: "main.tsp",
        apiVersion: API_VERSION,
        server: SERVER,
        rootNamespace: ["Contoso", "Widgets"],
      },
    ],
    servers: [{ id: SERVER, endpoint: { kind: "literal", url: "https://example.com" } }],
    models: [],
    enums: [],
    operations,
  });
}

function transform(operations: Record<string, unknown>[]): TransformedIR {
  return createCommandStep().run(document(operations), context);
}

describe("deriveDialect", () => {
  it("resolves verb and noun for a single-command family, stating no default parameter set", () => {
    const ir = transform([operation()]);

    const out = deriveDialect(ir, { subjectPrefix: "Az" });
    const command = out.commands[0]!;

    expect(command.dialect?.ps?.verb).toBe("Get");
    expect(command.dialect?.ps?.noun).toBe("AzWidgets");
    // One implicit set -- naming it would be noise nothing declares.
    expect(command.dialect?.ps?.defaultParameterSet).toBeUndefined();
  });

  it("merges sibling commands sharing subject+action into one family with a shared verb/noun", () => {
    const list = operation({ id: id("list", "Widgets"), name: "list" });
    const listByResourceGroup = operation({
      id: id("listByResourceGroup", "Widgets"),
      name: "listByResourceGroup",
      pathParameters: [pathParam("resourceGroupName")],
    });

    const out = deriveDialect(transform([list, listByResourceGroup]));

    expect(out.commands).toHaveLength(2);
    const ps = out.commands.map((c) => c.dialect?.ps);
    expect(ps.every((p) => p?.verb === "Get")).toBe(true);
    expect(new Set(ps.map((p) => p?.noun)).size).toBe(1);
  });

  it("names one family default on every sibling", () => {
    const top = { id: id("list.top"), name: "top", wireName: "top", type: { kind: "primitive", name: "int32" }, optional: true, nullable: false };
    const list = operation({ id: id("list", "Widgets"), name: "list", queryParameters: [top] });
    const listByResourceGroup = operation({
      id: id("listByResourceGroup", "Widgets"),
      name: "listByResourceGroup",
      pathParameters: [pathParam("resourceGroupName")],
      queryParameters: [top],
    });

    const out = deriveDialect(transform([list, listByResourceGroup]));

    const byVariant = new Map(out.commands.map((c) => [c.id.variant, c]));
    expect(byVariant.get("List")!.dialect?.ps?.defaultParameterSet).toBe("List");
    expect(byVariant.get("ByResourceGroup")!.dialect?.ps?.defaultParameterSet).toBe("List");
    expect(() => parseTransformedIR(out)).not.toThrow();
  });

  it("leaves the default parameter set unstated when the fewest-required sibling has no parameters to declare it on", () => {
    const list = operation({ id: id("list", "Widgets"), name: "list" });
    const listByResourceGroup = operation({
      id: id("listByResourceGroup", "Widgets"),
      name: "listByResourceGroup",
      pathParameters: [pathParam("resourceGroupName")],
    });

    const out = deriveDialect(transform([list, listByResourceGroup]));

    // A bare `List` has nothing to put set membership on, so it can never validly
    // name itself the default -- left unstated rather than invented.
    expect(out.commands.every((c) => c.dialect?.ps?.defaultParameterSet === undefined)).toBe(true);
    expect(() => parseTransformedIR(out)).not.toThrow();
  });

  it("states parameter-set membership only when a family has more than one sibling", () => {
    const list = operation({ id: id("list", "Widgets"), name: "list" });
    const listByResourceGroup = operation({
      id: id("listByResourceGroup", "Widgets"),
      name: "listByResourceGroup",
      pathParameters: [pathParam("resourceGroupName")],
    });

    const out = deriveDialect(transform([list, listByResourceGroup]));

    const byResourceGroup = out.commands.find((c) => c.id.variant === "ByResourceGroup")!;
    const parameter = byResourceGroup.parameters.find((p) => p.name === "ResourceGroupName")!;
    expect(parameter.dialect?.ps?.parameterSets).toEqual([{ set: "ByResourceGroup", mandatory: true }]);
  });

  it("places a path parameter only in the normal set when ViaIdentity is available", () => {
    const out = deriveDialect(
      transform([operation({ pathParameters: [pathParam("id")] })]),
    );

    expect(out.commands[0]!.parameters[0]!.dialect?.ps?.parameterSets).toEqual([
      { set: "Get", mandatory: true },
    ]);
  });

  it("shortens a path parameter that restates the subject, keeping the full name as an alias", () => {
    const out = deriveDialect(
      transform([operation({ pathParameters: [pathParam("widgetsName")] })]),
    );

    const parameter = out.commands[0]!.parameters[0]!;
    expect(parameter.dialect?.ps?.name).toBe("Name");
    expect(parameter.dialect?.ps?.aliases).toEqual(["WidgetsName"]);
  });

  it("derives module metadata and one canonical identity model referenced by each InputObject", () => {
    const get = operation({
      pathParameters: [pathParam("resourceGroupName"), pathParam("widgetsName")],
    });
    const remove = operation({
      id: id("delete", "Widgets"),
      name: "delete",
      method: "DELETE",
      pathParameters: [
        pathParam("resourceGroupName", { id: id("delete.resourceGroupName") }),
        pathParam("widgetsName", { id: id("delete.widgetsName") }),
      ],
    });

    const out = deriveDialect(transform([get, remove]), {
      modulePrefix: "Az",
      subjectPrefix: "Contoso",
    });

    expect(out.target.dialect?.ps).toEqual({
      name: "Contoso.Widgets",
      prefix: "Az",
      subjectPrefix: "Contoso",
    });
    const identity = out.models.find((model) => model.name === "ContosoIdentity")!;
    expect(identity).toMatchObject({
      name: "ContosoIdentity",
      usage: ["input"],
      properties: [
        { name: "ResourceGroupName", wireName: "resourceGroupName" },
        { name: "WidgetsName", wireName: "widgetsName" },
      ],
    });

    for (const command of out.commands) {
      const inputObject = command.parameters.find(
        (parameter) => parameter.source.kind === "synthetic",
      )!;
      expect(inputObject.type).toEqual({ kind: "model", id: identity.id });
      expect(inputObject.source).toEqual({ kind: "synthetic" });
      expect(command.dialect?.ps?.identity?.parameter).toBe(inputObject.name);
      expect(command.dialect?.ps?.identity?.properties).toHaveLength(2);
      expect(command.dialect?.ps?.identity?.properties.every((binding) =>
        identity.properties.some((property) => idKey(property.id) === idKey(binding.property)) &&
        binding.sourceParameters.length === 1,
      )).toBe(true);
    }
    expect(() => parseTransformedIR(out)).not.toThrow();
  });

  it("derives identity parameters idempotently", () => {
    const out = deriveDialect(
      transform([operation({ pathParameters: [pathParam("widgetsName")] })]),
    );

    deriveDialect(out);

    expect(out.models.filter((model) => model.name === "WidgetsIdentity")).toHaveLength(1);
    expect(out.commands[0]!.parameters.filter(
      (parameter) => parameter.source.kind === "synthetic",
    )).toHaveLength(1);
    expect(() => parseTransformedIR(out)).not.toThrow();
  });

  it("marks a mutating command with SupportsShouldProcess and Medium confirm impact", () => {
    const out = deriveDialect(transform([operation({ id: id("create", "Widgets"), name: "create", method: "PUT" })]));

    expect(out.commands[0]!.dialect?.ps?.supportsShouldProcess).toBe(true);
    expect(out.commands[0]!.dialect?.ps?.confirmImpact).toBe("Medium");
  });

  it("leaves a non-mutating command without SupportsShouldProcess", () => {
    const out = deriveDialect(transform([operation()]));

    expect(out.commands[0]!.dialect?.ps?.supportsShouldProcess).toBeUndefined();
    expect(out.commands[0]!.dialect?.ps?.confirmImpact).toBeUndefined();
  });

  it("hides a command projected from an internal operation", () => {
    const out = deriveDialect(transform([operation({ access: "internal" })]));

    expect(out.commands[0]!.dialect?.ps?.hidden).toBe(true);
  });

  it("mutates commands in place rather than returning a copy", () => {
    const ir = transform([operation()]);
    const out = deriveDialect(ir);

    expect(out).toBe(ir);
  });
});
