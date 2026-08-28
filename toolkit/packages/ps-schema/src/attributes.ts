/**
 * PowerShell-facing attribute schema: `PSCommandAttribute` / `PSParameterAttribute` /
 * `PSModuleAttribute`. Named after the real C# attribute each mirrors (`[Cmdlet]` +
 * `ProcessorInfoAttribute`, `[Parameter]` + `ArgumentInfoAttribute`), not "hints" —
 * these are standalone declarations, not per-node IR rendering metadata. They reference
 * IR by id (`IRId`) rather than living on `IROperation.rendering`/`IRParameter.rendering`.
 *
 * `sourceOperationIds`/`sourceParameterIds` are arrays from the start (even though every
 * producer today resolves exactly one element) so a later fan-out (one operation -> New
 * *and* Set) or fan-in (GetPut-style merge of two operations into one command) is a
 * derivation-logic change, not a schema break.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "@azure-client-engine/ir";

// ─── Shared sub-shapes ───────────────────────────────────────────────────

/** PowerShell `ConfirmImpact` level. */
export const ConfirmImpacts = ["None", "Low", "Medium", "High"] as const;
export type ConfirmImpact = (typeof ConfirmImpacts)[number];

/** Lifecycle tag, mirroring `ProcessorInfoAttribute`'s Preview/Deprecated/Experimental/GA tags. */
export const LifecycleTags = ["Preview", "Deprecated", "Experimental", "GA"] as const;
export type LifecycleTag = (typeof LifecycleTags)[number];

/** Mirrors AutoRest's `CompleterInfoAttribute`. Shared by command- and parameter-level completers. */
export type PSCompleterAttribute = {
  script?: string | undefined;
  name?: string | undefined;
  description?: string | undefined;
};

export const PSCompleterAttributeSchema: z.ZodType<PSCompleterAttribute> = z
  .object({
    script: z.string().min(1).optional(),
    name: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
  })
  .strict()
  .refine((hint) => hint.script !== undefined || hint.name !== undefined || hint.description !== undefined, {
    message: "PSCompleterAttribute requires at least one of script/name/description.",
  });

/** Mirrors AutoRest's `DefaultInfoAttribute` — a script-derived default value for a parameter. */
export type PSDefaultValueAttribute = {
  script: string;
  name?: string | undefined;
  description?: string | undefined;
  setCondition?: string | undefined;
};

export const PSDefaultValueAttributeSchema: z.ZodType<PSDefaultValueAttribute> = z
  .object({
    script: z.string().min(1),
    name: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
    setCondition: z.string().min(1).optional(),
  })
  .strict();

// ─── PSCommandAttribute (mirrors [Cmdlet] + ProcessorInfoAttribute) ──────

/** One source operation's contribution to a merged command; e.g. "Get"/"List", or "base"/"Expanded" for one fanned-out operation. */
export type PSParameterSetAttribute = {
  name: string;
  sourceOperationId: IRId;
};

export const PSParameterSetAttributeSchema: z.ZodType<PSParameterSetAttribute> = z
  .object({
    name: z.string().min(1),
    sourceOperationId: IRIdSchema,
  })
  .strict();

export type PSCommandAttribute = {
  verb: string;
  noun: string;
  /** Configurable subject prefix combined with `noun` at grouping time, e.g. "Az". */
  subjectPrefix?: string | undefined;
  supportsShouldProcess: boolean;
  confirmImpact?: ConfirmImpact | undefined;
  alias?: string[] | undefined;
  /** When true, the command is not exported from the generated module. */
  hidden?: boolean | undefined;
  completer?: PSCompleterAttribute | undefined;
  summary?: string | undefined;
  description?: string | undefined;
  examples?: string[] | undefined;
  lifecycleTag?: LifecycleTag | undefined;
  httpPath?: { path: string; apiVersion: string } | undefined;
  supportsPaging?: boolean | undefined;
  helpUri?: string | undefined;
  /** By-id references into IR for the command's output type(s). */
  outputTypes: IRId[];
  /** One parameter set per merged source operation (mirrors `CmdletCommonMetadataAttribute`'s parameter-set model). */
  parameterSets: PSParameterSetAttribute[];
  /** Mirrors `CmdletCommonMetadataAttribute.DefaultParameterSetName`. */
  defaultParameterSetName: string;
  /** IR operation id(s) this command derives from. One today; more than one is a valid shape (GetPut-style merge). */
  sourceOperationIds: IRId[];
};

export const PSCommandAttributeSchema: z.ZodType<PSCommandAttribute> = z
  .object({
    verb: z.string().min(1),
    noun: z.string().min(1),
    subjectPrefix: z.string().min(1).optional(),
    supportsShouldProcess: z.boolean(),
    confirmImpact: z.enum(ConfirmImpacts).optional(),
    alias: z.array(z.string().min(1)).optional(),
    hidden: z.boolean().optional(),
    completer: PSCompleterAttributeSchema.optional(),
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
    outputTypes: z.array(IRIdSchema),
    parameterSets: z.array(PSParameterSetAttributeSchema).min(1),
    defaultParameterSetName: z.string().min(1),
    sourceOperationIds: z.array(IRIdSchema).min(1),
  })
  .strict();

// ─── PSParameterAttribute (mirrors [Parameter] + ArgumentInfoAttribute) ──

/** Per-parameter-set override of the fields that legitimately vary by set (real `ParameterAttribute` can stack one instance per set). */
export type PSParameterSetOverride = {
  mandatory?: boolean | undefined;
  position?: number | undefined;
  helpMessage?: string | undefined;
};

export const PSParameterSetOverrideSchema: z.ZodType<PSParameterSetOverride> = z
  .object({
    mandatory: z.boolean().optional(),
    position: z.number().int().optional(),
    helpMessage: z.string().min(1).optional(),
  })
  .strict();

export type PSParameterAttribute = {
  name: string;
  mandatory?: boolean | undefined;
  /** Parameter-set name this parameter belongs to (defaults to the containing command's base set). */
  parameterSetName?: string | undefined;
  helpMessage?: string | undefined;
  position?: number | undefined;
  valueFromPipeline?: boolean | undefined;
  valueFromPipelineByPropertyName?: boolean | undefined;
  alias?: string[] | undefined;
  completer?: PSCompleterAttribute | undefined;
  defaultValue?: PSDefaultValueAttribute | undefined;
  /** Per-set overrides, keyed by `PsParameterSet` name, for fields that vary by set. */
  parameterSets?: Record<string, PSParameterSetOverride> | undefined;
  /** The IR parameter/property this types from and binds to. */
  sourceParameterId: IRId;
};

export const PSParameterAttributeSchema: z.ZodType<PSParameterAttribute> = z
  .object({
    name: z.string().min(1),
    mandatory: z.boolean().optional(),
    parameterSetName: z.string().min(1).optional(),
    helpMessage: z.string().min(1).optional(),
    position: z.number().int().optional(),
    valueFromPipeline: z.boolean().optional(),
    valueFromPipelineByPropertyName: z.boolean().optional(),
    alias: z.array(z.string().min(1)).optional(),
    completer: PSCompleterAttributeSchema.optional(),
    defaultValue: PSDefaultValueAttributeSchema.optional(),
    parameterSets: z.record(z.string(), PSParameterSetOverrideSchema).optional(),
    sourceParameterId: IRIdSchema,
  })
  .strict();

// ─── PSModuleAttribute (mirrors the generated .psd1's module-level facts) ─

export type PSModuleAttribute = {
  description?: string | undefined;
  /** IR source id this module derives from (`IRSource.id`, e.g. "Microsoft.Storage" — a plain string, not an `IRId`). */
  sourceId: string;
  /** `${verb}-${noun}` for every command grouped under this source; mirrors `.psd1`'s `CmdletsToExport`. */
  cmdletsToExport: string[];
};

export const PSModuleAttributeSchema: z.ZodType<PSModuleAttribute> = z
  .object({
    description: z.string().min(1).optional(),
    sourceId: z.string().min(1),
    cmdletsToExport: z.array(z.string().min(1)),
  })
  .strict();

// ─── Root (schema-emission entry point only; no aggregate document exists yet) ─

/** Envelope of the three top-level attribute kinds, solely so `emit-json-schema.mjs` has one root to derive JSON Schema from. */
export type PSSchemaRoot = {
  command: PSCommandAttribute;
  parameter: PSParameterAttribute;
  module: PSModuleAttribute;
};

export const PSSchemaRootSchema: z.ZodType<PSSchemaRoot> = z
  .object({
    command: PSCommandAttributeSchema,
    parameter: PSParameterAttributeSchema,
    module: PSModuleAttributeSchema,
  })
  .strict();
