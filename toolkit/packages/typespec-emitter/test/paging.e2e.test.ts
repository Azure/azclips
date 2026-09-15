import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { beforeAll, describe, expect, it, vi } from "vitest";
import {
  NodeHost, compile, createSourceFile, normalizePath,
  type CompilerHost, type Diagnostic, type Program,
} from "@typespec/compiler";
import { idEquals, parseIR, type IR, type IRPagingProperty } from "@azure-client-engine/ir";
import { reportSdkDiagnostics } from "../src/discriminators.js";

const here = dirname(fileURLToPath(import.meta.url));
const emitter = "@azure-client-engine/typespec-emitter";
let ir: IR;
let diagnostics: readonly Diagnostic[];
let program: Program;

async function compilePaging(source?: string) {
  const entrypoint = normalizePath(resolve(here, "fixtures", "paging-service.tsp"));
  const outputs: string[] = [];
  const host: CompilerHost = {
    ...NodeHost,
    readFile: async (path) => source !== undefined && normalizePath(path) === entrypoint
      ? createSourceFile(source, entrypoint) : NodeHost.readFile(path),
    writeFile: async (_path, content) => { outputs.push(content); },
    mkdirp: async (path) => path,
  };
  const program = await compile(host, entrypoint, {
    emit: [emitter],
    options: { [emitter]: { format: "json", sourceId: "paging-test" } },
  });
  return { program, outputs };
}

beforeAll(async () => {
  const result = await compilePaging();
  program = result.program;
  diagnostics = result.program.diagnostics;
  expect(diagnostics.filter((d) => d.severity === "error").map(({ code, message }) => ({ code, message }))).toEqual([]);
  expect(result.outputs).toHaveLength(1);
  ir = parseIR(JSON.parse(result.outputs[0]!));
});

function operation(name: string) {
  const op = ir.operations.find((op) => op.name === name);
  if (!op) throw new Error(`Missing operation ${name}`);
  return op;
}

function paging(name: string) {
  const page = operation(name).semantics?.pageable;
  if (!page) throw new Error(`Missing paging metadata for ${name}`);
  return page;
}

function sourcePath(property: IRPagingProperty | undefined) {
  return property?.sourcePath.map((segment) => segment.name);
}

describe("source-faithful paging", () => {
  it("retains every input/output role without selecting a strategy", () => {
    const page = paging("all");
    expect(page.isList).toBe(true);
    expect(page.metadataSource).toBe("compiler");
    expect(Object.keys(page.input).sort()).toEqual(["continuationToken", "offset", "pageIndex", "pageSize"]);
    expect(Object.keys(page.output).sort()).toEqual(["continuationToken", "firstLink", "lastLink", "nextLink", "pageItems", "prevLink"]);
    for (const [role, wire] of [["offset", "$skip"], ["pageIndex", "page"], ["pageSize", "$top"], ["continuationToken", "in.cursor"]] as const) {
      const binding = page.input[role]!.bindings[0]!;
      expect(binding).toMatchObject({ location: "query", path: [wire] });
      expect(operation("all").queryParameters.some((p) => binding.parameter && idEquals(p.id, binding.parameter))).toBe(true);
    }
    expect(page.output.pageItems.bindings[0]).toMatchObject({ location: "body", path: ["page.items"] });
    expect(page.output.nextLink?.bindings[0]).toMatchObject({ location: "body", path: ["next.link"] });
    expect(page.output.continuationToken?.bindings[0]).toMatchObject({ location: "body", path: ["out.cursor"] });
    expect(page.itemType).toMatchObject({ kind: "model", id: { name: "Item" } });
    expect(ir.models.some((model) => page.itemType.kind === "model" && idEquals(model.id, page.itemType.id))).toBe(true);
  });

  it("uses the same compiler metadata with or without @list", () => {
    const basic = paging("basic");
    expect(basic.isList).toBe(false);
    expect(basic.output).toEqual(paging("all").output);
    for (const role of ["offset", "pageIndex", "pageSize", "continuationToken"] as const) {
      expect(sourcePath(basic.input[role])).toEqual(sourcePath(paging("all").input[role]));
      expect(basic.input[role]?.bindings[0]?.path).toEqual(paging("all").input[role]?.bindings[0]?.path);
    }
  });

  it("retains offset, page-index, and items-only lists", () => {
    expect(paging("byOffset").input.offset).toBeDefined();
    expect(paging("byOffset").input.pageSize).toBeDefined();
    expect(paging("byIndex").input.pageIndex).toBeDefined();
    expect(paging("itemsOnly").input).toEqual({});
    for (const name of ["byOffset", "byIndex", "itemsOnly"]) {
      expect(Object.keys(paging(name).output)).toEqual(["pageItems"]);
    }
  });

  it("binds headers using HTTP names and removes source-only body wrappers", () => {
    for (const name of ["headers", "headersBasic"]) {
      const page = paging(name);
      expect(sourcePath(page.output.pageItems)).toEqual(["payload", "values"]);
      expect(page.output.pageItems.bindings[0]).toMatchObject({ location: "body", path: ["page.items"] });
      expect(page.output.nextLink?.bindings[0]).toMatchObject({ location: "header", path: ["x-next-url"] });
      expect(page.output.continuationToken?.bindings[0]).toMatchObject({ location: "header", path: ["x-next-token"] });
      expect(page.input.continuationToken?.bindings[0]).toMatchObject({ location: "query", path: ["cursor"] });
    }
    expect(paging("headers").output).toEqual(paging("headersBasic").output);
  });

  it("distinguishes header, path, cookie, and whole-body request tokens", () => {
    for (const [name, location, path] of [
      ["headerInput", "header", ["x-cursor"]],
      ["pathInput", "path", ["cursor"]],
      ["cookieInput", "cookie", ["cursor"]],
      ["bodyToken", "body", []],
    ] as const) {
      expect(paging(name).input.continuationToken?.bindings[0]).toMatchObject({ location, path });
    }
    expect(paging("cookieInput").input.continuationToken?.bindings[0]?.parameter).toBeUndefined();
  });

  it("retains full nested input paths, names containing dots, and nullability", () => {
    const page = paging("nested");
    expect(sourcePath(page.input.continuationToken)).toEqual(["payload", "options", "paging", "cursor"]);
    expect(page.input.continuationToken).toMatchObject({
      optional: true, nullable: true,
      bindings: [{ location: "body", path: ["all.options", "page.options", "in.cursor"], contentType: "application/json" }],
    });
    expect(page.input.pageSize?.bindings[0]?.path).toEqual(["all.options", "page.options", "page.size"]);
  });

  it("keeps status/media alternatives and their distinct wire names", () => {
    const page = paging("alternatives");
    expect(page.output.pageItems.bindings).toEqual([
      { location: "body", path: ["page.items"], contentType: "application/json", response: { statusCode: { kind: "exact", code: 200 } } },
      { location: "body", path: ["entries"], contentType: "application/xml", response: { statusCode: { kind: "exact", code: 201 } } },
    ]);
    expect(page.output.continuationToken?.bindings.map((b) => [b.location, b.path, b.response?.statusCode]))
      .toEqual([["header", ["x-token"], { kind: "exact", code: 200 }], ["header", ["x-token"], { kind: "exact", code: 201 }]]);
  });

  it("qualifies request paths by media type rather than merging wire-name alternatives", () => {
    expect(paging("requestMedia").input.continuationToken?.bindings).toEqual([
      { location: "body", path: ["all.options", "page.options", "in.cursor"], contentType: "application/json" },
      { location: "body", path: ["options", "paging", "cursor"], contentType: "application/xml" },
    ]);
  });

  it("keeps nested transport-parameter paths and references the containing parameter", () => {
    const binding = paging("queryObject").input.continuationToken?.bindings[0];
    expect(binding).toMatchObject({ location: "query", path: ["options", "in.cursor"] });
    expect(operation("queryObject").queryParameters.some((p) => binding?.parameter && idEquals(p.id, binding.parameter))).toBe(true);
    expect(sourcePath(paging("queryObject").input.continuationToken)).toEqual(["options", "cursor"]);
  });

  it("preserves next-link reinjection bindings even without SDK paging classification", () => {
    for (const name of ["reinjected", "reinjectedBasic", "reinjectedNullable"]) {
      const parameters = paging(name).sdk.nextLinkReinjectedParameters;
      expect(parameters).toHaveLength(1);
      expect(sourcePath(parameters[0])).toEqual(["includeExtra"]);
      expect(parameters[0]?.bindings[0]).toMatchObject({ location: "query", path: ["include-extra"] });
      expect(operation(name).queryParameters.some((p) => idEquals(p.id, parameters[0]!.bindings[0]!.parameter!))).toBe(true);
    }
  });

  it("preserves every transport occurrence of shared request and response properties", () => {
    expect(paging("sharedCursor").input.continuationToken?.bindings).toEqual([
      expect.objectContaining({ location: "query", path: ["options", "cursor"] }),
      expect.objectContaining({ location: "body", path: ["cursor"] }),
    ]);
    expect(paging("sharedResponse").output.continuationToken?.bindings).toEqual([
      expect.objectContaining({ location: "header", path: ["x-options", "cursor"] }),
      expect.objectContaining({ location: "body", path: ["token", "cursor"] }),
    ]);
    expect(paging("reinjectedNullable").output.nextLink?.nullable).toBe(true);
  });

  it("locates page items inside a source-defined discriminated union envelope", () => {
    expect(paging("unionBody").output.pageItems.bindings[0]).toMatchObject({
      location: "body", path: ["value", "page.items"],
    });
    expect(sourcePath(paging("unionBody").output.pageItems)).toEqual(["payload", "values"]);
  });

  it("represents an entire response body as the item array", () => {
    expect(paging("wholeBody").output.pageItems.bindings[0]).toMatchObject({ location: "body", path: [] });
    expect(paging("wholeBody").itemType).toMatchObject({ kind: "model", id: { name: "Item" } });
  });

  it("does not invent a request binding for an output-only token", () => {
    expect(paging("outputToken").output.continuationToken).toBeDefined();
    expect(paging("outputToken").input.continuationToken).toBeUndefined();
  });

  it("separates SDK presentation overrides from REST metadata", () => {
    expect(paging("disabled")).toMatchObject({ isList: true, metadataSource: "compiler", sdk: { disabled: true, forced: false } });
    expect(paging("disabled").output).toEqual(paging("all").output);
    expect(paging("forced")).toMatchObject({ isList: false, metadataSource: "sdk", sdk: { forced: true } });
    expect(paging("forced").output.pageItems.bindings[0]?.path).toEqual(["value"]);
    expect(paging("forced").input.pageSize?.bindings[0]).toMatchObject({ location: "query", path: ["limit"] });
    expect(paging("postNext").sdk.nextLinkVerb).toBe("POST");
  });

  it("retains paging and LRO on the same operation through both detection paths", () => {
    for (const name of ["startPages", "startPagesBasic"]) {
      expect(operation(name).semantics?.longRunning).toMatchObject({
        pollingStrategy: "location", finalStateVia: "location",
      });
      expect(paging(name).output.pageItems.bindings[0]).toMatchObject({
        location: "body", path: ["page.items"], response: { statusCode: { kind: "exact", code: 202 } },
      });
    }
    expect(paging("startPages").isList).toBe(true);
    expect(paging("startPagesBasic").isList).toBe(false);
  });

  it("only suppresses SDK return-shape restrictions for compiler-validated paging", () => {
    const operations = program.getGlobalNamespaceType().namespaces.get("PagingService")!.operations;
    const diagnostic: Diagnostic = {
      code: "@azure-tools/typespec-client-generator-core/unexpected-pageable-operation-return-type",
      severity: "error", message: "SDK model-return restriction", target: operations.get("wholeBody")!,
    };
    const unrelated = { ...diagnostic, target: operations.get("ordinary")! };
    const report = vi.spyOn(program, "reportDiagnostic").mockImplementation(() => {});
    try {
      reportSdkDiagnostics(program, [diagnostic, unrelated]);
      expect(report).toHaveBeenCalledExactlyOnceWith(unrelated);
    } finally {
      report.mockRestore();
    }
  });

  it("leaves ordinary operations unpaged and reports no spurious paging errors", () => {
    expect(operation("ordinary").semantics).toBeUndefined();
    expect(diagnostics.filter((d) => d.code === "missing-paging-items" || d.code.endsWith("/paging-property-unbound"))).toEqual([]);
  });
});

describe("invalid paging without @list", () => {
  it.each([
    ["duplicate-paging-prop", "model Page { @pageItems first: string[]; @pageItems second: string[]; } op read(): Page;"],
    ["incompatible-paging-props", "model Page { @pageItems items: string[]; @nextLink @prevLink link?: url; } op read(): Page;"],
    ["invalid-paging-prop", "model Page { @pageItems items: string[]; @pageSize size: int32; } op read(): Page;"],
    ["missing-paging-items", "op read(@offset @query offset?: int32): { value: string[]; };"],
  ])("reports %s rather than emitting a selected or incomplete interpretation", async (code, definition) => {
    const { program } = await compilePaging(`
      import "@typespec/http";
      using TypeSpec.Http;
      @service @server("https://paging.example.test", "Paging") namespace InvalidPaging;
      ${definition}
    `);
    expect(program.diagnostics.filter((d) => d.severity === "error").map((d) => d.code)).toContain(code);
  });
});
