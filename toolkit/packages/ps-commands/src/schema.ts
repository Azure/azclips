/**
 * Thin, cross-operation grouping shape (`Command`/`ParameterSet`).
 *
 * Per ps-cmdlet-schema's design.md Decisions 2/8: per-node PS vocabulary
 * (verb, noun, mandatory, help, etc.) lives as `hints.powershell` directly
 * on IR operations/parameters, not here. This package holds only what
 * `hints.powershell` structurally cannot express — the fact that multiple
 * IR operations (e.g. `get` + `list`) merge into one PowerShell command
 * with one parameter set per source operation. See `deriveCommands` in
 * `derive.js` for how this is assembled from an enriched `IR`.
 */

import { z } from "zod";
import { IRIdSchema, ConfirmImpacts, type IRId, type ConfirmImpact } from "@azure-client-engine/ir";

/**
 * One source operation's contribution to a merged `Command`. Deliberately
 * bare — parameter-level detail (mandatory, help, etc.) lives on the
 * referenced operation's own parameters' `hints.powershell`, not here.
 */
export type ParameterSet = {
  /** Source-operation-derived, e.g. "Get", "List". */
  name: string;
  /** The IR operation this parameter set was derived from. */
  sourceOperationId: IRId;
};

export const ParameterSetSchema: z.ZodType<ParameterSet> = z
  .object({
    name: z.string().min(1),
    sourceOperationId: IRIdSchema,
  })
  .strict();

/**
 * A PowerShell command, grouped from one or more IR operations sharing a
 * resolved verb+noun. `verb`/`noun`/`supportsShouldProcess`/`confirmImpact`
 * are copied from the grouped operations' resolved `hints.powershell` at
 * grouping time (see `deriveCommands`), not independently authored here.
 */
export type Command = {
  verb: string;
  noun: string;
  /** Configurable subject prefix, e.g. "Az" in "AzResourceGroup". */
  subjectPrefix?: string | undefined;
  supportsShouldProcess: boolean;
  confirmImpact?: ConfirmImpact | undefined;
  /** By-id references into IR for the command's output type(s). */
  outputTypes: IRId[];
  /** One parameter set per merged source operation. */
  parameterSets: ParameterSet[];
  /** IR operation id(s) this command derives from. */
  sourceOperationIds: IRId[];
};

export const CommandSchema: z.ZodType<Command> = z
  .object({
    verb: z.string().min(1),
    noun: z.string().min(1),
    subjectPrefix: z.string().min(1).optional(),
    supportsShouldProcess: z.boolean(),
    confirmImpact: z.enum(ConfirmImpacts).optional(),
    outputTypes: z.array(IRIdSchema),
    parameterSets: z.array(ParameterSetSchema).min(1),
    sourceOperationIds: z.array(IRIdSchema).min(1),
  })
  .strict();
