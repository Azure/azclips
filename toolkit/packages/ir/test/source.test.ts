/**
 * `IRSource.rendering` (module-level) — round-trip test for the first hint
 * namespace ever added to `IRSource`. See ps-cmdlet-schema's design.md
 * Module-level section for why this is scoped to just `description`.
 */

import { describe, expect, it } from "vitest";

import { IRSourceSchema, type IRSource } from "../src/index.js";

function makeSource(overrides: Partial<IRSource> = {}): IRSource {
  return {
    id: "Microsoft.Resources",
    entrypoint: "resourcegroup.tsp",
    apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
    server: { kind: "literal", url: "https://management.azure.com" },
    rootNamespace: ["ResourceGroups"],
    ...overrides,
  };
}

describe("IRSource.rendering.powershell", () => {
  it("round-trips a source with a powershell.description hint", () => {
    const source = makeSource({
      rendering: { powershell: { description: "Resource group management cmdlets." } },
    });

    expect(IRSourceSchema.parse(source)).toEqual(source);
  });

  it("accepts a source with no rendering field at all (existing validation unaffected)", () => {
    const source = makeSource();

    expect(IRSourceSchema.parse(source)).toEqual(source);
  });
});
