/**
 * Server resolution.
 *
 * Inputs:
 *   - `SdkEndpointType` (single endpoint), OR
 *   - `SdkUnionType<SdkEndpointType>` (multi-`@server` spec).
 *
 * Mapping:
 *   - SdkEndpointType with no templateArguments  → IRServer.literal
 *   - SdkEndpointType with templateArguments     → IRServer.parameterized
 *   - SdkUnionType<SdkEndpointType>              → IRServer.oneOf, with
 *                                                  each variant recursively
 *                                                  resolved.
 */

import type {
  SdkEndpointType,
  SdkPathParameter,
  SdkType,
  SdkUnionType,
} from "@azure-tools/typespec-client-generator-core";
import type {
  IRServer,
  IRServerParameter,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { typeRefFor, unwrapNullable } from "./types.js";

function paramFor(ctx: BuildContext, p: SdkPathParameter): IRServerParameter {
  const wireName = p.serializedName ?? p.name;
  const { type } = unwrapNullable(ctx, p.type);
  const irType = typeRefFor(ctx, type, `server.${wireName}`);
  return {
    name: p.name,
    wireName,
    type: irType,
    ...(typeof p.clientDefaultValue === "string"
      ? { defaultValue: { kind: "string", value: p.clientDefaultValue } as const }
      : {}),
  };
}

function fromEndpoint(ctx: BuildContext, ep: SdkEndpointType): IRServer {
  if (ep.templateArguments.length === 0) {
    return { kind: "literal", url: ep.serverUrl };
  }
  const params = ep.templateArguments.map((p) => paramFor(ctx, p));
  return { kind: "parameterized", template: ep.serverUrl, parameters: params };
}

/**
 * Top-level: convert a raw endpoint type (which itself may be a union of
 * endpoints if the spec has multiple `@server` decorators) into the IR
 * server. May return `undefined` if no endpoint info is available; the
 * caller is then responsible for issuing `cannot-resolve-endpoint`.
 */
export function serverFrom(
  ctx: BuildContext,
  raw: SdkType | undefined,
): IRServer | undefined {
  if (!raw) return undefined;
  if (raw.kind === "endpoint") {
    return fromEndpoint(ctx, raw as SdkEndpointType);
  }
  if (raw.kind === "union") {
    const u = raw as SdkUnionType;
    const subs: IRServer[] = [];
    for (const v of u.variantTypes) {
      if ((v as SdkType).kind === "endpoint") {
        subs.push(fromEndpoint(ctx, v as SdkEndpointType));
      }
    }
    if (subs.length === 0) return undefined;
    if (subs.length === 1) {
      const only = subs[0];
      if (only) return only;
    }
    return { kind: "oneOf", servers: subs };
  }
  return undefined;
}
