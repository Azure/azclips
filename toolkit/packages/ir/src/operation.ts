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

// ─── Request body ───────────────────────────────────────────────────────

/**
 * Request body. Single content-type only (multi-content not supported).
 * Streaming is handled by the consumer framework, not modelled here.
 *
 * `rendering.flatten`: when `self`, the generator hoists body properties
 * into the processor method signature; when `never`, the method takes
 * the body as one parameter. Default policy is computed by a transform
 * (flatten unless polymorphic / binary / dictionary / array / too-wide).
 */
export type IRRequestBody = {
  type: IRTypeRef;
  contentType: string;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
  rendering?: RequestBodyHints | undefined;
};

export const IRRequestBodySchema: z.ZodType<IRRequestBody> = z
  .object({
    type: IRTypeRefSchema,
    contentType: z.string().min(1),
    docs: z.string().optional(),
    summary: z.string().optional(),
    rendering: RequestBodyHintsSchema.optional(),
  })
  .strict();

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
  /** Undefined when the response has no body (e.g. 204). */
  type?: IRTypeRef | undefined;
  contentType?: string | undefined;
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

// ─── Server (endpoint) ──────────────────────────────────────────────────

/**
 * Endpoint description, resolved per source from TypeSpec's `@server`.
 * `literal` becomes a baked-in URL constant; `parameterized` becomes
 * processor constructor parameters.
 */
export type IRServerParameter = {
  name: string;
  wireName: string;
  type: IRTypeRef;
  defaultValue?: IRLiteralValue | undefined;
  docs?: string | undefined;
};

export const IRServerParameterSchema: z.ZodType<IRServerParameter> = z
  .object({
    name: z.string().min(1),
    wireName: z.string().min(1),
    type: IRTypeRefSchema,
    defaultValue: IRLiteralValueSchema.optional(),
    docs: z.string().optional(),
  })
  .strict();

export type IRServer =
  | { kind: "literal"; url: string }
  | { kind: "parameterized"; template: string; parameters: IRServerParameter[] }
  | { kind: "oneOf"; servers: IRServer[] };

export const IRServerSchema: z.ZodType<IRServer> = z.lazy(() =>
  z.discriminatedUnion("kind", [
    z
      .object({
        kind: z.literal("literal"),
        url: z.string().url(),
      })
      .strict(),
    z
      .object({
        kind: z.literal("parameterized"),
        template: z.string().min(1),
        parameters: z.array(IRServerParameterSchema).min(1),
      })
      .strict(),
    z
      .object({
        kind: z.literal("oneOf"),
        servers: z.array(IRServerSchema).min(2),
      })
      .strict(),
  ]),
);

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

  // Per-operation context (denormalized from source for generator convenience).
  apiVersion: IRApiVersionStamp;
  server: IRServer;

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
    server: IRServerSchema,

    semantics: IROperationSemanticsSchema.optional(),

    docs: z.string().optional(),
    summary: z.string().optional(),
    access: z.enum(AccessKinds).optional(),
    rendering: OperationHintsSchema.optional(),
  })
  .strict();
