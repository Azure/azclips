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
// Each variant is a plain (non-lazy) ZodObject so it can serve BOTH as a
// member of the `IRTypeRefSchema` discriminated union AND be reused directly
// by narrowed callers (the property variants in model.ts). Recursion is
// deferred at the *field* level (`element` / `value` / `variants`) via
// `z.lazy`, instead of wrapping the whole variant in `z.lazy` — that keeps
// every variant a discriminated-union-eligible object.
//
// These are intentionally left un-annotated (no `: z.ZodType<...>`) so
// TypeScript keeps the concrete ZodObject type that `z.discriminatedUnion`
// requires. The authoritative shapes are the exported `IRTypeRef*` types above.

export const IRTypeRefPrimitiveSchema = z
  .object({
    kind: z.literal("primitive"),
    name: z.enum(PrimitiveKinds),
    format: z.enum(FormatKinds).optional(),
  })
  .strict();

export const IRTypeRefModelSchema = z
  .object({
    kind: z.literal("model"),
    id: IRIdSchema,
  })
  .strict();

export const IRTypeRefEnumSchema = z
  .object({
    kind: z.literal("enum"),
    id: IRIdSchema,
  })
  .strict();

export const IRTypeRefArraySchema = z
  .object({
    kind: z.literal("array"),
    element: z.lazy(() => IRTypeRefSchema),
  })
  .strict();

export const IRTypeRefDictionarySchema = z
  .object({
    kind: z.literal("dictionary"),
    value: z.lazy(() => IRTypeRefSchema),
  })
  .strict();

export const IRTypeRefUnionSchema = z
  .object({
    kind: z.literal("union"),
    variants: z.array(z.lazy(() => IRTypeRefSchema)).min(2),
  })
  .strict();

/**
 * Convenience: model-or-dictionary, used by object-typed model properties
 * (a property whose value is a named model OR an open string-keyed map).
 * Reuses the shared model/dictionary variant schemas so an object property's
 * `type` references the same `#/definitions/IRTypeRef*` nodes.
 */
export const IRTypeRefObjectSchema: z.ZodType<IRTypeRefModel | IRTypeRefDictionary> = z.union([
  IRTypeRefModelSchema,
  IRTypeRefDictionarySchema,
]);

/**
 * The full type-ref union — built directly from the shared per-kind object
 * schemas above, so both the union's variants and every narrowed caller
 * (`IRProperty.type`, ...) reference the SAME schemas, and (once registered in
 * the JSON-Schema emitter) the SAME `#/definitions/IRTypeRef*` nodes.
 */
export const IRTypeRefSchema: z.ZodType<IRTypeRef> = z.discriminatedUnion("kind", [
  IRTypeRefPrimitiveSchema,
  IRTypeRefModelSchema,
  IRTypeRefEnumSchema,
  IRTypeRefArraySchema,
  IRTypeRefDictionarySchema,
  IRTypeRefUnionSchema,
]);
