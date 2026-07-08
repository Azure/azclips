/**
 * End-to-end backbone test.
 *
 * Drives the *whole* `compile()` orchestrator against the committed
 * `packages/ir/examples/pets.tsp` and asserts it yields schema-valid IR with
 * no error diagnostics and no generated files (the default no-op generator).
 *
 * This exercises the real compile -> emit -> capture path through the built
 * emitter; assertions are structural to stay robust against machine-specific
 * absolute paths in `IRSource`.
 */

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { beforeAll, describe, expect, it } from "vitest";
import { normalizePath } from "@typespec/compiler";
import { IR_SCHEMA_VERSION, parseIR } from "@azure-client-engine/ir";

import { compile, type CompileResult } from "../src/index.js";

const PETS_TSP = normalizePath(
  resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "ir", "examples", "pets.tsp"),
);

let result: CompileResult;

beforeAll(async () => {
  result = await compile({ entrypoint: PETS_TSP });
});

describe("compiler backbone: compile(pets.tsp) -> IR", () => {
  it("succeeds with no error-severity diagnostics", () => {
    expect(result.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(result.success).toBe(true);
  });

  it("returns schema-valid IR", () => {
    expect(result.ir).toBeDefined();
    expect(result.ir?.schemaVersion).toBe(IR_SCHEMA_VERSION);
    expect(() => parseIR(result.ir)).not.toThrow();
  });

  it("emits the domain models and operations from the spec", () => {
    expect(result.ir?.models.some((m) => m.name === "Pet")).toBe(true);
    expect(result.ir?.operations.some((o) => o.name === "list")).toBe(true);
    expect(result.ir?.operations.some((o) => o.name === "rename")).toBe(true);
  });

  it("generates no files by default (no-op generator)", () => {
    expect(result.files.size).toBe(0);
  });
});
