import { describe, expect, it } from "vitest";

import { IRIdSchema, idEquals, serializeId, type IRId } from "../src/index.js";

const operation: IRId = {
  module: "Example",
  namespace: ["Example"],
  interface: "Widgets",
  name: "get",
};

describe("interface-scoped IR identity", () => {
  it("accepts optional interface names but rejects empty ones", () => {
    expect(IRIdSchema.parse(operation)).toEqual(operation);
    expect(IRIdSchema.parse({ module: "Example", namespace: [], name: "health" })).toEqual({
      module: "Example",
      namespace: [],
      name: "health",
    });
    expect(IRIdSchema.safeParse({ ...operation, interface: "" }).success).toBe(false);
  });

  it("includes the interface in structural equality", () => {
    expect(idEquals(operation, structuredClone(operation))).toBe(true);
    expect(idEquals(operation, { ...operation, interface: "Accounts" })).toBe(false);
    expect(idEquals(operation, { ...operation, interface: undefined })).toBe(false);
  });

  it("keeps interface and namespace scopes distinct", () => {
    const namespaceOperation: IRId = {
      module: operation.module,
      namespace: [...operation.namespace, operation.interface!],
      name: operation.name,
    };
    expect(idEquals(operation, namespaceOperation)).toBe(false);
    expect(serializeId(operation)).not.toBe(serializeId(namespaceOperation));
  });

  it("serializes interface scopes explicitly without changing unscoped identities", () => {
    expect(serializeId(operation)).toBe("[Example] Example::Widgets.get");
    expect(serializeId({ ...operation, namespace: [] })).toBe("[Example] ::Widgets.get");
    expect(serializeId({ module: "Example", namespace: ["Example"], name: "health" }))
      .toBe("[Example] Example.health");
  });

  it("includes interface ownership recursively in template arguments", () => {
    const first: IRId = {
      module: "Example",
      namespace: [],
      name: "Wrapper",
      typeArgs: [operation],
    };
    const second: IRId = {
      ...first,
      typeArgs: [{ ...operation, interface: "Accounts" }],
    };
    expect(idEquals(first, structuredClone(first))).toBe(true);
    expect(idEquals(first, second)).toBe(false);
    expect(serializeId(first)).not.toBe(serializeId(second));
  });
});
