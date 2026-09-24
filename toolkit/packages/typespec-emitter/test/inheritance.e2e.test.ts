import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { beforeAll, describe, expect, it } from "vitest";
import { NodeHost, compile, normalizePath, type CompilerHost } from "@typespec/compiler";
import { idEquals, parseIR, type IR, type IRModel } from "@azure-client-engine/ir";

const emitter = "@azure-client-engine/typespec-emitter";
const entrypoint = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "fixtures", "inheritance-service.tsp"),
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
  if (!entry) throw new Error("inheritance fixture emitted no ir.json");
  ir = parseIR(JSON.parse(entry[1]));
});

function modelNamed(name: string): IRModel {
  const model = ir.models.find((model) => model.name === name);
  if (!model) throw new Error(`Expected model '${name}' in emitted IR`);
  return model;
}

describe("emitter e2e: inherited property provenance", () => {
  it("uses the root defining model ID after an intermediate property redeclaration", () => {
    const base = modelNamed("Base");
    const middle = modelNamed("Middle");
    const leaf = modelNamed("Leaf");
    const values = leaf.properties.filter((property) => property.wireName === "value");
    expect(values).toHaveLength(1);
    expect(values[0]!.inheritedFrom).toEqual(base.id);
    expect(values[0]!.id).toEqual(middle.properties.find((property) => property.wireName === "value")?.id);
    expect(values[0]!.defaultValue).toEqual({ kind: "string", value: "fixed" });
    expect(leaf.parent).toEqual(middle.id);
    expect(leaf.properties.find((property) => property.wireName === "baseOnly")?.inheritedFrom).toEqual(base.id);
    expect(leaf.properties.find((property) => property.wireName === "middleOnly")?.inheritedFrom).toEqual(middle.id);
    expect(leaf.properties.find((property) => property.wireName === "leafOnly")).not.toHaveProperty("inheritedFrom");
  });

  it("keeps overrides pointing to the original property ID, not its model ID", () => {
    const baseProperty = modelNamed("Base").properties.find((property) => property.wireName === "value")!;
    for (const name of ["Middle", "OverrideLeaf"]) {
      const values = modelNamed(name).properties.filter((property) => property.wireName === "value");
      expect(values).toHaveLength(1);
      expect(values[0]!.overrides).toEqual(baseProperty.id);
      expect(values[0]).not.toHaveProperty("inheritedFrom");
    }
  });

  it("resolves inheritedFrom to model definitions and overrides to property definitions", () => {
    const properties = ir.models.flatMap((model) => model.properties);
    const inherited = properties.filter((property) => property.inheritedFrom !== undefined);
    const overrides = properties.filter((property) => property.overrides !== undefined);
    expect(inherited.length).toBeGreaterThan(0);
    expect(overrides.length).toBeGreaterThan(0);
    for (const property of inherited) {
      expect(ir.models.some((model) => idEquals(model.id, property.inheritedFrom!))).toBe(true);
    }
    for (const property of overrides) {
      expect(properties.some((definition) => idEquals(definition.id, property.overrides!))).toBe(true);
    }
  });
});
