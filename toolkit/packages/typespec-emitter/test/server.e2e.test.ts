import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { beforeAll, describe, expect, it } from "vitest";
import { NodeHost, compile, normalizePath, type CompilerHost } from "@typespec/compiler";
import { idEquals, parseIR, type IR } from "@azure-client-engine/ir";

const emitter = "@azure-client-engine/typespec-emitter";
const entrypoint = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "fixtures", "server-service.tsp"),
);
let ir: IR;

beforeAll(async () => {
  const written = new Map<string, string>();
  const host: CompilerHost = {
    ...NodeHost,
    writeFile: async (path, content) => { written.set(normalizePath(path), content); },
    mkdirp: async (path) => path,
  };
  const program = await compile(host, entrypoint, {
    emit: [emitter],
    options: { [emitter]: { format: "json", outputFile: "ir.json", sourceId: "server-test" } },
  });
  expect(
    program.diagnostics.filter((d) => d.severity === "error").map(({ code, message }) => ({ code, message })),
  ).toEqual([]);
  const entry = [...written.entries()].find(([path]) => path.endsWith("ir.json"));
  if (!entry) throw new Error("server fixture emitted no ir.json");
  ir = parseIR(JSON.parse(entry[1]));
});

describe("emitter e2e: shared servers", () => {
  it("emits one definition referenced by the source and every operation", () => {
    expect(ir.servers).toHaveLength(1);
    const server = ir.servers[0]!;
    expect(server.id).toEqual({ module: "server-test", namespace: ["ServerService"], name: "server" });
    expect(ir.sources[0]!.server).toEqual(server.id);
    expect(ir.operations.map((operation) => operation.name)).toEqual(expect.arrayContaining(["first", "second"]));
    for (const operation of ir.operations) {
      expect(idEquals(operation.server, server.id)).toBe(true);
      expect(operation.server).not.toHaveProperty("kind");
    }
  });

  it("preserves multiple endpoint alternatives and parameter defaults in the shared definition", () => {
    const endpoint = ir.servers[0]!.endpoint;
    expect(endpoint.kind).toBe("oneOf");
    if (endpoint.kind !== "oneOf") throw new Error("Expected multiple endpoint alternatives");
    const regional = endpoint.servers.find((server) => server.kind === "parameterized" && server.template.includes("{region}"));
    expect(regional).toBeDefined();
    if (regional?.kind !== "parameterized") throw new Error("Expected regional endpoint parameters");
    expect(regional.parameters.find((parameter) => parameter.wireName === "region")?.defaultValue)
      .toEqual({ kind: "string", value: "west" });
  });
});
