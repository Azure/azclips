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

describe("emitter e2e: source interface identity", () => {
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

  it("does not add interface ownership to namespace-level models", () => {
    const widget = ir.models.find((model) => model.name === "Widget");
    expect(widget).toBeDefined();
    expect(widget!.id).not.toHaveProperty("interface");
  });
});
