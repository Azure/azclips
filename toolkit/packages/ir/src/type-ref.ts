/**
 * Type references. Lightweight pointers — named types (models, enums)
 * are referenced by IRId; primitives and structural types are inlined.
 *
 * Why references not embeddings: models and enums live in their own
 * top-level collections to keep them unique. Embedding them at every
 * use site would duplicate definitions and make rename transforms
 * combinatorially expensive.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";
import { PrimitiveKinds, FormatKinds, type PrimitiveKind, type FormatKind } from "./primitives.js";

export type IRTypeRefPrimitive = {
  kind: "primitive";
  name: PrimitiveKind;
  format?: FormatKind | undefined;
};

export type IRTypeRefModel = { kind: "model"; id: IRId };
export type IRTypeRefEnum = { kind: "enum"; id: IRId };
export type IRTypeRefArray = { kind: "array"; element: IRTypeRef };
/** Dictionary keys are always strings on the JSON wire. */
export type IRTypeRefDictionary = { kind: "dictionary"; value: IRTypeRef };
/** Structural unions; discriminated polymorphism is modelled on IRModel instead. */
export type IRTypeRefUnion = { kind: "union"; variants: IRTypeRef[] };

export type IRTypeRef =
  | IRTypeRefPrimitive
  | IRTypeRefModel
  | IRTypeRefEnum
  | IRTypeRefArray
  | IRTypeRefDictionary
  | IRTypeRefUnion;

// ─── Per-kind schemas ───────────────────────────────────────────────────
// Exported so callers (e.g. property variants in model.ts) can reference
// the tight per-kind schema directly instead of refining the full union.
// That avoids running the full discriminated-union validation just to
// then reject 5/6 variants in a refine.
//
// The non-recursive variants (primitive/model/enum) are plain ZodObject;
// the recursive ones (array/dictionary/union) need `z.lazy` so they can
// refer back to the full union via `IRTypeRefSchema`.

export const IRTypeRefPrimitiveSchema: z.ZodType<IRTypeRefPrimitive> = z
  .object({
    kind: z.literal("primitive"),
    name: z.enum(PrimitiveKinds),
    format: z.enum(FormatKinds).optional(),
  })
  .strict();

export const IRTypeRefModelSchema: z.ZodType<IRTypeRefModel> = z
  .object({
    kind: z.literal("model"),
    id: IRIdSchema,
  })
  .strict();

export const IRTypeRefEnumSchema: z.ZodType<IRTypeRefEnum> = z
  .object({
    kind: z.literal("enum"),
    id: IRIdSchema,
  })
  .strict();

export const IRTypeRefArraySchema: z.ZodType<IRTypeRefArray> = z.lazy(() =>
  z
    .object({
      kind: z.literal("array"),
      element: IRTypeRefSchema,
    })
    .strict(),
);

export const IRTypeRefDictionarySchema: z.ZodType<IRTypeRefDictionary> = z.lazy(() =>
  z
    .object({
      kind: z.literal("dictionary"),
      value: IRTypeRefSchema,
    })
    .strict(),
);

export const IRTypeRefUnionSchema: z.ZodType<IRTypeRefUnion> = z.lazy(() =>
  z
    .object({
      kind: z.literal("union"),
      variants: z.array(IRTypeRefSchema).min(2),
    })
    .strict(),
);

/**
 * Convenience: model-or-dictionary, used by object-typed model properties
 * (a property whose value is a named model OR an open string-keyed map).
 * `z.union` rather than `z.discriminatedUnion` because dictionary is lazy.
 */
export const IRTypeRefObjectSchema: z.ZodType<IRTypeRefModel | IRTypeRefDictionary> = z.union([
  IRTypeRefModelSchema,
  IRTypeRefDictionarySchema,
]);

export const IRTypeRefSchema: z.ZodType<IRTypeRef> = z.lazy(() =>
  z.discriminatedUnion("kind", [
    z
      .object({
        kind: z.literal("primitive"),
        name: z.enum(PrimitiveKinds),
        format: z.enum(FormatKinds).optional(),
      })
      .strict(),
    z
      .object({
        kind: z.literal("model"),
        id: IRIdSchema,
      })
      .strict(),
    z
      .object({
        kind: z.literal("enum"),
        id: IRIdSchema,
      })
      .strict(),
    z
      .object({
        kind: z.literal("array"),
        element: IRTypeRefSchema,
      })
      .strict(),
    z
      .object({
        kind: z.literal("dictionary"),
        value: IRTypeRefSchema,
      })
      .strict(),
    z
      .object({
        kind: z.literal("union"),
        variants: z.array(IRTypeRefSchema).min(2),
      })
      .strict(),
  ]),
);
