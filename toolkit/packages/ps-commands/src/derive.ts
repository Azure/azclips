/**
 * Groups an already-enriched `IR` (post `create-ps-commands`) into the final `PsCommand[]`.
 * Plain function, not a pipeline stage (design.md Decision 8). Operations under the same
 * `id.namespace` sharing a resolved verb+noun merge into one `PsCommand` with one
 * `PsParameterSet` per source operation (real precedent: `GetResourceGroupCmdlet`'s
 * `"Get"`/`"List"` sets). Operations `create-ps-commands` hasn't enriched are skipped.
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
import type { PsCommand, PsModule, PsParameterSet } from "./schema.js";

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
 * One source operation's parameter set(s). Fan-out (design.md Decision 5b/6) is limited to
 * `base` and `Expanded` (flattened body, reusing IR's `FlattenHint`); `ViaIdentity` and
 * `ViaJsonString`/`ViaJsonFilePath` are not implemented.
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
 * Resolves a parameter's effective `mandatory`/`position`/`helpMessage` for one `PsParameterSet`,
 * checking `parameterSets[setName]` first and falling back to the flat value — mirrors real
 * `ParameterSetMetadata`, where the same parameter can carry different values per set.
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
 * Mirrors `CmdletCommonMetadataAttribute.DefaultParameterSetName` (design.md Command-level
 * section). Merged operations: fewest required parameters wins (e.g. `GetResourceGroupCmdlet`'s
 * `"List"` over `"Get"`). Single operation fanned into `base`+`Expanded`: `Expanded` wins.
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
 * Groups `derivePsCommands`'s output by source into the final `PsModule[]`. Plain function,
 * not a pipeline stage (design.md Decision 8). `PsCommand` doesn't store a source reference
 * directly, so each command's source is recovered by looking up its first source operation's
 * own `IROperation.source` (already the source's id) via a pre-built index.
 */
export function deriveModules(ir: IR): PsModule[] {
  const commands = derivePsCommands(ir);
  const operationsById = new Map(ir.operations.map((op) => [serializeId(op.id), op]));
  const groups = new Map<string, PsCommand[]>();

  for (const command of commands) {
    const firstOperationId = command.sourceOperationIds[0];
    const operation = operationsById.get(serializeId(firstOperationId!));
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
