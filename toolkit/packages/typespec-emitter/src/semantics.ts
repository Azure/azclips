/**
 * Operation semantics: pageable + LRO.
 *
 * Two detection paths, tried in order:
 *
 *   1. TCGC classification (`method.kind`): when TCGC already recognized the
 *      method as `paging` / `lro` / `lropaging`, use its pre-chewed
 *      `pagingMetadata` / `lroMetadata`.
 *   2. Raw-decorator inspection: plain `@typespec/http` + `@typespec/rest`
 *      specs come through as `kind: "basic"`, so TCGC carries no paging/LRO
 *      metadata even when the spec uses `@pageItems` / `@nextLink` /
 *      `@pollingOperation`. We fall back to the compiler's
 *      `getPagingOperation` and azure-core's `getLroMetadata` on `method.__raw`.
 *
 * Both paths normalize into the same builders so the produced IR is identical
 * regardless of which one fired.
 *
 * Pageable mapping:
 *   - page-items path → `itemsPath` (dotted)
 *   - next-link path  → `nextLinkPath`
 *   - OR: continuation-token response path → `continuationTokenPath`
 *         + continuation-token parameter   → `continuationTokenParameter`
 *   - XOR enforced by the IR schema (we prefer nextLink when both present).
 *
 * LRO mapping:
 *   - `finalStateVia` → `finalStateVia` (mapped + clamped to IR enum)
 *   - statusMonitor step location → `pollingStrategy`
 *   - Anything ambiguous → `custom` polling + `customPollingUrlPath`.
 */

import { NoTarget } from "@typespec/compiler";
import type { ModelProperty, Operation } from "@typespec/compiler";
import { getPagingOperation, resolveEncodedName } from "@typespec/compiler";
import { getLroMetadata } from "@azure-tools/typespec-azure-core";
import type {
  SdkHttpOperation,
  SdkLroPagingServiceMethod,
  SdkLroServiceMethod,
  SdkPagingServiceMethod,
  SdkServiceMethod,
  SdkType,
} from "@azure-tools/typespec-client-generator-core";
import { getClientType } from "@azure-tools/typespec-client-generator-core";
import type {
  IRLongRunningSemantics,
  IROperationSemantics,
  IRPageableSemantics,
  IRTypeRef,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { typeRefFor, unwrapNullable } from "./types.js";

const JSON_MIME = "application/json";

function segPath(segments: { serializedName?: string; name: string }[] | undefined): string | undefined {
  if (!segments || segments.length === 0) return undefined;
  const parts: string[] = [];
  for (const s of segments) {
    const name = (s.serializedName ?? s.name);
    if (!name) continue;
    parts.push(name);
  }
  if (parts.length === 0) return undefined;
  return parts.join(".");
}

/** Dotted wire path for a raw `ModelProperty[]` chain (decorator path). */
function rawSegPath(ctx: BuildContext, segments: ModelProperty[] | undefined): string | undefined {
  if (!segments || segments.length === 0) return undefined;
  const parts: string[] = [];
  for (const p of segments) {
    const name = resolveEncodedName(ctx.program, p, JSON_MIME);
    if (!name) continue;
    parts.push(name);
  }
  if (parts.length === 0) return undefined;
  return parts.join(".");
}

/** Element type of a page-items property, unwrapping the array layer. */
function itemTypeOf(ctx: BuildContext, propType: SdkType): IRTypeRef {
  const { type: unwrapped } = unwrapNullable(ctx, propType);
  if (unwrapped.kind === "array") {
    return typeRefFor(ctx, (unwrapped as { valueType: SdkType }).valueType, "page.itemType");
  }
  return typeRefFor(ctx, unwrapped, "page.itemType");
}

/**
 * Assemble an `IRPageableSemantics` from already-resolved parts, enforcing
 * the IR's nextLink-XOR-continuation rule (prefer nextLink — the Azure REST
 * norm — when a spec somehow declares both).
 */
function assemblePageable(
  itemType: IRTypeRef,
  itemsPath: string,
  nextLinkPath: string | undefined,
  continuationTokenPath: string | undefined,
  continuationTokenParameter: string | undefined,
): IRPageableSemantics | undefined {
  if (nextLinkPath !== undefined) {
    return { kind: "pageable", itemType, itemsPath, nextLinkPath };
  }
  if (continuationTokenPath !== undefined || continuationTokenParameter !== undefined) {
    return {
      kind: "pageable",
      itemType,
      itemsPath,
      ...(continuationTokenPath !== undefined ? { continuationTokenPath } : {}),
      ...(continuationTokenParameter !== undefined ? { continuationTokenParameter } : {}),
    };
  }
  // No paging signal we can express in IR — drop.
  return undefined;
}

// ─── Path 1: TCGC pagingMetadata ────────────────────────────────────────

function pageableSemantics(
  ctx: BuildContext,
  method: SdkPagingServiceMethod<SdkHttpOperation> | SdkLroPagingServiceMethod<SdkHttpOperation>,
): IRPageableSemantics | undefined {
  const meta = method.pagingMetadata;
  if (!meta) return undefined;

  const itemSegs = meta.pageItemsSegments;
  if (!itemSegs || itemSegs.length === 0) return undefined;
  const lastItemSeg = itemSegs[itemSegs.length - 1];
  if (!lastItemSeg) return undefined;
  const itemPathStr = segPath(itemSegs);
  if (!itemPathStr) return undefined;

  const itemType = itemTypeOf(ctx, lastItemSeg.type);

  const nextLinkPath = segPath(meta.nextLinkSegments);
  const continuationTokenPath = segPath(meta.continuationTokenResponseSegments);
  const ctParamSegs = meta.continuationTokenParameterSegments;
  let continuationTokenParameter: string | undefined;
  if (ctParamSegs && ctParamSegs.length > 0) {
    const lastInPath = ctParamSegs[ctParamSegs.length - 1];
    if (lastInPath) {
      continuationTokenParameter =
        (lastInPath as { serializedName?: string; name: string }).serializedName ?? lastInPath.name;
    }
  }

  return assemblePageable(
    itemType,
    itemPathStr,
    nextLinkPath,
    continuationTokenPath,
    continuationTokenParameter,
  );
}

// ─── Path 2: compiler getPagingOperation (decorators) ───────────────────

function pageableFromDecorators(
  ctx: BuildContext,
  raw: Operation,
): IRPageableSemantics | undefined {
  // getPagingOperation returns [undefined, diags] when there's no @pageItems;
  // the diagnostics are surfaced by the compiler's own paging validator, so
  // we intentionally discard them here.
  const [paging] = getPagingOperation(ctx.program, raw);
  if (!paging) return undefined;

  const itemsProp = paging.output.pageItems;
  const itemsPath = rawSegPath(ctx, itemsProp.path);
  if (!itemsPath) return undefined;

  const sdkItemsType = getClientType(ctx.sdkContext, itemsProp.property.type, raw);
  const itemType = itemTypeOf(ctx, sdkItemsType);

  const nextLinkPath = paging.output.nextLink
    ? rawSegPath(ctx, paging.output.nextLink.path)
    : undefined;
  const continuationTokenPath = paging.output.continuationToken
    ? rawSegPath(ctx, paging.output.continuationToken.path)
    : undefined;

  let continuationTokenParameter: string | undefined;
  const ctInput = paging.input.continuationToken;
  if (ctInput && ctInput.path.length > 0) {
    const last = ctInput.path[ctInput.path.length - 1];
    if (last) continuationTokenParameter = resolveEncodedName(ctx.program, last, JSON_MIME);
  }

  return assemblePageable(
    itemType,
    itemsPath,
    nextLinkPath,
    continuationTokenPath,
    continuationTokenParameter,
  );
}

// ─── LRO: shared builder + both detection paths ─────────────────────────

function mapFinalStateVia(raw: string | undefined): IRLongRunningSemantics["finalStateVia"] | "unknown" {
  // FinalStateValue is a string enum from @azure-tools/typespec-azure-core:
  //   "azure-async-operation" | "location" | "operation-location"
  //   | "original-uri" | "custom-link" | "custom-operation-reference"
  // IR's enum only covers azureAsyncOperation/location/originalUri; everything
  // else either maps onto one of those (operation-location ~ location) or is
  // dropped with `undefined` since the polling strategy carries the rest.
  switch (raw) {
    case undefined:
      return undefined;
    case "azure-async-operation":
      return "azureAsyncOperation";
    case "location":
    case "operation-location":
      return "location";
    case "original-uri":
      return "originalUri";
    case "custom-link":
    case "custom-operation-reference":
      // No IR enum value — the pollingStrategy carries the custom path.
      return undefined;
    default:
      return "unknown";
  }
}

/** Normalized inputs both LRO detection paths reduce to. */
type LroInputs = {
  opName: string;
  stepKind: "nextOperationLink" | "nextOperationReference" | undefined;
  /** Only meaningful for `nextOperationLink`. */
  stepLocation: "ResponseHeader" | "ResponseBody" | "Self" | undefined;
  finalStateViaRaw: string | undefined;
  logicalPath: string | undefined;
  finalResultType: IRTypeRef | undefined;
};

function buildLro(ctx: BuildContext, inputs: LroInputs): IRLongRunningSemantics {
  const { opName } = inputs;

  // Polling strategy is derived from the statusMonitor step location.
  let pollingStrategy: IRLongRunningSemantics["pollingStrategy"] = "custom";
  let customPath: string | undefined;
  if (inputs.stepKind === "nextOperationLink") {
    const loc = inputs.stepLocation;
    if (loc === "ResponseHeader") {
      // Without the header name we can't differentiate; pick `location`
      // as a sensible default and let the transformer specialize.
      pollingStrategy = "location";
    } else if (loc === "ResponseBody") {
      pollingStrategy = "operationResource";
    } else {
      pollingStrategy = "custom";
      customPath = inputs.logicalPath ?? "";
    }
  } else if (inputs.stepKind === "nextOperationReference") {
    // A separate polling operation is referenced; use azureAsyncOperation
    // as the conventional name. Transformer can refine.
    pollingStrategy = "azureAsyncOperation";
  } else {
    pollingStrategy = "custom";
    customPath = inputs.logicalPath ?? "";
  }

  // If we chose `custom` but have no path, fall back and warn.
  if (pollingStrategy === "custom") {
    if (!customPath || customPath.length === 0) {
      customPath = "_";
      reportDiagnostic(ctx.program, {
        code: "ambiguous-polling-strategy",
        format: { operation: opName, path: customPath },
        target: NoTarget,
      });
    }
  }

  // finalStateVia
  const mapped = mapFinalStateVia(inputs.finalStateViaRaw);
  let finalStateVia: IRLongRunningSemantics["finalStateVia"];
  if (mapped === "unknown") {
    reportDiagnostic(ctx.program, {
      code: "unknown-final-state-via",
      format: { operation: opName, value: String(inputs.finalStateViaRaw) },
      target: NoTarget,
    });
    finalStateVia = undefined;
  } else {
    finalStateVia = mapped;
  }

  const finalResultType = inputs.finalResultType;

  const out: IRLongRunningSemantics = {
    kind: "longRunning",
    pollingStrategy,
    ...(finalStateVia !== undefined ? { finalStateVia } : {}),
    ...(finalResultType !== undefined ? { finalResultType } : {}),
    ...(pollingStrategy === "custom" && customPath !== undefined
      ? { customPollingUrlPath: customPath }
      : {}),
  };
  return out;
}

function lroSemantics(
  ctx: BuildContext,
  method: SdkLroServiceMethod<SdkHttpOperation> | SdkLroPagingServiceMethod<SdkHttpOperation>,
): IRLongRunningSemantics {
  const meta = method.lroMetadata;
  const opName = method.name;

  const step = meta.statusMonitorStep;
  const stepKind = step?.kind;
  const stepLocation = step && step.kind === "nextOperationLink" ? step.target?.location : undefined;

  let finalResultType: IRTypeRef | undefined;
  const fr = meta.finalResult;
  if (fr && fr !== "void") {
    finalResultType = typeRefFor(ctx, fr as unknown as SdkType, `${opName}.lro.finalResult`);
  }

  return buildLro(ctx, {
    opName,
    stepKind,
    stepLocation,
    finalStateViaRaw: meta.finalStateVia as unknown as string | undefined,
    logicalPath: meta.logicalPath,
    finalResultType,
  });
}

function lroFromDecorators(
  ctx: BuildContext,
  opName: string,
  raw: Operation,
): IRLongRunningSemantics | undefined {
  const meta = getLroMetadata(ctx.program, raw);
  if (!meta) return undefined;

  const step = meta.statusMonitorStep;
  const stepKind = step?.kind;
  const stepLocation = step && step.kind === "nextOperationLink" ? step.target?.location : undefined;

  let finalResultType: IRTypeRef | undefined;
  const fr = meta.finalResult;
  if (fr && fr !== "void") {
    const sdkType = getClientType(ctx.sdkContext, fr, raw);
    finalResultType = typeRefFor(ctx, sdkType, `${opName}.lro.finalResult`);
  }

  return buildLro(ctx, {
    opName,
    stepKind,
    stepLocation,
    finalStateViaRaw: meta.finalStateVia as unknown as string | undefined,
    logicalPath: meta.logicalPath,
    finalResultType,
  });
}

export function semanticsFor(
  ctx: BuildContext,
  method: SdkServiceMethod<SdkHttpOperation>,
): IROperationSemantics | undefined {
  // 1. Prefer TCGC's own classification. IR can't carry both paging + LRO,
  //    so LRO wins for `lropaging` (the paging half is recoverable downstream).
  if (method.kind === "lro" || method.kind === "lropaging") {
    return lroSemantics(ctx, method);
  }
  if (method.kind === "paging") {
    const p = pageableSemantics(ctx, method);
    if (p) return p;
  }

  // 2. Fallback: inspect the raw operation's decorators. Covers plain
  //    @typespec/http + @typespec/rest specs that TCGC classifies as "basic".
  const raw = method.__raw;
  if (!raw) return undefined;

  // LRO takes precedence over paging, mirroring the lropaging rule above.
  const lro = lroFromDecorators(ctx, method.name, raw);
  if (lro) return lro;

  const page = pageableFromDecorators(ctx, raw);
  if (page) return page;

  return undefined;
}
