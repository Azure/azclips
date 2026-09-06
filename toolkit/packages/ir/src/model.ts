/**
 * Models and their properties.
 *
 * Design points pinned earlier in the design discussion:
 *
 *  - Properties are a discriminated union by *type kind* (object, primitive,
 *    collection, enum, union). Hints are per-kind, so the schema rejects
 *    nonsense combinations like `wrapperType` on a model-typed property at
 *    parse time, not at generator time.
 *
 *  - Polymorphism is closed, multi-level allowed, fallback throws on
 *    unknown discriminator values. A model may simultaneously be a base
 *    (has `polymorphism`) and a child (has `parent` + `discriminatorValue`).
 *
 *  - Each model carries the *full denormalized* property list (own +
 *    inherited). Inherited properties have `inheritedFrom` set to the
 *    defining model's id; own properties have it undefined. The
 *    `rematerializeInherited` pass (separate module) re-derives the
 *    inherited copies between stages from the defining originals so
 *    transforms can't accidentally corrupt the chain.
 *
 *  - Overrides (e.g. a child narrowing the discriminator type to a string
 *    literal) carry `overrides` pointing at the property being overridden.
 *    The rematerialize pass preserves overrides instead of overwriting them.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";
import {
  IRTypeRefArraySchema,
  IRTypeRefEnumSchema,
  IRTypeRefObjectSchema,
  IRTypeRefPrimitiveSchema,
  IRTypeRefSchema,
  IRTypeRefUnionSchema,
  type IRTypeRef,
  type IRTypeRefArray,
  type IRTypeRefDictionary,
  type IRTypeRefEnum,
  type IRTypeRefModel,
  type IRTypeRefPrimitive,
  type IRTypeRefUnion,
} from "./type-ref.js";
import { IRLiteralValueSchema, type IRLiteralValue } from "./literal.js";
import {
  AccessKinds,
  UsageKinds,
  type IRAccess,
  type IRUsage,
} from "./primitives.js";
import {
  CollectionPropertyHintsSchema,
  ModelHintsSchema,
  ObjectPropertyHintsSchema,
  ValuePropertyHintsSchema,
  type CollectionPropertyHints,
  type ModelHints,
  type ObjectPropertyHints,
  type ValuePropertyHints,
} from "./hints.js";

// ─── Property visibility (lifecycle phases) ─────────────────────────────

/**
 * Lifecycle visibility phase, mirroring TypeSpec's `TypeSpec.Lifecycle`
 * visibility class (`@visibility(Lifecycle.Read | Create | Update | Delete |
 * Query)`). ace preserves the full phase set instead of collapsing to
 * read/write-only, so downstream generators keep the source's create- vs
 * update-only distinction.
 */
export type IRVisibility = "read" | "create" | "update" | "delete" | "query";

export const IRVisibilitySchema = z.enum(["read", "create", "update", "delete", "query"]);

// ─── Shared property base ───────────────────────────────────────────────

type IRPropertyCommon = {
  id: IRId;
  /** Logical (C#-facing) name. Mutable by renaming transforms. */
  name: string;
  /** JSON wire name. Always reflects nested wire truth, even when flattening hints are set. */
  wireName: string;
  optional: boolean;
  nullable: boolean;
  /**
   * Lifecycle phases in which the property is visible, mirroring the source's
   * `@visibility(Lifecycle.*)`. **Omitted when unconstrained** (visible in
   * every phase — TypeSpec's default when no `@visibility` is declared): an
   * absent field means "all phases", not "no phases".
   */
  visibility?: IRVisibility[] | undefined;
  /**
   * Defining model id for inherited copies. Undefined on the own/defining
   * copy. The rematerialize pass uses this to identify which properties
   * to overwrite from the source of truth.
   */
  inheritedFrom?: IRId | undefined;
  /**
   * If this property *replaces* an inherited property (e.g., discriminator
   * narrowing), points at the inherited property's id. Override copies are
   * preserved by the rematerialize pass.
   */
  overrides?: IRId | undefined;
  defaultValue?: IRLiteralValue | undefined;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
  /** Generated-code visibility (`@access`). Omitted when `public` (the default). */
  access?: IRAccess | undefined;
};

const propertyCommon = {
  id: IRIdSchema,
  name: z.string().min(1),
  wireName: z.string().min(1),
  optional: z.boolean(),
  nullable: z.boolean(),
  visibility: IRVisibilitySchema.array().optional(),
  inheritedFrom: IRIdSchema.optional(),
  overrides: IRIdSchema.optional(),
  defaultValue: IRLiteralValueSchema.optional(),
  docs: z.string().optional(),
  summary: z.string().optional(),
  access: z.enum(AccessKinds).optional(),
};

// ─── Property variants by type kind ─────────────────────────────────────

export type IRPropertyPrimitive = IRPropertyCommon & {
  typeKind: "primitive";
  type: IRTypeRefPrimitive;
  rendering?: ValuePropertyHints | undefined;
};

export type IRPropertyObject = IRPropertyCommon & {
  typeKind: "object";
  /** A model reference (`{ kind: 'model', id }`) or a dictionary. */
  type: IRTypeRefModel | IRTypeRefDictionary;
  rendering?: ObjectPropertyHints | undefined;
};

export type IRPropertyCollection = IRPropertyCommon & {
  typeKind: "collection";
  type: IRTypeRefArray;
  rendering?: CollectionPropertyHints | undefined;
};

export type IRPropertyEnum = IRPropertyCommon & {
  typeKind: "enum";
  type: IRTypeRefEnum;
  rendering?: ValuePropertyHints | undefined;
};

export type IRPropertyUnion = IRPropertyCommon & {
  typeKind: "union";
  type: IRTypeRefUnion;
  rendering?: ValuePropertyHints | undefined;
};

export type IRProperty =
  | IRPropertyPrimitive
  | IRPropertyObject
  | IRPropertyCollection
  | IRPropertyEnum
  | IRPropertyUnion;

// Per-variant schemas. The `type` field uses the tight per-kind schema
// from `type-ref.ts` instead of refining the full `IRTypeRef` union —
// this avoids running the full 6-variant union just to reject 5/6 in
// a refine, and produces precise error paths.
const makePropertyVariant = <
  K extends IRProperty["typeKind"],
  T extends z.ZodTypeAny,
  R extends z.ZodTypeAny,
>(
  typeKind: K,
  typeSchema: T,
  renderingSchema: R,
) =>
  z
    .object({
      ...propertyCommon,
      typeKind: z.literal(typeKind),
      type: typeSchema,
      rendering: renderingSchema.optional(),
    })
    .strict();

const IRPropertyPrimitiveSchema = makePropertyVariant(
  "primitive",
  IRTypeRefPrimitiveSchema,
  ValuePropertyHintsSchema,
);

const IRPropertyObjectSchema = makePropertyVariant(
  "object",
  IRTypeRefObjectSchema,
  ObjectPropertyHintsSchema,
);

const IRPropertyCollectionSchema = makePropertyVariant(
  "collection",
  IRTypeRefArraySchema,
  CollectionPropertyHintsSchema,
);

const IRPropertyEnumSchema = makePropertyVariant(
  "enum",
  IRTypeRefEnumSchema,
  ValuePropertyHintsSchema,
);

const IRPropertyUnionSchema = makePropertyVariant(
  "union",
  IRTypeRefUnionSchema,
  ValuePropertyHintsSchema,
);

// The cast is needed because z.ZodType<X> doesn't unify with the
// concrete ZodDiscriminatedUnion<...> shape; the per-variant schemas
// are plain ZodObjects (no .refine) so no `unknown` intermediate needed.
export const IRPropertySchema = z.discriminatedUnion("typeKind", [
  IRPropertyPrimitiveSchema,
  IRPropertyObjectSchema,
  IRPropertyCollectionSchema,
  IRPropertyEnumSchema,
  IRPropertyUnionSchema,
]) as z.ZodType<IRProperty>;

// ─── Polymorphism ───────────────────────────────────────────────────────

export type IRPolymorphism = {
  /** Id of the property carrying the discriminator value (must live on THIS model or be inherited). */
  discriminator: IRId;
  /** Immediate-level subtypes. Multi-level chains are walked, not duplicated here. */
  subtypes: Array<{
    /** Discriminator value used to select this subtype. */
    value: string;
    /** Pointer to the subtype model. */
    type: IRId;
  }>;
};

export const IRPolymorphismSchema: z.ZodType<IRPolymorphism> = z
  .object({
    discriminator: IRIdSchema,
    subtypes: z
      .array(
        z
          .object({
            value: z.string().min(1),
            type: IRIdSchema,
          })
          .strict(),
      )
      .min(1),
  })
  .strict();

// ─── Model ──────────────────────────────────────────────────────────────

export type IRModel = {
  id: IRId;
  name: string;
  /** Direct parent in the extends chain. Walk recursively for full ancestry. */
  parent?: IRId | undefined;
  /** Set iff parent has `polymorphism`. The discriminator value for this child. */
  discriminatorValue?: string | undefined;
  /** Set iff THIS model is a polymorphic base. */
  polymorphism?: IRPolymorphism | undefined;
  /** Own + inherited properties (denormalized). Rematerialized between stages. */
  properties: IRProperty[];
  /**
   * If set, declares this model is an open dictionary that accepts extra
   * properties whose value matches this type-ref. Corresponds to TCGC's
   * `SdkModelType.additionalProperties` (Record<T> / `extends Record<T>`).
   */
  additionalProperties?: IRTypeRef | undefined;
  /**
   * Source (IRSource.id) this model is owned by. Library-shared models
   * use the library's module string here, not any source's id.
   */
  source: string;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
  /** Data-flow usage (`@usage`/derived). Omitted when empty (`Usage.None`). */
  usage?: IRUsage[] | undefined;
  /** Generated-code visibility (`@access`). Omitted when `public` (the default). */
  access?: IRAccess | undefined;
  rendering?: ModelHints | undefined;
};

export const IRModelSchema: z.ZodType<IRModel> = z
  .object({
    id: IRIdSchema,
    name: z.string().min(1),
    parent: IRIdSchema.optional(),
    discriminatorValue: z.string().min(1).optional(),
    polymorphism: IRPolymorphismSchema.optional(),
    properties: z.array(IRPropertySchema),
    additionalProperties: IRTypeRefSchema.optional(),
    source: z.string().min(1),
    docs: z.string().optional(),
    summary: z.string().optional(),
    usage: z.enum(UsageKinds).array().optional(),
    access: z.enum(AccessKinds).optional(),
    rendering: ModelHintsSchema.optional(),
  })
  .strict();
