import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";
import { IRLiteralValueSchema, type IRLiteralValue } from "./literal.js";
import { IRTypeRefSchema, type IRTypeRef } from "./type-ref.js";

export type IRDiscriminatedUnionVariant = {
  /** Wire tag. Absent for the unnamed default variant; an empty string is a named tag. */
  value?: string | undefined;
  /** Absent only when the payload is exactly the null literal. */
  type?: IRTypeRef | undefined;
  nullable: boolean;
  /** Exact payload literal, not a default for an unrestricted payload type. */
  literalValue?: IRLiteralValue | undefined;
  docs?: string | undefined;
  summary?: string | undefined;
};

export const IRDiscriminatedUnionVariantSchema: z.ZodType<IRDiscriminatedUnionVariant> = z.object({
  value: z.string().optional(),
  type: IRTypeRefSchema.optional(),
  nullable: z.boolean(),
  literalValue: IRLiteralValueSchema.optional(),
  docs: z.string().optional(),
  summary: z.string().optional(),
}).strict().superRefine((variant, ctx) => {
  if (variant.type === undefined && (variant.literalValue?.kind !== "null" || !variant.nullable)) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ["type"],
      message: "A union variant requires a type unless its payload is exactly null",
    });
  }
});

export type IRDiscriminatedUnion = {
  id: IRId;
  name: string;
  source: string;
  discriminatorPropertyName: string;
  envelope: "object" | "none";
  /** JSON field containing the payload when envelope is object. */
  envelopePropertyName?: string | undefined;
  /** Source declaration order, including the default variant when present. */
  variants: IRDiscriminatedUnionVariant[];
  docs?: string | undefined;
  summary?: string | undefined;
};

export const IRDiscriminatedUnionSchema: z.ZodType<IRDiscriminatedUnion> = z.object({
  id: IRIdSchema,
  name: z.string().min(1),
  source: z.string().min(1),
  discriminatorPropertyName: z.string(),
  envelope: z.enum(["object", "none"]),
  envelopePropertyName: z.string().optional(),
  variants: z.array(IRDiscriminatedUnionVariantSchema),
  docs: z.string().optional(),
  summary: z.string().optional(),
}).strict().superRefine((union, ctx) => {
  if (union.envelope === "object" && union.envelopePropertyName === undefined) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ["envelopePropertyName"],
      message: "An object envelope requires its payload property name",
    });
  }
  const tags = new Set<string | undefined>();
  union.variants.forEach((variant, index) => {
    if (tags.has(variant.value)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["variants", index, "value"],
        message: variant.value === undefined ? "Duplicate default variant" : `Duplicate union tag '${variant.value}'`,
      });
    }
    tags.add(variant.value);
  });
});
