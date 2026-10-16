/**
 * SdkServiceMethod → IROperation.
 *
 * Walks the client tree (`sdkPackage.clients` + each client's `methods`,
 * recursive into `children`/sub-clients) and produces one IROperation per
 * leaf method. Drops override methods (`isOverride`), diagnosed once.
 *
 * Per-op we delegate the bulk of the work to `http.ts` (HTTP framing) and
 * `semantics.ts` (paging/LRO). Each operation carries the source's api-version
 * stamp and a reference to its shared server definition.
 */

import { NoTarget } from "@typespec/compiler";
import type { Operation } from "@typespec/compiler";
import { getClientLocation } from "@azure-tools/typespec-client-generator-core";
import type {
  SdkClientType,
  SdkHttpOperation,
  SdkServiceMethod,
} from "@azure-tools/typespec-client-generator-core";
import type {
  IRApiVersionStamp,
  IRId,
  IROperation,
  HttpMethod,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { bodyFrom, parametersByLocation, responsesFrom } from "./http.js";
import { semanticsFor } from "./semantics.js";
import { accessFrom } from "./annotations.js";
import { operationIdFor } from "./operation-id.js";

const VALID_HTTP_METHODS: ReadonlySet<string> = new Set([
  "GET",
  "PUT",
  "POST",
  "DELETE",
  "PATCH",
  "HEAD",
  "OPTIONS",
]);

function clampHttpMethod(verb: string): HttpMethod | undefined {
  const up = verb.toUpperCase();
  return VALID_HTTP_METHODS.has(up) ? (up as HttpMethod) : undefined;
}

/**
 * The group `@clientLocation` explicitly moved this operation to, or `undefined`
 * when it sits where it was declared.
 *
 * TCGC resolves the decorator for this emitter's scope, so a spec that scopes a
 * relocation (`"!csharp"`) is honoured or ignored automatically. The target is a
 * string, an `Interface`, or a `Namespace`; all three name a group, so all three
 * reduce to that name.
 *
 * Only an *explicit* relocation is carried. TCGC also reports a default placement
 * through the client tree, but that merely restates the declaration site, which
 * `id.interface` already records — passing it on would make the two
 * indistinguishable and lose exactly the authored signal this field exists for.
 */
function clientLocationOf(ctx: BuildContext, raw: Operation): string | undefined {
  const location = getClientLocation(ctx.sdkContext, raw);
  if (location === undefined) return undefined;
  const name = typeof location === "string" ? location : location.name;
  return name.length > 0 ? name : undefined;
}

function methodToOperation(
  ctx: BuildContext,
  method: SdkServiceMethod<SdkHttpOperation>,
  apiVersion: IRApiVersionStamp,
  server: IRId,
): IROperation | undefined {
  if (method.isOverride) {
    if (!ctx.flags.overrideMethodWarned) {
      reportDiagnostic(ctx.program, { code: "override-method-skipped", target: NoTarget });
      ctx.flags.overrideMethodWarned = true;
    }
    return undefined;
  }

  const http = method.operation;
  const methodVerb = clampHttpMethod(http.verb);
  if (!methodVerb) {
    // Cannot represent the HTTP verb in IR — skip and continue.
    return undefined;
  }

  const id = operationIdFor(ctx, method.__raw ?? method.operation.__raw.operation);
  const clientLocation = clientLocationOf(ctx, method.__raw ?? method.operation.__raw.operation);
  const body = bodyFrom(ctx, http, method.name);
  if (!body.ok) return undefined; // streaming-body-dropped already reported

  const { pathParameters, queryParameters, headerParameters } = parametersByLocation(
    ctx,
    http,
    id,
  );
  const responses = responsesFrom(ctx, http, method.name);
  if (responses.length === 0) {
    // IR requires min(1). Synthesize a default empty 200.
    responses.push({
      statusCode: { kind: "exact", code: 200 },
      isError: false,
    });
  }

  const semantics = semanticsFor(ctx, method, id);
  const access = accessFrom(method.access);

  const op: IROperation = {
    id,
    name: method.name,
    source: ctx.options.sourceId,
    method: methodVerb,
    pathTemplate: http.path,
    pathParameters,
    queryParameters,
    headerParameters,
    ...(body.body !== undefined ? { requestBody: body.body } : {}),
    responses,
    apiVersion,
    server,
    ...(clientLocation !== undefined ? { clientLocation } : {}),
    ...(semantics !== undefined ? { semantics } : {}),
    ...(method.doc !== undefined ? { docs: method.doc } : {}),
    ...(method.summary !== undefined ? { summary: method.summary } : {}),
    ...(access !== undefined ? { access } : {}),
  };
  return op;
}

function walkClient(
  ctx: BuildContext,
  client: SdkClientType<SdkHttpOperation>,
  apiVersion: IRApiVersionStamp,
  server: IRId,
  out: IROperation[],
): void {
  for (const m of client.methods) {
    const op = methodToOperation(ctx, m, apiVersion, server);
    if (op) out.push(op);
  }
  if (client.children) {
    for (const c of client.children) walkClient(ctx, c, apiVersion, server, out);
  }
}

export function operationsFrom(
  ctx: BuildContext,
  apiVersion: IRApiVersionStamp,
  server: IRId,
): IROperation[] {
  const out: IROperation[] = [];
  for (const client of ctx.sdkContext.sdkPackage.clients) {
    walkClient(ctx, client, apiVersion, server, out);
  }
  return out;
}
