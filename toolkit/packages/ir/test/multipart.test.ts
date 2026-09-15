import { describe, expect, it } from "vitest";
import {
  IRMultipartBodySchema,
  IRMultipartPartBodySchema,
  IRRequestBodySchema,
  IRResponseSchema,
  type IRMultipartBody,
} from "../src/index.js";

const multipart: IRMultipartBody = {
  kind: "tuple",
  contentTypes: ["multipart/mixed"],
  parts: [{
    name: "documents",
    optional: false,
    multi: true,
    filename: { name: "Filename", type: { kind: "primitive", name: "string" }, optional: true, nullable: false },
    headers: [{
      name: "Checksum",
      wireName: "x-checksum",
      type: { kind: "primitive", name: "string" },
      optional: false,
      nullable: false,
    }],
    body: {
      kind: "file",
      type: { kind: "model", id: { module: "M", namespace: [], name: "Document" } },
      nullable: false,
      contentTypes: ["application/pdf"],
      contentType: {
        name: "ContentType", type: { kind: "primitive", name: "string" }, optional: false, nullable: false,
        defaultValue: { kind: "string", value: "application/pdf" },
      },
      contents: { name: "Contents", type: { kind: "primitive", name: "bytes" }, optional: false, nullable: false },
      isText: false,
    },
  }],
};

describe("multipart contract", () => {
  it("preserves the complete multipart structure through request and response schemas", () => {
    const request = { contentType: "multipart/mixed", multipart };
    const response = { ...request, statusCode: { kind: "exact", code: 200 }, isError: false };
    expect(IRRequestBodySchema.parse(JSON.parse(JSON.stringify(request)))).toEqual(request);
    expect(IRResponseSchema.parse(JSON.parse(JSON.stringify(response)))).toEqual(response);
  });

  it("still requires types on ordinary and model-based multipart requests", () => {
    expect(IRRequestBodySchema.safeParse({ contentType: "application/json" }).success).toBe(false);
    expect(IRRequestBodySchema.safeParse({
      contentType: "multipart/form-data", multipart: { ...multipart, kind: "model" },
    }).success).toBe(false);
    expect(IRRequestBodySchema.safeParse({
      type: { kind: "primitive", name: "string" }, contentType: "text/plain",
    }).success).toBe(true);
  });

  it("requires file contents and rejects nested multipart bodies as part payloads", () => {
    const body = multipart.parts[0]!.body;
    if (body.kind !== "file") throw new Error("Expected a file body");
    const { contents, ...withoutContents } = body;
    expect(contents).toBeDefined();
    expect(IRMultipartPartBodySchema.safeParse(withoutContents).success).toBe(false);
    expect(IRMultipartPartBodySchema.safeParse({ ...multipart, kind: "multipart" }).success).toBe(false);
  });

  it("does not accept a header-only multipart marker or arbitrary unmodeled metadata", () => {
    expect(IRMultipartBodySchema.safeParse({ contentTypes: ["multipart/form-data"] }).success).toBe(false);
    expect(IRMultipartBodySchema.safeParse({ ...multipart, boundary: "compile-time-boundary" }).success).toBe(false);
  });
});
