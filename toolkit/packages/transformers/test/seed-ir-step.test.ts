/**
 * Document facts.
 *
 * These pin how the stage turns gen-time facts into the IR's target: the defaults
 * that let an unconfigured document generate, the facts that override them, and how
 * a bad fact is reported without leaving an invalid target behind.
 */

import { describe, expect, it } from "vitest";

import { emptyIR, parseIR, type IR } from "@azure-client-engine/ir";

import { seedIrStep } from "../src/seed-ir-step.js";

const SERVER = { module: "Widgets", namespace: ["Contoso", "Widgets"], name: "server" };

function document(rootNamespace: string[] = ["Contoso", "Widgets"]): IR {
  return {
    ...emptyIR(),
    sources: [
      {
        id: "Widgets",
        entrypoint: "main.tsp",
        rootNamespace,
        apiVersion: { value: "2026-01-01", location: "query", parameterName: "api-version" },
        server: SERVER,
      },
    ],
    servers: [{ id: SERVER, endpoint: { kind: "literal", url: "https://example.com" } }],
  };
}

interface Diagnostic {
  severity: string;
  code: string;
  message: string;
}

/** Run the step, asserting it leaves a valid document behind. */
function run(
  facts: Record<string, unknown>,
  ir: IR = document(),
): { out: IR; diagnostics: Diagnostic[] } {
  const diagnostics: Diagnostic[] = [];
  const out = seedIrStep.run(ir, { reportDiagnostic: (d) => diagnostics.push(d), config: facts }) as IR;
  parseIR(out);
  return { out, diagnostics };
}

/** `severity:code`, without the step's prefix. */
const codes = (diagnostics: Diagnostic[]): string[] =>
  diagnostics.map((d) => `${d.severity}:${d.code.split("/").pop()}`);

const DEFAULT_DIRECTORIES = { models: "Models", processors: "Processors" };

describe("seed-ir: defaults", () => {
  it("places an unconfigured document under its source's namespace, in place", () => {
    const ir = document();
    const { out, diagnostics } = run({}, ir);

    expect(out).toBe(ir);
    expect(out.target).toEqual({
      rootNamespace: ["Contoso", "Widgets"],
      projectName: "Contoso.Widgets",
      directories: DEFAULT_DIRECTORIES,
    });
    expect(diagnostics).toEqual([]);
  });

  it("falls back to 'Generated' when the source declares no namespace", () => {
    const { out } = run({}, document([]));

    expect(out.target?.rootNamespace).toEqual(["Generated"]);
    expect(out.target?.projectName).toBe("Generated");
  });
});

describe("seed-ir: facts", () => {
  it("takes every fact from its config", () => {
    const { out, diagnostics } = run({
      "service-name": "Gadgets",
      "root-namespace": "Fabrikam.Gadgets",
      "project-name": "Fabrikam.Gadgets.Client",
      version: "0.1.0",
      description: "Gadgets for AzClips",
      directories: { models: "Types", processors: "Handlers" },
    });

    expect(out.target).toEqual({
      rootNamespace: ["Fabrikam", "Gadgets"],
      projectName: "Fabrikam.Gadgets.Client",
      directories: { models: "Types", processors: "Handlers" },
      serviceName: "Gadgets",
      version: "0.1.0",
      description: "Gadgets for AzClips",
    });
    expect(diagnostics).toEqual([]);
  });

  it("defaults each unset fact on its own", () => {
    const { out } = run({ "root-namespace": "Fabrikam.Gadgets", directories: { models: "Types" } });

    expect(out.target).toEqual({
      rootNamespace: ["Fabrikam", "Gadgets"],
      projectName: "Fabrikam.Gadgets",
      directories: { models: "Types", processors: "Processors" },
    });
  });

  it("treats a fact with no value as absent", () => {
    const { out, diagnostics } = run({ version: null, directories: null });

    expect(out.target?.version).toBeUndefined();
    expect(out.target?.directories).toEqual(DEFAULT_DIRECTORIES);
    expect(diagnostics).toEqual([]);
  });
});

describe("seed-ir: checking its config", () => {
  it("reports every fact of the wrong type, and writes no target", () => {
    const { out, diagnostics } = run({
      "root-namespace": ["Fabrikam", "Gadgets"],
      version: 1,
      directories: "Types",
    });

    // Every bad fact is reported, not just the first, so one run shows them all.
    expect(codes(diagnostics)).toEqual([
      "error:invalid-fact",
      "error:invalid-fact",
      "error:invalid-fact",
    ]);
    // An unquoted YAML `version: 1.0` arrives as a number; the message says so.
    expect(diagnostics.map((d) => d.message)).toContain(
      "'version' must be a non-empty string, not the number 1.",
    );
    // No default stands in for a value the config got wrong: the stage fails.
    expect(out.target).toBeUndefined();
  });

  it("reports an empty value, including an empty namespace segment", () => {
    const { out, diagnostics } = run({
      "root-namespace": "Fabrikam..Gadgets",
      "project-name": "",
      directories: { processors: "" },
    });

    expect(codes(diagnostics)).toEqual([
      "error:invalid-fact",
      "error:invalid-fact",
      "error:invalid-fact",
    ]);
    expect(diagnostics[2]!.message).toContain("'directories.processors'");
    expect(out.target).toBeUndefined();
  });

  it("warns about keys it does not know, at the top level and under directories", () => {
    const { out, diagnostics } = run({
      rootNamespace: "Fabrikam.Gadgets",
      directories: { model: "Types" },
    });

    expect(codes(diagnostics)).toEqual(["warning:unknown-fact", "warning:unknown-fact"]);
    expect(diagnostics[0]!.message).toContain("'rootNamespace'");
    expect(diagnostics[1]!.message).toContain("'directories.model'");
    // Unknown keys are ignored, not guessed at.
    expect(out.target?.rootNamespace).toEqual(["Contoso", "Widgets"]);
    expect(out.target?.directories).toEqual(DEFAULT_DIRECTORIES);
  });
});
