/**
 * Identity keying and lookup.
 *
 * The point of `idKey` is that it agrees with `idEquals` where `serializeId` does not:
 * `serializeId` joins namespace segments for display, so structurally different ids can
 * render the same. These pin that difference, because an index keyed on the display form
 * would silently conflate them.
 */

import { describe, expect, it } from "vitest";

import { emptyIR, idEquals, idKey, indexIR, serializeId, type IR, type IRId } from "../src/index.js";

const id = (over: Partial<IRId> = {}): IRId => ({
  module: "<root>",
  namespace: ["Contoso", "Inventory"],
  name: "Widget",
  ...over,
});

describe("idKey", () => {
  it("matches idEquals for equal ids", () => {
    expect(idKey(id())).toBe(idKey(id()));
    expect(idEquals(id(), id())).toBe(true);
  });

  it("separates namespace segments that serializeId flattens together", () => {
    const split = id({ namespace: ["Contoso", "Inventory"] });
    const joined = id({ namespace: ["Contoso.Inventory"] });

    // The display form cannot tell these apart — which is exactly why it is not the key.
    expect(serializeId(split)).toBe(serializeId(joined));
    expect(idEquals(split, joined)).toBe(false);
    expect(idKey(split)).not.toBe(idKey(joined));
  });

  it("distinguishes an absent interface from a namespace segment of the same name", () => {
    const scoped = id({ namespace: ["Contoso"], interface: "Inventory" });
    const nested = id({ namespace: ["Contoso", "Inventory"] });

    expect(idEquals(scoped, nested)).toBe(false);
    expect(idKey(scoped)).not.toBe(idKey(nested));
  });

  it("distinguishes absent type arguments from empty ones, matching idEquals", () => {
    const absent = id();
    const empty = id({ typeArgs: [] });

    expect(idEquals(absent, empty)).toBe(false);
    expect(idKey(absent)).not.toBe(idKey(empty));
  });

  it("compares type arguments structurally, including order and nesting", () => {
    const one = id({ typeArgs: [id({ name: "A" }), id({ name: "B" })] });
    const swapped = id({ typeArgs: [id({ name: "B" }), id({ name: "A" })] });
    const nested = id({ typeArgs: [id({ name: "A", typeArgs: [id({ name: "B" })] })] });

    expect(idKey(one)).not.toBe(idKey(swapped));
    expect(idKey(one)).not.toBe(idKey(nested));
    expect(idKey(one)).toBe(idKey(id({ typeArgs: [id({ name: "A" }), id({ name: "B" })] })));
  });
});

describe("indexIR", () => {
  const widgetId = id({ name: "Widget" });
  const sizeId = id({ name: "Widget.size" });
  const serverId = id({ name: "server" });

  function document(): IR {
    return {
      ...emptyIR(),
      sources: [{ id: "<root>", server: serverId }],
      servers: [{ id: serverId, endpoint: { kind: "literal", url: "https://example.com" } }],
      models: [
        {
          id: widgetId,
          name: "Widget",
          source: "<root>",
          properties: [
            {
              id: sizeId,
              name: "Size",
              wireName: "size",
              optional: false,
              nullable: false,
              typeKind: "primitive",
              type: { kind: "primitive", name: "int64" },
            },
          ],
        },
      ],
      enums: [],
      operations: [
        {
          id: id({ name: "get" }),
          name: "get",
          source: "<root>",
          server: serverId,
          method: "GET",
          pathTemplate: "/widgets",
          pathParameters: [],
          queryParameters: [],
          headerParameters: [],
          responses: [],
        },
      ],
    };
  }

  it("resolves each top-level collection by id", () => {
    const index = indexIR(document());

    expect(index.model(widgetId)?.name).toBe("Widget");
    expect(index.operation(id({ name: "get" }))?.name).toBe("get");
    expect(index.server(serverId)?.id.name).toBe("server");
  });

  it("resolves a property, which is not a top-level collection", () => {
    const index = indexIR(document());

    expect(index.property(sizeId)?.name).toBe("Size");
    expect(index.ownerOf(sizeId)?.name).toBe("Widget");
  });

  it("returns undefined rather than throwing on an unresolved reference", () => {
    const index = indexIR(document());

    expect(index.model(id({ name: "Missing" }))).toBeUndefined();
    expect(index.property(id({ name: "Widget.missing" }))).toBeUndefined();
    expect(index.ownerOf(id({ name: "Widget.missing" }))).toBeUndefined();
  });

  it("does not conflate ids that only differ in namespace segmentation", () => {
    const ir = document();
    const index = indexIR(ir);

    // Same `serializeId`, different id — the whole reason the key is structural.
    expect(index.model(id({ namespace: ["Contoso.Inventory"] }))).toBeUndefined();
    expect(index.model(widgetId)).toBeDefined();
  });
});
