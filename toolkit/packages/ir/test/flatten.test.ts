import { describe, expect, it } from "vitest";
import {
  FlattenedMemberSchema,
  FlattenedParameterSchema,
  IRCommandParameterSourceSchema,
  ModelHintsSchema,
  RequestBodyHintsSchema,
  type FlattenedMember,
  type IRId,
} from "../src/index.js";

const id = (name: string): IRId => ({ module: "M", namespace: ["Service"], name });

const member: FlattenedMember = {
  name: "SkuName",
  property: id("Sku.name"),
  via: [id("Resource.properties"), id("ResourceProperties.sku")],
  optional: true,
  visibility: ["read", "create"],
};

describe("flattening metadata", () => {
  it("preserves a model's flattened members through serialization", () => {
    const hints = { flattened: [member] };
    expect(ModelHintsSchema.parse(JSON.parse(JSON.stringify(hints)))).toEqual(hints);
  });

  it("requires a flattened member to cross at least one nesting hop", () => {
    expect(FlattenedMemberSchema.safeParse({ ...member, via: [] }).success).toBe(false);
    const { via: _via, ...withoutVia } = member;
    expect(FlattenedMemberSchema.safeParse(withoutVia).success).toBe(false);
  });

  it("treats absent visibility as unconstrained rather than empty", () => {
    const { visibility, ...unconstrained } = member;
    expect(visibility).toBeDefined();
    const parsed = FlattenedMemberSchema.parse(unconstrained);
    expect("visibility" in parsed).toBe(false);
    expect(FlattenedMemberSchema.safeParse({ ...member, visibility: [] }).success).toBe(true);
    expect(FlattenedMemberSchema.safeParse({ ...member, visibility: ["archive"] }).success).toBe(false);
  });

  it("carries the body projection on the request body, alongside the flatten decision", () => {
    const hints = {
      flatten: { into: "self" as const },
      parameters: [
        // A top-level body property promoted as-is: no containers to walk.
        { name: "Name", property: id("Sku.name"), via: [], required: true },
        { name: "SkuTier", property: id("Sku.tier"), via: [id("Resource.sku")], required: false },
      ],
    };
    expect(RequestBodyHintsSchema.parse(JSON.parse(JSON.stringify(hints)))).toEqual(hints);
    // `via` is always present, even when empty.
    expect(FlattenedParameterSchema.safeParse({ name: "Name", property: id("Sku.name"), required: true }).success).toBe(false);
  });

  it("sources a command parameter from the same property reference as its body projection", () => {
    const source = { kind: "body" as const, property: id("Sku.tier"), via: [id("Resource.sku")] };
    expect(IRCommandParameterSourceSchema.parse(JSON.parse(JSON.stringify(source)))).toEqual(source);
    expect(IRCommandParameterSourceSchema.safeParse({ ...source, via: [] }).success).toBe(true);
    expect(IRCommandParameterSourceSchema.safeParse({ kind: "body", property: id("Sku.tier") }).success).toBe(false);
    expect(IRCommandParameterSourceSchema.safeParse({ kind: "body", path: [id("Resource.sku"), id("Sku.tier")] }).success).toBe(false);
  });

  it("rejects unmodeled flattening metadata rather than silently carrying it", () => {
    expect(FlattenedMemberSchema.safeParse({ ...member, nameOptions: ["SkuName"] }).success).toBe(false);
    expect(FlattenedMemberSchema.safeParse({ ...member, sharedWith: [] }).success).toBe(false);
    expect(ModelHintsSchema.safeParse({ flattened: [member], virtualProperties: {} }).success).toBe(false);
    // The pre-0.13 combined `path` is no longer accepted.
    const legacy = { name: "SkuName", path: [...member.via, member.property], optional: true };
    expect(FlattenedMemberSchema.safeParse(legacy).success).toBe(false);
  });
});
