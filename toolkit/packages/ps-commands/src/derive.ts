/**
 * `derivePsCommands`: groups an already-enriched `IR` (post `create-ps-commands`)
 * into the final `PsCommand[]`. Plain function, not a pipeline stage — see
 * ps-cmdlet-schema's design.md Decision 8 for why grouping can't happen
 * inside `create-ps-commands` itself.
 *
 * Operations under the same `id.namespace` sharing a resolved verb+noun
 * merge into one `PsCommand` with one `PsParameterSet` per source operation
 * (real precedent: `GetResourceGroupCmdlet`'s `"Get"`/`"List"` parameter
 * sets). Operations `create-ps-commands` hasn't enriched yet (no resolved
 * `verb`/`noun`) are skipped.
 */

import {
  idEquals,
  type IR,
  type IROperation,
  type IRId,
  type IRParameter,
  type IRProperty,
  type IRTypeRef,
} from "@azure-client-engine/ir";
import type { PsCommand, PsFormatView, PsModule, PsParameterSet } from "./schema.js";

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

/** Grouping key: same module + namespace + resolved verb + resolved (bare) noun + subject prefix. */
function groupKey(operation: IROperation): string | undefined {
  const ps = operation.rendering?.powershell;
  if (ps?.verb === undefined || ps.noun === undefined) return undefined;
  return [operation.id.module, ...operation.id.namespace, ps.verb, ps.noun, ps.subjectPrefix ?? ""].join(
    "\u0000",
  );
}

/**
 * One source operation's parameter set(s). Single-operation variant fan-out
 * (design.md Decision 5b) is limited to `base` (whole body object) and
 * `Expanded` (body properties flattened, reusing IR's existing `FlattenHint`
 * rather than a new PS-specific heuristic — Decision 6). `ViaIdentity` and
 * `ViaJsonString`/`ViaJsonFilePath` are explicitly not implemented.
 */
function parameterSetsFor(operation: IROperation): PsParameterSet[] {
  const baseName = operation.rendering!.powershell!.parameterSetName ?? operation.id.name;
  const sets: PsParameterSet[] = [{ name: baseName, sourceOperationId: operation.id }];

  if (operation.requestBody?.rendering?.flatten?.into === "self") {
    sets.push({ name: `${baseName}Expanded`, sourceOperationId: operation.id });
  }

  return sets;
}

/** Every path/query/header parameter on an operation — the ones PS binds directly. */
function allParametersOf(operation: IROperation): IRParameter[] {
  return [...operation.pathParameters, ...operation.queryParameters, ...operation.headerParameters];
}

/** A parameter's effective mandatory/position/helpMessage for one parameter set. */
export type ResolvedParameterHint = {
  mandatory?: boolean | undefined;
  position?: number | undefined;
  helpMessage?: string | undefined;
};

/**
 * Resolves a parameter's effective `mandatory`/`position`/`helpMessage` for one
 * `PsParameterSet`, checking `parameterSets[setName]` first and falling back to
 * the flat value — matching the real `System.Management.Automation.
 * ParameterSetMetadata` structure, where the same parameter can carry different
 * values per set it belongs to. Called by a future generator per `PsParameterSet`
 * it emits a binding for; `PsParameterSet` itself stays bare (see schema.ts) since
 * this is parameter-level detail, not command-grouping detail.
 */
export function resolveParameterHintForSet(parameter: IRParameter, setName: string): ResolvedParameterHint {
  const ps = parameter.rendering?.powershell;
  const override = ps?.parameterSets?.[setName];
  return {
    mandatory: override?.mandatory ?? ps?.mandatory,
    position: override?.position ?? ps?.position,
    helpMessage: override?.helpMessage ?? ps?.helpMessage,
  };
}

/**
 * Mirrors `CmdletCommonMetadataAttribute.DefaultParameterSetName`. Two real
 * precedents, checked directly against the generated ecosystem (design.md's
 * Command-level section):
 * - Merging distinct source operations (e.g. `get`+`list`): the one with
 *   fewer required parameters is more general and wins — real precedent
 *   `GetResourceGroupCmdlet`'s `DefaultParameterSetName = "List"`, not `"Get"`.
 * - A single source operation fanned into `base`+`Expanded` variants:
 *   `Expanded` wins — real precedent, e.g. generated modules commonly set
 *   `DefaultParameterSetName='CreateExpanded'`, not the base/whole-body set.
 */
function defaultParameterSetNameFor(
  operations: readonly IROperation[],
  parameterSets: readonly PsParameterSet[],
): string {
  if (operations.length > 1) {
    const requiredCount = (operation: IROperation): number =>
      allParametersOf(operation).filter((p) => !p.optional).length;
    const mostGeneral = operations.reduce((best, operation) =>
      requiredCount(operation) < requiredCount(best) ? operation : best,
    );
    return mostGeneral.rendering!.powershell!.parameterSetName ?? mostGeneral.id.name;
  }

  const expanded = parameterSets.find((set) => set.name.endsWith("Expanded"));
  return expanded?.name ?? parameterSets[0]!.name;
}

export function derivePsCommands(ir: IR): PsCommand[] {
  const groups = new Map<string, IROperation[]>();

  for (const operation of ir.operations) {
    const key = groupKey(operation);
    if (key === undefined) continue; // not yet enriched by create-ps-commands — nothing to derive
    const members = groups.get(key);
    if (members) members.push(operation);
    else groups.set(key, [operation]);
  }

  const commands: PsCommand[] = [];
  for (const operations of groups.values()) {
    const first = operations[0]!;
    const ps = first.rendering!.powershell!;

    const parameterSets: PsParameterSet[] = operations.flatMap((operation) => parameterSetsFor(operation));

    commands.push({
      verb: ps.verb!,
      noun: (ps.subjectPrefix ?? "") + ps.noun!,
      subjectPrefix: ps.subjectPrefix,
      supportsShouldProcess: ps.confirmImpact !== undefined,
      confirmImpact: ps.confirmImpact,
      outputTypes: outputTypeIdsFor(operations),
      parameterSets,
      defaultParameterSetName: defaultParameterSetNameFor(operations, parameterSets),
      sourceOperationIds: operations.map((operation) => operation.id),
      alias: ps.alias,
      hidden: ps.hidden,
      completer: ps.completer,
      summary: ps.summary,
      description: ps.description,
      examples: ps.examples,
      lifecycleTag: ps.lifecycleTag,
      httpPath: ps.httpPath,
      supportsPaging: ps.supportsPaging,
      helpUri: ps.helpUri,
    });
  }

  return commands;
}

/**
 * `deriveModules`: groups `derivePsCommands`'s output by source into the
 * final `PsModule[]`. Plain function, not a pipeline stage — same rationale
 * as `derivePsCommands` (design.md Decision 8). Assumes every operation
 * merged into one `PsCommand` shares a single source — true for any real
 * input, since merging groups by `id.module`, which "doubles as" a source's
 * id (see `IRSource`'s docstring); only the first source operation is
 * actually looked up.
 */
export function deriveModules(ir: IR): PsModule[] {
  const commands = derivePsCommands(ir);
  const groups = new Map<string, PsCommand[]>();

  for (const command of commands) {
    const firstOperationId = command.sourceOperationIds[0];
    const operation = ir.operations.find((op) => idEquals(op.id, firstOperationId!));
    if (operation === undefined) continue; // shouldn't happen for real IR; defensive skip

    const members = groups.get(operation.source);
    if (members) members.push(command);
    else groups.set(operation.source, [command]);
  }

  const modules: PsModule[] = [];
  for (const [sourceId, groupCommands] of groups) {
    const source = ir.sources.find((s) => s.id === sourceId);
    modules.push({
      sourceId,
      description: source?.rendering?.powershell?.description,
      cmdletsToExport: groupCommands.map((command) => `${command.verb}-${command.noun}`),
    });
  }

  return modules;
}

/** Mirrors the real generator's `IsPsSimple()` check: scalar types only, no nested object/array/union. */
function isSimpleScalar(property: IRProperty): boolean {
  return property.typeKind === "primitive" || property.typeKind === "enum";
}

/** Any override field present forces inclusion, mirroring the real `pf.FormatTable != null` check. */
function hasFormatOverride(property: IRProperty): boolean {
  const format = property.rendering?.powershell;
  return format?.formatIndex !== undefined || format?.formatLabel !== undefined || format?.formatWidth !== undefined;
}

function isEligibleForFormat(property: IRProperty): boolean {
  if (property.rendering?.powershell?.excludeFromFormat) return false;
  return hasFormatOverride(property) || isSimpleScalar(property);
}

/** Inherited properties sort before owned ones — `model.properties` denormalizes inheritance, so `inheritedFrom` is already known. */
function originRank(property: IRProperty): 0 | 1 {
  return property.inheritedFrom !== undefined ? 0 : 1;
}

/**
 * Default column order: explicit `formatIndex` wins outright; otherwise
 * inherited properties sort before owned ones (mirrors the real generator's
 * `Origin` ordering), then declaration order.
 */
function compareForFormat(a: IRProperty, b: IRProperty, declarationIndex: Map<IRProperty, number>): number {
  const aIndex = a.rendering?.powershell?.formatIndex;
  const bIndex = b.rendering?.powershell?.formatIndex;
  if (aIndex !== undefined && bIndex !== undefined) return aIndex - bIndex;
  if (aIndex !== undefined) return -1;
  if (bIndex !== undefined) return 1;

  const originDelta = originRank(a) - originRank(b);
  if (originDelta !== 0) return originDelta;

  return declarationIndex.get(a)! - declarationIndex.get(b)!;
}

/**
 * `deriveFormatViews`: computes one `Format.ps1xml`-equivalent `PsFormatView`
 * per `IRModel` that has at least one eligible column. Plain function, not a
 * pipeline stage — same rationale as `derivePsCommands`/`deriveModules`.
 * Scoped to every `IRModel` in the IR (not just operation output types) —
 * real precedent (`Az.Astro.format.ps1xml`) shows request-only/pipeline-
 * identity model types get views too, not only response bodies.
 */
export function deriveFormatViews(ir: IR): PsFormatView[] {
  const views: PsFormatView[] = [];

  for (const model of ir.models) {
    const eligible = model.properties.filter((property) => isEligibleForFormat(property));
    if (eligible.length === 0) continue; // matches the real generator: no view for zero qualifying columns

    const declarationIndex = new Map(model.properties.map((property, index) => [property, index] as const));
    const ordered = [...eligible].sort((a, b) => compareForFormat(a, b, declarationIndex));

    views.push({
      sourceModelId: model.id,
      columns: ordered.map((property) => ({
        propertyName: property.name,
        label: property.rendering?.powershell?.formatLabel,
        width: property.rendering?.powershell?.formatWidth,
      })),
    });
  }

  return views;
}
