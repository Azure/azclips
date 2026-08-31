/**
 * Derives `PSCommandAttribute[]`/`PSParameterAttribute[]`/`PSModuleAttribute[]` directly from a
 * plain `IR` document — a single pass, with no intermediate mutation of IR nodes (PS vocabulary
 * doesn't live on IR; there's nothing to mutate). Operations sharing a resolved verb+noun merge
 * into one `PSCommandAttribute` with one parameter set per source operation (real precedent:
 * `GetResourceGroupCmdlet`'s `Get`/`List` sets).
 *
 * Operations carrying a request body derive an `Expanded` parameter set, whose body is flattened
 * into one parameter per settable leaf. The non-expanded companion set (the body as a single
 * object parameter) is not derived: its parameter name comes from the spec's body parameter name,
 * which IR does not carry, and shipped modules name it 15 different ways.
 *
 * Not yet implemented: the `ViaIdentity` parameter-set fan-out. `PSModuleAttribute.description`
 * has no IR-derivable source today (`IRSource` carries no rendering/hints) and is always left
 * `undefined`; a future authoring mechanism would need to supply it.
 */

import {
  idEquals,
  serializeId,
  type IR,
  type IRModel,
  type IROperation,
  type IRId,
  type IRParameter,
  type IRProperty,
  type IRTypeRef,
} from "@azure-client-engine/ir";
import { inferCommandNames, deconstruct, pascalCase, singularize, deduplicateNoun } from "./naming.js";
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
  modules: PSModuleAttribute[];
};

// ─── Verb/noun inference helpers ─────────────────────────────────────────

/** PUT/POST/PATCH/DELETE mutate; GET does not. */
function isMutatingMethod(method: IROperation["method"]): boolean {
  return method === "PUT" || method === "POST" || method === "PATCH" || method === "DELETE";
}

/**
 * The operation's declaring group -- AutoRest's operation group, i.e. the terminal namespace
 * segment. A root client contributes its own generated name there (`Microsoft.ElasticSan` ->
 * `ElasticSanClient`), which names a client rather than a resource, so fall back to the
 * namespace it sits in. Matching the exact `<parent>Client` shape keeps real resources whose
 * name simply ends in "Client" (e.g. `VpnClients`) untouched.
 */
function groupOf(operation: IROperation): string {
  const segments = operation.id.namespace;
  if (segments.length === 0) return operation.id.name;

  const terminal = segments[segments.length - 1]!;
  const parent = segments[segments.length - 2];
  return parent !== undefined && terminal === `${parent}Client` ? parent : terminal;
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
  if (value === undefined || value.typeKind !== "collection" || value.optional) return undefined;
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

type Inferred = {
  verb: string;
  subject: string;
  variant: string;
  confirmImpact: "Medium" | undefined;
  hidden: boolean | undefined;
};

/** Usually one name; an `X`-or-`Y` action word yields one per branch (createOrUpdate -> New + Update). */
function inferOperation(operation: IROperation): Inferred[] {
  return inferCommandNames(groupOf(operation), operation.id.name).map((name) => ({
    verb: name.verb,
    subject: name.subject,
    variant: name.variant,
    confirmImpact: isMutatingMethod(operation.method) ? "Medium" : undefined,
    hidden: operation.access === "internal" ? true : undefined,
  }));
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
 * Content-negotiation headers are protocol plumbing rather than user input: no generated Az cmdlet
 * exposes them, and autorest.powershell drops them because modelerfour tags them
 * `modelerfour:synthesized`. The IR carries no equivalent flag, so match on the wire name. Every
 * other header stays -- `x-ms-force-delete` and friends really do ship as cmdlet parameters.
 */
const PROTOCOL_HEADER_WIRE_NAMES: ReadonlySet<string> = new Set(["accept", "content-type"]);

function userFacingParameters(operation: IROperation): IRParameter[] {
  return [
    ...operation.pathParameters,
    ...operation.queryParameters,
    ...operation.headerParameters.filter(
      (p) => !PROTOCOL_HEADER_WIRE_NAMES.has(p.wireName.toLowerCase()),
    ),
  ];
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
    userFacingParameters(operation).filter((p) => !p.optional).length +
    (operation.requestBody === undefined ? 0 : 1);
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
function parameterSetNamesFor(entries: readonly CommandEntry[], models: ModelIndex): string[] {
  const preferred = entries.map((entry) =>
    entry.inferred.variant === ""
      ? pascalCase(deconstruct(entry.operation.id.name))
      : entry.inferred.variant,
  );

  return preferred.map((name, index) => {
    const resolved = preferred.some((other, otherIndex) => other === name && otherIndex !== index)
      ? pascalCase(deconstruct(entries[index]!.operation.id.name))
      : name;
    const expanded = expandableBody(models, entries[index]!.operation) !== undefined;
    return expanded ? `${resolved}Expanded` : resolved;
  });
}

// ─── Request-body flattening ─────────────────────────────────────────────

/**
 * The body model an `Expanded` set flattens, or undefined when the body cannot be expanded.
 * autorest.powershell also expands dictionary-typed bodies; a dictionary has no declared members
 * to hoist, and no operation in the corpus has one, so only models are handled here.
 */
function expandableBody(models: ModelIndex, operation: IROperation): IRModel | undefined {
  const body = operation.requestBody;
  if (body === undefined || body.rendering?.flatten?.into === "never") return undefined;
  if (body.type.kind !== "model") return undefined;
  return models.get(serializeId(body.type.id));
}

/**
 * Settable in a request: `visibility` absent means every lifecycle phase, so only an explicit
 * list without `create`/`update` marks a property as server-assigned.
 */
function isSettable(property: IRProperty): boolean {
  return (
    property.visibility === undefined ||
    property.visibility.includes("create") ||
    property.visibility.includes("update")
  );
}

type BodyLeaf = { name: string; property: IRProperty; nested: boolean };

/**
 * What an object property contributes to the names of the members it hoists: its own name,
 * singularized word by word (`scaleUpProperties` -> `ScaleUpProperty`). The ARM `properties`
 * envelope contributes nothing, which is why the real cmdlet takes `-SizeGiB`.
 */
function inlinePrefix(property: IRProperty): string {
  const flatten = property.typeKind === "object" ? property.rendering?.flatten : undefined;
  if (flatten?.into === "self") return "";
  const wireName = property.wireName.toLowerCase();
  if (wireName === "properties" || wireName === "error") return "";
  return pascalCase(deconstruct(property.name).map(singularize));
}

/**
 * One leaf per settable scalar reachable through the body. Flattening is bottom-up and only the
 * immediately containing property contributes a prefix: a leaf that has already been hoisted once
 * travels up unchanged, so `properties.autoScaleProperties.scaleUpProperties.unusedSizeTiB`
 * arrives as `ScaleUpPropertyUnusedSizeTiB` rather than accumulating every segment.
 */
function bodyLeaves(
  models: ModelIndex,
  model: IRModel,
  seen: ReadonlySet<string> = new Set(),
): BodyLeaf[] {
  const modelKey = serializeId(model.id);
  if (seen.has(modelKey)) return [];
  const visited = new Set([...seen, modelKey]);

  const leaves: BodyLeaf[] = [];
  for (const property of model.properties) {
    if (!isSettable(property)) continue;

    const flatten = property.typeKind === "object" ? property.rendering?.flatten : undefined;
    // A dictionary-typed property has no members to hoist, so it stays one parameter (`-Tag`).
    const target =
      property.typeKind === "object" && flatten?.into !== "never" && property.type.kind === "model"
        ? models.get(serializeId(property.type.id))
        : undefined;

    if (target === undefined) {
      leaves.push({ name: pascalCase(deconstruct(property.name)), property, nested: false });
      continue;
    }

    const prefix = inlinePrefix(property);
    for (const leaf of bodyLeaves(models, target, visited)) {
      leaves.push({
        name: leaf.nested ? leaf.name : prefix + leaf.name,
        property: leaf.property,
        nested: true,
      });
    }
  }
  return leaves;
}

type CommandEntry = { operation: IROperation; inferred: Inferred };

const helpOf = (parameter: IRParameter): string | undefined => parameter.summary ?? parameter.docs;

/** PowerShell parameters are PascalCase and singular; `tags` becomes `Tag`. */
const normalizeParameterName = (name: string): string => singularize(pascalCase(deconstruct(name)));

/**
 * Drops a redundant subject prefix from a path parameter, as `ps-namer-v2.ts` does via
 * `removeProhibitedPrefix`: `Get-AzElasticSan -ElasticSanName` reads better as `-Name`. Refuses
 * a result under two characters, or one that would collide with another parameter.
 */
function withoutSubjectPrefix(name: string, subject: string, otherNames: readonly string[]): string {
  if (subject === "" || !name.toLowerCase().startsWith(subject.toLowerCase())) return name;

  const stripped = name.slice(subject.length);
  if (stripped.length < 2) return name;

  const candidate = stripped[0]!.toUpperCase() + stripped.slice(1);
  return otherNames.includes(candidate) ? name : candidate;
}

/**
 * One `PSParameterAttribute` per distinct parameter name on the command, mirroring C# where a
 * cmdlet has one property however many sets use it. A parameter present in every set with the
 * same facets states them directly; one that varies, or covers only some sets, carries per-set
 * overrides instead. Where sets disagree on the IR parameter, `sourceParameterId` keeps the first
 * -- the others stay reachable through the set's own `sourceOperationId`.
 */
type Occurrence = { setName: string; optional: boolean; help: string | undefined; sourceId: IRId };

/** Collapses one name's occurrences into the single attribute the cmdlet exposes. */
function attributeFor(
  name: string,
  alias: string | undefined,
  found: readonly Occurrence[],
  setCount: number,
): PSParameterAttribute {
  const first = found[0]!;
  const base: PSParameterAttribute = {
    name,
    ...(alias === undefined ? {} : { alias: [alias] }),
    mandatory: !first.optional,
    helpMessage: first.help,
    sourceParameterId: first.sourceId,
  };

  const uniform = found.every((o) => o.optional === first.optional && o.help === first.help);
  if (found.length === setCount && uniform) return base;
  if (found.length === 1) return { ...base, parameterSetName: first.setName };
  return {
    ...base,
    parameterSets: Object.fromEntries(
      found.map((o) => [o.setName, { mandatory: !o.optional, helpMessage: o.help }]),
    ),
  };
}

function parametersFor(
  entries: readonly CommandEntry[],
  parameterSets: readonly PSParameterSetAttribute[],
  subject: string,
  models: ModelIndex,
): PSParameterAttribute[] {
  const occurrences = new Map<string, { setName: string; parameter: IRParameter; isPath: boolean }[]>();

  entries.forEach((entry, index) => {
    const setName = parameterSets[index]!.name;
    const pathNames = new Set(entry.operation.pathParameters.map((p) => p.name));
    for (const parameter of userFacingParameters(entry.operation)) {
      const name = normalizeParameterName(parameter.name);
      const occurrence = { setName, parameter, isPath: pathNames.has(parameter.name) };
      const bucket = occurrences.get(name);
      if (bucket) bucket.push(occurrence);
      else occurrences.set(name, [occurrence]);
    }
  });

  const allNames = [...occurrences.keys()];
  const parameters: PSParameterAttribute[] = [];
  const taken = new Set<string>();

  for (const [name, found] of occurrences) {
    // Only path parameters are shortened; a query or header name carries no such redundancy.
    const shortened = found.some((o) => o.isPath)
      ? withoutSubjectPrefix(
          name,
          subject,
          allNames.filter((other) => other !== name),
        )
      : name;

    taken.add(shortened);
    parameters.push(
      attributeFor(
        shortened,
        shortened === name ? undefined : name,
        found.map((o) => ({
          setName: o.setName,
          optional: o.parameter.optional,
          help: helpOf(o.parameter),
          sourceId: o.parameter.id,
        })),
        parameterSets.length,
      ),
    );
  }

  const bodyOccurrences = new Map<string, Occurrence[]>();
  entries.forEach((entry, index) => {
    const body = expandableBody(models, entry.operation);
    if (body === undefined) return;
    for (const leaf of bodyLeaves(models, body)) {
      const name = singularize(leaf.name);
      // A body property that restates a path parameter is already covered by it.
      if (taken.has(name)) continue;
      const occurrence: Occurrence = {
        setName: parameterSets[index]!.name,
        optional: leaf.property.optional,
        help: leaf.property.summary ?? leaf.property.docs,
        sourceId: leaf.property.id,
      };
      const bucket = bodyOccurrences.get(name);
      if (bucket) bucket.push(occurrence);
      else bodyOccurrences.set(name, [occurrence]);
    }
  });

  for (const [name, found] of bodyOccurrences) {
    parameters.push(attributeFor(name, undefined, found, parameterSets.length));
  }
  return parameters;
}

// ─── Main entry point ────────────────────────────────────────────────────

export function deriveAttributes(ir: IR, options: DeriveAttributesOptions = {}): DerivedAttributes {
  const models = indexModels(ir);
  const groups = new Map<string, CommandEntry[]>();

  for (const operation of ir.operations) {
    for (const inferred of inferOperation(operation)) {
      const key = groupKey(operation, inferred);
      const group = groups.get(key);
      if (group) group.push({ operation, inferred });
      else groups.set(key, [{ operation, inferred }]);
    }
  }

  const commands: PSCommandAttribute[] = [];
  const commandSources: { command: PSCommandAttribute; source: string }[] = [];

  for (const entries of groups.values()) {
    const operations = entries.map((entry) => entry.operation);
    const inferred = entries[0]!.inferred;
    const setNames = parameterSetNamesFor(entries, models);
    const parameterSets: PSParameterSetAttribute[] = entries.map((entry, index) => ({
      name: setNames[index]!,
      sourceOperationId: entry.operation.id,
    }));

    // The module prefix sits outside the dedup: AutoRest reconciles only subject-prefix against
    // subject, so "Az" + ("ElasticSan" + "ElasticSan") collapses to Get-AzElasticSan.
    const deduped = deduplicateNoun(options.subjectPrefix ?? "", inferred.subject);

    commands.push({
      verb: inferred.verb,
      noun: (options.modulePrefix ?? "") + deduped.subjectPrefix + deduped.subject,
      subject: deduped.subject,
      subjectPrefix: deduped.subjectPrefix === "" ? undefined : deduped.subjectPrefix,
      supportsShouldProcess: inferred.confirmImpact !== undefined,
      confirmImpact: inferred.confirmImpact,
      hidden: inferred.hidden,
      summary: operations[0]!.summary,
      description: operations[0]!.docs,
      // Only meaningful for a single-operation command: a merged one spans several endpoints,
      // which one path/apiVersion pair cannot represent. Per-set paths live on the operations.
      httpPath:
        operations.length === 1
          ? { path: operations[0]!.pathTemplate, apiVersion: operations[0]!.apiVersion.value }
          : undefined,
      // `supportsPaging` mirrors the cmdlet's SupportsPaging attribute, which autorest.powershell
      // only sets when a module opts in via the `clientside-pagination` directive. Pageability
      // alone makes the cmdlet follow nextLink transparently; it does not surface -First/-Skip.
      supportsPaging: undefined,
      outputTypes: outputTypeIdsFor(operations, models),
      parameterSets,
      parameters: parametersFor(entries, parameterSets, deduped.subject, models),
      defaultParameterSetName: defaultParameterSetNameFor(operations, parameterSets),
      sourceOperationIds: operations.map((operation) => operation.id),
    });
    commandSources.push({ command: commands[commands.length - 1]!, source: operations[0]!.source });
  }

  const modules = deriveModules(ir, commandSources);

  return { commands, modules };
}

/**
 * Groups derived commands by the source of the operations they came from. The source is carried
 * alongside each command rather than recovered by id lookup, because `IRId` is only unique within
 * a source -- two sources can declare the same id and a lookup would attribute one to the other.
 */
function deriveModules(ir: IR, commands: readonly { command: PSCommandAttribute; source: string }[]): PSModuleAttribute[] {
  const bySource = new Map<string, PSCommandAttribute[]>();

  for (const { command, source } of commands) {
    const members = bySource.get(source);
    if (members) members.push(command);
    else bySource.set(source, [command]);
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
