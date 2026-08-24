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
  type IRTypeRef,
} from "@azure-client-engine/ir";
import type { PsCommand, PsParameterSet } from "./schema.js";

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
