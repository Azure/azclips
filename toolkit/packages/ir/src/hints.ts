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
import { VisibilityKinds, type IRVisibility } from "./primitives.js";

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

// ─── Flattened members (the result of hoisting) ─────────────────────────

/**
 * One member hoisted onto a containing model from a nested object property —
 * the resolved *outcome* of `FlattenHint`, computed by a transform so that the
 * generator does not have to re-derive naming or collision rules.
 *
 * `IRModel.properties` is left untouched: this is rendering metadata, and the
 * wire truth stays with the properties the `path` walks through.
 */
export type FlattenedMember = {
  /**
   * Final C#-facing member name on the containing model. Unique within that
   * model's flattened set, and resolved against its ordinary property names.
   */
  name: string;
  /**
   * Property ids from the containing model down to the leaf, in access order
   * (e.g. `[properties, sku, name]` for `Properties.Sku.Name`). At least two
   * entries, since a flattened member always crosses one nesting hop. Ids are
   * invariant under renaming transforms, and the wire path is each hop's
   * `wireName`, so this stays correct however the members are later renamed.
   */
  path: IRId[];
  /** True when any hop along `path` is optional — reaching the leaf may fail. */
  optional: boolean;
  /**
   * Lifecycle phases in which *every* hop along `path` is visible (the
   * intersection). **Omitted when unconstrained**, matching
   * `IRProperty.visibility`: an absent field means "all phases", not "none".
   */
  visibility?: IRVisibility[] | undefined;
};

export const FlattenedMemberSchema: z.ZodType<FlattenedMember> = z
  .object({
    name: z.string().min(1),
    path: z.array(IRIdSchema).min(2),
    optional: z.boolean(),
    visibility: z.enum(VisibilityKinds).array().optional(),
  })
  .strict();

/**
 * One request-body property projected onto an operation's parameter surface.
 *
 * Deliberately *not* a reference into the body model's `ModelHints.flattened`
 * table: the projection filters by HTTP binding and lifecycle phase, and
 * resolves names against **this** operation's path/query/header parameters — so
 * two operations sharing a body model can legitimately disagree. Carrying the
 * `path` inline also keeps the generator free of a cross-model lookup.
 */
export type FlattenedParameter = {
  /** Final parameter name, after resolving collisions against the operation's own parameters. */
  name: string;
  /**
   * Property ids into the request-body model, container → leaf. A single entry
   * is a top-level body property promoted as-is; longer paths cross nesting.
   */
  path: IRId[];
  /** Whether the operation requires a value. Narrower than the property's own optionality. */
  required: boolean;
};

export const FlattenedParameterSchema: z.ZodType<FlattenedParameter> = z
  .object({
    name: z.string().min(1),
    path: z.array(IRIdSchema).min(1),
    required: z.boolean(),
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

// ─── Model hints ────────────────────────────────────────────────────────

export type ModelHints = CommonHints & {
  /**
   * Members hoisted onto this model from nested object properties. Owned by the
   * model (rather than derived per use site) because name-collision resolution
   * considers the model's whole member set, and because every operation using
   * this model as a payload must agree on the result.
   */
  flattened?: FlattenedMember[] | undefined;
};

export const ModelHintsSchema: z.ZodType<ModelHints> = z
  .object({
    clientApi: ClientApiHintSchema.optional(),
    docs: DocsHintSchema.optional(),
    flattened: z.array(FlattenedMemberSchema).optional(),
  })
  .strict();

// ─── Other node-kind hints (currently identical to CommonHints) ─────────
// These are deliberate aliases (not just re-exports) so that future
// kind-specific additions land in the right place without breaking
// downstream import sites.

export type EnumHints = CommonHints;
export const EnumHintsSchema: z.ZodType<EnumHints> = CommonHintsSchema;

export type ParameterHints = CommonHints;
export const ParameterHintsSchema: z.ZodType<ParameterHints> = CommonHintsSchema;

export type OperationHints = CommonHints;
export const OperationHintsSchema: z.ZodType<OperationHints> = CommonHintsSchema;

/**
 * Hints attached to an operation's request body. Reuses the property-level
 * `FlattenHint` so the body's "flatten into method signature" decision is
 * expressed with the same vocabulary as model-property flattening.
 */
export type RequestBodyHints = {
  flatten?: FlattenHint | undefined;
  /**
   * The body expanded onto the operation's parameter surface. Populated by a
   * transform when `flatten.into === "self"`; absent means the body stays a
   * single payload member.
   */
  parameters?: FlattenedParameter[] | undefined;
};

export const RequestBodyHintsSchema: z.ZodType<RequestBodyHints> = z
  .object({
    flatten: FlattenHintSchema.optional(),
    parameters: z.array(FlattenedParameterSchema).optional(),
  })
  .strict();
