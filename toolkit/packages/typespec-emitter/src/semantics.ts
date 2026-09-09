/**
 * Operation semantics: pageable + LRO.
 *
 * Paging is resolved independently from the original REST operation by
 * `paging.ts`. LRO uses SDK metadata when classified, then Azure Core's raw
 * metadata. Neither trait takes precedence over the other.
 *
 * LRO mapping:
 *   - `finalStateVia` → `finalStateVia` (mapped + clamped to IR enum)
 *   - statusMonitor step location → `pollingStrategy`
 *   - Anything ambiguous → `custom` polling + `customPollingUrlPath`.
 */

import { NoTarget } from "@typespec/compiler";
import type { Operation } from "@typespec/compiler";
import { getLroMetadata } from "@azure-tools/typespec-azure-core";
import type {
  SdkHttpOperation,
  SdkLroPagingServiceMethod,
  SdkLroServiceMethod,
  SdkServiceMethod,
  SdkType,
} from "@azure-tools/typespec-client-generator-core";
import { getClientType } from "@azure-tools/typespec-client-generator-core";
import type {
  IRLongRunningSemantics,
  IRId,
  IROperationSemantics,
  IRTypeRef,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { typeRefFor } from "./types.js";
import { pagingFor } from "./paging.js";

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
  operationId: IRId,
): IROperationSemantics | undefined {
  const pageable = pagingFor(ctx, method, operationId);
  const longRunning = method.kind === "lro" || method.kind === "lropaging"
    ? lroSemantics(ctx, method)
    : lroFromDecorators(ctx, method.name, method.operation.__raw.operation);
  if (!pageable && !longRunning) return undefined;
  return {
    ...(pageable ? { pageable } : {}),
    ...(longRunning ? { longRunning } : {}),
  };
}
