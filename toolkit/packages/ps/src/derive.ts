/**
 * Derives `PSCommandAttribute[]`/`PSParameterAttribute[]`/`PSModuleAttribute[]` directly from a
 * plain `IR` document — a single pass, with no intermediate mutation of IR nodes (PS vocabulary
 * doesn't live on IR; there's nothing to mutate). Operations sharing a resolved verb+noun merge
 * into one `PSCommandAttribute` with one parameter set per source operation (real precedent:
 * `GetResourceGroupCmdlet`'s `Get`/`List` sets).
 *
 * Not yet implemented: directive-based authoring overrides, and the `Expanded` (flattened-body)
 * parameter-set fan-out. `PSModuleAttribute.description` has no IR-derivable source today
 * (`IRSource` carries no rendering/hints) and is always left `undefined`; a future authoring
 * mechanism would need to supply it.
 */

import {
  idEquals,
  serializeId,
  type IR,
  type IRModel,
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
function firstResponseName(operation: IROperation, models: ModelIndex): string | undefined {
  for (const type of effectiveResponseTypes(operation, models)) {
    const name = modelNameOfTypeRef(type);
    if (name !== undefined) return name;
  }
  return undefined;
}

type ModelIndex = ReadonlyMap<string, IRModel>;

function indexModels(ir: IR): ModelIndex {
  return new Map(ir.models.map((model) => [serializeId(model.id), model]));
}

/**
 * Element of an ARM collection envelope (a required `value` array plus optional envelope fields
 * such as `nextLink`), else undefined. Keyed on the `value` name rather than "holds one array"
 * because ordinary models carry a single array too (`NetworkRuleSet.virtualNetworkRules`).
 */
function collectionElementType(models: ModelIndex, type: IRTypeRef): IRTypeRef | undefined {
  if (type.kind !== "model") return undefined;
  const model = models.get(serializeId(type.id));
  if (model === undefined) return undefined;

  const value = model.properties.find((p) => p.name.toLowerCase() === "value");
  if (value === undefined || value.typeKind !== "collection") return undefined;
  if (model.properties.some((p) => p !== value && !p.optional)) return undefined;

  return value.type.element;
}

/** Response types with pageable/collection envelopes unwrapped to the item they carry. */
function effectiveResponseTypes(operation: IROperation, models: ModelIndex): IRTypeRef[] {
  if (operation.semantics?.kind === "pageable") return [operation.semantics.itemType];

  const types: IRTypeRef[] = [];
  for (const response of operation.responses) {
    if (response.isError || response.type === undefined) continue;
    types.push(collectionElementType(models, response.type) ?? response.type);
  }
  return types;
}

/** True when the operation yields a collection rather than a single resource. */
function isCollectionOperation(operation: IROperation, models: ModelIndex): boolean {
  if (operation.semantics?.kind === "pageable") return true;
  return operation.responses.some(
    (r) => !r.isError && r.type !== undefined && collectionElementType(models, r.type) !== undefined,
  );
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

function inferOperation(operation: IROperation, noun: string): Inferred {
  return {
    verb: resolveVerb(operation.id.name, operation.method),
    noun,
    confirmImpact: isMutatingMethod(operation.method) ? "Medium" : undefined,
    hidden: operation.access === "internal" ? true : undefined,
    supportsPaging: operation.semantics?.kind === "pageable" ? true : undefined,
  };
}

/** Resource identity: everything about an operation's id except the action word. */
function resourceKey(operation: IROperation): string {
  // `source` is kept distinct from `id.module` even though the TypeSpec emitter sets both from
  // the same option: merging across sources would misattribute the command in `deriveModules`.
  return [operation.source, operation.id.module, ...operation.id.namespace].join("\u0000");
}

/**
 * One noun per resource, so operations on the same resource can never disagree. A DELETE carries
 * no response body and would otherwise fall back to the namespace segment while its sibling GET
 * used the response model -- real ElasticSan case: `Remove-AzVolumeSnapshot` next to
 * `Get-AzSnapshot` for the same `VolumeSnapshots` resource.
 */
function resourceNounsFor(ir: IR, models: ModelIndex): Map<string, string> {
  const byResource = new Map<string, IROperation[]>();
  for (const operation of ir.operations) {
    const key = resourceKey(operation);
    const bucket = byResource.get(key);
    if (bucket) bucket.push(operation);
    else byResource.set(key, [operation]);
  }

  const nouns = new Map<string, string>();
  for (const [key, operations] of byResource) {
    nouns.set(key, resourceNoun(operations, models));
  }
  return nouns;
}

function resourceNoun(operations: readonly IROperation[], models: ModelIndex): string {
  // A single-resource read names the resource most directly; a collection read only names it
  // once unwrapped; anything else is weaker evidence than the declaring namespace.
  for (const operation of operations) {
    if (operation.method !== "GET" || isCollectionOperation(operation, models)) continue;
    const name = firstResponseName(operation, models);
    if (name !== undefined) return name;
  }
  for (const operation of operations) {
    const name = firstResponseName(operation, models);
    if (name !== undefined) return name;
  }
  return singularize(resourceNameCandidate(operations[0]!));
}

/** Grouping key: same resource + resolved verb + resolved (bare) noun. */
function groupKey(operation: IROperation, inferred: Inferred): string {
  return [resourceKey(operation), inferred.verb, inferred.noun].join("\u0000");
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

function outputTypeIdsFor(operations: readonly IROperation[], models: ModelIndex): IRId[] {
  const ids: IRId[] = [];
  for (const operation of operations) {
    for (const type of effectiveResponseTypes(operation, models)) {
      const id = extractTypeRefId(type);
      if (id !== undefined && !ids.some((existing) => idEquals(existing, id))) ids.push(id);
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
    ).length + (operation.requestBody === undefined ? 0 : 1);
  const mostGeneral = operations.reduce((best, operation) =>
    requiredCount(operation) < requiredCount(best) ? operation : best,
  );
  const set = parameterSets.find((s) => idEquals(s.sourceOperationId, mostGeneral.id));
  return set?.name ?? parameterSets[0]!.name;
}

// ─── Main entry point ────────────────────────────────────────────────────

export function deriveAttributes(ir: IR, options: DeriveAttributesOptions = {}): DerivedAttributes {
  const models = indexModels(ir);
  const resourceNouns = resourceNounsFor(ir, models);
  const groups = new Map<string, { operations: IROperation[]; inferred: Inferred }>();

  for (const operation of ir.operations) {
    const inferred = inferOperation(operation, resourceNouns.get(resourceKey(operation))!);
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
      outputTypes: outputTypeIdsFor(operations, models),
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
