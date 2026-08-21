import { describe, expect, it } from "vitest";

import { IRConstraintsSchema } from "../src/index.js";

describe("constraints", () => {
  it("accepts string constraints", () => {
    const constraints = {
      minLength: 0,
      maxLength: 24,
      pattern: "^[a-z]+$",
    };

    expect(IRConstraintsSchema.parse(constraints)).toEqual(constraints);
  });

  it("rejects invalid string constraints", () => {
    expect(() => IRConstraintsSchema.parse({ minLength: -1 })).toThrow();
    expect(() => IRConstraintsSchema.parse({ maxLength: 1.5 })).toThrow();
    expect(() => IRConstraintsSchema.parse({ pattern: "" })).toThrow();
    expect(() => IRConstraintsSchema.parse({ pattern: "   " })).toThrow();
  });
});