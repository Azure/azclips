/**
 * `rendering.powershell` (output-formatting) — round-trip tests for the
 * `formatIndex`/`formatLabel`/`formatWidth`/`excludeFromFormat` overrides
 * shared across all three per-property hint variants (Object/Value/
 * Collection). See ps-cmdlet-schema's design.md "Output-formatting level"
 * section for the real `FormatTableAttribute`/`DoNotFormatAttribute`
 * precedent this mirrors.
 */

import { describe, expect, it } from "vitest";

import {
  ObjectPropertyHintsSchema,
  ValuePropertyHintsSchema,
  CollectionPropertyHintsSchema,
  type ObjectPropertyHints,
  type ValuePropertyHints,
  type CollectionPropertyHints,
} from "../src/index.js";

describe("rendering.powershell (output-formatting property overrides)", () => {
  it("round-trips a fully populated override on a value property", () => {
    const hint: ValuePropertyHints = {
      powershell: { formatIndex: 0, formatLabel: "Resource Group", formatWidth: 20, excludeFromFormat: false },
    };

    expect(ValuePropertyHintsSchema.parse(hint)).toEqual(hint);
  });

  it("round-trips on an object property (alongside the existing flatten hint)", () => {
    const hint: ObjectPropertyHints = {
      flatten: { into: "self" },
      powershell: { excludeFromFormat: true },
    };

    expect(ObjectPropertyHintsSchema.parse(hint)).toEqual(hint);
  });

  it("round-trips on a collection property", () => {
    const hint: CollectionPropertyHints = { powershell: { formatIndex: 2 } };

    expect(CollectionPropertyHintsSchema.parse(hint)).toEqual(hint);
  });

  it("accepts a property hint with no powershell namespace at all", () => {
    expect(ValuePropertyHintsSchema.parse({}).powershell).toBeUndefined();
  });

  it("rejects a negative formatIndex", () => {
    expect(() => ValuePropertyHintsSchema.parse({ powershell: { formatIndex: -1 } })).toThrow();
  });

  it("rejects a non-positive formatWidth", () => {
    expect(() => ValuePropertyHintsSchema.parse({ powershell: { formatWidth: 0 } })).toThrow();
  });
});
