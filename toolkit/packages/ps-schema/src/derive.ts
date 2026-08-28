/**
 * Derives `PSCommandAttribute[]`/`PSParameterAttribute[]`/`PSModuleAttribute[]` directly from a
 * plain `IR` document — no intermediate mutation of IR nodes (there's nothing to mutate; PS
 * vocabulary no longer lives on IR). Ported from the old `create-ps-commands` TransformStep +
 * `derivePsCommands`/`deriveModules` (verb/noun inference table, singularization, parameter-set
 * grouping), collapsed into one pass since there's no separate "enrich in place, then group"
 * step anymore.
 *
 * Not yet ported: directive-based authoring overrides, and the `Expanded` (flattened-body)
 * parameter-set fan-out — both real gaps in the old design, deliberately deferred here too so
 * this stays a single, reviewable pass. `PSModuleAttribute.description` has no IR-derivable
 * source today (IRSource carries no rendering/hints in this design) and is always left
 * `undefined`; a future authoring mechanism would need to supply it.
 */

import {
  idEquals,
  serializeId,
  type IR,
  type IROperation,
  type IRId,
  type IRParameter,
  type IRTypeRef,
} from "@azure-client-engine/ir";
import { resolveVerb } from "./verb-mapping.js";
import type {
  PSCommandAttribute,
  PSModuleAttribute,
  PSParameterAttribute,
  PSParameterSetAttribute,
} from "./attributes.js";

export type DeriveAttributesOptions = {
  /** Applied to every inferred noun, e.g. "Az" -> "AzResourceGroup". Default: undefined (no prefix). */
  subjectPrefix?: string | undefined;
};

export type DerivedAttributes = {
  commands: PSCommandAttribute[];
  parameters: PSParameterAttribute[];
  modules: PSModuleAttribute[];
};

// ─── Verb/noun inference helpers ─────────────────────────────────────────

/** PUT/POST/PATCH/DELETE mutate; GET does not. */
function isMutatingMethod(method: IROperation["method"]): boolean {
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
 * declared under. Prefers IR's own `itemType` for pageable operations over guessing at the
 * page-wrapper model's name.
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

/**
 * Combines subject prefix + bare noun, skipping the bare noun when it's already a suffix of the
 * prefix (case-insensitive) — real precedent: the real `ComputeFleet` module's own
 * `subject-prefix` config produces `Get-AzComputeFleet`, not `Get-AzComputeFleetFleet`, for its
 * own eponymous `Fleet` resource, while unrelated nouns (`VirtualMachine`) still get the prefix
 * appended in full.
 */
function combineNoun(subjectPrefix: string | undefined, noun: string): string {
  const prefix = subjectPrefix ?? "";
  if (prefix !== "" && prefix.toLowerCase().endsWith(noun.toLowerCase())) return prefix;
  return prefix + noun;
}

type Inferred = {
  verb: string;
  noun: string;
  confirmImpact: "Medium" | undefined;
  hidden: boolean | undefined;
  supportsPaging: boolean | undefined;
};

function inferOperation(operation: IROperation): Inferred {
  return {
    verb: resolveVerb(operation.id.name, operation.method),
    noun: responseModelNameCandidate(operation) ?? singularize(resourceNameCandidate(operation)),
    confirmImpact: isMutatingMethod(operation.method) ? "Medium" : undefined,
    hidden: operation.access === "internal" ? true : undefined,
    supportsPaging: operation.semantics?.kind === "pageable" ? true : undefined,
  };
}

/** Grouping key: same module + namespace + resolved verb + resolved (bare) noun. */
function groupKey(operation: IROperation, inferred: Inferred): string {
  return [operation.id.module, ...operation.id.namespace, inferred.verb, inferred.noun].join("\u0000");
}

// ─── Output types / parameter sets ───────────────────────────────────────

/** Model/enum refs carry an id directly; array refs recurse into their element. Others have none. */
function extractTypeRefId(type: IRTypeRef): IRId | undefined {
  switch (type.kind) {
    case "model":
    case "enum":
      return type.id;
    case "array":
      return extractTypeRefId(type.element);
    default:
      return undefined;
  }
}

function outputTypeIdsFor(operations: readonly IROperation[]): IRId[] {
  const ids: IRId[] = [];
  for (const operation of operations) {
    for (const response of operation.responses) {
      if (response.isError || response.type === undefined) continue;
      const id = extractTypeRefId(response.type);
      if (id !== undefined && !ids.some((existing) => idEquals(existing, id))) {
        ids.push(id);
      }
    }
  }
  return ids;
}

/**
 * Mirrors `CmdletCommonMetadataAttribute.DefaultParameterSetName`. Merged operations: fewest
 * required parameters wins (e.g. `List` over `Get`).
 */
function defaultParameterSetNameFor(
  operations: readonly IROperation[],
  parameterSets: readonly PSParameterSetAttribute[],
): string {
  const requiredCount = (operation: IROperation): number =>
    [...operation.pathParameters, ...operation.queryParameters, ...operation.headerParameters].filter(
      (p) => !p.optional,
    ).length;
  const mostGeneral = operations.reduce((best, operation) =>
    requiredCount(operation) < requiredCount(best) ? operation : best,
  );
  const set = parameterSets.find((s) => idEquals(s.sourceOperationId, mostGeneral.id));
  return set?.name ?? parameterSets[0]!.name;
}

// ─── Main entry point ────────────────────────────────────────────────────

export function deriveAttributes(ir: IR, options: DeriveAttributesOptions = {}): DerivedAttributes {
  const groups = new Map<string, { operations: IROperation[]; inferred: Inferred }>();

  for (const operation of ir.operations) {
    const inferred = inferOperation(operation);
    const key = groupKey(operation, inferred);
    const group = groups.get(key);
    if (group) group.operations.push(operation);
    else groups.set(key, { operations: [operation], inferred });
  }

  const commands: PSCommandAttribute[] = [];
  const parameters: PSParameterAttribute[] = [];

  for (const { operations, inferred } of groups.values()) {
    const parameterSets: PSParameterSetAttribute[] = operations.map((operation) => ({
      name: capitalize(operation.id.name),
      sourceOperationId: operation.id,
    }));

    commands.push({
      verb: inferred.verb,
      noun: combineNoun(options.subjectPrefix, inferred.noun),
      subjectPrefix: options.subjectPrefix,
      supportsShouldProcess: inferred.confirmImpact !== undefined,
      confirmImpact: inferred.confirmImpact,
      hidden: inferred.hidden,
      summary: operations[0]!.summary,
      description: operations[0]!.docs,
      httpPath: { path: operations[0]!.pathTemplate, apiVersion: operations[0]!.apiVersion.value },
      supportsPaging: inferred.supportsPaging,
      outputTypes: outputTypeIdsFor(operations),
      parameterSets,
      defaultParameterSetName: defaultParameterSetNameFor(operations, parameterSets),
      sourceOperationIds: operations.map((operation) => operation.id),
    });

    for (const operation of operations) {
      const setName = parameterSets.find((s) => idEquals(s.sourceOperationId, operation.id))!.name;
      const allParameters: IRParameter[] = [
        ...operation.pathParameters,
        ...operation.queryParameters,
        ...operation.headerParameters,
      ];
      for (const parameter of allParameters) {
        parameters.push({
          name: parameter.name,
          mandatory: !parameter.optional,
          parameterSetName: setName,
          helpMessage: parameter.summary ?? parameter.docs,
          sourceParameterId: parameter.id,
        });
      }
    }
  }

  const modules = deriveModules(ir, commands);

  return { commands, parameters, modules };
}

/**
 * Groups derived commands by source into `PSModuleAttribute[]`. `PSCommandAttribute` doesn't
 * store a source reference directly, so each command's source is recovered via its first
 * source operation's own `IROperation.source`.
 */
function deriveModules(ir: IR, commands: readonly PSCommandAttribute[]): PSModuleAttribute[] {
  const operationsById = new Map(ir.operations.map((op) => [serializeId(op.id), op]));
  const bySource = new Map<string, PSCommandAttribute[]>();

  for (const command of commands) {
    const firstOperationId = command.sourceOperationIds[0]!;
    const operation = operationsById.get(serializeId(firstOperationId));
    if (operation === undefined) continue; // shouldn't happen for real IR; defensive skip

    const members = bySource.get(operation.source);
    if (members) members.push(command);
    else bySource.set(operation.source, [command]);
  }

  const modules: PSModuleAttribute[] = [];
  for (const [sourceId, groupCommands] of bySource) {
    const source = ir.sources.find((s) => s.id === sourceId);
    if (source === undefined) continue;
    modules.push({
      sourceId: source.id,
      cmdletsToExport: groupCommands.map((command) => `${command.verb}-${command.noun}`),
    });
  }
  return modules;
}
