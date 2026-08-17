/**
 * `hints.powershell` (operation-level) — round-trip and isolation tests.
 *
 * Confirms the new namespace validates through IR's existing Zod machinery
 * and that adding it to `OperationHints` didn't leak onto unrelated node
 * kinds (`ModelHints`/`EnumHints`/`ParameterHints` stay plain `CommonHints`).
 */

import { describe, expect, it } from "vitest";

import { ModelHintsSchema, OperationHintsSchema, type OperationHints } from "../src/index.js";

describe("hints.powershell (OperationHints)", () => {
  it("round-trips a fully populated powershell hint", () => {
    const hint: OperationHints = {
      powershell: {
        verb: "New",
        noun: "AzResourceGroup",
        parameterSetName: "Get",
        confirmImpact: "Medium",
      },
    };

    const parsed = OperationHintsSchema.parse(hint);

    expect(parsed).toEqual(hint);
  });

  it("accepts a partially populated powershell hint (all fields optional)", () => {
    const parsed = OperationHintsSchema.parse({ powershell: { verb: "Get" } });

    expect(parsed.powershell?.verb).toBe("Get");
    expect(parsed.powershell?.noun).toBeUndefined();
  });

  it("accepts OperationHints with no powershell namespace at all", () => {
    const parsed = OperationHintsSchema.parse({});

    expect(parsed.powershell).toBeUndefined();
  });

  it("rejects a confirmImpact value outside the PowerShell enum", () => {
    expect(() =>
      OperationHintsSchema.parse({ powershell: { confirmImpact: "Extreme" } }),
    ).toThrow();
  });

  it("does not leak the powershell namespace onto unrelated node kinds", () => {
    expect(() => ModelHintsSchema.parse({ powershell: { verb: "Get" } })).toThrow();
  });
});
