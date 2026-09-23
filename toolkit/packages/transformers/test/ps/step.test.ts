import { describe, expect, it } from "vitest";
import { parseIR, parseTransformedIR, type TransformedIR } from "@azure-client-engine/ir";
import type { CompilerDiagnostic, TransformContext } from "@azure-client-engine/compiler";

import { createCommandStep } from "../../src/create-command-step.js";
import {
  createPSDeriveStep,
  createPSDirectivesStep,
  PS_DERIVE_STEP_ID,
  PS_DIRECTIVES_STEP_ID,
} from "../../src/index.js";

const context: TransformContext = { reportDiagnostic: () => {}, config: {} };

function collectingContext(): { context: TransformContext; diagnostics: CompilerDiagnostic[] } {
  const diagnostics: CompilerDiagnostic[] = [];
  return { context: { reportDiagnostic: (diagnostic) => diagnostics.push(diagnostic), config: {} }, diagnostics };
}

function projected(): TransformedIR {
  const server = { module: "M", namespace: [], name: "server" };
  const apiVersion = { value: "2026-01-01", location: "query" as const, parameterName: "api-version" };
  const ir = parseIR({
    schemaVersion: "0.12.0",
    sources: [{ id: "M", entrypoint: "main.tsp", apiVersion, server, rootNamespace: ["Contoso"] }],
    servers: [{ id: server, endpoint: { kind: "literal", url: "https://example.com" } }],
    models: [],
    enums: [],
    operations: [{
      id: { module: "M", namespace: [], interface: "Things", name: "get" },
      name: "get",
      source: "M",
      method: "GET",
      pathTemplate: "/things",
      pathParameters: [],
      queryParameters: [],
      headerParameters: [],
      responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
      apiVersion,
      server,
    }],
  });
  return createCommandStep().run(ir, context);
}

describe("ps.derive step", () => {
  it("is a TransformedIR step that preserves #805 deriveDialect behavior", () => {
    const ir = projected();
    const out = createPSDeriveStep({ modulePrefix: "Az", subjectPrefix: "Test" }).run(ir, context);

    expect(out).toBe(ir);
    expect(out.commands[0]!.dialect?.ps?.verb).toBe("Get");
    expect(out.commands[0]!.dialect?.ps?.noun).toBe("AzTestThings");
    expect(() => parseTransformedIR(out)).not.toThrow();
  });

  it("names itself and validates options", () => {
    expect(createPSDeriveStep().name).toBe(PS_DERIVE_STEP_ID);
    expect(() => createPSDeriveStep({ unknown: true }).run(projected(), context)).toThrow(/unknown option/);
    expect(() => createPSDeriveStep({ modulePrefix: 42 }).run(projected(), context)).toThrow(/must be a string/);
  });
});

describe("ps.directives step", () => {
  function derived(): TransformedIR {
    return createPSDeriveStep({ modulePrefix: "Az" }).run(projected(), context);
  }

  it("applies directives to canonical commands", () => {
    const input = derived();
    const out = createPSDirectivesStep({
      directives: [{ where: { subject: "Things" }, set: { subject: "Widgets" } }],
    }).run(input, context);

    expect(out.commands[0]!.dialect?.ps?.noun).toBe("AzWidgets");
    expect(input.commands[0]!.dialect?.ps?.noun).toBe("AzThings");
  });

  it("diagnoses a missing command PS dialect and returns the canonical IR unchanged", () => {
    const input = projected();
    const { context: diagnosticContext, diagnostics } = collectingContext();
    const out = createPSDirectivesStep().run(input, diagnosticContext);

    expect(out).toBe(input);
    expect(diagnostics).toEqual([expect.objectContaining({
      severity: "error",
      code: "@azure-client-engine/transformers/ps-command-dialect-missing",
    })]);
    expect(diagnostics[0]!.message).toContain(PS_DERIVE_STEP_ID);
  });

  it("does not require parameter PS dialects because display names fall back to neutral names", () => {
    const input = derived();
    expect(input.commands[0]!.parameters).toEqual([]);
    const out = createPSDirectivesStep().run(input, context);
    expect(out.commands).toHaveLength(1);
  });

  it("reports unmatched directives and applies supported pagination", () => {
    const { context: diagnosticContext, diagnostics } = collectingContext();
    const out = createPSDirectivesStep({
      directives: [
        { where: { subject: "Absent" }, remove: true },
        { where: { verb: "Get" }, set: { "clientside-pagination": true } },
      ],
    }).run(derived(), diagnosticContext);

    expect(diagnostics.map((diagnostic) => diagnostic.code)).toEqual([
      "@azure-client-engine/transformers/unmatched-directive",
    ]);
    expect(out.commands[0]!.dialect?.ps?.supportsPaging).toBe(true);
  });

  it("names itself and validates options", () => {
    expect(createPSDirectivesStep().name).toBe(PS_DIRECTIVES_STEP_ID);
    expect(() => createPSDirectivesStep({ directive: [] }).run(derived(), context)).toThrow(/unknown option/);
    expect(() => createPSDirectivesStep({ directives: [{ where: {} }] }).run(derived(), context)).toThrow(/invalid 'directives' option/);
  });
});
