import { describe, expect, it } from "vitest";
import { emptyIR, type IROperation } from "@azure-client-engine/ir";

import { createPsCommandsFactory } from "../src/create-ps-commands.js";
import { DirectiveSchema } from "../src/directives.js";

const CONTEXT = { reportDiagnostic: () => undefined, extensions: {} };

function makeOperation(overrides: Partial<IROperation> & Pick<IROperation, "id" | "method">): IROperation {
  return {
    name: overrides.id.name,
    source: "<root>",
    pathTemplate: "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
    pathParameters: [],
    queryParameters: [],
    headerParameters: [],
    responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
    apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
    server: { kind: "literal", url: "https://management.azure.com" },
    ...overrides,
  };
}

function makeParameter(name: string): IROperation["pathParameters"][number] {
  return {
    id: { module: "<root>", namespace: ["ResourceGroups"], name },
    name,
    wireName: name,
    type: { kind: "primitive", name: "string" },
    optional: false,
    nullable: false,
  };
}

describe("create-ps-commands: directives (Milestone 2)", () => {
  it("renames a parameter's alias across every matching command", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "get" },
      method: "GET",
      pathParameters: [makeParameter("Id")],
    });

    const step = createPsCommandsFactory({
      subjectPrefix: "Az",
      directives: [{ where: { parameterName: "Id" }, set: { alias: ["ResourceId"] } }],
    });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.pathParameters[0]!.rendering!.powershell!.aliases).toEqual([
      "ResourceId",
    ]);
  });

  it("hides a command matched by verb and subject", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["RestMethod"], name: "invoke" },
      method: "POST",
    });

    const step = createPsCommandsFactory({
      directives: [
        { select: "command", where: { verb: "Invoke", subject: "RestMethod" }, set: { hidden: true } },
      ],
    });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.hidden).toBe(true);
  });

  it("does not hide a command whose verb/subject doesn't match the directive", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
    });

    const step = createPsCommandsFactory({
      subjectPrefix: "Az",
      directives: [
        { select: "command", where: { verb: "Invoke", subject: "RestMethod" }, set: { hidden: true } },
      ],
    });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.hidden).toBeUndefined();
  });

  it("resolves a conflicting directive + inline hint in the inline hint's favor", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      rendering: { powershell: { confirmImpact: "Medium" } },
    });

    const step = createPsCommandsFactory({
      subjectPrefix: "Az",
      directives: [
        {
          select: "command",
          where: { verb: "New", subject: "ResourceGroup" },
          set: { confirmImpact: "High" },
        },
      ],
    });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    // Author-set inline hint wins over the directive's conflicting value.
    expect(result.operations[0]!.rendering!.powershell!.confirmImpact).toBe("Medium");
  });

  it("matches a parameter directive against the pre-directive command identity, even when a command directive renames the verb", async () => {
    const operation = makeOperation({
      id: { module: "<root>", namespace: ["ResourceGroups"], name: "createOrUpdate" },
      method: "PUT",
      pathParameters: [makeParameter("Location")],
    });

    const step = createPsCommandsFactory({
      subjectPrefix: "Az",
      directives: [
        // Renames the inferred verb "New" -> "Set".
        { select: "command", where: { verb: "New", subject: "ResourceGroup" }, set: { verb: "Set" } },
        // Scoped to the PRE-rename verb "New" — must still match the same command.
        {
          where: { parameterName: "Location", verb: "New", subject: "ResourceGroup" },
          set: { alias: ["Loc"] },
        },
      ],
    });
    const result = await step.run({ ...emptyIR(), operations: [operation] }, CONTEXT);

    expect(result.operations[0]!.rendering!.powershell!.verb).toBe("Set");
    expect(result.operations[0]!.pathParameters[0]!.rendering!.powershell!.aliases).toEqual(["Loc"]);
  });
});

describe("DirectiveSchema: parameter selector requires a parameterName", () => {
  it("rejects an explicit \"parameter\" selector with no where.parameterName", () => {
    const result = DirectiveSchema.safeParse({ select: "parameter", where: {}, set: { dontShow: true } });
    expect(result.success).toBe(false);
  });

  it("accepts an explicit \"parameter\" selector when where.parameterName is set", () => {
    const result = DirectiveSchema.safeParse({
      select: "parameter",
      where: { parameterName: "Foo" },
      set: { dontShow: true },
    });
    expect(result.success).toBe(true);
  });
});
