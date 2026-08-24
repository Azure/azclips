/**
 * Thin, cross-operation grouping shape (`PsCommand`/`PsParameterSet`). Named
 * with a `Ps` prefix (not bare `Command`) to stay unambiguous alongside
 * azclips's own `CommandMetadata`, which spans both PS and CLI surfaces —
 * this schema is PowerShell-only.
 *
 * Per ps-cmdlet-schema's design.md Decisions 2/8: per-node PS vocabulary
 * (verb, noun, mandatory, help, etc.) lives as `hints.powershell` directly
 * on IR operations/parameters, not here. This package holds only what
 * `hints.powershell` structurally cannot express — the fact that multiple
 * IR operations (e.g. `get` + `list`) merge into one PowerShell command
 * with one parameter set per source operation. See `derivePsCommands` in
 * `derive.js` for how this is assembled from an enriched `IR`.
 */

import { z } from "zod";
import {
  IRIdSchema,
  ConfirmImpacts,
  LifecycleTags,
  CompleterHintSchema,
  type IRId,
  type ConfirmImpact,
  type LifecycleTag,
  type CompleterHint,
} from "@azure-client-engine/ir";

/**
 * One source operation's contribution to a merged `PsCommand`. Deliberately
 * bare — parameter-level detail (mandatory, help, etc.) lives on the
 * referenced operation's own parameters' `hints.powershell`, not here.
 */
export type PsParameterSet = {
  /** Source-operation-derived, e.g. "Get", "List". */
  name: string;
  /** The IR operation this parameter set was derived from. */
  sourceOperationId: IRId;
};

export const PsParameterSetSchema: z.ZodType<PsParameterSet> = z
  .object({
    name: z.string().min(1),
    sourceOperationId: IRIdSchema,
  })
  .strict();

/**
 * A PowerShell command, grouped from one or more IR operations sharing a
 * resolved verb+noun. All fields below are copied from the grouped
 * operations' resolved `hints.powershell` at grouping time (see
 * `derivePsCommands`), not independently authored here.
 */
export type PsCommand = {
  verb: string;
  noun: string;
  /** Configurable subject prefix, e.g. "Az" in "AzResourceGroup". */
  subjectPrefix?: string | undefined;
  supportsShouldProcess: boolean;
  confirmImpact?: ConfirmImpact | undefined;
  /** By-id references into IR for the command's output type(s). */
  outputTypes: IRId[];
  /** One parameter set per merged source operation. */
  parameterSets: PsParameterSet[];
  /**
   * Mirrors `System.Management.Automation.CmdletCommonMetadataAttribute
   * .DefaultParameterSetName`. `derivePsCommands` computes this: the most
   * general merged source operation wins (fewest required parameters —
   * real precedent: `GetResourceGroupCmdlet`'s `"List"` over `"Get"`); for
   * a single source operation fanned into `base`+`Expanded` variants,
   * `Expanded` wins (real precedent: generated modules commonly set e.g.
   * `DefaultParameterSetName='CreateExpanded'`).
   */
  defaultParameterSetName: string;
  /** IR operation id(s) this command derives from. */
  sourceOperationIds: IRId[];
  /** Additional cmdlet aliases. */
  alias?: string[] | undefined;
  /** When true, the command is not exported from the generated module. */
  hidden?: boolean | undefined;
  /** Completer for a command-level dynamic-parameter scenario (rare; usually per-parameter). */
  completer?: CompleterHint | undefined;
  summary?: string | undefined;
  description?: string | undefined;
  examples?: string[] | undefined;
  lifecycleTag?: LifecycleTag | undefined;
  httpPath?: { path: string; apiVersion: string } | undefined;
  supportsPaging?: boolean | undefined;
  helpUri?: string | undefined;
};

export const PsCommandSchema: z.ZodType<PsCommand> = z
  .object({
    verb: z.string().min(1),
    noun: z.string().min(1),
    subjectPrefix: z.string().min(1).optional(),
    supportsShouldProcess: z.boolean(),
    confirmImpact: z.enum(ConfirmImpacts).optional(),
    outputTypes: z.array(IRIdSchema),
    parameterSets: z.array(PsParameterSetSchema).min(1),
    defaultParameterSetName: z.string().min(1),
    sourceOperationIds: z.array(IRIdSchema).min(1),
    alias: z.array(z.string().min(1)).optional(),
    hidden: z.boolean().optional(),
    completer: CompleterHintSchema.optional(),
    summary: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
    examples: z.array(z.string().min(1)).optional(),
    lifecycleTag: z.enum(LifecycleTags).optional(),
    httpPath: z
      .object({ path: z.string().min(1), apiVersion: z.string().min(1) })
      .strict()
      .optional(),
    supportsPaging: z.boolean().optional(),
    helpUri: z.string().min(1).optional(),
  })
  .strict();

/**
 * One generated PowerShell module (one merged `IRSource`). Thin aggregate,
 * same rationale as `PsCommand`: per-source authoring (`description`) lives
 * as `hints.powershell` on `IRSource.rendering`; this only holds what that
 * hint structurally cannot express — the flattened list of every `PsCommand`
 * grouped under this source. See `deriveModules` in `derive.js`.
 */
export type PsModule = {
  /** `IRSource.id` this module derives from. */
  sourceId: string;
  /** Copied from `IRSource.rendering.powershell.description`. */
  description?: string | undefined;
  /** Flattened `verb`-`noun` command names (e.g. `"New-AzResourceGroup"`), one per grouped `PsCommand`. */
  cmdletsToExport: string[];
};

export const PsModuleSchema: z.ZodType<PsModule> = z
  .object({
    sourceId: z.string().min(1),
    description: z.string().min(1).optional(),
    cmdletsToExport: z.array(z.string().min(1)),
  })
  .strict();
