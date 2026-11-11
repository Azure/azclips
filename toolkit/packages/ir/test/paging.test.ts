import { describe, expect, it } from "vitest";
import {
  IRCommandPaginationSchema, IROperationSemanticsSchema, IRPageableSemanticsSchema, IRPagingPropertySchema,
  type IRCommandPagination, type IRPageableSemantics, type IRPagingProperty,
} from "../src/index.js";

const property: IRPagingProperty = {
  sourcePath: [{ id: { module: "service", namespace: ["Pages"], name: "Page.items" }, name: "items" }],
  type: { kind: "array", element: { kind: "primitive", name: "string" } },
  optional: false,
  nullable: false,
  bindings: [{ location: "body", path: ["page.items"], response: { statusCode: { kind: "exact", code: 200 } }, contentType: "application/json" }],
};
const page: IRPageableSemantics = {
  isList: true,
  metadataSource: "compiler",
  itemType: { kind: "primitive", name: "string" },
  input: {},
  output: { pageItems: property },
  sdk: { disabled: false, forced: false, nextLinkVerb: "GET", nextLinkReinjectedParameters: [] },
};

describe("paging contract", () => {
  it("allows items-only paging and independent, simultaneous navigation roles", () => {
    expect(IRPageableSemanticsSchema.parse(page)).toEqual(page);
    const all = {
      ...page,
      input: { offset: property, pageIndex: property, pageSize: property, continuationToken: property },
      output: { pageItems: property, nextLink: property, prevLink: property, firstLink: property, lastLink: property, continuationToken: property },
    };
    expect(IRPageableSemanticsSchema.parse(JSON.parse(JSON.stringify(all)))).toEqual(all);
  });

  it("allows paging and LRO together and preserves LRO validation", () => {
    const semantics = { pageable: page, longRunning: { pollingStrategy: "location" } };
    expect(IROperationSemanticsSchema.parse(semantics)).toEqual(semantics);
    expect(IROperationSemanticsSchema.safeParse({}).success).toBe(false);
    expect(IROperationSemanticsSchema.safeParse({ pageable: page, longRunning: { pollingStrategy: "custom" } }).success).toBe(false);
    expect(IROperationSemanticsSchema.safeParse({ longRunning: { pollingStrategy: "custom", customPollingUrlPath: "status.url" } }).success).toBe(true);
  });

  it("retains empty body paths and does not split dotted names", () => {
    const value = { ...property, bindings: [{ location: "body", path: [] }, { location: "header", path: ["x.next.token"] }] };
    expect(IRPagingPropertySchema.parse(value)).toEqual(value);
    expect(IRPagingPropertySchema.safeParse({ ...property, sourcePath: [] }).success).toBe(false);
    expect(IRPagingPropertySchema.safeParse({ ...property, bindings: [{ location: "body", path: "page.items" }] }).success).toBe(false);
  });

  it("does not require a token counterpart or erase SDK settings", () => {
    const value = { ...page, output: { ...page.output, continuationToken: property }, sdk: { ...page.sdk, disabled: true, nextLinkVerb: "POST" } };
    expect(IRPageableSemanticsSchema.parse(value)).toEqual(value);
    expect(IRPageableSemanticsSchema.safeParse({ ...page, output: {} }).success).toBe(false);
  });
});

describe("command pagination", () => {
  const id = (name: string) => ({ module: "service", namespace: ["Pages"], name });
  const pagination: IRCommandPagination = {
    itemType: { kind: "primitive", name: "string" },
    items: { property: id("Page.items"), via: [] },
    nextLink: { property: id("PageLinks.next"), via: [id("Page.links")] },
    nextLinkVerb: "GET",
  };

  it("references the items and next-link properties with the containers walked to reach them", () => {
    expect(IRCommandPaginationSchema.parse(JSON.parse(JSON.stringify(pagination)))).toEqual(pagination);
    const { nextLink: _nextLink, nextLinkVerb: _verb, ...itemsOnly } = pagination;
    expect(IRCommandPaginationSchema.parse(itemsOnly)).toEqual(itemsOnly);
  });

  it("requires `via` on each reference and rejects the pre-0.13 paths", () => {
    expect(IRCommandPaginationSchema.safeParse({ ...pagination, items: { property: id("Page.items") } }).success).toBe(false);
    const { items: _items, nextLink: _nextLink, ...rest } = pagination;
    expect(IRCommandPaginationSchema.safeParse({ ...rest, itemsPath: [id("Page.items")] }).success).toBe(false);
  });

  it("carries a continuation token as its response property and the parameter that sends it back", () => {
    const { nextLink: _nextLink, ...itemsOnly } = pagination;
    const tokenPaged = { ...itemsOnly, continuationToken: { response: { property: id("Page.nextMarker"), via: [] }, parameter: "Marker" } };
    expect(IRCommandPaginationSchema.parse(JSON.parse(JSON.stringify(tokenPaged)))).toEqual(tokenPaged);
    // A step follows one strategy.
    expect(IRCommandPaginationSchema.safeParse({ ...tokenPaged, nextLink: pagination.nextLink }).success).toBe(false);
  });
});
