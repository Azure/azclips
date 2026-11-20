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
  /**
   * For a path parameter: the value may contain reserved characters (`/`, `?`, …)
   * that go on the wire as written instead of percent-encoded, as TypeSpec's
   * `@path(#{ allowReserved: true })` or a route's `{+name}` declares for a scope
   * or resource id. Omitted when false.
   */
  allowReserved?: boolean | undefined;
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
    allowReserved: z.boolean().optional(),
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
  /**
   * Client-facing name of the body parameter -- `pet` for `@body pet: Pet`. TCGC always
   * supplies one, synthesizing a name when the spec gives it nothing to take one from, so
   * `isGeneratedName` rather than absence is what says whether an author wrote it. Optional
   * because IR need not come from TCGC.
   */
  name?: string | undefined;
  /**
   * True when `name` was synthesized rather than written by the spec author. A consumer
   * needing an author-intended name should treat a generated one as unusable.
   */
  isGeneratedName?: boolean | undefined;
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
    name: z.string().min(1).optional(),
    isGeneratedName: z.boolean().optional(),
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
    if (body.isGeneratedName !== undefined && body.name === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["isGeneratedName"],
        message: "`isGeneratedName` describes `name`, so it cannot be set without one.",
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

/** Source declarations, including HTTP envelope properties absent from IR.models. */
export const IRPagingSegmentSchema = z.object({
  id: IRIdSchema,
  name: z.string(),
}).strict();
export type IRPagingSegment = z.infer<typeof IRPagingSegmentSchema>;

/** A wire path is segmented: a property named "next.link" is one segment. */
export const IRPagingBindingSchema = z.object({
  location: z.enum(["body", "query", "header", "path", "cookie"]),
  path: z.array(z.string()),
  /** Media type for which this request/response binding applies. */
  contentType: z.string().optional(),
  /** Reference to an emitted request parameter, when there is one. */
  parameter: IRIdSchema.optional(),
  /** Absent for requests; selects the HTTP response alternative otherwise. */
  response: z.object({
    statusCode: IRStatusCodeSchema,
  }).strict().optional(),
}).strict();
export type IRPagingBinding = z.infer<typeof IRPagingBindingSchema>;

export const IRPagingPropertySchema = z.object({
  sourcePath: z.array(IRPagingSegmentSchema).min(1),
  type: IRTypeRefSchema,
  optional: z.boolean(),
  nullable: z.boolean(),
  bindings: z.array(IRPagingBindingSchema),
}).strict();
export type IRPagingProperty = z.infer<typeof IRPagingPropertySchema>;

/** Independent declared roles, not a selected runtime paging strategy. */
export type IRPageableSemantics = {
  /** Whether the REST operation has the standard @list marker. */
  isList: boolean;
  metadataSource: "compiler" | "sdk";
  itemType: IRTypeRef;
  input: {
    offset?: IRPagingProperty | undefined;
    pageIndex?: IRPagingProperty | undefined;
    pageSize?: IRPagingProperty | undefined;
    continuationToken?: IRPagingProperty | undefined;
  };
  output: {
    pageItems: IRPagingProperty;
    nextLink?: IRPagingProperty | undefined;
    prevLink?: IRPagingProperty | undefined;
    firstLink?: IRPagingProperty | undefined;
    lastLink?: IRPagingProperty | undefined;
    continuationToken?: IRPagingProperty | undefined;
  };
  /** SDK presentation settings do not erase the REST operation's metadata. */
  sdk: {
    disabled: boolean;
    forced: boolean;
    nextLinkVerb: "GET" | "POST";
    nextLinkReinjectedParameters: IRPagingProperty[];
    nextLinkOperation?: IRId | undefined;
  };
};

export const IRPageableSemanticsSchema: z.ZodType<IRPageableSemantics> = z
  .object({
    isList: z.boolean(),
    metadataSource: z.enum(["compiler", "sdk"]),
    itemType: IRTypeRefSchema,
    input: z.object({
      offset: IRPagingPropertySchema.optional(),
      pageIndex: IRPagingPropertySchema.optional(),
      pageSize: IRPagingPropertySchema.optional(),
      continuationToken: IRPagingPropertySchema.optional(),
    }).strict(),
    output: z.object({
      pageItems: IRPagingPropertySchema,
      nextLink: IRPagingPropertySchema.optional(),
      prevLink: IRPagingPropertySchema.optional(),
      firstLink: IRPagingPropertySchema.optional(),
      lastLink: IRPagingPropertySchema.optional(),
      continuationToken: IRPagingPropertySchema.optional(),
    }).strict(),
    sdk: z.object({
      disabled: z.boolean(),
      forced: z.boolean(),
      nextLinkVerb: z.enum(["GET", "POST"]),
      nextLinkReinjectedParameters: z.array(IRPagingPropertySchema),
      nextLinkOperation: IRIdSchema.optional(),
    }).strict(),
  })
  .strict();

/**
 * Long-running operations. `pollingStrategy` controls how the runtime
 * polls; `finalStateVia` controls where to fetch the final result. The
 * `custom` polling strategy is the escape hatch for non-Azure-conforming
 * LROs and carries an explicit polling URL path into the response body.
 */
export type IRLongRunningSemantics = {
  finalResultType?: IRTypeRef | undefined;
  pollingStrategy: "azureAsyncOperation" | "location" | "operationResource" | "custom";
  finalStateVia?: "azureAsyncOperation" | "location" | "originalUri" | undefined;
  customPollingUrlPath?: string | undefined;
};

export const IRLongRunningSemanticsSchema = z
  .object({
    finalResultType: IRTypeRefSchema.optional(),
    pollingStrategy: z.enum(["azureAsyncOperation", "location", "operationResource", "custom"]),
    finalStateVia: z.enum(["azureAsyncOperation", "location", "originalUri"]).optional(),
    customPollingUrlPath: z.string().min(1).optional(),
  })
  .strict()
  .superRefine((s, ctx) => {
    if (s.pollingStrategy === "custom" && s.customPollingUrlPath === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "pollingStrategy 'custom' requires customPollingUrlPath",
        path: ["customPollingUrlPath"],
      });
    }
  });

export type IROperationSemantics = {
  pageable?: IRPageableSemantics | undefined;
  longRunning?: IRLongRunningSemantics | undefined;
};

export const IROperationSemanticsSchema: z.ZodType<IROperationSemantics> = z.object({
  pageable: IRPageableSemanticsSchema.optional(),
  longRunning: IRLongRunningSemanticsSchema.optional(),
}).strict().refine((s) => s.pageable !== undefined || s.longRunning !== undefined, {
  message: "operation semantics must contain pageable or longRunning metadata",
});

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
  /**
   * The group the spec **explicitly** placed this operation in (`@clientLocation`),
   * resolved for this emitter's scope. Absent when the operation sits where it was
   * declared.
   *
   * Client-layer, not declaration-layer: `id` always records the declaration site,
   * so this is carried beside it rather than folded into it. A brownfield spec
   * routinely declares several resources in one interface and separates them only
   * here, which makes this the author's own answer to "what does this act on?" —
   * strictly better than inferring it from a name.
   */
  clientLocation?: string | undefined;

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
    clientLocation: z.string().min(1).optional(),

    semantics: IROperationSemanticsSchema.optional(),

    docs: z.string().optional(),
    summary: z.string().optional(),
    access: z.enum(AccessKinds).optional(),
    rendering: OperationHintsSchema.optional(),
  })
  .strict();
