/**
 * IRSource construction.
 *
 * One IRSource per service namespace. The plan calls for a single source
 * per emit (multi-service is warned and only the first is emitted).
 *
 * Composition:
 *   - `id`           ← user-supplied `sourceId` option, or the service
 *                     namespace's FQN.
 *   - `entrypoint`   ← the program's main file path.
 *   - `apiVersion`   ← walk the first op's parameters for `isApiVersionParam`;
 *                     value from `sdkPackage.metadata.apiVersions.get(fqn)`
 *                     or `getDefaultApiVersion(serviceNs)`.
 *   - `server`       ← reference to the shared definition built from
 *                     rootClient.clientInitialization's endpoint parameter.
 *   - `rootNamespace`← FQN split by '.'.
 */

import { NoTarget } from "@typespec/compiler";
import type { Namespace } from "@typespec/compiler";
import { getNamespaceFullName, getSourceLocation } from "@typespec/compiler";
import type {
  SdkClientType,
  SdkEndpointParameter,
  SdkHttpOperation,
  SdkServiceMethod,
} from "@azure-tools/typespec-client-generator-core";
import {
  getDefaultApiVersion,
  isApiVersion,
} from "@azure-tools/typespec-client-generator-core";
import type {
  IRApiVersionStamp,
  IRServer,
  IRServerDefinition,
  IRSource,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { authFrom } from "./auth.js";
import { reportDiagnostic } from "./lib.js";
import { serverFrom } from "./server.js";

function rootClientOfPackage(
  ctx: BuildContext,
): SdkClientType<SdkHttpOperation> | undefined {
  const clients = ctx.sdkContext.sdkPackage.clients;
  return clients[0];
}

function findEndpointParameter(
  client: SdkClientType<SdkHttpOperation>,
): SdkEndpointParameter | undefined {
  const params = client.clientInitialization.parameters;
  for (const p of params) {
    if (p.kind === "endpoint") return p as SdkEndpointParameter;
  }
  return undefined;
}

function apiVersionValueFor(
  ctx: BuildContext,
  serviceNs: Namespace,
  fqn: string,
): string {
  const fromMetadata = ctx.sdkContext.sdkPackage.metadata.apiVersions?.get(fqn);
  if (fromMetadata !== undefined && fromMetadata.length > 0) return fromMetadata;
  const def = getDefaultApiVersion(ctx.sdkContext, serviceNs);
  if (def && def.value) return def.value;
  return "unknown";
}

/**
 * Walk the first available op to learn where its api-version parameter
 * lives (`query` vs `header`) and what its wire name is. Subsequent ops
 * that disagree trigger an `inconsistent-api-version-location` warning.
 */
function apiVersionLocationFor(
  ctx: BuildContext,
  fqn: string,
): { location: "query" | "header"; parameterName: string } {
  // Walk every op once; record the first hit, warn on disagreement.
  let chosen: { location: "query" | "header"; parameterName: string } | undefined;
  const recordLoc = (
    method: SdkServiceMethod<SdkHttpOperation>,
  ): void => {
    for (const p of method.operation.parameters) {
      const raw = p.__raw;
      if (!raw) continue;
      if (!isApiVersion(ctx.sdkContext, raw)) continue;
      if (p.kind !== "query" && p.kind !== "header") continue;
      const loc = p.kind;
      const parameterName = p.serializedName ?? p.name;
      if (!chosen) {
        chosen = { location: loc, parameterName };
      } else if (chosen.location !== loc || chosen.parameterName !== parameterName) {
        reportDiagnostic(ctx.program, {
          code: "inconsistent-api-version-location",
          format: { source: fqn, chosen: `${chosen.location}:${chosen.parameterName}` },
          target: NoTarget,
        });
      }
    }
  };

  for (const client of ctx.sdkContext.sdkPackage.clients) {
    for (const m of client.methods) recordLoc(m as SdkServiceMethod<SdkHttpOperation>);
    if (client.children) {
      for (const sub of client.children) {
        for (const m of sub.methods) recordLoc(m as SdkServiceMethod<SdkHttpOperation>);
      }
    }
  }

  if (!chosen) {
    reportDiagnostic(ctx.program, {
      code: "missing-api-version",
      format: { source: fqn },
      target: NoTarget,
    });
    return { location: "query", parameterName: "api-version" };
  }
  return chosen;
}

export type SourceContext = {
  source: IRSource;
  apiVersion: IRApiVersionStamp;
  server: IRServerDefinition;
};

export function buildSource(
  ctx: BuildContext,
  serviceNs: Namespace,
): SourceContext {
  const fqn = getNamespaceFullName(serviceNs);

  const apiVersionLoc = apiVersionLocationFor(ctx, fqn);
  const apiVersionValue = apiVersionValueFor(ctx, serviceNs, fqn);
  const apiVersion: IRApiVersionStamp = {
    value: apiVersionValue,
    location: apiVersionLoc.location,
    parameterName: apiVersionLoc.parameterName,
  };

  const rootClient = rootClientOfPackage(ctx);
  let server: IRServer | undefined;
  if (rootClient) {
    const endpointParam = findEndpointParameter(rootClient);
    if (endpointParam) {
      server = serverFrom(ctx, endpointParam.type);
    }
  }
  if (!server) {
    reportDiagnostic(ctx.program, {
      code: "cannot-resolve-endpoint",
      format: { source: fqn },
      target: NoTarget,
    });
    // Synthesize a placeholder so we can still emit the IR for inspection.
    server = { kind: "literal", url: "https://invalid.example/" };
  }

  // entrypoint: the file path where the service namespace is declared.
  // `program.projectRoot` is a *directory*, not the entry file, so we resolve
  // the actual .tsp file via the namespace's source location, falling back to
  // `<unknown>` if the location can't be determined (e.g. synthetic types).
  let entrypoint = "<unknown>";
  const path = getSourceLocation(serviceNs, { locateId: true })?.file?.path;
  if (path && path.length > 0) entrypoint = path;

  const rootNamespace = fqn.length === 0 ? [] : fqn.split(".");
  const definition: IRServerDefinition = {
    id: { module: ctx.options.sourceId, namespace: rootNamespace, name: "server" },
    endpoint: server,
  };

  const auth = authFrom(ctx, serviceNs);

  const source: IRSource = {
    id: ctx.options.sourceId,
    entrypoint,
    apiVersion,
    server: definition.id,
    rootNamespace,
    ...(auth !== undefined ? { auth } : {}),
  };
  return { source, apiVersion, server: definition };
}
