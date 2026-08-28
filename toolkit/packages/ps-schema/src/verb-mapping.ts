/**
 * Verb inference table (ported from the `create-ps-commands` TransformStep design). An
 * action-word table match always wins over the HTTP-method fallback — required by real
 * precedent: `ResourceGroups.createOrUpdate` is a PUT, but the real cmdlet is
 * `New-AzResourceGroup`, not `Set-AzResourceGroup`.
 */

import type { HttpMethod } from "@azure-client-engine/ir";

/** Seeded from AutoRest's default `verb-mapping:` table (autorest-configuration.md). */
const ACTION_WORD_VERBS: ReadonlyMap<string, string> = new Map([
  ["create", "New"],
  ["createorupdate", "New"],
  ["update", "Update"],
  ["delete", "Remove"],
  ["get", "Get"],
  ["list", "Get"],
]);

/** Longest-key-first so compound action words (e.g. "listByResourceGroup") match correctly. */
const ACTION_WORD_KEYS_BY_LENGTH: readonly string[] = [...ACTION_WORD_VERBS.keys()].sort(
  (a, b) => b.length - a.length,
);

/** Fallback only when the action word has no table entry at all. */
const HTTP_METHOD_VERB_FALLBACK: Record<HttpMethod, string> = {
  GET: "Get",
  PUT: "Set",
  POST: "Invoke",
  DELETE: "Remove",
  PATCH: "Update",
  HEAD: "Invoke",
  OPTIONS: "Invoke",
};

/**
 * Resolve a PowerShell verb from an operation's action word (its IR id's terminal `name`,
 * e.g. "createOrUpdate", "listByResourceGroup") and HTTP method. Table match wins; HTTP
 * method is only a fallback for an unrecognized action word.
 */
export function resolveVerb(actionWord: string, method: HttpMethod): string {
  const normalized = actionWord.toLowerCase();

  const exact = ACTION_WORD_VERBS.get(normalized);
  if (exact !== undefined) return exact;

  for (const key of ACTION_WORD_KEYS_BY_LENGTH) {
    if (normalized.startsWith(key)) return ACTION_WORD_VERBS.get(key)!;
  }

  return HTTP_METHOD_VERB_FALLBACK[method];
}
