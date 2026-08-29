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
import { inferCommandName, deconstruct, pascalCase, deduplicateNoun } from "./naming.js";
import type {
  PSCommandAttribute,
  PSModuleAttribute,
  PSParameterAttribute,
  PSParameterSetAttribute,
} from "./attributes.js";

export type DeriveAttributesOptions = {
  /** Module-level prefix applied to every noun, e.g. "Az". Default: undefined (none). */
  modulePrefix?: string | undefined;
  /** Authored service prefix, e.g. "ElasticSan" -> "AzElasticSanVolume". Default: undefined (none). */
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

/** The operation's declaring group — AutoRest's operation group, i.e. the terminal namespace segment. */
function groupOf(operation: IROperation): string {
  const segments = operation.id.namespace;
  return segments.length > 0 ? segments[segments.length - 1]! : operation.id.name;
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

type Inferred = {
  verb: string;
  subject: string;
  variant: string;
  confirmImpact: "Medium" | undefined;
  hidden: boolean | undefined;
  supportsPaging: boolean | undefined;
};

function inferOperation(operation: IROperation): Inferred {
  const name = inferCommandName(groupOf(operation), operation.id.name, operation.method);
  return {
    verb: name.verb,
    subject: name.subject,
    variant: name.variant,
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

/** Grouping key: same resource + resolved verb + resolved (bare) subject. */
function groupKey(operation: IROperation, inferred: Inferred): string {
  return [resourceKey(operation), inferred.verb, inferred.subject].join("\u0000");
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

/**
 * Parameter-set name per operation. A trailing preposition already discriminates the set
 * (`listByResourceGroup` -> `ResourceGroup`); the full action word is the fallback, and also the
 * tie-break when two operations in one command would otherwise collide.
 */
function parameterSetNamesFor(operations: readonly IROperation[]): string[] {
  const preferred = operations.map((operation) => {
    const name = inferCommandName(groupOf(operation), operation.id.name, operation.method);
    return name.variant === "" ? pascalCase(deconstruct(operation.id.name)) : name.variant;
  });

  return preferred.map((name, index) =>
    preferred.some((other, otherIndex) => other === name && otherIndex !== index)
      ? pascalCase(deconstruct(operations[index]!.id.name))
      : name,
  );
}

// ─── Main entry point ────────────────────────────────────────────────────

export function deriveAttributes(ir: IR, options: DeriveAttributesOptions = {}): DerivedAttributes {
  const models = indexModels(ir);
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
    const setNames = parameterSetNamesFor(operations);
    const parameterSets: PSParameterSetAttribute[] = operations.map((operation, index) => ({
      name: setNames[index]!,
      sourceOperationId: operation.id,
    }));

    // The module prefix sits outside the dedup: AutoRest reconciles only subject-prefix against
    // subject, so "Az" + ("ElasticSan" + "ElasticSan") collapses to Get-AzElasticSan.
    const deduped = deduplicateNoun(options.subjectPrefix ?? "", inferred.subject);

    commands.push({
      verb: inferred.verb,
      noun: (options.modulePrefix ?? "") + deduped.subjectPrefix + deduped.subject,
      subjectPrefix: deduped.subjectPrefix === "" ? undefined : deduped.subjectPrefix,
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
