import { describe, expect, it } from "vitest";
import { IRRequestBodySchema } from "../src/operation.js";

const body = (overrides: Record<string, unknown> = {}) => ({
  type: { kind: "primitive", name: "string" },
  contentType: "application/json",
  ...overrides,
});

describe("IRRequestBody", () => {
  it("accepts a body with no name", () => {
    expect(IRRequestBodySchema.safeParse(body()).success).toBe(true);
  });

  it("accepts a name alongside the flag that describes it", () => {
    expect(
      IRRequestBodySchema.safeParse(body({ name: "pet", isGeneratedName: false })).success,
    ).toBe(true);
  });

  it("rejects the flag without the name it describes", () => {
    const result = IRRequestBodySchema.safeParse(body({ isGeneratedName: true }));
    expect(result.success).toBe(false);
    expect(JSON.stringify(result)).toContain("cannot be set without one");
  });
});
