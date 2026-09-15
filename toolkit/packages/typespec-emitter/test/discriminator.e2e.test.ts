import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { NodeHost, compile, normalizePath, type CompilerHost, type Diagnostic, type Program } from "@typespec/compiler";
import { idEquals, parseIR, type IR, type IRId, type IRTypeRef } from "@azure-client-engine/ir";
import { reportSdkDiagnostics } from "../src/discriminators.js";

let ir: IR;
let diagnostics: readonly Diagnostic[];
let program: Program;

beforeAll(async () => {
  const emitter = "@azure-client-engine/typespec-emitter";
  const entrypoint = normalizePath(resolve(dirname(fileURLToPath(import.meta.url)), "fixtures", "discriminator-service.tsp"));
  const written = new Map<string, string>();
  const host: CompilerHost = {
    ...NodeHost,
    writeFile: async (path, content) => { written.set(normalizePath(path), content); },
    mkdirp: async (path) => path,
  };
  program = await compile(host, entrypoint, {
    emit: [emitter],
    options: { [emitter]: { format: "json", outputFile: "ir.json", sourceId: "discriminator-test" } },
  });
  diagnostics = program.diagnostics;
  expect(diagnostics.filter((d) => d.severity === "error").map(({ code, message }) => ({ code, message }))).toEqual([]);
  const entry = [...written.entries()].find(([path]) => path.endsWith("ir.json"));
  if (!entry) throw new Error("discriminator fixture emitted no IR");
  ir = parseIR(JSON.parse(entry[1]));
});

function model(name: string) {
  const found = ir.models.find((m) => m.name === name);
  if (!found) throw new Error(`Missing model ${name}`);
  return found;
}

function union(name: string, namespace = ["DiscriminatorService"]) {
  const found = ir.discriminatedUnions?.find((u) => u.name === name && u.id.namespace.join(".") === namespace.join("."));
  if (!found) throw new Error(`Missing union ${namespace.join(".")}.${name}`);
  return found;
}

function property(name: string) {
  const found = model("Holder").properties.find((p) => p.name === name);
  if (!found) throw new Error(`Missing Holder.${name}`);
  return found;
}

function operation(name: string) {
  const found = ir.operations.find((op) => op.name === name);
  if (!found) throw new Error(`Missing operation ${name}`);
  return found;
}

function subtypeMap(name: string) {
  return model(name).polymorphism?.subtypes.map((subtype) => [subtype.value, subtype.type.name]);
}

describe("source-faithful model discriminators", () => {
  it("retains renamed discriminator identity and the JSON wire name", () => {
    const animal = model("Animal");
    const discriminator = animal.properties.find((p) => animal.polymorphism && idEquals(p.id, animal.polymorphism.discriminator));
    expect(discriminator).toMatchObject({ name: "AnimalKind", wireName: "@kind" });
    expect(subtypeMap("Animal")).toEqual([["cat", "Cat"], ["dog", "Dog"]]);
    expect(model("Cat").discriminatorValue).toBe("cat");
    expect(model("Cat").properties.find((p) => p.name === "AnimalKind")?.defaultValue)
      .toEqual({ kind: "string", value: "cat" });
  });

  it("finds dispatch targets through untagged intermediate models without changing inheritance", () => {
    expect(subtypeMap("Root")).toEqual([["leaf", "Leaf"]]);
    expect(model("Leaf").parent).toEqual(model("Middle").id);
    expect(model("Middle").parent).toEqual(model("Root").id);
    expect(model("Middle").polymorphism).toBeUndefined();
    expect(model("Leaf").discriminatorValue).toBe("leaf");
    expect(model("Leaf").properties.map((p) => p.name)).toEqual(["Kind", "Middle", "Value"]);
  });

  it("retains declarations with no variants and references synthesized base properties", () => {
    for (const name of ["NoVariants", "ImplicitBase"]) {
      const base = model(name);
      expect(base.polymorphism).toBeDefined();
      expect(base.properties.some((p) => idEquals(p.id, base.polymorphism!.discriminator))).toBe(true);
    }
    expect(subtypeMap("NoVariants")).toEqual([]);
    expect(subtypeMap("ImplicitBase")).toEqual([["implicit", "ImplicitChild"]]);
  });

  it("retains all aliases rather than forcing one tag per child", () => {
    expect(subtypeMap("AliasedBase")).toEqual([["first", "AliasedChild"], ["second", "AliasedChild"]]);
    expect(model("AliasedChild").discriminatorValue).toBeUndefined();
  });

  it("preserves string enum tags and empty-string discriminator values", () => {
    expect(subtypeMap("EnumBase")).toEqual([["kitten", "EnumChild"], ["", "EmptyTagChild"]]);
    expect(model("EnumChild").discriminatorValue).toBe("kitten");
    expect(model("EmptyTagChild").discriminatorValue).toBe("");
  });

  it("keeps multi-level discriminator declarations separate", () => {
    expect(subtypeMap("Event")).toEqual([["categorized", "Categorized"]]);
    expect(subtypeMap("Categorized")).toEqual([["audit", "Audit"]]);
    expect(model("Categorized").discriminatorValue).toBe("categorized");
    expect(model("Audit").discriminatorValue).toBe("audit");
  });
});

describe("source-faithful discriminated unions", () => {
  it("preserves envelopes, wire tags, defaults, declaration order, and documentation", () => {
    const choice = union("ObjectChoice");
    expect(choice).toMatchObject({
      discriminatorPropertyName: "type", envelope: "object", envelopePropertyName: "data",
      docs: "An enveloped choice.",
    });
    expect(choice.variants.map((variant) => variant.value)).toEqual(["cat", undefined, "dog"]);
    expect(choice.variants[0]?.docs).toBe("The cat case.");
    expect(choice.variants[1]?.type).toEqual({ kind: "model", id: model("Fallback").id });
    expect(property("ObjectChoice")).toMatchObject({
      typeKind: "union", type: { kind: "discriminatedUnion", id: choice.id }, nullable: false,
    });
    expect(property("Repeated").type).toEqual({
      kind: "array", element: { kind: "discriminatedUnion", id: choice.id },
    });
  });

  it("preserves unenveloped unions, including empty tags distinct from defaults", () => {
    const choice = union("FlatChoice");
    expect(choice.envelope).toBe("none");
    expect(choice.discriminatorPropertyName).toBe("tag");
    expect(choice.envelopePropertyName).toBeUndefined();
    expect(choice.variants.map((variant) => variant.value)).toEqual(["cat", "", undefined]);
  });

  it("does not collapse single-variant, enum-like, or null-only discriminated unions", () => {
    expect(union("SingleChoice")).toMatchObject({ envelope: "object", discriminatorPropertyName: "kind", envelopePropertyName: "value" });
    expect(union("SingleChoice").variants).toHaveLength(1);
    expect(union("LiteralChoice").variants.map((variant) => [variant.value, variant.literalValue])).toEqual([
      ["up", { kind: "string", value: "UP" }], ["down", { kind: "string", value: "DOWN" }],
    ]);
    expect(ir.enums.some((e) => e.name === "LiteralChoice")).toBe(false);
    expect(union("NullOnlyChoice").variants).toEqual([{ value: "none", nullable: true, literalValue: { kind: "null" } }]);
    for (const name of ["Single", "Literal", "NullOnly"]) expect(property(name).type.kind).toBe("discriminatedUnion");
  });

  it("distinguishes a tagged null payload, an unnamed null default, and a nullable envelope", () => {
    expect(union("NullableChoice").variants[1]).toEqual({
      value: "none", nullable: true, literalValue: { kind: "null" },
    });
    expect(union("NullDefaultChoice").variants[1]).toEqual({ nullable: true, literalValue: { kind: "null" } });
    expect(property("NullableCase").nullable).toBe(false);
    expect(property("NullableEnvelope")).toMatchObject({
      nullable: true, type: { kind: "discriminatedUnion", id: union("ObjectChoice").id },
    });
    expect(union("NullablePayloadChoice").variants[0]).toMatchObject({
      value: "maybe", nullable: true, type: { kind: "model", id: model("CatData").id },
    });
  });

  it("retains exact scalar and enum-member payload literals", () => {
    expect(union("ConstantChoice").variants.slice(0, 3).map((variant) => variant.literalValue)).toEqual([
      { kind: "boolean", value: true }, { kind: "number", value: 42 }, { kind: "string", value: "kitten" },
    ]);
    expect(union("NumericChoice").variants.map((variant) => variant.literalValue)).toEqual([
      { kind: "bigint", value: "9007199254740993" }, { kind: "number", value: 1.5 },
    ]);
    expect(property("VariantReference")).toMatchObject({
      type: { kind: "primitive", name: "string" }, defaultValue: { kind: "string", value: "UP" },
    });
    expect(union("AliasChoice").variants[0]).toMatchObject({
      value: "picked", type: { kind: "primitive", name: "string" }, literalValue: { kind: "string", value: "UP" },
    });
  });

  it("uses stable references for shared, nested, scoped, and recursive declarations", () => {
    expect(union("NestedChoice").variants[0]?.type).toEqual({ kind: "discriminatedUnion", id: union("ObjectChoice").id });
    expect(union("RecursiveChoice").variants[0]?.type).toEqual({ kind: "discriminatedUnion", id: union("RecursiveChoice").id });
    expect(subtypeMap("LazyBase")).toEqual([["lazy-a", "LazyChild"], ["lazy-b", "LazyChild"]]);
    const branch = model("Branch").properties.find((p) => p.name === "Children");
    expect(branch?.type).toEqual({ kind: "array", element: { kind: "discriminatedUnion", id: union("TreeChoice").id } });
    expect(union("ObjectChoice").id.namespace).toEqual(["DiscriminatorService"]);
    expect(union("ObjectChoice", ["DiscriminatorService", "Nested"]).id.namespace).toEqual(["DiscriminatorService", "Nested"]);
    expect(ir.discriminatedUnions?.filter((u) => idEquals(u.id, union("ObjectChoice").id))).toHaveLength(1);
    expect(() => JSON.stringify(ir)).not.toThrow();
  });

  it("carries definitions through direct request/response bodies and multipart payloads", () => {
    expect(operation("direct").requestBody?.type).toEqual({ kind: "discriminatedUnion", id: union("NullableChoice").id });
    expect(operation("direct").responses[0]?.type).toEqual({ kind: "discriminatedUnion", id: union("ObjectChoice").id });
    const part = operation("multipart").requestBody?.multipart?.parts[0];
    expect(part?.body.type).toEqual({ kind: "discriminatedUnion", id: union("NullableChoice").id });
    expect(part?.body.nullable).toBe(false);
  });

  it("keeps ordinary unions and nullability unchanged and emits a closed reference graph", () => {
    expect(property("OrdinaryUnion").type.kind).toBe("union");
    expect(property("OrdinaryNullable")).toMatchObject({ nullable: true, type: { kind: "model", id: model("CatData").id } });
    const assertDefinition = (type: IRTypeRef): void => {
      let ids: IRId[] | undefined;
      if (type.kind === "model") ids = ir.models.map((m) => m.id);
      if (type.kind === "enum") ids = ir.enums.map((e) => e.id);
      if (type.kind === "discriminatedUnion") ids = ir.discriminatedUnions?.map((u) => u.id);
      if ("id" in type) expect(ids?.filter((id) => idEquals(id, type.id))).toHaveLength(1);
      if (type.kind === "array") assertDefinition(type.element);
      if (type.kind === "dictionary") assertDefinition(type.value);
      if (type.kind === "union") type.variants.forEach(assertDefinition);
    };
    ir.models.forEach((m) => m.properties.forEach((p) => assertDefinition(p.type)));
    ir.discriminatedUnions?.forEach((u) => u.variants.forEach((v) => { if (v.type) assertDefinition(v.type); }));
    expect(diagnostics.filter((d) => d.code.endsWith("/union-non-mappable") || d.code.endsWith("/discriminator-property-not-found")))
      .toEqual([]);
  });
});

it("only suppresses SDK restrictions for discriminator cases the compiler accepts", () => {
  const namespace = program.getGlobalNamespaceType().namespaces.get("DiscriminatorService")!;
  const sdkPrefix = "@azure-tools/typespec-client-generator-core/";
  const supported: Diagnostic[] = [
    { code: sdkPrefix + "discriminator-not-constant", severity: "error", message: "SDK alias restriction", target: namespace.models.get("AliasedChild")! },
    { code: sdkPrefix + "union-circular", severity: "error", message: "SDK recursion restriction", target: namespace.unions.get("RecursiveChoice")! },
    { code: sdkPrefix + "union-null", severity: "error", message: "SDK null restriction", target: namespace.unions.get("NullOnlyChoice")! },
  ];
  const retained: Diagnostic[] = [
    { code: sdkPrefix + "discriminator-not-constant", severity: "error", message: "Unresolved discriminator", target: namespace.models.get("NoVariants")! },
    { code: sdkPrefix + "union-circular", severity: "error", message: "Ordinary recursive union", target: namespace.unions.get("UntaggedRecursive")! },
    { code: sdkPrefix + "unsupported-kind", severity: "error", message: "Unrelated SDK failure", target: namespace.models.get("AliasedChild")! },
  ];
  const reported = vi.spyOn(program, "reportDiagnostic").mockImplementation(() => {});
  try {
    reportSdkDiagnostics(program, [...supported, ...retained]);
    expect(reported.mock.calls.map(([diagnostic]) => diagnostic)).toEqual(retained);
  } finally {
    reported.mockRestore();
  }
});
