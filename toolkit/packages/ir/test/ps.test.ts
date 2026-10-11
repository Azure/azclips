import { assert, describe, expect, it } from "vitest";
import {
  PSDeprecatedSchema,
  PSPreviewSchema,
  PsCommandDialectSchema,
  PsModuleDialectSchema,
  PsParameterDialectSchema,
  IRCommandSchema,
  emptyIR,
  safeParseTransformedIR,
} from "../src/index.js";

const target = {
  rootNamespace: ["Microsoft", "Azure", "Widgets"],
  projectName: "Widgets",
  directories: { models: "Models", processors: "Processors" },
};

describe("PsModuleDialect", () => {
  it("carries module naming metadata without client-surface declarations", () => {
    const result = PsModuleDialectSchema.safeParse({
      name: "ElasticSan",
      description: "Manages Elastic SAN resources.",
      prefix: "Az",
      subjectPrefix: "ElasticSan",
    });

    expect(result.success).toBe(true);
  });
});

describe("PsCommandDialect", () => {
  it("accepts PowerShell processor metadata on a canonical command dialect", () => {
    const result = PsCommandDialectSchema.safeParse({
      verb: "Remove",
      subject: "AzWidget",
      aliases: ["Remove-AzLegacyWidget"],
      defaultParameterSet: "ByName",
      supportsShouldProcess: true,
      confirmImpact: "High",
      examples: ["Remove-AzWidget -Name example"],
      deprecated: { message: "Use Remove-AzWidgetV2 instead." },
      preview: {
        message: "This command is in preview.",
        estimatedGaDate: "2027-01-01",
      },
      helpUri: "https://learn.microsoft.com/powershell/module/az.widgets/remove-azwidget",
      outputTypes: [
        { kind: "model", id: { module: "Widgets", namespace: [], name: "Widget" } },
      ],
      httpPath: { path: "/widgets/{name}", apiVersion: "2025-01-01" },
    });

    expect(result.success).toBe(true);
  });

  it("requires a message for preview metadata", () => {
    const result = PSPreviewSchema.safeParse({ estimatedGaDate: "2027-01-01" });

    expect(result.success).toBe(false);
  });

  it("requires a message for deprecation metadata", () => {
    expect(PSDeprecatedSchema.safeParse({}).success).toBe(false);
  });

  it("rejects an unknown ConfirmImpact value", () => {
    const result = PsCommandDialectSchema.safeParse({
      verb: "Remove",
      subject: "AzWidget",
      confirmImpact: "Critical",
    });

    expect(result.success).toBe(false);
  });

  it("rejects a malformed help URI", () => {
    const result = PsCommandDialectSchema.safeParse({
      verb: "Get",
      subject: "AzWidget",
      helpUri: "docs",
    });

    expect(result.success).toBe(false);
  });

  it("defers family default validation when parsing a standalone command", () => {
    const operation = { module: "Widgets", namespace: [], name: "getByName" };
    const result = IRCommandSchema.safeParse({
      id: { module: "Widgets", subject: "Widget", action: "Get", variant: "ByName" },
      name: "GetWidgetByName",
      template: "atomic",
      server: { module: "Widgets", namespace: [], name: "server" },
      steps: [
        {
          operation,
          request: { method: "GET", path: [], query: [], headers: [] },
          responses: [
            { statusCodes: [{ kind: "exact", code: 200 }], isError: false },
          ],
        },
      ],
      parameters: [],
      surface: { paginated: false },
      dialect: {
        ps: {
          verb: "Get",
          subject: "AzWidget",
          defaultParameterSet: "ByName",
        },
      },
    });

    expect(result.success).toBe(true);
  });
});

describe("PsParameterDialect", () => {
  it("accepts per-set bindings, values, validation, and lifecycle metadata", () => {
    const result = PsParameterDialectSchema.safeParse({
      name: "ResourceGroupName",
      aliases: ["ResourceGroup"],
      parameterSets: [
        { set: "ByName", mandatory: true, position: 0, valueFromPipeline: true },
        { set: "ById", mandatory: false, valueFromPipelineByPropertyName: true },
      ],
      completer: [
        { kind: "string", value: "eastus" },
        { kind: "string", value: "westus" },
      ],
      defaultValue: { kind: "string", value: "eastus" },
      validateSet: ["eastus", "westus"],
      deprecated: { message: "Use Location instead." },
      preview: { message: "This parameter is in preview." },
    });

    expect(result.success).toBe(true);
    assert(result.success);
    expect(result.data.parameterSets?.[0]?.valueFromPipeline).toBe(true);
    expect(result.data.parameterSets?.[1]?.valueFromPipelineByPropertyName).toBe(true);
    expect(result.data.completer).toHaveLength(2);
    expect(result.data.defaultValue).toEqual({ kind: "string", value: "eastus" });
  });

  it("rejects empty completion and validation sets", () => {
    expect(PsParameterDialectSchema.safeParse({ completer: [] }).success).toBe(false);
    expect(PsParameterDialectSchema.safeParse({ validateSet: [] }).success).toBe(false);
  });

  it.each([
    { kind: "string", value: "eastus" },
    { kind: "boolean", value: true },
    { kind: "number", value: 1 },
    { kind: "bigint", value: "1" },
    { kind: "null" },
  ])("rejects duplicate $kind completer values", (value) => {
    const result = PsParameterDialectSchema.safeParse({ completer: [value, value] });

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["completer", 1] }),
      ]));
    }
  });

  it("treats completer values with different literal kinds as distinct", () => {
    const result = PsParameterDialectSchema.safeParse({
      completer: [
        { kind: "string", value: "1" },
        { kind: "number", value: 1 },
        { kind: "bigint", value: "1" },
      ],
    });

    expect(result.success).toBe(true);
  });

  it("rejects duplicate ValidateSet values", () => {
    expect(PsParameterDialectSchema.safeParse({ validateSet: ["eastus", "eastus"] }).success).toBe(false);
  });

  it("rejects duplicate parameter-set memberships", () => {
    const result = PsParameterDialectSchema.safeParse({
      parameterSets: [
        { set: "ByName", mandatory: true },
        { set: "ByName", mandatory: false },
      ],
    });

    expect(result.success).toBe(false);
  });

  it("accepts one all-set fallback alongside named overrides", () => {
    const result = PsParameterDialectSchema.safeParse({
      parameterSets: [
        { mandatory: false },
        { set: "ByIdentity", mandatory: true, valueFromPipeline: true },
      ],
    });

    expect(result.success).toBe(true);
  });

  it("rejects removed parameter metadata", () => {
    expect(PsParameterDialectSchema.safeParse({ removed: true }).success).toBe(false);
  });

  it("rejects multiple all-set fallbacks", () => {
    const result = PsParameterDialectSchema.safeParse({
      parameterSets: [{ mandatory: false }, { mandatory: true }],
    });

    expect(result.success).toBe(false);
  });
});

describe("PowerShell parameter-set families", () => {
  const serverId = { module: "Widgets", namespace: [], name: "server" };
  const operationId = { module: "Widgets", namespace: [], name: "get" };
  const pathParameterId = { module: "Widgets", namespace: [], name: "get.resourceGroupName" };
  const apiVersion = { value: "2025-01-01", location: "query", parameterName: "api-version" };
  function document() {
    return {
      ...emptyIR(),
      sources: [{
        id: "Widgets",
        entrypoint: "main.tsp",
        apiVersion,
        rootNamespace: ["Widgets"],
        server: serverId,
      }],
      servers: [{ id: serverId, endpoint: { kind: "literal", url: "https://example.com" } }],
      operations: [{
        id: operationId,
        name: "get",
        source: "Widgets",
        method: "GET",
        pathTemplate: "/{resourceGroupName}",
        pathParameters: [{
          id: pathParameterId,
          name: "resourceGroupName",
          wireName: "resourceGroupName",
          type: { kind: "primitive", name: "string" },
          optional: false,
          nullable: false,
        }],
        queryParameters: [],
        headerParameters: [],
        responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
        apiVersion,
        server: serverId,
      }],
      commands: [{
        id: { module: "Widgets", subject: "Widget", action: "Get", variant: "ByName" },
        name: "GetWidgetByName",
        template: "atomic",
        server: serverId,
        steps: [{
          operation: operationId,
          request: { method: "GET", path: [], query: [], headers: [] },
          responses: [{ statusCodes: [{ kind: "exact", code: 200 }], isError: false }],
        }],
        parameters: [
          {
            name: "ResourceGroupName",
            source: { kind: "path", parameter: pathParameterId },
            type: { kind: "primitive", name: "string" },
            required: true,
            nullable: false,
          },
        ],
        surface: { paginated: false },
        dialect: {
          ps: {
            verb: "Get",
            subject: "AzWidget",
          },
        },
      }],
      target,
    };
  }

  function familyDocument(defaults: (string | undefined)[]) {
    const input = document();
    const command = input.commands[0]!;
    input.commands = defaults.map((defaultParameterSet, index) => ({
      ...command,
      id: { ...command.id, variant: `Variant${index}` },
      name: `GetWidgetVariant${index}`,
      parameters: command.parameters.map((parameter) => ({
        ...parameter,
        dialect: { ps: { parameterSets: [{ set: "ByName", mandatory: true }] } },
      })),
      dialect: {
        ps: {
          ...command.dialect.ps,
          defaultParameterSet,
        },
      },
    }));
    return input;
  }

  it("accepts sibling commands that agree on a declared family default", () => {
    expect(safeParseTransformedIR(familyDocument(["ByName", "ByName"])).success).toBe(true);
  });

  it("accepts a default covered by an omitted all-set membership", () => {
    const input = familyDocument(["ByName"]);
    delete input.commands[0]!.parameters[0]!.dialect.ps.parameterSets;

    expect(safeParseTransformedIR(input).success).toBe(true);
  });

  it("accepts a default covered by an unnamed all-set membership", () => {
    const input = familyDocument(["ByName"]);
    input.commands[0]!.parameters[0]!.dialect.ps.parameterSets = [{ mandatory: true }];

    expect(safeParseTransformedIR(input).success).toBe(true);
  });

  it("rejects a default that no visible parameter membership covers", () => {
    const result = safeParseTransformedIR(familyDocument(["Missing"]));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["commands", 0, "dialect", "ps", "defaultParameterSet"] }),
      ]));
    }
  });

  it("rejects a sibling that omits the family default", () => {
    const result = safeParseTransformedIR(familyDocument(["ByName", undefined]));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["commands", 1, "dialect", "ps", "defaultParameterSet"] }),
      ]));
    }
  });

  it("rejects conflicting sibling defaults", () => {
    const result = safeParseTransformedIR(familyDocument(["ByName", "ById"]));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["commands", 0, "dialect", "ps", "defaultParameterSet"] }),
        expect.objectContaining({ path: ["commands", 1, "dialect", "ps", "defaultParameterSet"] }),
      ]));
    }
  });

});