/**
 * SdkHttpOperation → HTTP framing (parameters, body, responses).
 *
 * Slots feed `operations.ts`:
 *   - `parametersByLocation` splits the param list into path/query/header.
 *   - `bodyFrom` picks one content-type if more than one is offered
 *     (warns).
 *   - `responsesFrom` flattens both `responses` and `exceptions` into
 *     IR responses, marking `isError` on exceptions.
 *
 * Skips: cookie params (warn each), streaming bodies (warn + return
 * sentinel that `operations.ts` interprets as "skip this op").
 */

import { NoTarget } from "@typespec/compiler";
import type {
  SdkBodyParameter,
  SdkHeaderParameter,
  SdkHttpErrorResponse,
  SdkHttpOperation,
  SdkHttpResponse,
  SdkPathParameter,
  SdkQueryParameter,
  SdkServiceResponseHeader,
} from "@azure-tools/typespec-client-generator-core";
import { isApiVersion } from "@azure-tools/typespec-client-generator-core";
import type { HttpStatusCodeRange } from "@typespec/http";
import type {
  CollectionFormat,
  FormatKind,
  IRParameter,
  IRRequestBody,
  IRResponse,
  IRResponseHeader,
  IRStatusCode,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { pascalCase } from "./naming.js";
import { typeRefFor, unwrapNullable } from "./types.js";

const VALID_COLLECTION_FORMATS: ReadonlySet<string> = new Set([
  "csv",
  "multi",
  "ssv",
  "tsv",
  "pipes",
]);

function clampCollectionFormat(c: string | undefined): CollectionFormat | undefined {
  if (!c) return undefined;
  if (VALID_COLLECTION_FORMATS.has(c)) return c as CollectionFormat;
  // "simple" and "form" don't exist in IR; map to undefined.
  return undefined;
}

/** Lift the `format` hint off a primitive IRTypeRef. Returns undefined for non-primitives. */
function formatHintOf(irType: { kind: string; format?: FormatKind | undefined }): FormatKind | undefined {
  return irType.kind === "primitive" ? irType.format : undefined;
}

function paramOf(
  ctx: BuildContext,
  opNs: string[],
  opName: string,
  p: SdkHeaderParameter | SdkQueryParameter | SdkPathParameter,
): IRParameter {
  const wireName = p.serializedName ?? p.name;
  const { type, nullable } = unwrapNullable(p.type);
  const irType = typeRefFor(ctx, type, `${opName}.${p.name}`);
  const id = {
    module: ctx.options.sourceId,
    namespace: opNs,
    name: `${opName}.${p.name}`,
  };
  const format = formatHintOf(irType);
  const cf =
    (p as SdkHeaderParameter | SdkQueryParameter).collectionFormat !== undefined
      ? clampCollectionFormat((p as SdkHeaderParameter | SdkQueryParameter).collectionFormat)
      : undefined;

  return {
    id,
    name: p.name,
    wireName,
    type: irType,
    optional: p.optional,
    nullable,
    ...(cf !== undefined ? { collectionFormat: cf } : {}),
    ...(format !== undefined ? { format } : {}),
    ...(p.doc !== undefined ? { docs: p.doc } : {}),
    ...(p.summary !== undefined ? { summary: p.summary } : {}),
  };
}

export function parametersByLocation(
  ctx: BuildContext,
  op: SdkHttpOperation,
  opNs: string[],
  opName: string,
): {
  pathParameters: IRParameter[];
  queryParameters: IRParameter[];
  headerParameters: IRParameter[];
} {
  const pathParameters: IRParameter[] = [];
  const queryParameters: IRParameter[] = [];
  const headerParameters: IRParameter[] = [];

  for (const p of op.parameters) {
    // The api-version parameter is denormalized onto IROperation.apiVersion;
    // skip it here so it doesn't appear twice in the IR.
    if (p.__raw && isApiVersion(ctx.sdkContext, p.__raw)) continue;

    switch (p.kind) {
      case "path":
        pathParameters.push(paramOf(ctx, opNs, opName, p));
        break;
      case "query":
        queryParameters.push(paramOf(ctx, opNs, opName, p));
        break;
      case "header":
        headerParameters.push(paramOf(ctx, opNs, opName, p));
        break;
      case "cookie":
        reportDiagnostic(ctx.program, {
          code: "cookie-parameter-dropped",
          format: { name: p.name, operation: opName },
          target: NoTarget,
        });
        break;
    }
  }

  return { pathParameters, queryParameters, headerParameters };
}

/** Sentinel value returned when the body should cause the whole op to be skipped. */
export type BodyResult =
  | { ok: true; body: IRRequestBody | undefined }
  | { ok: false; reason: "streaming" };

export function bodyFrom(
  ctx: BuildContext,
  op: SdkHttpOperation,
  opName: string,
): BodyResult {
  const b: SdkBodyParameter | undefined = op.bodyParam;
  if (!b) return { ok: true, body: undefined };

  if (b.streamMetadata) {
    reportDiagnostic(ctx.program, {
      code: "streaming-body-dropped",
      format: { operation: opName },
      target: NoTarget,
    });
    return { ok: false, reason: "streaming" };
  }

  const cts = b.contentTypes ?? [];
  if (cts.length > 1) {
    reportDiagnostic(ctx.program, {
      code: "unsupported-multi-content-body",
      format: { operation: opName, contentTypes: cts.join(", ") },
      target: NoTarget,
    });
  }
  const contentType = b.defaultContentType ?? cts[0] ?? "application/json";

  const { type } = unwrapNullable(b.type);
  const irType = typeRefFor(ctx, type, `${opName}.body`);
  const body: IRRequestBody = {
    type: irType,
    contentType,
    ...(b.doc !== undefined ? { docs: b.doc } : {}),
    ...(b.summary !== undefined ? { summary: b.summary } : {}),
  };
  return { ok: true, body };
}

function statusCodeFrom(
  s: number | HttpStatusCodeRange | "*",
): IRStatusCode {
  if (s === "*") return { kind: "default" };
  if (typeof s === "number") return { kind: "exact", code: s };
  return { kind: "range", start: s.start, end: s.end };
}

function responseHeaderFrom(
  ctx: BuildContext,
  opName: string,
  h: SdkServiceResponseHeader,
): IRResponseHeader {
  const wireName = h.serializedName ?? h.name;
  const { type } = unwrapNullable(h.type);
  const irType = typeRefFor(ctx, type, `${opName}.responseHeader.${wireName}`);
  const format = formatHintOf(irType);
  return {
    name: pascalCase(h.name),
    wireName,
    type: irType,
    ...(format !== undefined ? { format } : {}),
    ...(h.doc !== undefined ? { docs: h.doc } : {}),
    ...(h.summary !== undefined ? { summary: h.summary } : {}),
  };
}

function responseOf(
  ctx: BuildContext,
  opName: string,
  r: SdkHttpResponse | SdkHttpErrorResponse,
  isError: boolean,
): IRResponse {
  const statusCode = statusCodeFrom(r.statusCodes);
  const headers = r.headers.map((h) => responseHeaderFrom(ctx, opName, h));
  let typeRef;
  if (r.type) {
    const { type } = unwrapNullable(r.type);
    typeRef = typeRefFor(ctx, type, `${opName}.response`);
  }
  const contentType = r.defaultContentType ?? r.contentTypes?.[0];
  return {
    statusCode,
    ...(typeRef !== undefined ? { type: typeRef } : {}),
    ...(contentType !== undefined ? { contentType } : {}),
    ...(headers.length > 0 ? { headers } : {}),
    isError,
    ...(r.description !== undefined ? { docs: r.description } : {}),
  };
}

export function responsesFrom(
  ctx: BuildContext,
  op: SdkHttpOperation,
  opName: string,
): IRResponse[] {
  const out: IRResponse[] = [];
  for (const r of op.responses) out.push(responseOf(ctx, opName, r, false));
  for (const r of op.exceptions) out.push(responseOf(ctx, opName, r, true));
  return out;
}
