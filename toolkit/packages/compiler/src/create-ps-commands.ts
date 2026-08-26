/**
 * `TransformStep` that enriches each `IROperation`'s `hints.powershell` in place
 * (verb, noun, parameterSetName, confirmImpact) — grouping into `PsCommand`s
 * happens later, in `derivePsCommands` (design.md Decision 8). Only fills in
 * fields an author hasn't already set; directives (`directives.ts`) apply as a
 * fallback layer between inference and inline hints (design.md Decision 7).
 */

import type {
  HttpMethod,
  IR,
  IROperation,
  IRParameter,
  IRTypeRef,
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

/** Model/enum refs carry a name directly; array refs recurse into their element. Others have none. */
function modelNameOfTypeRef(type: IRTypeRef): string | undefined {
  switch (type.kind) {
    case "model":
    case "enum":
      return type.id.name;
    case "array":
      return modelNameOfTypeRef(type.element);
    default:
      return undefined;
  }
}

/**
 * The resource name implied by what the operation actually returns, not by which interface it's
 * declared under — a nested list operation can return a different resource than its containing
 * interface implies. Prefers IR's own `itemType` for pageable operations over guessing at the
 * page-wrapper model's name (wrapper naming has no consistent convention across specs). Response/
 * item type names are already singular, so never additionally singularized.
 */
function responseModelNameCandidate(operation: IROperation): string | undefined {
  if (operation.semantics?.kind === "pageable") {
    const name = modelNameOfTypeRef(operation.semantics.itemType);
    if (name !== undefined) return name;
  }

  for (const response of operation.responses) {
    if (response.isError || response.type === undefined) continue;
    const name = modelNameOfTypeRef(response.type);
    if (name === undefined) continue;
    return name;
  }
  return undefined;
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
): { hints: PowershellOperationHint; identity: CommandIdentity } {
  const existing = operation.rendering?.powershell ?? {};

  const inferredVerb = resolveVerb(operation.id.name, operation.method);
  const inferredNoun = responseModelNameCandidate(operation) ?? singularize(resourceNameCandidate(operation));
  // "" means "not configured"; keep as undefined since the schema requires a non-empty string.
  const inferredSubjectPrefix = options.subjectPrefix || undefined;
  const inferredConfirmImpact = isMutatingMethod(operation.method) ? "Medium" : undefined;
  // `@access(internal)` operations aren't meant to be part of the public surface.
  const inferredHidden = operation.access === "internal" ? true : undefined;
  const inferredSupportsPaging = operation.semantics?.kind === "pageable" ? true : undefined;

  // Directive matching uses this pre-directive identity, so parameter-level directives see
  // the same values regardless of what a command-level directive later overrides.
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
  const hidden = existing.hidden ?? directiveSet.hidden ?? inferredHidden;
  const summary = existing.summary ?? operation.summary;
  const description = existing.description ?? operation.docs;
  const httpPath =
    existing.httpPath ?? { path: operation.pathTemplate, apiVersion: operation.apiVersion.value };
  const supportsPaging = existing.supportsPaging ?? inferredSupportsPaging;

  return {
    hints: {
      ...existing,
      verb,
      noun,
      subjectPrefix,
      parameterSetName,
      confirmImpact,
      alias,
      hidden,
      summary,
      description,
      httpPath,
      supportsPaging,
    },
    identity,
  };
}

/** Resolve one parameter's `hints.powershell`: infers `mandatory`/`helpMessage` from wire optionality/docs, applies matching directives as a fallback default for the rest. */
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
  const directiveSet = mergeDirectiveSets(matching.map((d) => d.set));

  const mandatory = existing?.mandatory ?? !parameter.optional;
  const helpMessage = existing?.helpMessage ?? parameter.summary ?? parameter.docs;
  const aliases = existing?.aliases ?? directiveSet.alias;
  const dontShow = existing?.dontShow ?? directiveSet.dontShow;

  return {
    ...parameter.rendering,
    powershell: { ...existing, mandatory, helpMessage, aliases, dontShow },
  };
}

function enrichOperation(operation: IROperation, options: Required<CreatePsCommandsOptions>): IROperation {
  const { hints: powershell, identity: owningCommand } = resolveOperationHints(operation, options);

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
