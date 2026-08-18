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

import { idEquals, type IR, type IROperation, type IRId, type IRTypeRef } from "@azure-client-engine/ir";
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
      sourceOperationIds: operations.map((operation) => operation.id),
    });
  }

  return commands;
}
