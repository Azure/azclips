import { describe, expect, it } from "vitest";
import {
  IRDiscriminatedUnionSchema, IRDiscriminatedUnionVariantSchema, IRModelSchema,
  IRPolymorphismSchema, IRPropertySchema, IRTypeRefSchema,
  type IRDiscriminatedUnion,
} from "../src/index.js";

const id = { module: "M", namespace: ["Service"], name: "Choice" };
const reference = { kind: "discriminatedUnion", id } as const;
const choice: IRDiscriminatedUnion = {
  id,
  name: "Choice",
  source: "M",
  envelope: "object",
  discriminatorPropertyName: "tag",
  envelopePropertyName: "payload",
  variants: [
    { value: "", type: reference, nullable: false },
    { nullable: true, literalValue: { kind: "null" } },
  ],
};

describe("discriminator IR contracts", () => {
  it("preserves empty dispatch maps and empty-string model tags", () => {
    const empty = { discriminator: { ...id, name: "Base.kind" }, subtypes: [] };
    expect(IRPolymorphismSchema.parse(empty)).toEqual(empty);
    const mapped = { ...empty, subtypes: [{ value: "", type: id }] };
    expect(IRPolymorphismSchema.parse(mapped)).toEqual(mapped);
    const model = { id, name: "Choice", source: "M", properties: [], discriminatorValue: "" };
    expect(IRModelSchema.parse(model)).toEqual(model);
  });

  it("preserves named/default variants and recursive references through JSON", () => {
    const parsed = IRDiscriminatedUnionSchema.parse(JSON.parse(JSON.stringify(choice)));
    expect(parsed).toEqual(choice);
    expect(parsed.variants.map((variant) => variant.value)).toEqual(["", undefined]);
    expect(IRTypeRefSchema.parse(reference)).toEqual(reference);
  });

  it("requires a payload property for object envelopes without inventing one for flat unions", () => {
    const { envelopePropertyName, ...withoutProperty } = choice;
    expect(envelopePropertyName).toBe("payload");
    expect(IRDiscriminatedUnionSchema.safeParse(withoutProperty).success).toBe(false);
    expect(IRDiscriminatedUnionSchema.parse({ ...withoutProperty, envelope: "none" }))
      .not.toHaveProperty("envelopePropertyName");
  });

  it("accepts missing payload types only for an exact null literal", () => {
    expect(IRDiscriminatedUnionVariantSchema.safeParse({ nullable: true }).success).toBe(false);
    expect(IRDiscriminatedUnionVariantSchema.safeParse({
      nullable: false, literalValue: { kind: "null" },
    }).success).toBe(false);
    expect(IRDiscriminatedUnionVariantSchema.parse({ nullable: true, literalValue: { kind: "null" } }))
      .toEqual({ nullable: true, literalValue: { kind: "null" } });
  });

  it("rejects duplicate tags and duplicate defaults without confusing an empty tag with a default", () => {
    for (const variant of choice.variants) {
      expect(IRDiscriminatedUnionSchema.safeParse({ ...choice, variants: [variant, variant] }).success).toBe(false);
    }
    expect(IRDiscriminatedUnionSchema.safeParse(choice).success).toBe(true);
  });

  it("classifies discriminated-union properties as unions and retains ordinary union constraints", () => {
    const property = {
      id: { ...id, name: "Holder.choice" }, name: "Choice", wireName: "choice",
      optional: false, nullable: false, typeKind: "union", type: reference,
    };
    expect(IRPropertySchema.parse(property)).toEqual(property);
    expect(IRPropertySchema.safeParse({ ...property, typeKind: "object" }).success).toBe(false);
    expect(IRTypeRefSchema.safeParse({ kind: "union", variants: [reference] }).success).toBe(false);
  });
});
