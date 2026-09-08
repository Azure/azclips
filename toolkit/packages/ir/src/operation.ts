/**
 * Operations: one HTTP API call.
 *
 * Each operation will be generated as one C# class (IProcessor pattern),
 * so the IR carries everything needed to construct an HTTP request and
 * surface its response. Operations live flat at the IR root; `id.namespace`
 * and optional `id.interface` retain their TypeSpec ownership for grouping.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";
import { IRTypeRefSchema, type IRTypeRef } from "./type-ref.js";
import { IRLiteralValueSchema, type IRLiteralValue } from "./literal.js";
import { AccessKinds, FormatKinds, type FormatKind, type IRAccess } from "./primitives.js";
import {
  OperationHintsSchema,
  ParameterHintsSchema,
  RequestBodyHintsSchema,
  type OperationHints,
  type ParameterHints,
  type RequestBodyHints,
} from "./hints.js";

// ─── HTTP framing ───────────────────────────────────────────────────────

export const HttpMethods = ["GET", "PUT", "POST", "DELETE", "PATCH", "HEAD", "OPTIONS"] as const;
export type HttpMethod = (typeof HttpMethods)[number];

export const CollectionFormats = ["csv", "multi", "tsv", "ssv", "pipes"] as const;
export type CollectionFormat = (typeof CollectionFormats)[number];

export type IRParameter = {
  id: IRId;
  name: string;
  /** Path placeholder name, query key, or header name on the wire. */
  wireName: string;
  type: IRTypeRef;
  optional: boolean;
  nullable: boolean;
  defaultValue?: IRLiteralValue | undefined;
  /** For array-typed query/header params: how multiple values are serialized. */
  collectionFormat?: CollectionFormat | undefined;
  /** Serialization format for primitive-shaped parameters (e.g. date-time). */
  format?: FormatKind | undefined;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
  rendering?: ParameterHints | undefined;
};

export const IRParameterSchema: z.ZodType<IRParameter> = z
  .object({
    id: IRIdSchema,
    name: z.string().min(1),
    wireName: z.string().min(1),
    type: IRTypeRefSchema,
    optional: z.boolean(),
    nullable: z.boolean(),
    defaultValue: IRLiteralValueSchema.optional(),
    collectionFormat: z.enum(CollectionFormats).optional(),
    format: z.enum(FormatKinds).optional(),
    docs: z.string().optional(),
    summary: z.string().optional(),
    rendering: ParameterHintsSchema.optional(),
  })
  .strict();

// Multipart metadata is separate from both the payload model and outer headers.

export type IRMultipartValue = {
  name: string;
  type: IRTypeRef;
  optional: boolean;
  nullable: boolean;
  defaultValue?: IRLiteralValue | undefined;
  docs?: string | undefined;
  summary?: string | undefined;
};

const multipartValueFields = {
  name: z.string().min(1),
  type: IRTypeRefSchema,
  optional: z.boolean(),
  nullable: z.boolean(),
  defaultValue: IRLiteralValueSchema.optional(),
  docs: z.string().optional(),
  summary: z.string().optional(),
};

export const IRMultipartValueSchema: z.ZodType<IRMultipartValue> =
  z.object(multipartValueFields).strict();

export type IRMultipartHeader = IRMultipartValue & {
  wireName: string;
  collectionFormat?: CollectionFormat | undefined;
};

export const IRMultipartHeaderSchema: z.ZodType<IRMultipartHeader> = z.object({
  ...multipartValueFields,
  wireName: z.string().min(1),
  collectionFormat: z.enum(CollectionFormats).optional(),
}).strict();

type MultipartPartBodyFields = {
  type: IRTypeRef;
  nullable: boolean;
  contentTypes: string[];
  contentType?: IRMultipartValue | undefined;
  defaultValue?: IRLiteralValue | undefined;
};

export type IRMultipartPartBody =
  | (MultipartPartBodyFields & { kind: "single" })
  | (MultipartPartBodyFields & {
      kind: "file";
      contents: IRMultipartValue;
      isText: boolean;
    });

const multipartPartBodyFields = {
  type: IRTypeRefSchema,
  nullable: z.boolean(),
  contentTypes: z.array(z.string().min(1)),
  contentType: IRMultipartValueSchema.optional(),
  defaultValue: IRLiteralValueSchema.optional(),
};

export const IRMultipartPartBodySchema: z.ZodType<IRMultipartPartBody> =
  z.discriminatedUnion("kind", [
    z.object({ ...multipartPartBodyFields, kind: z.literal("single") }).strict(),
    z.object({
      ...multipartPartBodyFields,
      kind: z.literal("file"),
      contents: IRMultipartValueSchema,
      isText: z.boolean(),
    }).strict(),
  ]);

export type IRMultipartPart = {
  /** Content-Disposition part name; unnamed multipart/mixed tuple entries omit it. */
  name?: string | undefined;
  /** Payload-model property supplying this part; tuple entries have no property. */
  property?: IRId | undefined;
  body: IRMultipartPartBody;
  headers: IRMultipartHeader[];
  optional: boolean;
  /** Repeated MIME parts, not an array serialized within a single part. */
  multi: boolean;
  filename?: IRMultipartValue | undefined;
  docs?: string | undefined;
  summary?: string | undefined;
};

export const IRMultipartPartSchema: z.ZodType<IRMultipartPart> = z.object({
  name: z.string().optional(),
  property: IRIdSchema.optional(),
  body: IRMultipartPartBodySchema,
  headers: z.array(IRMultipartHeaderSchema),
  optional: z.boolean(),
  multi: z.boolean(),
  filename: IRMultipartValueSchema.optional(),
  docs: z.string().optional(),
  summary: z.string().optional(),
}).strict();

export type IRMultipartBody = {
  kind: "model" | "tuple";
  contentTypes: string[];
  /** Declaration order is significant, particularly for tuple bodies. */
  parts: IRMultipartPart[];
};

export const IRMultipartBodySchema: z.ZodType<IRMultipartBody> = z.object({
  kind: z.enum(["model", "tuple"]),
  contentTypes: z.array(z.string().min(1)).min(1),
  parts: z.array(IRMultipartPartSchema),
}).strict();

// ─── Request body ───────────────────────────────────────────────────────

/**
 * Request body. Ordinary bodies select a single content-type; multipart
 * bodies additionally retain all resolved content types and ordered parts.
 * Streaming is handled by the consumer framework, not modelled here.
 *
 * `rendering.flatten`: when `self`, the generator hoists body properties
 * into the processor method signature; when `never`, the method takes
 * the body as one parameter. Default policy is computed by a transform
 * (flatten unless polymorphic / binary / dictionary / array / too-wide).
 */
export type IRRequestBody = {
  /** Absent for a multipart tuple, whose complete shape is in `multipart.parts`. */
  type?: IRTypeRef | undefined;
  contentType: string;
  multipart?: IRMultipartBody | undefined;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
  rendering?: RequestBodyHints | undefined;
};

export const IRRequestBodySchema: z.ZodType<IRRequestBody> = z
  .object({
    type: IRTypeRefSchema.optional(),
    contentType: z.string().min(1),
    multipart: IRMultipartBodySchema.optional(),
    docs: z.string().optional(),
    summary: z.string().optional(),
    rendering: RequestBodyHintsSchema.optional(),
  })
  .strict()
  .superRefine((body, ctx) => {
    if (body.type === undefined && body.multipart?.kind !== "tuple") {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["type"],
        message: "A request body requires a type unless it is a multipart tuple",
      });
    }
  });

// ─── Responses ──────────────────────────────────────────────────────────

export type IRStatusCode =
  | { kind: "exact"; code: number }
  | { kind: "range"; start: number; end: number }
  | { kind: "default" };

export const IRStatusCodeSchema: z.ZodType<IRStatusCode> = z.discriminatedUnion("kind", [
  z
    .object({
      kind: z.literal("exact"),
      code: z.number().int().min(100).max(599),
    })
    .strict(),
  z
    .object({
      kind: z.literal("range"),
      start: z.number().int().min(100).max(599),
      end: z.number().int().min(100).max(599),
    })
    .strict(),
  z.object({ kind: z.literal("default") }).strict(),
]);

export type IRResponseHeader = {
  name: string;
  wireName: string;
  type: IRTypeRef;
  format?: FormatKind | undefined;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
};

export const IRResponseHeaderSchema: z.ZodType<IRResponseHeader> = z
  .object({
    name: z.string().min(1),
    wireName: z.string().min(1),
    type: IRTypeRefSchema,
    format: z.enum(FormatKinds).optional(),
    docs: z.string().optional(),
    summary: z.string().optional(),
  })
  .strict();

export type IRResponse = {
  statusCode: IRStatusCode;
  /** Absent for no body (e.g. 204) or a multipart tuple described by `multipart`. */
  type?: IRTypeRef | undefined;
  contentType?: string | undefined;
  multipart?: IRMultipartBody | undefined;
  headers?: IRResponseHeader[] | undefined;
  /** Precomputed: true if the generator should treat this as an error path. */
  isError: boolean;
  docs?: string | undefined;
};

export const IRResponseSchema: z.ZodType<IRResponse> = z
  .object({
    statusCode: IRStatusCodeSchema,
    type: IRTypeRefSchema.optional(),
    contentType: z.string().min(1).optional(),
    multipart: IRMultipartBodySchema.optional(),
    headers: z.array(IRResponseHeaderSchema).optional(),
    isError: z.boolean(),
    docs: z.string().optional(),
  })
  .strict();

// ─── API version stamp ──────────────────────────────────────────────────

/**
 * Per-operation API version. Multi-spec emits can mix providers with
 * different versions; each operation carries its own stamp so the
 * generator emits a per-processor constant.
 */
export type IRApiVersionStamp = {
  value: string;
  location: "query" | "header";
  parameterName: string;
};

export const IRApiVersionStampSchema: z.ZodType<IRApiVersionStamp> = z
  .object({
    value: z.string().min(1),
    location: z.enum(["query", "header"]),
    parameterName: z.string().min(1),
  })
  .strict();

// ─── Pageable / LRO semantics ───────────────────────────────────────────

/**
 * Pageable: either next-link style (Azure REST norm) or continuation-token
 * style. Validation enforces exactly-one-style. The transform that fills
 * this reads `@pagedResult` / `@items` / `@nextLink` decorators.
 */
export type IRPageableSemantics = {
  kind: "pageable";
  itemType: IRTypeRef;
  itemsPath: string;
  nextLinkPath?: string | undefined;
  continuationTokenPath?: string | undefined;
  continuationTokenParameter?: string | undefined;
};

export const IRPageableSemanticsSchema = z
  .object({
    kind: z.literal("pageable"),
    itemType: IRTypeRefSchema,
    itemsPath: z.string().min(1),
    nextLinkPath: z.string().min(1).optional(),
    continuationTokenPath: z.string().min(1).optional(),
    continuationTokenParameter: z.string().min(1).optional(),
  })
  .strict();

/**
 * Long-running operations. `pollingStrategy` controls how the runtime
 * polls; `finalStateVia` controls where to fetch the final result. The
 * `custom` polling strategy is the escape hatch for non-Azure-conforming
 * LROs and carries an explicit polling URL path into the response body.
 */
export type IRLongRunningSemantics = {
  kind: "longRunning";
  finalResultType?: IRTypeRef | undefined;
  pollingStrategy: "azureAsyncOperation" | "location" | "operationResource" | "custom";
  finalStateVia?: "azureAsyncOperation" | "location" | "originalUri" | undefined;
  customPollingUrlPath?: string | undefined;
};

export const IRLongRunningSemanticsSchema = z
  .object({
    kind: z.literal("longRunning"),
    finalResultType: IRTypeRefSchema.optional(),
    pollingStrategy: z.enum(["azureAsyncOperation", "location", "operationResource", "custom"]),
    finalStateVia: z.enum(["azureAsyncOperation", "location", "originalUri"]).optional(),
    customPollingUrlPath: z.string().min(1).optional(),
  })
  .strict();

export type IROperationSemantics = IRPageableSemantics | IRLongRunningSemantics;

export const IROperationSemanticsSchema = z
  .discriminatedUnion("kind", [IRPageableSemanticsSchema, IRLongRunningSemanticsSchema])
  .superRefine((s, ctx) => {
    if (s.kind === "pageable") {
      const usesNextLink = s.nextLinkPath !== undefined;
      const usesContinuation =
        s.continuationTokenPath !== undefined || s.continuationTokenParameter !== undefined;
      if (usesNextLink === usesContinuation) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            "pageable must use either nextLinkPath OR continuation-token fields, not both/neither",
        });
      }
    } else if (s.kind === "longRunning") {
      if (
        s.pollingStrategy === "custom" &&
        (s.customPollingUrlPath === undefined || s.customPollingUrlPath.length === 0)
      ) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "pollingStrategy 'custom' requires customPollingUrlPath",
          path: ["customPollingUrlPath"],
        });
      }
    }
  }) as z.ZodType<IROperationSemantics>;

// ─── Operation ──────────────────────────────────────────────────────────

export type IROperation = {
  id: IRId;
  name: string;
  /** Source (IRSource.id) this operation came from. */
  source: string;

  // HTTP framing
  method: HttpMethod;
  pathTemplate: string;
  pathParameters: IRParameter[];
  queryParameters: IRParameter[];
  headerParameters: IRParameter[];
  requestBody?: IRRequestBody | undefined;
  responses: IRResponse[];

  // Per-operation context.
  apiVersion: IRApiVersionStamp;
  /** Reference to a definition in IR.servers. */
  server: IRId;

  // Optional Azure-flavoured semantics.
  semantics?: IROperationSemantics | undefined;

  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
  /** Generated-code visibility (`@access`). Omitted when `public` (the default). */
  access?: IRAccess | undefined;
  rendering?: OperationHints | undefined;
};

export const IROperationSchema: z.ZodType<IROperation> = z
  .object({
    id: IRIdSchema,
    name: z.string().min(1),
    source: z.string().min(1),

    method: z.enum(HttpMethods),
    pathTemplate: z.string().min(1),
    pathParameters: z.array(IRParameterSchema),
    queryParameters: z.array(IRParameterSchema),
    headerParameters: z.array(IRParameterSchema),
    requestBody: IRRequestBodySchema.optional(),
    responses: z.array(IRResponseSchema).min(1),

    apiVersion: IRApiVersionStampSchema,
    server: IRIdSchema,

    semantics: IROperationSemanticsSchema.optional(),

    docs: z.string().optional(),
    summary: z.string().optional(),
    access: z.enum(AccessKinds).optional(),
    rendering: OperationHintsSchema.optional(),
  })
  .strict();
