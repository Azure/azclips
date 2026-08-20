import { describe, expect, it } from "vitest";

import { IRLengthConstraintsSchema } from "../src/index.js";

describe("length constraints", () => {
  it("accepts minLength and maxLength", () => {
    const constraints = {
      minLength: 0,
      maxLength: 24,
    };

    expect(IRLengthConstraintsSchema.parse(constraints)).toEqual(constraints);
  });

  it("rejects invalid length constraints", () => {
    expect(() => IRLengthConstraintsSchema.parse({ minLength: -1 })).toThrow();
    expect(() => IRLengthConstraintsSchema.parse({ maxLength: 1.5 })).toThrow();
  });
});