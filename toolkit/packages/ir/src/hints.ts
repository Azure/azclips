/**
 * Rendering hints — metadata, not mutation.
 *
 * Hints describe how the generator should *surface* an IR element, never
 * what the element *is*. The IR always carries wire truth; flattening,
 * naming, visibility, etc. are decisions the generator consults at the
 * last possible moment.
 *
 * Policy:
 *   - Hint namespaces are typed per node kind (model property hints differ
 *     by the property's type kind, so we have one variant per kind).
 *   - Within a namespace, fields use last-writer-wins shallow merge.
 *   - The `docs.remarks` field is an exception — additive across writers.
 *
 * Transforms MUST mutate hints through helper functions (see hints/helpers,
 * future module), not by directly assigning `node.rendering = ...`. The
 * helpers enforce per-field merge/override/additive semantics.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";

// ─── Shared sub-namespaces ──────────────────────────────────────────────

export type SerializationHint = {
  /** When to omit the field from the serialized payload. Default: 'never'. */
  omitWhen?: "never" | "null" | "default" | undefined;
  /** Override the wire name. Almost always set by the emitter; rare for transforms to touch. */
  wireNameOverride?: string | undefined;
};

export const SerializationHintSchema: z.ZodType<SerializationHint> = z
  .object({
    omitWhen: z.enum(["never", "null", "default"]).optional(),
    wireNameOverride: z.string().min(1).optional(),
  })
  .strict();

export type ClientApiHint = {
  /** Override the logical (C#-visible) name. */
  name?: string | undefined;
  /** Visibility of the generated member. Default 'public'. */
  visibility?: "public" | "internal" | undefined;
};

export const ClientApiHintSchema: z.ZodType<ClientApiHint> = z
  .object({
    name: z.string().min(1).optional(),
    visibility: z.enum(["public", "internal"]).optional(),
  })
  .strict();

export type DocsHint = {
  /** Additional remarks appended to the element's docstring. Additive. */
  remarks?: string[] | undefined;
};

export const DocsHintSchema: z.ZodType<DocsHint> = z
  .object({
    remarks: z.array(z.string()).optional(),
  })
  .strict();

// ─── Flatten hint (property-level) ──────────────────────────────────────

export type FlattenHint = {
  /**
   * Whether to hoist this property's nested members onto the containing
   * class. `self` = flatten; `never` = render as nested. The IR's wire
   * truth is preserved either way; only the C# rendering changes.
   */
  into: "self" | "never";
  /**
   * Used when flattening would collide with an existing property name
   * on the containing class. The flattened property gets this prefix
   * or replacement name instead of failing.
   */
  nameOverride?: string | undefined;
};

export const FlattenHintSchema: z.ZodType<FlattenHint> = z
  .object({
    into: z.enum(["self", "never"]),
    nameOverride: z.string().min(1).optional(),
  })
  .strict();

// ─── Common hints (the baseline every node-kind carries) ────────────────

/**
 * The two hint namespaces present on every kind of IR node: the
 * C#-facing client-API surface (name/visibility) and additive docs.
 * Per-kind hint aggregates extend this with kind-specific additions.
 */
export type CommonHints = {
  clientApi?: ClientApiHint | undefined;
  docs?: DocsHint | undefined;
};

export const CommonHintsSchema: z.ZodType<CommonHints> = z
  .object({
    clientApi: ClientApiHintSchema.optional(),
    docs: DocsHintSchema.optional(),
  })
  .strict();

// ─── Property hints, parameterized by type kind ─────────────────────────

export type ObjectPropertyHints = CommonHints & {
  flatten?: FlattenHint | undefined;
  serialization?: SerializationHint | undefined;
};

export const ObjectPropertyHintsSchema: z.ZodType<ObjectPropertyHints> = z
  .object({
    clientApi: ClientApiHintSchema.optional(),
    docs: DocsHintSchema.optional(),
    flatten: FlattenHintSchema.optional(),
    serialization: SerializationHintSchema.optional(),
  })
  .strict();

/**
 * Hints for value-shaped properties: primitives, enums, and structural
 * unions. (The name historically said "primitive" but enums and unions
 * also render as values and share the same hint surface.)
 */
export type ValuePropertyHints = CommonHints & {
  /** Render this value as a strong-typed wrapper (e.g., a value object). */
  wrapperType?: { id: IRId } | undefined;
  serialization?: SerializationHint | undefined;
};

export const ValuePropertyHintsSchema: z.ZodType<ValuePropertyHints> = z
  .object({
    clientApi: ClientApiHintSchema.optional(),
    docs: DocsHintSchema.optional(),
    wrapperType: z.object({ id: IRIdSchema }).strict().optional(),
    serialization: SerializationHintSchema.optional(),
  })
  .strict();

export type CollectionPropertyHints = CommonHints & {
  serialization?: SerializationHint | undefined;
};

export const CollectionPropertyHintsSchema: z.ZodType<CollectionPropertyHints> = z
  .object({
    clientApi: ClientApiHintSchema.optional(),
    docs: DocsHintSchema.optional(),
    serialization: SerializationHintSchema.optional(),
  })
  .strict();

// ─── Other node-kind hints (currently identical to CommonHints) ─────────
// These are deliberate aliases (not just re-exports) so that future
// kind-specific additions land in the right place without breaking
// downstream import sites.

export type ModelHints = CommonHints;
export const ModelHintsSchema: z.ZodType<ModelHints> = CommonHintsSchema;

export type EnumHints = CommonHints;
export const EnumHintsSchema: z.ZodType<EnumHints> = CommonHintsSchema;

// ─── PowerShell hints (operation-level and parameter-level) ─────────────
//
// No prior mention of "powershell" existed in this file before the
// ps-cmdlet-schema change (confirmed by inspection, not an existing
// anticipated namespace). Fields are both an authoring-override input and
// the `create-ps-commands` transform's (in @azure-client-engine/compiler)
// write target: it resolves verb/noun/parameterSetName/confirmImpact from
// IR + policy and writes the result back here, leaving any value an author
// already set untouched (ordinary last-writer-wins hint-merge semantics —
// no special-casing needed). Mirrors AutoRest.PowerShell's own
// `CommandOperation extends ProgrammaticOperation` pattern (PS facet bolted
// onto the same operation object), adapted to IR's immutable-node model.

/**
 * PowerShell `ConfirmImpact` level. Shared between `PowershellOperationHint`
 * and `@azure-client-engine/ps-commands`'s `PsCommand` aggregate so the two
 * packages can't drift on the allowed values.
 */
export const ConfirmImpacts = ["None", "Low", "Medium", "High"] as const;
export type ConfirmImpact = (typeof ConfirmImpacts)[number];

/**
 * PowerShell lifecycle tag, mirroring `ProcessorInfoAttribute`'s
 * Preview/Deprecated/Experimental/GA tags.
 */
export const LifecycleTags = ["Preview", "Deprecated", "Experimental", "GA"] as const;
export type LifecycleTag = (typeof LifecycleTags)[number];

/** Shared between operation and parameter hints. Mirrors AutoRest's `CompleterInfoAttribute`. */
export type CompleterHint = {
  script?: string | undefined;
  name?: string | undefined;
  description?: string | undefined;
};

export const CompleterHintSchema: z.ZodType<CompleterHint> = z
  .object({
    script: z.string().min(1).optional(),
    name: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
  })
  .strict();

/** Mirrors AutoRest's `DefaultInfoAttribute` — a script-derived default value for a parameter. */
export type DefaultValueHint = {
  script?: string | undefined;
  name?: string | undefined;
  description?: string | undefined;
  setCondition?: string | undefined;
};

export const DefaultValueHintSchema: z.ZodType<DefaultValueHint> = z
  .object({
    script: z.string().min(1).optional(),
    name: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
    setCondition: z.string().min(1).optional(),
  })
  .strict();

/**
 * PowerShell command-shape fields for an operation. See ps-cmdlet-schema's
 * design.md Decisions 3/8 for the full field list and why this lives here
 * rather than in a separate package.
 */
export type PowershellOperationHint = {
  /** PowerShell approved verb, e.g. "Get", "New", "Set". */
  verb?: string | undefined;
  /** PowerShell noun, singularized, WITHOUT subject prefix, e.g. "ResourceGroup". */
  noun?: string | undefined;
  /** Configurable subject prefix combined with `noun` at grouping time, e.g. "Az". */
  subjectPrefix?: string | undefined;
  /** Parameter-set name this operation contributes when merged with others sharing a verb+noun. */
  parameterSetName?: string | undefined;
  /** ConfirmImpact level for SupportsShouldProcess commands. */
  confirmImpact?: ConfirmImpact | undefined;
  /** Additional cmdlet aliases. */
  alias?: string[] | undefined;
  /** When true, the derived command is not exported from the generated module. */
  hidden?: boolean | undefined;
  /** Completer for a command-level dynamic-parameter scenario (rare; usually per-parameter). */
  completer?: CompleterHint | undefined;
  /** Mirrors `ProcessorInfoAttribute`'s Summary. */
  summary?: string | undefined;
  /** Mirrors `ProcessorInfoAttribute`'s Description. */
  description?: string | undefined;
  /** Mirrors `ProcessorInfoAttribute`'s Examples. */
  examples?: string[] | undefined;
  /** Mirrors `ProcessorInfoAttribute`'s Tags (Preview/Deprecated/Experimental/GA). */
  lifecycleTag?: LifecycleTag | undefined;
  /** Path template + api-version, for introspection/support tooling. */
  httpPath?: { path: string; apiVersion: string } | undefined;
};

export const PowershellOperationHintSchema: z.ZodType<PowershellOperationHint> = z
  .object({
    verb: z.string().min(1).optional(),
    noun: z.string().min(1).optional(),
    subjectPrefix: z.string().min(1).optional(),
    parameterSetName: z.string().min(1).optional(),
    confirmImpact: z.enum(ConfirmImpacts).optional(),
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
  })
  .strict();

export type OperationHints = CommonHints & {
  powershell?: PowershellOperationHint | undefined;
};

export const OperationHintsSchema: z.ZodType<OperationHints> = z
  .object({
    clientApi: ClientApiHintSchema.optional(),
    docs: DocsHintSchema.optional(),
    powershell: PowershellOperationHintSchema.optional(),
  })
  .strict();

/**
 * PowerShell command-shape fields for a parameter. Mirrors `ArgumentInfoAttribute`
 * (mandatory/mutability/default-value) and PS `[Parameter]` (pipeline-binding,
 * wildcards, aliases, `ValidateSet`, completer). See design.md Decision 3.
 */
export type PowershellParameterHint = {
  mandatory?: boolean | undefined;
  /** Parameter-set name this parameter belongs to (defaults to the containing operation's). */
  parameterSetName?: string | undefined;
  helpMessage?: string | undefined;
  valueFromPipeline?: boolean | undefined;
  valueFromPipelineByPropertyName?: boolean | undefined;
  supportsWildcards?: boolean | undefined;
  aliases?: string[] | undefined;
  /** `ValidateSet` equivalent. */
  allowedValues?: string[] | undefined;
  completer?: CompleterHint | undefined;
  /** Settable on create. Mirrors `InfoAttribute.Create`. */
  create?: boolean | undefined;
  /** Settable on update. Mirrors `InfoAttribute.Update`. */
  update?: boolean | undefined;
  /** Never settable — server-computed. Mirrors `InfoAttribute.ReadOnly`. */
  readOnly?: boolean | undefined;
  defaultValue?: DefaultValueHint | undefined;
};

export const PowershellParameterHintSchema: z.ZodType<PowershellParameterHint> = z
  .object({
    mandatory: z.boolean().optional(),
    parameterSetName: z.string().min(1).optional(),
    helpMessage: z.string().min(1).optional(),
    valueFromPipeline: z.boolean().optional(),
    valueFromPipelineByPropertyName: z.boolean().optional(),
    supportsWildcards: z.boolean().optional(),
    aliases: z.array(z.string().min(1)).optional(),
    allowedValues: z.array(z.string().min(1)).optional(),
    completer: CompleterHintSchema.optional(),
    create: z.boolean().optional(),
    update: z.boolean().optional(),
    readOnly: z.boolean().optional(),
    defaultValue: DefaultValueHintSchema.optional(),
  })
  .strict();

export type ParameterHints = CommonHints & {
  powershell?: PowershellParameterHint | undefined;
};

export const ParameterHintsSchema: z.ZodType<ParameterHints> = z
  .object({
    clientApi: ClientApiHintSchema.optional(),
    docs: DocsHintSchema.optional(),
    powershell: PowershellParameterHintSchema.optional(),
  })
  .strict();

/**
 * Hints attached to an operation's request body. Reuses the property-level
 * `FlattenHint` so the body's "flatten into method signature" decision is
 * expressed with the same vocabulary as model-property flattening.
 */
export type RequestBodyHints = {
  flatten?: FlattenHint | undefined;
};

export const RequestBodyHintsSchema: z.ZodType<RequestBodyHints> = z
  .object({
    flatten: FlattenHintSchema.optional(),
  })
  .strict();
