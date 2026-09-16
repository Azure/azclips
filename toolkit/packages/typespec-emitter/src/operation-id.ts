import { getNamespaceFullName, type Operation } from "@typespec/compiler";
import type { IRId } from "@azure-client-engine/ir";
import type { BuildContext } from "./context.js";

/** Effective TypeSpec ownership, independent of SDK client organization. */
export function operationIdFor(ctx: BuildContext, raw: Operation): IRId {
  const owner = raw.interface;
  const namespace = owner?.namespace ?? raw.namespace;
  const fqn = namespace !== undefined ? getNamespaceFullName(namespace) : "";
  return {
    module: ctx.options.sourceId,
    namespace: fqn.length === 0 ? [] : fqn.split("."),
    ...(owner !== undefined ? { interface: owner.name } : {}),
    name: raw.name,
  };
}
