/**
 * `ps.derive`'s pure function: annotating `TransformedIR` commands with `dialect.ps`.
 *
 * Built on top of `createCommandStep`, matching how the stages actually run --
 * `ps.derive` never sees a plain `IR`, only the `TransformedIR` `create-command`
 * produces.
 */

import { describe, expect, it } from "vitest";
import { parseIR, parseTransformedIR, type IR, type TransformedIR } from "@azure-client-engine/ir";
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
    schemaVersion: "0.12.0",
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

  it("names the default parameter set only on the sibling with the fewest required parameters", () => {
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
    expect(byVariant.get("ByResourceGroup")!.dialect?.ps?.defaultParameterSet).toBeUndefined();

    // The winning sibling names a set only its own parameters declare membership in,
    // so the document must still validate.
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

  it("leaves parameter dialects absent for a single-command family with no name to shorten", () => {
    const out = deriveDialect(
      transform([operation({ pathParameters: [pathParam("id")] })]),
    );

    expect(out.commands[0]!.parameters[0]!.dialect).toBeUndefined();
  });

  it("shortens a path parameter that restates the subject, keeping the full name as an alias", () => {
    const out = deriveDialect(
      transform([operation({ pathParameters: [pathParam("widgetsName")] })]),
    );

    const parameter = out.commands[0]!.parameters[0]!;
    expect(parameter.dialect?.ps?.name).toBe("Name");
    expect(parameter.dialect?.ps?.aliases).toEqual(["WidgetsName"]);
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
