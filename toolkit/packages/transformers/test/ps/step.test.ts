import { describe, expect, it } from "vitest";
import { emptyIR, IRSchema, type IROperation } from "@azure-client-engine/ir";
import type { CompilerDiagnostic, TransformContext } from "@azure-client-engine/compiler";
import {
  createPSDeriveStep,
  createPSDirectivesStep,
  PS_DERIVE_STEP_ID,
  PS_DIRECTIVES_STEP_ID,
} from "../../src/index.js";

const context: TransformContext = { reportDiagnostic: () => {}, config: {} };

/** A context that keeps what the step reported, so diagnostics can be asserted on. */
function collectingContext(): { context: TransformContext; diagnostics: CompilerDiagnostic[] } {
  const diagnostics: CompilerDiagnostic[] = [];
  return { context: { reportDiagnostic: (d) => diagnostics.push(d), config: {} }, diagnostics };
}

const serverId = { module: "Microsoft.Test", namespace: [], name: "defaultServer" };

const operation: IROperation = {
  id: { module: "Microsoft.Test", namespace: ["Things"], name: "get" },
  name: "get",
  method: "GET",
  source: "Microsoft.Test",
  pathTemplate: "/things/{thingName}",
  pathParameters: [],
  queryParameters: [],
  headerParameters: [],
  responses: [
    {
      statusCode: { kind: "exact", code: 200 },
      isError: false,
      type: { kind: "model", id: { module: "Microsoft.Test", namespace: [], name: "Thing" } },
    },
  ],
  apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
  server: serverId,
};

const ir = {
  ...emptyIR(),
  servers: [{ id: serverId, endpoint: { kind: "literal" as const, url: "https://management.azure.com" } }],
  sources: [
    {
      id: "Microsoft.Test",
      entrypoint: "main.tsp",
      apiVersion: { value: "2025-04-01", location: "query" as const, parameterName: "api-version" },
      server: serverId,
      rootNamespace: ["Microsoft", "Test"],
    },
  ],
  operations: [operation],
};

describe("ps.derive step", () => {
  it("names itself after its stage id", () => {
    expect(createPSDeriveStep().name).toBe(PS_DERIVE_STEP_ID);
  });

  it("puts the derived surface on IR.ps without mutating the input", () => {
    const out = createPSDeriveStep().run(ir, context);

    expect(out.ps).toBeDefined();
    expect(out.operations).toBe(ir.operations);
    expect(ir).not.toHaveProperty("ps");
  });

  it("produces a document that still validates", () => {
    const out = createPSDeriveStep({ modulePrefix: "Az" }).run(ir, context);
    expect(IRSchema.safeParse(out).success).toBe(true);
  });

  it("threads options through to the derivation", () => {
    const out = createPSDeriveStep({ modulePrefix: "Az", subjectPrefix: "Test" }).run(ir, context);
    expect(out.ps!.commands).toHaveLength(1);
    expect(out.ps!.commands[0]!.noun).toBe("AzTestThing");
  });

  it("rejects an unknown option", () => {
    expect(() => createPSDeriveStep({ modulPrefix: "Az" }).run(ir, context)).toThrow(
      /unknown option 'modulPrefix'/,
    );
  });

  it("rejects a non-string option", () => {
    expect(() => createPSDeriveStep({ modulePrefix: 42 }).run(ir, context)).toThrow(
      /must be a string, got number/,
    );
  });
});

describe("ps.directives step", () => {
  const derived = createPSDeriveStep().run(ir, context);

  it("names itself after its stage id", () => {
    expect(createPSDirectivesStep().name).toBe(PS_DIRECTIVES_STEP_ID);
  });

  it("applies a directive to the surface ps.derive produced", () => {
    const out = createPSDirectivesStep({
      directives: [{ where: { subject: "Thing" }, set: { subject: "Widget" } }],
    }).run(derived, context);

    expect(out.ps!.commands[0]!.noun).toBe("Widget");
    expect(derived.ps!.commands[0]!.noun).toBe("Thing");
  });

  it("reports a directive that matched nothing rather than dropping it", () => {
    const { context: ctx, diagnostics } = collectingContext();
    createPSDirectivesStep({
      directives: [{ where: { subject: "Absent" }, set: { subject: "X" } }],
    }).run(derived, ctx);

    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0]!.severity).toBe("warning");
    expect(diagnostics[0]!.code).toBe("@azure-client-engine/transformers/unmatched-directive");
    expect(diagnostics[0]!.message).toMatch(/^directive\[0\]:/);
  });

  it("errors when the PowerShell surface is missing, leaving the document untouched", () => {
    const { context: ctx, diagnostics } = collectingContext();
    const out = createPSDirectivesStep().run(ir, ctx);

    expect(out).toBe(ir);
    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0]!.severity).toBe("error");
    expect(diagnostics[0]!.code).toBe("@azure-client-engine/transformers/ps-surface-missing");
    expect(diagnostics[0]!.message).toContain(PS_DERIVE_STEP_ID);
  });

  it("is a no-op with no directives", () => {
    const out = createPSDirectivesStep().run(derived, context);
    expect(out.ps).toEqual(derived.ps);
  });

  it("rejects an unknown option", () => {
    expect(() => createPSDirectivesStep({ directive: [] }).run(derived, context)).toThrow(
      /unknown option 'directive'/,
    );
  });

  it("rejects a malformed directive", () => {
    expect(() =>
      createPSDirectivesStep({ directives: [{ where: {} }] }).run(derived, context),
    ).toThrow(/invalid 'directives' option/);
  });
});
