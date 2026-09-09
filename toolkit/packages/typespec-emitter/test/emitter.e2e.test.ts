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

  it("omits IRSource.auth when the service declares no authentication", () => {
    // pets.tsp uses @service/@server but no @useAuth, so auth is absent.
    expect(ir.sources[0]?.auth).toBeUndefined();
  });

  it("omits visibility on unconstrained properties (pets declares no @visibility)", () => {
    const props = ir.models.flatMap((m) => m.properties);
    expect(props.length).toBeGreaterThan(0);
    // No pets property carries @visibility, so every one is visible in all
    // lifecycle phases -> the field is omitted (absent = all phases).
    expect(props.every((p) => p.visibility === undefined)).toBe(true);
  });
});

describe("emitter e2e: enum-service.tsp → IREnum.isFixed", () => {
  const ENUM_TSP = normalizePath(resolve(here, "fixtures", "enum-service.tsp"));
  let enumIr: IR;
  let enumDiagnostics: readonly Diagnostic[];

  beforeAll(async () => {
    const written = new Map<string, string>();
    const host: CompilerHost = {
      ...NodeHost,
      writeFile: async (path: string, content: string) => {
        written.set(normalizePath(path), content);
      },
      mkdirp: async (path: string) => path,
    };
    const program = await compile(host, ENUM_TSP, {
      emit: [EMITTER],
      options: { [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true } },
    });
    enumDiagnostics = program.diagnostics;
    const errors = program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
    const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
    if (!entry) throw new Error("enum fixture emitted no ir.json");
    enumIr = JSON.parse(entry[1]) as IR;
  });

  it("preserves fixed and extensible enum semantics", () => {
    expect(() => parseIR(enumIr)).not.toThrow();
    expect(enumIr.enums.find((e) => e.name === "ClosedEnum")?.isFixed).toBe(true);
    expect(enumIr.enums.find((e) => e.name === "ClosedUnion")?.isFixed).toBe(true);
    const openUnion = enumIr.enums.find((e) => e.name === "OpenUnion");
    expect(openUnion?.isFixed).toBe(false);
    expect(openUnion?.members.map((member) => member.value)).toEqual(["one", "two"]);
  });

  it("warns when emitting an extensible enum", () => {
    const warnings = enumDiagnostics.filter((d) => d.severity === "warning");
    expect(warnings.filter((d) => d.code.endsWith("/open-enum-emitted"))).toHaveLength(1);
  });
});

describe("emitter e2e: auth-service.tsp → IRSource.auth", () => {
  const AUTH_TSP = normalizePath(resolve(here, "fixtures", "auth-service.tsp"));
  let authIr: IR;

  beforeAll(async () => {
    const written = new Map<string, string>();
    const host: CompilerHost = {
      ...NodeHost,
      writeFile: async (path: string, content: string) => {
        written.set(normalizePath(path), content);
      },
      mkdirp: async (path: string) => path,
    };
    const program = await compile(host, AUTH_TSP, {
      emit: [EMITTER],
      options: { [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true } },
    });
    const errors = program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
    const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
    if (!entry) throw new Error("auth fixture emitted no ir.json");
    authIr = JSON.parse(entry[1]) as IR;
  });

  it("maps @useAuth onto a schema-valid IRSource.auth", () => {
    expect(() => parseIR(authIr)).not.toThrow();
    const auth = authIr.sources[0]?.auth;
    expect(auth).toBeDefined();
    const scheme = auth?.options[0]?.schemes[0];
    expect(scheme?.kind).toBe("apiKey");
    if (scheme?.kind === "apiKey") {
      expect(scheme.in).toBe("header");
      expect(scheme.name).toBe("x-api-key");
    }
  });
});

describe("emitter e2e: visibility-service.tsp → IRProperty.visibility", () => {
  const VIS_TSP = normalizePath(resolve(here, "fixtures", "visibility-service.tsp"));
  let visIr: IR;

  beforeAll(async () => {
    const written = new Map<string, string>();
    const host: CompilerHost = {
      ...NodeHost,
      writeFile: async (path: string, content: string) => {
        written.set(normalizePath(path), content);
      },
      mkdirp: async (path: string) => path,
    };
    const program = await compile(host, VIS_TSP, {
      emit: [EMITTER],
      options: { [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true } },
    });
    const errors = program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
    const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
    if (!entry) throw new Error("visibility fixture emitted no ir.json");
    visIr = JSON.parse(entry[1]) as IR;
  });

  it("preserves the full lifecycle phase set (not collapsed to read/write-only)", () => {
    expect(() => parseIR(visIr)).not.toThrow();
    const widget = visIr.models.find((m) => m.name === "Widget");
    expect(widget).toBeDefined();
    const byWire = (w: string) => widget!.properties.find((p) => p.wireName === w);

    // @visibility(Lifecycle.Read) -> ["read"].
    expect(byWire("id")?.visibility).toEqual(["read"]);
    // @visibility(Lifecycle.Read, Lifecycle.Create) -> ["read","create"]
    // (the create-vs-update distinction the old readOnly/writeOnly pair lost).
    expect(byWire("size")?.visibility).toEqual(["read", "create"]);
    // No @visibility -> unconstrained -> field omitted.
    expect(byWire("label")?.visibility).toBeUndefined();
  });
});

describe("emitter e2e: flatten-service.tsp → IRProperty.rendering.flatten", () => {
  const FLAT_TSP = normalizePath(resolve(here, "fixtures", "flatten-service.tsp"));
  let flatIr: IR;

  beforeAll(async () => {
    const written = new Map<string, string>();
    const host: CompilerHost = {
      ...NodeHost,
      writeFile: async (path: string, content: string) => {
        written.set(normalizePath(path), content);
      },
      mkdirp: async (path: string) => path,
    };
    const program = await compile(host, FLAT_TSP, {
      emit: [EMITTER],
      options: { [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true } },
    });
    const errors = program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
    const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
    if (!entry) throw new Error("flatten fixture emitted no ir.json");
    flatIr = JSON.parse(entry[1]) as IR;
  });

  it("records @Legacy.flattenProperty as an object-property flatten hint", () => {
    expect(() => parseIR(flatIr)).not.toThrow();
    const widget = flatIr.models.find((m) => m.name === "Widget");
    expect(widget).toBeDefined();
    const byWire = (w: string) => widget!.properties.find((p) => p.wireName === w);

    // The `"!javascript"` scope applies to ace's `engine` scope, so TCGC
    // surfaces flatten=true and the emitter records the source's intent.
    const props = byWire("properties");
    expect(props?.typeKind).toBe("object");
    expect(props?.typeKind === "object" ? props.rendering?.flatten : undefined).toEqual({
      into: "self",
    });
    // An unflattened sibling carries no flatten hint (absent = not flattened).
    const name = byWire("name");
    expect(name?.typeKind === "object" ? name.rendering?.flatten : undefined).toBeUndefined();
  });
});

describe("emitter e2e: summary-service.tsp → IRProperty/model/operation.summary", () => {
  const SUM_TSP = normalizePath(resolve(here, "fixtures", "summary-service.tsp"));
  let sumIr: IR;

  beforeAll(async () => {
    const written = new Map<string, string>();
    const host: CompilerHost = {
      ...NodeHost,
      writeFile: async (path: string, content: string) => {
        written.set(normalizePath(path), content);
      },
      mkdirp: async (path: string) => path,
    };
    const program = await compile(host, SUM_TSP, {
      emit: [EMITTER],
      options: { [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true } },
    });
    const errors = program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
    const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
    if (!entry) throw new Error("summary fixture emitted no ir.json");
    sumIr = JSON.parse(entry[1]) as IR;
  });

  it("keeps @summary distinct from @doc on models, properties, and operations", () => {
    expect(() => parseIR(sumIr)).not.toThrow();
    const widget = sumIr.models.find((m) => m.name === "Widget");
    expect(widget).toBeDefined();
    // Model: short summary vs long doc, both preserved separately.
    expect(widget!.summary).toBe("A widget.");
    expect(widget!.docs).toBe("A widget is a long-form described thing with many nuances.");

    const byWire = (w: string) => widget!.properties.find((p) => p.wireName === w);
    // Property with both.
    expect(byWire("id")?.summary).toBe("Widget id.");
    expect(byWire("id")?.docs).toBe(
      "The unique widget identifier, assigned by the service on create.",
    );
    // Property with only a doc-comment -> summary omitted, docs present.
    expect(byWire("name")?.summary).toBeUndefined();
    expect(byWire("name")?.docs).toBe("The display name.");

    // Operation: short summary vs long doc.
    const get = sumIr.operations.find((o) => o.name === "get");
    expect(get?.summary).toBe("Get a widget.");
    expect(get?.docs).toBe("Retrieve a single widget by its identifier from the widget store.");
  });
});

describe("emitter e2e: access-service.tsp → IRModel.usage / .access", () => {
  const ACC_TSP = normalizePath(resolve(here, "fixtures", "access-service.tsp"));
  let accIr: IR;

  beforeAll(async () => {
    const written = new Map<string, string>();
    const host: CompilerHost = {
      ...NodeHost,
      writeFile: async (path: string, content: string) => {
        written.set(normalizePath(path), content);
      },
      mkdirp: async (path: string) => path,
    };
    const program = await compile(host, ACC_TSP, {
      emit: [EMITTER],
      options: { [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true } },
    });
    const errors = program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
    const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
    if (!entry) throw new Error("access fixture emitted no ir.json");
    accIr = JSON.parse(entry[1]) as IR;
  });

  it("carries TCGC usage flags onto models", () => {
    expect(() => parseIR(accIr)).not.toThrow();
    const widget = accIr.models.find((m) => m.name === "Widget");
    // Widget is both a request body and a response body -> input + output + json.
    expect(widget?.usage).toEqual(expect.arrayContaining(["input", "output", "json"]));
  });

  it("emits internal-access types and operations (carry everything) with access=internal", () => {
    // Before item L, @access(Access.internal) types/ops were silently skipped.
    const secret = accIr.models.find((m) => m.name === "Secret");
    expect(secret).toBeDefined();
    expect(secret!.access).toBe("internal");

    const getSecret = accIr.operations.find((o) => o.name === "getSecret");
    expect(getSecret).toBeDefined();
    expect(getSecret!.access).toBe("internal");

    // Public model/operation omit access (absent = public).
    const widget = accIr.models.find((m) => m.name === "Widget");
    expect(widget!.access).toBeUndefined();
    const put = accIr.operations.find((o) => o.name === "put");
    expect(put!.access).toBeUndefined();
  });
});

describe("emitter e2e: scalar-service.tsp → semantic scalar formats", () => {
  const SCALAR_TSP = normalizePath(resolve(here, "fixtures", "scalar-service.tsp"));
  let scalarIr: IR;

  beforeAll(async () => {
    const written = new Map<string, string>();
    const host: CompilerHost = {
      ...NodeHost,
      writeFile: async (path: string, content: string) => {
        written.set(normalizePath(path), content);
      },
      mkdirp: async (path: string) => path,
    };
    const program = await compile(host, SCALAR_TSP, {
      emit: [EMITTER],
      options: { [EMITTER]: { format: "json", outputFile: "ir.json", pretty: true } },
    });
    const errors = program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toEqual([]);
    const entry = [...written.entries()].find(([p]) => p.endsWith("ir.json"));
    if (!entry) throw new Error("scalar fixture emitted no ir.json");
    scalarIr = JSON.parse(entry[1]) as IR;
  });

  it("recovers Azure.Core semantic scalar identity as an IR format", () => {
    expect(() => parseIR(scalarIr)).not.toThrow();
    const thing = scalarIr.models.find((m) => m.name === "Thing");
    expect(thing).toBeDefined();
    const fmtOf = (wire: string): string | undefined => {
      const p = thing!.properties.find((x) => x.wireName === wire);
      const ref = p?.type;
      if (ref === undefined) return undefined;
      // primitive properties carry `type.format`; collections carry it on `element`.
      if (ref.kind === "array") {
        return ref.element.kind === "primitive" ? ref.element.format : undefined;
      }
      return ref.kind === "primitive" ? ref.format : undefined;
    };

    // armResourceIdentifier extends string -> TCGC kind "string"; ace stamps arm-id.
    expect(fmtOf("resourceId")).toBe("arm-id");
    expect(fmtOf("resourceIds")).toBe("arm-id"); // array element
    expect(fmtOf("correlationId")).toBe("uuid");
    expect(fmtOf("address")).toBe("ip-address"); // ipV4Address
    expect(fmtOf("address6")).toBe("ip-address"); // ipV6Address
    // A plain string carries no format.
    expect(fmtOf("plain")).toBeUndefined();
  });
});
