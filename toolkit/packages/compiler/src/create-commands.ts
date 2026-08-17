/**
 * `create-commands`: enriches each `IROperation`'s `hints.powershell` in
 * place (verb, noun, parameterSetName, confirmImpact). Genuinely `IR -> IR`
 * — see ps-cmdlet-schema's design.md Decision 8 for why this can't instead
 * return a `Command[]`, and why grouping into `Command`s is a separate,
 * later function (`deriveCommands`, in `@azure-client-engine/ps-commands`).
 *
 * Any field an author already set via `hints.powershell` is left untouched
 * (ordinary last-writer-wins hint semantics) — this transform only fills in
 * what's missing.
 */

import type { HttpMethod, IR, IROperation, PowershellOperationHint } from "@azure-client-engine/ir";
import type { TransformStep } from "./transform.js";
import { resolveVerb } from "./verb-mapping.js";

/** PUT/POST/PATCH/DELETE mutate; GET does not (design.md Decision 4's ShouldProcess policy). */
function isMutatingMethod(method: HttpMethod): boolean {
  return method === "PUT" || method === "POST" || method === "PATCH" || method === "DELETE";
}

/** Strip a trailing "s" plural, handling the common English suffix patterns. */
function singularize(word: string): string {
  if (/[^aeiou]ies$/i.test(word)) return word.slice(0, -3) + "y";
  if (/(sses|xes|zes|ches|shes)$/i.test(word)) return word.slice(0, -2);
  if (/s$/i.test(word) && !/ss$/i.test(word)) return word.slice(0, -1);
  return word;
}

function capitalize(word: string): string {
  return word.length > 0 ? word[0]!.toUpperCase() + word.slice(1) : word;
}

/** The operation's IR id's terminal namespace segment — the resource/model name candidate. */
function resourceNameCandidate(operation: IROperation): string {
  const segments = operation.id.namespace;
  return segments.length > 0 ? segments[segments.length - 1]! : operation.id.name;
}

export interface CreateCommandsOptions {
  /** Applied to every inferred noun, e.g. "Az" -> "AzResourceGroup". Default: "" (no prefix). */
  subjectPrefix?: string;
}

/** Resolve one operation's `hints.powershell`, filling in only what's not already authored. */
function resolveOperationHints(
  operation: IROperation,
  options: Required<CreateCommandsOptions>,
): PowershellOperationHint {
  const existing = operation.rendering?.powershell ?? {};

  const verb = existing.verb ?? resolveVerb(operation.id.name, operation.method);
  const noun = existing.noun ?? singularize(resourceNameCandidate(operation));
  // "" means "no prefix configured" — must stay undefined, not "", since
  // PowershellOperationHintSchema's subjectPrefix is a non-empty string when present.
  const subjectPrefix = existing.subjectPrefix ?? (options.subjectPrefix || undefined);
  const parameterSetName = existing.parameterSetName ?? capitalize(operation.id.name);
  const confirmImpact =
    existing.confirmImpact ?? (isMutatingMethod(operation.method) ? "Medium" : undefined);

  return { verb, noun, subjectPrefix, parameterSetName, confirmImpact };
}

function enrichOperation(operation: IROperation, options: Required<CreateCommandsOptions>): IROperation {
  return {
    ...operation,
    rendering: {
      ...operation.rendering,
      powershell: resolveOperationHints(operation, options),
    },
  };
}

/** Factory for `CompilerRegistry.transforms`, registered under id `"create-commands"`. */
export function createCommandsFactory(options: Record<string, unknown> = {}): TransformStep {
  const resolvedOptions: Required<CreateCommandsOptions> = {
    subjectPrefix: typeof options.subjectPrefix === "string" ? options.subjectPrefix : "",
  };

  return {
    name: "create-commands",
    run: (ir: IR): IR => ({
      ...ir,
      operations: ir.operations.map((operation) => enrichOperation(operation, resolvedOptions)),
    }),
  };
}
