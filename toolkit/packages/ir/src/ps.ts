/**
 * PowerShell-facing attribute schema: `PSCommandAttribute` / `PSParameterAttribute` /
 * `PSModuleAttribute`. Each is named after the real C# attribute it mirrors (`[Cmdlet]` +
 * `ProcessorInfoAttribute`, `[Parameter]` + `ArgumentInfoAttribute`) and references the IR
 * node it was derived from by id (`IRId`).
 *
 * `sourceOperationIds` is an array from the start (even though every producer today resolves
 * exactly one element) so a later fan-out (one operation -> New *and* Set) or fan-in
 * (GetPut-style merge of two operations into one command) is a derivation-logic change, not
 * a schema break. `sourceParameterId` stays singular — a `PSParameterAttribute` always binds
 * to exactly one IR parameter.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";

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
  /** Set this parameter belongs to, when it belongs to exactly one; omitted when it spans sets. */
  parameterSetName?: string | undefined;
  /** When true, the parameter is not exported on the generated cmdlet (mirrors `DoNotExportAttribute`). */
  hidden?: boolean | undefined;
  helpMessage?: string | undefined;
  position?: number | undefined;
  valueFromPipeline?: boolean | undefined;
  valueFromPipelineByPropertyName?: boolean | undefined;
  alias?: string[] | undefined;
  completer?: PSCompleterAttribute | undefined;
  defaultValue?: PSDefaultValueAttribute | undefined;
  /** Per-set overrides, keyed by the parameter set's name (`PSParameterSetAttribute.name`), for fields that vary by set. */
  parameterSets?: Record<string, PSParameterSetOverride> | undefined;
  /**
   * Typed as the module's identity model rather than from IR, and supplies the path parameters its
   * set leaves out. The one parameter kind with no IR node behind it, so it carries no
   * `sourceParameterId`; exactly one of the two is always present.
   */
  identity?: true | undefined;
  /** The IR parameter/property this types from and binds to. */
  sourceParameterId?: IRId | undefined;
};

export const PSParameterAttributeSchema: z.ZodType<PSParameterAttribute> = z
  .object({
    name: z.string().min(1),
    mandatory: z.boolean().optional(),
    parameterSetName: z.string().min(1).optional(),
    hidden: z.boolean().optional(),
    helpMessage: z.string().min(1).optional(),
    position: z.number().int().optional(),
    valueFromPipeline: z.boolean().optional(),
    valueFromPipelineByPropertyName: z.boolean().optional(),
    alias: z.array(z.string().min(1)).optional(),
    completer: PSCompleterAttributeSchema.optional(),
    defaultValue: PSDefaultValueAttributeSchema.optional(),
    parameterSets: z.record(z.string().min(1), PSParameterSetOverrideSchema).optional(),
    identity: z.literal(true).optional(),
    sourceParameterId: IRIdSchema.optional(),
  })
  .strict()
  .superRefine((parameter, ctx) => {
    if ((parameter.identity === true) === (parameter.sourceParameterId !== undefined)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message:
          "A parameter must either carry `sourceParameterId` or be marked `identity`, not both and not neither.",
      });
    }

    if (parameter.parameterSetName !== undefined && parameter.parameterSets !== undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["parameterSetName"],
        message:
          "A parameter belongs to one set (`parameterSetName`) or spans several (`parameterSets`), not both.",
      });
    }

    // Omitting the map means "every set"; an empty one would mean "no set", which is nothing.
    if (parameter.parameterSets !== undefined && Object.keys(parameter.parameterSets).length === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["parameterSets"],
        message: "`parameterSets` must name at least one set; omit it for a parameter in every set.",
      });
    }
  });

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
  /** The composed cmdlet noun: module prefix + `subjectPrefix` + `subject`. */
  noun: string;
  /**
   * The bare subject, without either prefix. Kept alongside `noun` because authoring overrides
   * address the subject rather than the composed noun, and a composed value alone cannot be
   * rewritten back into its parts. Empty when the prefix carries the whole noun, which real
   * modules do deliberately: ApplicationInsights rewrites subject `Component` to nothing so the
   * cmdlet becomes `Get-AzApplicationInsights`. `noun` is the field that must be non-empty.
   */
  subject: string;
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
  /**
   * The command's own parameters. `[Parameter]` only ever decorates a property of a cmdlet class,
   * so a parameter belongs to exactly one command; nesting is also what makes `parameterSetName`
   * unambiguous, since set names are unique only within a command.
   */
  parameters: PSParameterAttribute[];
  /** Mirrors `CmdletCommonMetadataAttribute.DefaultParameterSetName`. */
  defaultParameterSetName: string;
  /** IR operation id(s) this command derives from. One today; more than one is a valid shape (GetPut-style merge). */
  sourceOperationIds: IRId[];
};

export const PSCommandAttributeSchema: z.ZodType<PSCommandAttribute> = z
  .object({
    verb: z.string().min(1),
    noun: z.string().min(1),
    subject: z.string(),
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
    parameters: z.array(PSParameterAttributeSchema),
    defaultParameterSetName: z.string().min(1),
    sourceOperationIds: z.array(IRIdSchema).min(1),
  })
  .strict()
  .superRefine((command, ctx) => {
    const setNames = new Set(command.parameterSets.map((set) => set.name));
    if (!setNames.has(command.defaultParameterSetName)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["defaultParameterSetName"],
        message: "defaultParameterSetName must match the name of one of parameterSets.",
      });
    }

    command.parameters.forEach((parameter, index) => {
      if (parameter.parameterSetName !== undefined && !setNames.has(parameter.parameterSetName)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["parameters", index, "parameterSetName"],
          message: `parameterSetName '${parameter.parameterSetName}' does not match any of the command's parameterSets.`,
        });
      }
      for (const setName of Object.keys(parameter.parameterSets ?? {})) {
        if (!setNames.has(setName)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ["parameters", index, "parameterSets", setName],
            message: `Per-set override keyed '${setName}' does not match any of the command's parameterSets.`,
          });
        }
      }
    });
  });

// ─── PSModuleAttribute (mirrors the generated .psd1's module-level facts) ─

/** One property of the module identity. */
export type PSIdentityPropertyAttribute = {
  name: string;
  helpMessage?: string | undefined;
  /** The path parameters this property stands for; empty for the synthesized ARM resource id. */
  sourceParameterIds: IRId[];
};

export const PSIdentityPropertyAttributeSchema: z.ZodType<PSIdentityPropertyAttribute> = z
  .object({
    name: z.string().min(1),
    helpMessage: z.string().min(1).optional(),
    sourceParameterIds: z.array(IRIdSchema),
  })
  .strict();

/**
 * The object an `identity` parameter binds. Synthesized rather than taken from the spec: one per
 * module, holding every path parameter any of its operations declares, all optional -- so a volume
 * and its parent share a type and each leaves the properties it doesn't use null. Mirrors
 * `tweakModelV2`, which builds it before naming and directives so both can still reach it.
 */
export type PSIdentityAttribute = {
  /** Type name without a language-specific prefix, e.g. "ElasticSanIdentity". */
  name: string;
  properties: PSIdentityPropertyAttribute[];
};

export const PSIdentityAttributeSchema: z.ZodType<PSIdentityAttribute> = z
  .object({
    name: z.string().min(1),
    properties: z.array(PSIdentityPropertyAttributeSchema),
  })
  .strict();

export type PSModuleAttribute = {
  /** Module name, e.g. "ElasticSan" -- names the `.psd1` and the identity type. */
  name: string;
  description?: string | undefined;
  /** IR source id this module derives from (`IRSource.id`, e.g. "Microsoft.Storage" — a plain string, not an `IRId`). */
  sourceId: string;
  /** `${verb}-${noun}` for every command grouped under this source; mirrors `.psd1`'s `CmdletsToExport`. */
  cmdletsToExport: string[];
  /** Absent when no operation in the module declares a path parameter, so nothing can be identified. */
  identity?: PSIdentityAttribute | undefined;
};

export const PSModuleAttributeSchema: z.ZodType<PSModuleAttribute> = z
  .object({
    name: z.string().min(1),
    description: z.string().min(1).optional(),
    sourceId: z.string().min(1),
    cmdletsToExport: z.array(z.string().min(1)),
    identity: PSIdentityAttributeSchema.optional(),
  })
  .strict();

// ─── PS surface ──────────────────────────────────────────────────────────────

/**
 * The PowerShell surface derived from an IR document, carried on `IR.ps`. Commands and modules
 * are the only top-level kinds: a parameter belongs to exactly one command and is reached
 * through it.
 */
export type PS = {
  commands: PSCommandAttribute[];
  modules: PSModuleAttribute[];
};

export const PSSchema: z.ZodType<PS> = z
  .object({
    commands: z.array(PSCommandAttributeSchema),
    modules: z.array(PSModuleAttributeSchema),
  })
  .strict();
