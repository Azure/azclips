/**
 * Source annotations that TCGC computes on every type: data-flow `usage`
 * (a `UsageFlags` bitfield) and generated-code `access` (public/internal).
 *
 * ace carries both to the IR verbatim (source-faithful — nothing dropped),
 * decoding the numeric usage bitfield into IR's string tokens. Per the IR's
 * omit-when-default convention, `usage` is omitted when empty (`Usage.None`)
 * and `access` when `public` (the default).
 */

import { UsageFlags } from "@azure-tools/typespec-client-generator-core";
import type { AccessFlags } from "@azure-tools/typespec-client-generator-core";
import type { IRAccess, IRUsage } from "@azure-client-engine/ir";

/** Ordered so the emitted array reads low-bit → high-bit. */
const USAGE_BITS: ReadonlyArray<readonly [UsageFlags, IRUsage]> = [
  [UsageFlags.Input, "input"],
  [UsageFlags.Output, "output"],
  [UsageFlags.ApiVersionEnum, "apiVersionEnum"],
  [UsageFlags.JsonMergePatch, "jsonMergePatch"],
  [UsageFlags.MultipartFormData, "multipartFormData"],
  [UsageFlags.Spread, "spread"],
  [UsageFlags.Json, "json"],
  [UsageFlags.Xml, "xml"],
  [UsageFlags.Exception, "exception"],
  [UsageFlags.LroInitial, "lroInitial"],
  [UsageFlags.LroPolling, "lroPolling"],
  [UsageFlags.LroFinalEnvelope, "lroFinalEnvelope"],
  [UsageFlags.External, "external"],
];

/**
 * Decode a TCGC `UsageFlags` bitfield into IR usage tokens. Returns `undefined`
 * for `Usage.None` (empty) so the IR omits the field.
 */
export function usageFrom(usage: number): IRUsage[] | undefined {
  const out: IRUsage[] = [];
  for (const [bit, token] of USAGE_BITS) {
    if ((usage & bit) === bit) out.push(token);
  }
  return out.length > 0 ? out : undefined;
}

/**
 * Map TCGC's resolved `.access` onto the IR access token. Returns `undefined`
 * for `public` (the default) so the IR omits the field; `internal` is carried.
 */
export function accessFrom(access: AccessFlags | undefined): IRAccess | undefined {
  return access === "internal" ? "internal" : undefined;
}
