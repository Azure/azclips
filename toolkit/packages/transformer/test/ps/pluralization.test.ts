import { describe, expect, it } from "vitest";

import { singularize } from "../../src/ps/pluralization.js";

/**
 * Values below are what `@azure-tools/codegen`'s `EnglishPluralizationService` returns for the
 * same input; the port exists to match it, so these are parity assertions rather than opinions
 * about English.
 */
describe("singularize", () => {
  it("singularizes regular plurals", () => {
    expect(singularize("volumes")).toBe("Volume");
    expect(singularize("tags")).toBe("Tag");
    expect(singularize("identities")).toBe("Identity");
    expect(singularize("rules")).toBe("Rule");
  });

  it("leaves words that only look plural alone", () => {
    for (const word of ["alias", "status", "dns", "sas", "acls", "redis", "https", "bus", "lens"]) {
      expect(singularize(word)).toBe(word.charAt(0).toUpperCase() + word.slice(1));
    }
  });

  it("handles classical inflections", () => {
    expect(singularize("criteria")).toBe("Criterion");
    expect(singularize("indices")).toBe("Index");
    expect(singularize("vertices")).toBe("Vertex");
    expect(singularize("mice")).toBe("Mouse");
    expect(singularize("analyses")).toBe("Analysis");
  });

  it("upper-cases the acronyms the generator recognises, and only those", () => {
    expect(singularize("vm")).toBe("VM");
    expect(singularize("os")).toBe("OS");
    expect(singularize("ip")).toBe("IP");
    expect(singularize("dns")).toBe("Dns");
  });

  it("inflects only the final word of a compound name", () => {
    expect(singularize("VMNames")).toBe("VMName");
    expect(singularize("networkAcls")).toBe("NetworkAcls");
    expect(singularize("userAssignedIdentities")).toBe("UserAssignedIdentity");
  });

  it("applies the seeded user dictionary that name-inferrer.ts installs", () => {
    expect(singularize("databases")).toBe("Database");
    expect(singularize("premises")).toBe("Premise");
  });

  it("leaves pronouns, single characters and non-alphabetic input untouched", () => {
    expect(singularize("")).toBe("");
    expect(singularize("s")).toBe("S");
    expect(singularize("his")).toBe("His");
  });
});
