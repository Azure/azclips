/**
 * `create-ps-commands`: enriches each `IROperation`'s `hints.powershell` in
 * place (verb, noun, parameterSetName, confirmImpact). Genuinely `IR -> IR`
 * — see ps-cmdlet-schema's design.md Decision 8 for why this can't instead
 * return a `PsCommand[]`, and why grouping into `PsCommand`s is a separate,
 * later function (`derivePsCommands`, in `@azure-client-engine/ps-commands`).
 *
 * Named with a `Ps` qualifier (not bare `create-commands`) since azclips has
 * both a CLI and a PS surface — this step is PowerShell-only.
 *
 * Any field an author already set via `hints.powershell` is left untouched
 * (ordinary last-writer-wins hint semantics) — this transform only fills in
 * what's missing. Directive-style bulk overrides (design.md Decision 7) are
 * applied as a fallback default layered between inference and inline hints
 * — see `directives.ts`.
 */

import type {
  HttpMethod,
  IR,
  IROperation,
  IRParameter,
  ParameterHints,
  PowershellOperationHint,
} from "@azure-client-engine/ir";
import type { TransformStep } from "./transform.js";
import { resolveVerb } from "./verb-mapping.js";
import {
  DirectivesSchema,
  matchesCommand,
  matchesParameter,
  mergeDirectiveSets,
  resolveSelector,
  type CommandIdentity,
  type Directive,
} from "./directives.js";

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

/** Every parameter name on an operation — path, query, and header (directive `parameterName` matching scope). */
function parameterNamesOf(operation: IROperation): string[] {
  return [
    ...operation.pathParameters.map((p) => p.name),
    ...operation.queryParameters.map((p) => p.name),
    ...operation.headerParameters.map((p) => p.name),
  ];
}

export interface CreatePsCommandsOptions {
  /** Applied to every inferred noun, e.g. "Az" -> "AzResourceGroup". Default: "" (no prefix). */
  subjectPrefix?: string;
  /** Bulk `select`/`where`/`set` overrides, applied as defaults between inference and inline hints. */
  directives?: Directive[];
}

/** Resolve one operation's `hints.powershell`, filling in only what's not already authored. */
function resolveOperationHints(
  operation: IROperation,
  options: Required<CreatePsCommandsOptions>,
): PowershellOperationHint {
  const existing = operation.rendering?.powershell ?? {};

  const inferredVerb = resolveVerb(operation.id.name, operation.method);
  const inferredNoun = singularize(resourceNameCandidate(operation));
  // "" means "no prefix configured" — must stay undefined, not "", since
  // PowershellOperationHintSchema's subjectPrefix is a non-empty string when present.
  const inferredSubjectPrefix = options.subjectPrefix || undefined;
  const inferredConfirmImpact = isMutatingMethod(operation.method) ? "Medium" : undefined;

  // Directive matching evaluates against the "already-authored, else inferred" identity —
  // what the operation's verb/noun/subjectPrefix would be with no directive at all.
  const identity: CommandIdentity = {
    verb: existing.verb ?? inferredVerb,
    subject: existing.noun ?? inferredNoun,
    subjectPrefix: existing.subjectPrefix ?? inferredSubjectPrefix,
    parameterNames: parameterNamesOf(operation),
  };

  const matching = options.directives.filter(
    (d) => resolveSelector(d) === "command" && matchesCommand(d.where, identity),
  );
  const directiveSet = mergeDirectiveSets(matching.map((d) => d.set));

  const verb = existing.verb ?? directiveSet.verb ?? inferredVerb;
  const noun = existing.noun ?? directiveSet.subject ?? inferredNoun;
  const subjectPrefix = existing.subjectPrefix ?? inferredSubjectPrefix;
  const parameterSetName = existing.parameterSetName ?? capitalize(operation.id.name);
  const confirmImpact = existing.confirmImpact ?? directiveSet.confirmImpact ?? inferredConfirmImpact;
  const alias = existing.alias ?? directiveSet.alias;
  const hidden = existing.hidden ?? directiveSet.hidden;

  return { verb, noun, subjectPrefix, parameterSetName, confirmImpact, alias, hidden };
}

/** Resolve one parameter's `hints.powershell`, applying matching directives as a fallback default. */
function resolveParameterHints(
  parameter: IRParameter,
  owningCommand: CommandIdentity,
  directives: readonly Directive[],
): ParameterHints | undefined {
  const existing = parameter.rendering?.powershell;

  const matching = directives.filter(
    (d) =>
      resolveSelector(d) === "parameter" &&
      matchesParameter(d.where, { parameterName: parameter.name, owningCommand }),
  );
  if (matching.length === 0) return parameter.rendering;

  const directiveSet = mergeDirectiveSets(matching.map((d) => d.set));
  const aliases = existing?.aliases ?? directiveSet.alias;
  const dontShow = existing?.dontShow ?? directiveSet.dontShow;
  if (aliases === undefined && dontShow === undefined && existing === undefined) return parameter.rendering;

  return {
    ...parameter.rendering,
    powershell: { ...existing, aliases, dontShow },
  };
}

function enrichOperation(operation: IROperation, options: Required<CreatePsCommandsOptions>): IROperation {
  const powershell = resolveOperationHints(operation, options);
  const owningCommand: CommandIdentity = {
    verb: powershell.verb!,
    subject: powershell.noun!,
    subjectPrefix: powershell.subjectPrefix,
    parameterNames: parameterNamesOf(operation),
  };

  const enrichParameter = (parameter: IRParameter): IRParameter => ({
    ...parameter,
    rendering: resolveParameterHints(parameter, owningCommand, options.directives),
  });

  return {
    ...operation,
    pathParameters: operation.pathParameters.map(enrichParameter),
    queryParameters: operation.queryParameters.map(enrichParameter),
    headerParameters: operation.headerParameters.map(enrichParameter),
    rendering: {
      ...operation.rendering,
      powershell,
    },
  };
}

/** Factory for `CompilerRegistry.transforms`, registered under id `"create-ps-commands"`. */
export function createPsCommandsFactory(options: Record<string, unknown> = {}): TransformStep {
  const resolvedOptions: Required<CreatePsCommandsOptions> = {
    subjectPrefix: typeof options.subjectPrefix === "string" ? options.subjectPrefix : "",
    directives: Array.isArray(options.directives) ? DirectivesSchema.parse(options.directives) : [],
  };

  return {
    name: "create-ps-commands",
    run: (ir: IR): IR => ({
      ...ir,
      operations: ir.operations.map((operation) => enrichOperation(operation, resolvedOptions)),
    }),
  };
}
