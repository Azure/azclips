import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { beforeAll, describe, expect, it } from "vitest";
import { NodeHost, compile, normalizePath, type CompilerHost, type Diagnostic } from "@typespec/compiler";
import { IRTypeRefSchema, idEquals, parseIR, type IR, type IRId, type IRMultipartBody, type IRTypeRef } from "@azure-client-engine/ir";

const emitter = "@azure-client-engine/typespec-emitter";
const entrypoint = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "fixtures", "multipart-service.tsp"),
);
let ir: IR;
let diagnostics: readonly Diagnostic[];

beforeAll(async () => {
  const written = new Map<string, string>();
  const host: CompilerHost = {
    ...NodeHost,
    writeFile: async (path, content) => { written.set(normalizePath(path), content); },
    mkdirp: async (path) => path,
  };
  const program = await compile(host, entrypoint, {
    emit: [emitter],
    options: { [emitter]: { format: "json", outputFile: "ir.json", sourceId: "multipart-test" } },
  });
  diagnostics = program.diagnostics;
  expect(diagnostics.filter((d) => d.severity === "error").map(({ code, message }) => ({ code, message }))).toEqual([]);
  const entry = [...written.entries()].find(([path]) => path.endsWith("ir.json"));
  if (!entry) throw new Error("multipart fixture emitted no ir.json");
  ir = parseIR(JSON.parse(entry[1]));
});

function operation(name: string) {
  const found = ir.operations.find((op) => op.name === name);
  if (!found) throw new Error(`Missing operation ${name}`);
  return found;
}

function request(name: string): IRMultipartBody {
  const body = operation(name).requestBody?.multipart;
  if (!body) throw new Error(`Missing multipart request ${name}`);
  return body;
}

function part(body: IRMultipartBody, name: string) {
  const found = body.parts.find((candidate) => candidate.name === name);
  if (!found) throw new Error(`Missing part ${name}`);
  return found;
}

function model(id: IRId) {
  const found = ir.models.find((candidate) => idEquals(candidate.id, id));
  if (!found) throw new Error(`Missing model ${JSON.stringify(id)}`);
  return found;
}

describe("emitter e2e: resolved multipart bodies", () => {
  it("recognizes @multipartBody without an explicit Content-Type header", () => {
    const body = operation("upload").requestBody!;
    expect(body.contentType).toBe("multipart/form-data");
    expect(body.multipart?.kind).toBe("model");
    expect(body.multipart?.contentTypes).toEqual(["multipart/form-data"]);
    expect(body.type?.kind).toBe("model");
    expect(request("upload").parts).toHaveLength(6);
  });

  it("retains part names independently of JSON names and binds them to model properties", () => {
    const metadata = part(request("upload"), "meta-data");
    expect(metadata.docs).toBe("Metadata attached to the upload.");
    expect(metadata.optional).toBe(false);
    expect(metadata.multi).toBe(false);
    const type = operation("upload").requestBody?.type;
    if (type?.kind !== "model") throw new Error("Expected a multipart payload model");
    const property = model(type.id).properties.find((p) => metadata.property && idEquals(p.id, metadata.property));
    expect(property?.name).toBe("Metadata");
    expect(property?.wireName).toBe("json-metadata");
    expect(metadata.body.type).toEqual(property?.type);
  });

  it("keeps part headers, header constants, and media type bindings separate from outer headers", () => {
    const metadata = part(request("upload"), "meta-data");
    expect(metadata.body.contentTypes).toEqual(["application/json", "application/vnd.example+json"]);
    expect(metadata.body.contentType).toMatchObject({ name: "ContentType", optional: false, nullable: false });
    expect(metadata.headers).toEqual(expect.arrayContaining([
      expect.objectContaining({ wireName: "x-part-code", optional: true, type: { kind: "primitive", name: "int32" } }),
      expect.objectContaining({ wireName: "x-part-constant", defaultValue: { kind: "string", value: "accepted" } }),
      expect.objectContaining({ wireName: "x-part-values", collectionFormat: "multi" }),
    ]));
    expect(operation("upload").headerParameters.map((h) => h.wireName)).not.toContain("x-part-code");
    expect(operation("upload").headerParameters.map((h) => h.wireName)).toContain("x-request-token");
  });

  it("preserves optional/repeated files, filenames, content type constants, and raw file contents", () => {
    const file = part(request("upload"), "documents");
    expect(file).toMatchObject({ optional: true, multi: true, filename: { name: "Filename", optional: true } });
    expect(file.body).toMatchObject({
      kind: "file",
      isText: false,
      contentTypes: ["application/json"],
      contentType: { name: "ContentType", defaultValue: { kind: "string", value: "application/json" } },
      contents: { name: "Contents", optional: false, type: { kind: "primitive", name: "bytes" } },
    });
    if (file.body.kind !== "file") throw new Error("Expected file metadata");
    expect(file.body.contents.type).not.toHaveProperty("format");
    if (file.body.type.kind !== "model") throw new Error("Expected a File model reference");
    expect(model(file.body.type.id).properties.find((p) => p.name === "Contents")?.type)
      .toEqual({ kind: "primitive", name: "bytes", format: "base64" });
  });

  it("distinguishes repeated parts from arrays inside a part and preserves literals", () => {
    const body = request("upload");
    expect(part(body, "raw").body).toMatchObject({
      kind: "single", type: { kind: "primitive", name: "bytes" }, contentTypes: ["application/octet-stream"],
    });
    expect(part(body, "raw").body.type).not.toHaveProperty("format");
    expect(part(body, "values")).toMatchObject({
      multi: false, body: { type: { kind: "array", element: { kind: "primitive", name: "string" } } },
    });
    expect(part(body, "fixed").body.defaultValue).toEqual({ kind: "string", value: "fixed" });
    expect(part(body, "text").body).toMatchObject({
      kind: "file", isText: true, contentTypes: ["text/plain"],
      contents: { type: { kind: "primitive", name: "string" } },
    });
  });

  it("retains every allowed outer multipart media type without a lossy-content diagnostic", () => {
    expect(request("uploadAlternatives").contentTypes).toEqual(["multipart/form-data", "multipart/mixed"]);
    expect(diagnostics.filter((d) => d.code.endsWith("/unsupported-multi-content-body"))).toEqual([]);
  });

  it("represents tuple parts in order without substituting any for the body", () => {
    const body = request("tuple");
    expect(body.kind).toBe("tuple");
    expect(body.contentTypes).toEqual(["multipart/mixed"]);
    expect(body.parts.map((p) => p.name)).toEqual([undefined, undefined, "text"]);
    expect(body.parts.every((p) => p.property === undefined)).toBe(true);
    expect(body.parts[1]).toMatchObject({ multi: true, body: { type: { kind: "primitive", name: "bytes" } } });
    expect(body.parts[1]!.body.type).not.toHaveProperty("format");
    expect(body.parts[0]!.headers).toEqual(expect.arrayContaining([
      expect.objectContaining({ wireName: "x-tuple-code", optional: true }),
      expect.objectContaining({ wireName: "x-tuple-values", collectionFormat: "multi" }),
    ]));
    expect(operation("tuple").requestBody).not.toHaveProperty("type");
    expect(diagnostics.filter((d) => d.code.endsWith("/unsupported-type-kind"))).toEqual([]);
    expect(request("tupleForm").parts.map((p) => p.name)).toEqual(["data", "text"]);
  });

  it("emits the complete model/enum closure of types discovered only through tuple parts or part headers", () => {
    const assertReference = (type: IRTypeRef): void => {
      if (type.kind === "model") expect(ir.models.filter((m) => idEquals(m.id, type.id))).toHaveLength(1);
      else if (type.kind === "enum") expect(ir.enums.filter((e) => idEquals(e.id, type.id))).toHaveLength(1);
      else if (type.kind === "array") assertReference(type.element);
      else if (type.kind === "dictionary") assertReference(type.value);
      else if (type.kind === "union") type.variants.forEach(assertReference);
    };
    const visit = (value: unknown): void => {
      if (Array.isArray(value)) { value.forEach(visit); return; }
      if (value === null || typeof value !== "object") return;
      for (const [key, child] of Object.entries(value)) {
        if (key === "type" && child && typeof child === "object" && "kind" in child) {
          // All emitted type fields are checked through the public type-ref schema below.
          assertReference(IRTypeRefSchema.parse(child));
        } else visit(child);
      }
    };
    visit(ir);
    for (const m of ir.models) {
      if (m.parent) model(m.parent);
      m.properties.forEach((p) => assertReference(p.type));
    }
    expect(ir.models.map((m) => m.name)).toEqual(expect.arrayContaining(["TupleData", "TupleBase", "TupleChild"]));
    expect(ir.enums.map((e) => e.name)).toContain("Mode");
  });

  it("retains multipart success and error responses without mixing inner and outer headers", () => {
    const response = operation("download").responses[0]!;
    expect(response.multipart).toEqual(request("upload"));
    expect(response.headers?.map((h) => h.wireName)).toEqual(["x-response-token"]);
    const error = operation("failure").responses.find((r) => r.isError)!;
    expect(error.statusCode).toEqual({ kind: "exact", code: 400 });
    expect(error.headers?.map((h) => h.wireName)).toEqual(["x-error"]);
    expect(error.multipart?.parts[0]?.headers[0]?.wireName).toBe("x-part-error");
  });

  it("preserves anonymous tuple response payloads as distinct model definitions", () => {
    const response = operation("tupleDownload").responses[0]!;
    expect(response).not.toHaveProperty("type");
    const parts = response.multipart?.parts;
    expect(parts).toHaveLength(2);
    const first = parts![0]!.body.type;
    const second = parts![1]!.body.type;
    if (first.kind !== "model" || second.kind !== "model") throw new Error("Expected tuple payload models");
    expect(idEquals(first.id, second.id)).toBe(false);
    expect(model(first.id).properties.map((p) => p.name)).toEqual(["First"]);
    expect(model(second.id).properties.map((p) => p.name)).toEqual(["Second"]);
  });

  it("recovers response-only payload properties, inheritance, nested types, and cycles", () => {
    const type = operation("responseOnly").responses[0]?.multipart?.parts[0]?.body.type;
    if (type?.kind !== "model") throw new Error("Expected response-only payload model");
    const data = model(type.id);
    expect(data.properties.map((p) => p.name)).toEqual(["Id", "Children", "Next"]);
    const children = data.properties.find((p) => p.name === "Children")!.type;
    if (children.kind !== "array" || children.element.kind !== "model") throw new Error("Expected nested models");
    expect(model(children.element.id).properties.map((p) => p.name)).toEqual(["Value"]);
    expect(data.properties.find((p) => p.name === "Next")?.type).toEqual(type);
    expect(model(data.parent!).properties.map((p) => p.name)).toEqual(["Id"]);
  });

  it("recovers response-only fields when TCGC has already synthesized a discriminator", () => {
    const type = operation("polymorphicResponse").responses[0]?.multipart?.parts[0]?.body.type;
    if (type?.kind !== "model") throw new Error("Expected a polymorphic payload model");
    const shape = model(type.id);
    expect(shape.properties.map((p) => p.name)).toEqual(["Kind", "Label"]);
    expect(shape.polymorphism).toBeDefined();
    expect(ir.models.find((m) => m.name === "Circle")?.properties.map((p) => p.name))
      .toEqual(["Kind", "Label", "Radius"]);
  });

  it("associates same-status content alternatives with their own body and headers", () => {
    const responses = operation("alternatives").responses;
    expect(responses).toHaveLength(2);
    const multipart = responses.find((r) => r.multipart)!;
    const json = responses.find((r) => !r.multipart)!;
    expect(multipart.contentType).toBe("multipart/form-data");
    expect(multipart.headers?.map((h) => h.wireName)).toEqual(["x-form"]);
    expect(json.contentType).toBe("application/json");
    expect(json.headers?.map((h) => h.wireName)).toEqual(["x-json"]);
    expect(json.type).toEqual(operation("json").requestBody?.type);
  });

  it("leaves ordinary JSON requests, responses, and empty responses unchanged", () => {
    const json = operation("json");
    expect(json.requestBody).toEqual({
      type: json.responses[0]!.type,
      contentType: "application/json",
      name: "body",
      isGeneratedName: false,
    });
    expect(json.responses[0]).not.toHaveProperty("multipart");
    expect(operation("upload").responses.every((r) => r.multipart === undefined)).toBe(true);
  });
});
