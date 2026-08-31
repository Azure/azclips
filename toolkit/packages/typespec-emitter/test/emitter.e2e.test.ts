/**
 * End-to-end emitter test.
 *
 * Compiles the committed `packages/ir/examples/pets.tsp` through the *built*
 * emitter and asserts the emitted IR both validates against the IR schema
 * (`parseIR`) and exhibits the behaviours the fixes introduced:
 *   - PascalCase logical names (wireName preserved).
 *   - The `@versioned` api-version enum is filtered out of `enums`.
 *   - Pageable semantics on the `@pageItems`/`@nextLink` list op.
 *   - Long-running semantics on the `@pollingOperation` rename op.
 *   - Spec-declared types attributed to the source module.
 *
 * Implementation note: we drive the compiler with the real `NodeHost` (the
 * same on-disk module resolution the `tsp` CLI uses) rather than the in-memory
 * `Tester` harness, which mirrors the entire Azure library tree into a virtual
 * filesystem and exhausts the heap for a spec this size. The emitted file is
 * captured by wrapping `host.writeFile`, so nothing is written to disk.
 *
 * Assertions are structural (not an exact match against `pets.ir.json`) to keep
 * the test robust against machine-specific absolute paths in `IRSource`.
 */

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { beforeAll, describe, expect, it } from "vitest";
import {
  NodeHost,
  compile,
  normalizePath,
  type CompilerHost,
  type Diagnostic,
} from "@typespec/compiler";
import { IR_SCHEMA_VERSION, parseIR, type IR } from "@azure-client-engine/ir";

const EMITTER = "@azure-client-engine/typespec-emitter";

const here = dirname(fileURLToPath(import.meta.url));
const PETS_TSP = normalizePath(
  resolve(here, "..", "..", "ir", "examples", "pets.tsp"),
);

let ir: IR;
let diagnostics: readonly Diagnostic[];

beforeAll(async () => {
  // Capture emitted files in memory instead of writing to disk.
  const written = new Map<string, string>();
  const host: CompilerHost = {
    ...NodeHost,
    writeFile: async (path: string, content: string) => {
      written.set(normalizePath(path), content);
    },
    mkdirp: async (path: string) => path,
  };

  const program = await compile(host, PETS_TSP, {
    emit: [EMITTER],
    options: {
      [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true },
    },
  });
  diagnostics = program.diagnostics;

  const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
  if (!entry) {
    throw new Error(
      `Emitter wrote no ir.json. Files: ${[...written.keys()].join(", ") || "<none>"}`,
    );
  }
  ir = JSON.parse(entry[1]) as IR;
});

describe("emitter e2e: pets.tsp → IR", () => {
  it("produces schema-valid IR", () => {
    expect(ir.schemaVersion).toBe(IR_SCHEMA_VERSION);
    // Throws if the emitted IR violates the schema contract.
    expect(() => parseIR(ir)).not.toThrow();
  });

  it("reports no error-severity diagnostics", () => {
    const errors = diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
  });

  it("applies PascalCase to logical names while preserving wire names", () => {
    const pet = ir.models.find((m) => m.name === "Pet");
    expect(pet).toBeDefined();
    const id = pet!.properties.find((p) => p.wireName === "id");
    expect(id?.name).toBe("Id");
    const name = pet!.properties.find((p) => p.wireName === "name");
    expect(name?.name).toBe("Name");
  });

  it("filters the @versioned api-version enum out of enums", () => {
    expect(ir.enums.some((e) => e.name === "Versions")).toBe(false);
    // The unrelated domain enum is still emitted.
    expect(ir.enums.some((e) => e.name === "OperationState")).toBe(true);
  });

  it("emits pageable semantics for the list operation", () => {
    const list = ir.operations.find((o) => o.name === "list");
    expect(list?.semantics?.kind).toBe("pageable");
    if (list?.semantics?.kind === "pageable") {
      expect(list.semantics.itemsPath).toBe("items");
      expect(list.semantics.nextLinkPath).toBe("nextLink");
    }
  });

  it("emits long-running semantics for the rename operation", () => {
    const rename = ir.operations.find((o) => o.name === "rename");
    expect(rename?.semantics?.kind).toBe("longRunning");
  });

  it("attributes spec-declared types to the source module", () => {
    const pet = ir.models.find((m) => m.name === "Pet");
    expect(pet?.source).toBe("PetStore");
    expect(pet?.id.module).toBe("PetStore");
  });

  it("carries the author-supplied request body name and isGeneratedName", () => {
    const create = ir.operations.find((o) => o.name === "create");
    expect(create?.requestBody?.name).toBe("pet");
    expect(create?.requestBody?.isGeneratedName).toBe(false);

    const rename = ir.operations.find((o) => o.name === "rename");
    expect(rename?.requestBody?.name).toBe("body");
    expect(rename?.requestBody?.isGeneratedName).toBe(false);
  });
});
