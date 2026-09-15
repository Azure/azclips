import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { beforeAll, describe, expect, it } from "vitest";
import { NodeHost, compile, normalizePath, type CompilerHost } from "@typespec/compiler";
import { idEquals, parseIR, type IR } from "@azure-client-engine/ir";

const emitter = "@azure-client-engine/typespec-emitter";
const entrypoint = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "fixtures", "interface-service.tsp"),
);

let ir: IR;

beforeAll(async () => {
  const written = new Map<string, string>();
  const host: CompilerHost = {
    ...NodeHost,
    writeFile: async (path, content) => {
      written.set(normalizePath(path), content);
    },
    mkdirp: async (path) => path,
  };
  const program = await compile(host, entrypoint, {
    emit: [emitter],
    options: { [emitter]: { format: "json", outputFile: "ir.json" } },
  });
  expect(
    program.diagnostics.filter((d) => d.severity === "error").map(({ code, message }) => ({ code, message })),
  ).toEqual([]);
  const entry = [...written.entries()].find(([path]) => path.endsWith("ir.json"));
  if (!entry) throw new Error("interface fixture emitted no ir.json");
  ir = parseIR(JSON.parse(entry[1]));
});

describe("emitter e2e: source namespace and interface identity", () => {
  it("preserves separate interfaces and original names despite SDK regrouping and renaming", () => {
    const widget = ir.operations.find((op) => op.name === "fetchWidget");
    const account = ir.operations.find((op) => op.name === "fetchAccount");

    expect(widget?.id).toEqual({
      module: "InterfaceService",
      namespace: ["InterfaceService"],
      interface: "Widgets",
      name: "get",
    });
    expect(account?.id).toEqual({
      module: "InterfaceService",
      namespace: ["InterfaceService"],
      interface: "Accounts",
      name: "get",
    });
    expect(idEquals(widget!.id, account!.id)).toBe(false);
  });

  it("leaves namespace-level operations unscoped even when moved to a subclient", () => {
    const health = ir.operations.find((op) => op.name === "ping");
    expect(health?.id).toEqual({
      module: "InterfaceService",
      namespace: ["InterfaceService"],
      name: "health",
    });
    expect(health?.queryParameters.find((p) => p.wireName === "verbose")?.id).toEqual({
      module: "InterfaceService",
      namespace: ["InterfaceService"],
      name: "health.verbose",
    });
  });

  it("retains operation ownership and original parameter names for every HTTP parameter location", () => {
    const widget = ir.operations.find((op) => op.name === "fetchWidget")!;
    const parameters = [
      ...widget.pathParameters,
      ...widget.queryParameters,
      ...widget.headerParameters,
    ];
    expect(parameters.map((p) => p.id.name)).toEqual(
      expect.arrayContaining(["get.id", "get.filter", "get.trace"]),
    );
    for (const parameter of parameters) {
      expect(parameter.id.module).toBe(widget.id.module);
      expect(parameter.id.namespace).toEqual(widget.id.namespace);
      expect(parameter.id.interface).toBe("Widgets");
    }
    expect(widget.pathParameters.find((p) => p.wireName === "id")?.name).toBe("key");
  });

  it("assigns inherited operations to their effective service interface, not the library template", () => {
    const read = ir.operations.find((op) => op.name === "read");
    expect(read?.id).toEqual({
      module: "InterfaceService",
      namespace: ["InterfaceService"],
      interface: "DerivedWidgets",
      name: "read",
    });
  });

  it("keeps op-is aliases in the interface that declares the alias", () => {
    const alias = ir.operations.find((op) => op.name === "fetchAlias");
    expect(alias?.id).toEqual({
      module: "InterfaceService",
      namespace: ["InterfaceService"],
      interface: "Aliases",
      name: "fetchAlias",
    });
  });

  it("uses the source namespace for models without adding interface ownership", () => {
    const widget = ir.models.find((model) => model.name === "Widget");
    expect(widget).toBeDefined();
    expect(widget!.id.namespace).toEqual(["InterfaceService"]);
    expect(widget!.id).not.toHaveProperty("interface");
  });

  it("preserves nested source namespaces and SDK-facing model and enum names", () => {
    const metadata = ir.models.find((model) => model.name === "SdkMetadata");
    const state = ir.enums.find((type) => type.name === "SdkState");
    const kind = ir.enums.find((type) => type.name === "SdkKind");
    expect(metadata).toBeDefined();
    expect(state).toBeDefined();
    expect(kind).toBeDefined();

    for (const type of [metadata!, state!, kind!]) {
      expect(type.id.namespace).toEqual(["InterfaceService", "Details"]);
      expect(type.id.module).toBe("InterfaceService");
      expect(type.id).not.toHaveProperty("interface");
    }
    for (const property of metadata!.properties.filter((property) => !property.inheritedFrom)) {
      expect(property.id.namespace).toEqual(metadata!.id.namespace);
    }
    expect(metadata!.properties.find((property) => property.wireName === "state")?.type).toEqual({
      kind: "enum",
      id: state!.id,
    });
    expect(metadata!.properties.find((property) => property.wireName === "kind")?.type).toEqual({
      kind: "enum",
      id: kind!.id,
    });
  });

  it("retains the defining source namespace for parent models and inherited properties", () => {
    const metadata = ir.models.find((model) => model.name === "SdkMetadata")!;
    const base = ir.models.find((model) => model.name === "Metadata");
    expect(base).toBeDefined();
    expect(base!.id.namespace).toEqual(["Shared"]);
    expect(metadata.parent).toEqual(base!.id);

    const inherited = metadata.properties.find((property) => property.wireName === "sharedValue");
    expect(inherited).toBeDefined();
    expect(inherited!.id.namespace).toEqual(["Shared"]);
    expect(inherited!.inheritedFrom).toEqual(base!.id);
  });

  it("keeps global model and enum namespaces empty despite SDK namespace overrides", () => {
    const details = ir.models.find((model) => model.name === "GlobalDetails");
    const state = ir.enums.find((type) => type.name === "GlobalState");
    expect(details).toBeDefined();
    expect(state).toBeDefined();
    expect(details!.id.namespace).toEqual([]);
    expect(state!.id.namespace).toEqual([]);
    expect(details!.properties.find((property) => property.wireName === "state")?.type).toEqual({
      kind: "enum",
      id: state!.id,
    });
  });

  it("uses the same source identities in model properties and operation bodies and responses", () => {
    const widget = ir.models.find((model) => model.name === "Widget")!;
    const metadata = ir.models.find((model) => model.name === "SdkMetadata")!;
    const metadataRef = { kind: "model", id: metadata.id };
    const propertyType = (wireName: string) =>
      widget.properties.find((property) => property.wireName === wireName)?.type;
    expect(propertyType("metadata")).toEqual(metadataRef);
    expect(propertyType("items")).toEqual({ kind: "array", element: metadataRef });
    expect(propertyType("byName")).toEqual({ kind: "dictionary", value: metadataRef });

    const create = ir.operations.find((operation) => operation.name === "create");
    expect(create).toBeDefined();
    const widgetRef = { kind: "model", id: widget.id };
    expect(create!.requestBody?.type).toEqual(widgetRef);
    expect(create!.responses.map((response) => response.type)).toContainEqual(widgetRef);
    expect(create!.id.namespace).toEqual(widget.id.namespace);
  });
});
