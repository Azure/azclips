import { describe, expect, it } from "vitest";

import { resolveVerb } from "../src/verb-mapping.js";

describe("resolveVerb", () => {
  it("maps a recognized action word regardless of HTTP method", () => {
    expect(resolveVerb("create", "PUT")).toBe("New");
    expect(resolveVerb("createOrUpdate", "PUT")).toBe("New");
  });

  it("maps createOrUpdate over PUT to New, not Set (real ResourceGroups precedent)", () => {
    expect(resolveVerb("createOrUpdate", "PUT")).toBe("New");
  });

  it("maps update over PATCH to Update", () => {
    expect(resolveVerb("update", "PATCH")).toBe("Update");
  });

  it("maps list (including compound action words) to Get", () => {
    expect(resolveVerb("list", "GET")).toBe("Get");
    expect(resolveVerb("listByResourceGroup", "GET")).toBe("Get");
  });

  it("falls back to the HTTP-method default when the action word is unrecognized", () => {
    expect(resolveVerb("frobnicate", "PUT")).toBe("Set");
    expect(resolveVerb("frobnicate", "POST")).toBe("Invoke");
    expect(resolveVerb("frobnicate", "DELETE")).toBe("Remove");
  });
});
