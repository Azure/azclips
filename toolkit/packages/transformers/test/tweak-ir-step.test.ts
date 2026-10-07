/**
 * `tweak-ir` tests.
 *
 * The step is where IR-level renaming lives, so these pin *that* contract: a name
 * that two declarations would otherwise share is resolved here, ids are left alone,
 * and a rename is reported rather than applied silently.
 */

import { describe, expect, it } from "vitest";

import { parseIR, type IR } from "@azure-client-engine/ir";

import { tweakIrStep } from "../src/index.js";

const MODULE = "Contoso";
const LIBRARY = "@azure-tools/common";
const SERVER = { module: MODULE, namespace: [MODULE], name: "server" };

type Diagnostic = { severity: string; code: string; message: string };

function id(name: string, namespace: string[], module = MODULE) {
  return { module, namespace, name };
}

function model(name: string, namespace: string[], source = MODULE) {
  return {
    name,
    source,
    id: id(name, namespace, source),
    properties: [
      {
        typeKind: "primitive",
        id: id(`${name}.value`, namespace, source),
        name: "Value",
        wireName: "value",
        type: { kind: "primitive", name: "string" },
        optional: false,
        nullable: false,
      },
    ],
  };
}

function enumeration(name: string, namespace: string[], source = MODULE) {
  return {
    name,
    source,
    id: id(name, namespace, source),
    valueType: "string",
    isFixed: true,
    members: [{ name: "One", value: "one" }],
  };
}

function document(models: unknown[], enums: unknown[] = []): IR {
  return parseIR({
    schemaVersion: "0.13.0",
    sources: [
      {
        id: MODULE,
        entrypoint: "main.tsp",
        apiVersion: { value: "2026-01-01", location: "query", parameterName: "api-version" },
        server: SERVER,
        rootNamespace: [MODULE],
      },
    ],
    servers: [{ id: SERVER, endpoint: { kind: "literal", url: "https://example.com" } }],
    models,
    enums,
    operations: [],
  });
}

function run(ir: IR): { out: IR; diagnostics: Diagnostic[] } {
  const diagnostics: Diagnostic[] = [];
  const out = tweakIrStep().run(ir, {
    reportDiagnostic: (d) => diagnostics.push(d as Diagnostic),
  });
  parseIR(out);
  return { out, diagnostics };
}

const codes = (diagnostics: Diagnostic[]): string[] =>
  diagnostics.map((d) => d.code.split("/").pop()!);

describe("tweak-ir: type names", () => {
  it("leaves a document whose names are already unique alone", () => {
    const { out, diagnostics } = run(document([model("Widget", [MODULE]), model("Gadget", [MODULE])]));

    expect(diagnostics).toEqual([]);
    expect(out.models.map((m) => m.name)).toEqual(["Widget", "Gadget"]);
  });

  it("renames a library type that collides with one the spec declares", () => {
    // The real shape: `ErrorResponse` exists both in a provider's namespace and in
    // a shared library. Emitted as-is, one overwrites the other and every reference
    // resolves to the survivor — so the loser's shape is silently lost.
    const ir = document([
      model("ErrorResponse", ["Contoso", "Storage"]),
      model("ErrorResponse", ["Azure", "ResourceManager", "CommonTypes"], LIBRARY),
    ]);
    const { out, diagnostics } = run(ir);

    expect(codes(diagnostics)).toEqual(["type-name-collision"]);
    // The spec's own type keeps the plain name; the library type is qualified.
    expect(out.models.map((m) => m.name)).toEqual(["ErrorResponse", "CommonTypesErrorResponse"]);
  });

  it("keeps ids untouched so references still resolve after a rename", () => {
    const ir = document([
      model("ErrorResponse", ["Contoso", "Storage"]),
      model("ErrorResponse", ["Azure", "ResourceManager", "CommonTypes"], LIBRARY),
    ]);
    const before = ir.models.map((m) => ({ ...m.id }));
    const { out } = run(ir);

    // An id records where a declaration came from; renaming what it renders as must
    // not disturb that, or every reference to it breaks.
    expect(out.models.map((m) => m.id)).toEqual(before);
  });

  it("treats models and enums as one name space, since both become types", () => {
    const { out, diagnostics } = run(
      document([model("Status", [MODULE])], [enumeration("Status", ["Azure", "Common"], LIBRARY)]),
    );

    expect(codes(diagnostics)).toEqual(["type-name-collision"]);
    expect(out.models[0]!.name).toBe("Status");
    expect(out.enums[0]!.name).toBe("CommonStatus");
  });

  it("widens the qualifier until the name is free", () => {
    // `CommonStatus` is already taken, so one segment is not enough.
    const { out } = run(
      document([
        model("Status", [MODULE]),
        model("CommonStatus", [MODULE]),
        model("Status", ["Azure", "Common"], LIBRARY),
      ]),
    );

    expect(out.models.map((m) => m.name)).toEqual(["Status", "CommonStatus", "AzureCommonStatus"]);
  });

  it("falls back to a counter when qualifying cannot produce a free name", () => {
    // The qualified form is already taken and there is only one namespace segment to
    // draw on, so qualification runs out. The counter exists purely to guarantee the
    // rename terminates rather than looping.
    const { out } = run(
      document([
        model("Thing", [MODULE]),
        model("SharedThing", [MODULE]),
        model("Thing", ["Shared"], LIBRARY),
      ]),
    );

    expect(out.models.map((m) => m.name)).toEqual(["Thing", "SharedThing", "Thing2"]);
  });

  it("resolves three-way collisions without ever reusing a name", () => {
    const { out, diagnostics } = run(
      document([
        model("Error", [MODULE]),
        model("Error", ["Azure", "Common"], LIBRARY),
        model("Error", ["Azure", "Core"], LIBRARY),
      ]),
    );

    expect(diagnostics).toHaveLength(2);
    const names = out.models.map((m) => m.name);
    expect(names).toEqual(["Error", "CommonError", "CoreError"]);
    expect(new Set(names).size).toBe(names.length);
  });
});
