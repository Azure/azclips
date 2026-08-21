import { describe, expect, it } from "vitest";

import { IRConstraintsSchema } from "../src/index.js";

describe("constraints", () => {
  it("accepts primitive constraints", () => {
    const constraints = {
      minLength: 0,
      maxLength: 24,
      pattern: "^[a-z]+$",
      minValue: -10.5,
      maxValue: 100,
    };

    expect(IRConstraintsSchema.parse(constraints)).toEqual(constraints);
  });

  it("rejects invalid primitive constraints", () => {
    expect(() => IRConstraintsSchema.parse({ minLength: -1 })).toThrow();
    expect(() => IRConstraintsSchema.parse({ maxLength: 1.5 })).toThrow();
    expect(() => IRConstraintsSchema.parse({ pattern: "" })).toThrow();
    expect(() => IRConstraintsSchema.parse({ pattern: "   " })).toThrow();
    expect(() => IRConstraintsSchema.parse({ minValue: "0" })).toThrow();
  });
});