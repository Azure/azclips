import { describe, expect, it } from "vitest";
import {
  FlattenedMemberSchema,
  FlattenedParameterSchema,
  ModelHintsSchema,
  RequestBodyHintsSchema,
  type FlattenedMember,
  type IRId,
} from "../src/index.js";

const id = (name: string): IRId => ({ module: "M", namespace: ["Service"], name });

const member: FlattenedMember = {
  name: "SkuName",
  path: [id("Resource.properties"), id("ResourceProperties.sku"), id("Sku.name")],
  optional: true,
  visibility: ["read", "create"],
};

describe("flattening metadata", () => {
  it("preserves a model's flattened members through serialization", () => {
    const hints = { flattened: [member] };
    expect(ModelHintsSchema.parse(JSON.parse(JSON.stringify(hints)))).toEqual(hints);
  });

  it("requires a flattened member to cross at least one nesting hop", () => {
    expect(FlattenedMemberSchema.safeParse({ ...member, path: [id("Sku.name")] }).success).toBe(false);
    expect(FlattenedMemberSchema.safeParse({ ...member, path: [] }).success).toBe(false);
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
        // A top-level body property promoted as-is: a single-hop path.
        { name: "Name", path: [id("Sku.name")], required: true },
        { name: "SkuTier", path: [id("Resource.sku"), id("Sku.tier")], required: false },
      ],
    };
    expect(RequestBodyHintsSchema.parse(JSON.parse(JSON.stringify(hints)))).toEqual(hints);
    expect(FlattenedParameterSchema.safeParse({ name: "Name", path: [], required: true }).success).toBe(false);
  });

  it("rejects unmodeled flattening metadata rather than silently carrying it", () => {
    expect(FlattenedMemberSchema.safeParse({ ...member, nameOptions: ["SkuName"] }).success).toBe(false);
    expect(FlattenedMemberSchema.safeParse({ ...member, sharedWith: [] }).success).toBe(false);
    expect(ModelHintsSchema.safeParse({ flattened: [member], virtualProperties: {} }).success).toBe(false);
  });
});
