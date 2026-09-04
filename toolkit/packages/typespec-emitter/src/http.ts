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

import { NoTarget, type ModelProperty } from "@typespec/compiler";
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
import { getSdkModelPropertyType, isApiVersion } from "@azure-tools/typespec-client-generator-core";
import type { HttpOperationResponseContent, HttpStatusCodeRange } from "@typespec/http";
import type {
  CollectionFormat,
  FormatKind,
  IRId,
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
import { multipartFrom, sdkTypeFromHttp } from "./multipart.js";
import { reportSdkDiagnostics } from "./discriminators.js";

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

export function parameterIdFor(
  operationId: IRId,
  parameter: { name: string; __raw?: ModelProperty | undefined },
): IRId {
  return { ...operationId, name: `${operationId.name}.${parameter.__raw?.name ?? parameter.name}` };
}

function paramOf(
  ctx: BuildContext,
  operationId: IRId,
  p: SdkHeaderParameter | SdkQueryParameter | SdkPathParameter,
): IRParameter {
  const wireName = p.serializedName ?? p.name;
  const { type, nullable } = unwrapNullable(ctx, p.type);
  const irType = typeRefFor(ctx, type, `${operationId.name}.${p.name}`);
  const id = parameterIdFor(operationId, p);
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
  operationId: IRId,
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
        pathParameters.push(paramOf(ctx, operationId, p));
        break;
      case "query":
        queryParameters.push(paramOf(ctx, operationId, p));
        break;
      case "header":
        headerParameters.push(paramOf(ctx, operationId, p));
        break;
      case "cookie":
        reportDiagnostic(ctx.program, {
          code: "cookie-parameter-dropped",
          format: { name: p.name, operation: operationId.name },
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

  const rawBody = op.__raw.parameters.body;
  const multipart = rawBody?.bodyKind === "multipart"
    ? multipartFrom(ctx, rawBody, b.type, `${opName}.body`)
    : undefined;
  const cts = b.contentTypes ?? [];
  if (cts.length > 1 && !multipart) {
    reportDiagnostic(ctx.program, {
      code: "unsupported-multi-content-body",
      format: { operation: opName, contentTypes: cts.join(", ") },
      target: NoTarget,
    });
  }
  const contentType = b.defaultContentType ?? cts[0] ?? "application/json";

  const { type } = unwrapNullable(ctx, b.type);
  const irType = multipart?.kind === "tuple" ? undefined : typeRefFor(ctx, type, `${opName}.body`);
  const body: IRRequestBody = {
    ...(irType !== undefined ? { type: irType } : {}),
    contentType,
<<<<<<< HEAD
    ...(multipart !== undefined ? { multipart } : {}),
    ...(b.name !== undefined ? { name: b.name } : {}),
    ...(b.isGeneratedName !== undefined ? { isGeneratedName: b.isGeneratedName } : {}),
=======
    // The flag describes `name`, so it only travels with one.
    ...(b.name !== undefined
      ? {
          name: b.name,
          ...(b.isGeneratedName !== undefined ? { isGeneratedName: b.isGeneratedName } : {}),
        }
      : {}),
>>>>>>> f5e3043e0 ([Cirrus Codegen] IR: Tie isGeneratedName to the name it describes)
    ...(b.doc !== undefined ? { docs: b.doc } : {}),
    ...(b.summary !== undefined ? { summary: b.summary } : {}),
  };
  return { ok: true, body };
}

export function statusCodeFrom(
  s: number | HttpStatusCodeRange | "*",
): IRStatusCode {
  if (s === "*") return { kind: "default" };
  if (typeof s === "number") return { kind: "exact", code: s };
  return { kind: "range", start: s.start, end: s.end };
}

function responseHeaderFrom(
  ctx: BuildContext,
  opName: string,
  h: Pick<SdkServiceResponseHeader, "name" | "serializedName" | "type" | "doc" | "summary">,
): IRResponseHeader {
  const wireName = h.serializedName ?? h.name;
  const { type } = unwrapNullable(ctx, h.type);
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
  content?: HttpOperationResponseContent,
): IRResponse {
  const statusCode = statusCodeFrom(r.statusCodes);
  const headers = content
    ? Object.entries(content.headers ?? {}).map(([wireName, raw]) => {
        const [header, diagnostics] = getSdkModelPropertyType(ctx.sdkContext, raw);
        reportSdkDiagnostics(ctx.program, diagnostics);
        return responseHeaderFrom(ctx, opName, { ...header, serializedName: wireName });
      })
    : r.headers.map((h) => responseHeaderFrom(ctx, opName, h));
  const rawBody = content?.body;
  const sdkType = content
    ? (rawBody ? sdkTypeFromHttp(ctx, rawBody.type) : undefined)
    : r.type;
  const multipart = rawBody?.bodyKind === "multipart" && sdkType
    ? multipartFrom(ctx, rawBody, sdkType, `${opName}.response`)
    : undefined;
  let typeRef;
  if (sdkType && multipart?.kind !== "tuple") {
    const { type } = unwrapNullable(ctx, sdkType);
    typeRef = typeRefFor(ctx, type, `${opName}.response`);
  }
  const contentType = content
    ? (rawBody?.contentTypes.includes(r.defaultContentType ?? "") ? r.defaultContentType : rawBody?.contentTypes[0])
    : r.defaultContentType ?? r.contentTypes?.[0];
  return {
    statusCode,
    ...(typeRef !== undefined ? { type: typeRef } : {}),
    ...(contentType !== undefined ? { contentType } : {}),
    ...(multipart !== undefined ? { multipart } : {}),
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
  const append = (response: SdkHttpResponse | SdkHttpErrorResponse, isError: boolean): void => {
    // TCGC may combine several content alternatives under one status code.
    // Resolve multipart against each actual body, not the aggregate response type.
    if (response.__raw.responses.some((content) => content.body?.bodyKind === "multipart")) {
      for (const content of response.__raw.responses) {
        out.push(responseOf(ctx, opName, response, isError, content));
      }
    } else {
      out.push(responseOf(ctx, opName, response, isError));
    }
  };
  for (const r of op.responses) append(r, false);
  for (const r of op.exceptions) append(r, true);
  return out;
}
