import { assert, describe, expect, it } from "vitest";
import {
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  PsCommandDialectSchema,
  PsModuleDialectSchema,
  PsParameterDialectSchema,
  PsCommandIdentitySchema,
  IRCommandSchema,
  IRCommandParameterSourceSchema,
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

describe("PsCommandIdentity", () => {
  it("maps synthesized model properties to canonical path parameters", () => {
    const result = PsCommandIdentitySchema.safeParse({
      parameter: "InputObject",
      properties: [
        {
          property: { module: "ElasticSan", namespace: [], name: "ElasticSanIdentity.resourceGroupName" },
          sourceParameters: [
            { module: "ElasticSan", namespace: [], name: "resourceGroupName" },
          ],
        },
      ],
    });

    expect(result.success).toBe(true);
  });

  it("rejects identity metadata with no property bindings", () => {
    const result = PsCommandIdentitySchema.safeParse({ parameter: "InputObject", properties: [] });

    expect(result.success).toBe(false);
  });
});

describe("PsCommandDialect", () => {
  it("accepts PowerShell processor metadata on a canonical command dialect", () => {
    const result = PsCommandDialectSchema.safeParse({
      verb: "Remove",
      noun: "AzWidget",
      aliases: ["Remove-AzLegacyWidget"],
      defaultParameterSet: "ByName",
      supportsShouldProcess: true,
      confirmImpact: "High",
      examples: ["Remove-AzWidget -Name example"],
      lifecycleTag: "GA",
      helpUri: "https://learn.microsoft.com/powershell/module/az.widgets/remove-azwidget",
      supportsPaging: true,
      outputTypes: [
        { kind: "model", id: { module: "Widgets", namespace: [], name: "Widget" } },
      ],
      httpPath: { path: "/widgets/{name}", apiVersion: "2025-01-01" },
    });

    expect(result.success).toBe(true);
  });

  it("rejects an unknown ConfirmImpact value", () => {
    const result = PsCommandDialectSchema.safeParse({
      verb: "Remove",
      noun: "AzWidget",
      confirmImpact: "Critical",
    });

    expect(result.success).toBe(false);
  });

  it("does not require each sibling to declare the family default locally", () => {
    const operation = { module: "Widgets", namespace: [], name: "getByIdentity" };
    const result = IRCommandSchema.safeParse({
      id: { module: "Widgets", subject: "Widget", action: "Get", variant: "ByIdentity" },
      name: "GetWidgetByIdentity",
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
      parameters: [
        {
          name: "InputObject",
          source: { kind: "synthetic" },
          type: {
            kind: "model",
            id: { module: "Widgets", namespace: [], name: "WidgetIdentity" },
          },
          required: true,
          nullable: false,
          dialect: { ps: { parameterSets: [{ set: "ByIdentity", mandatory: true }] } },
        },
      ],
      surface: { paginated: false },
      dialect: {
        ps: {
          verb: "Get",
          noun: "AzWidget",
          defaultParameterSet: "ByName",
          identity: {
            parameter: "InputObject",
            properties: [{
              property: { module: "Widgets", namespace: [], name: "WidgetIdentity.resourceGroupName" },
              sourceParameters: [],
            }],
          },
        },
      },
    });

    expect(result.success).toBe(true);
  });
});

describe("PsParameterDialect", () => {
  it("accepts per-set bindings, completion, and a script-derived default", () => {
    const result = PsParameterDialectSchema.safeParse({
      name: "ResourceGroupName",
      aliases: ["ResourceGroup"],
      parameterSets: [
        { set: "ByName", mandatory: true, position: 0, valueFromPipeline: true },
        { set: "ById", mandatory: false, valueFromPipelineByPropertyName: true },
      ],
      completer: { name: "ResourceGroupCompleter" },
      defaultValue: {
        script: "(Get-AzContext).Subscription.Id",
        setCondition: "$PSBoundParameters.ContainsKey('SubscriptionId')",
      },
    });

    expect(result.success).toBe(true);
    assert(result.success);
    expect(result.data.parameterSets?.[0]?.valueFromPipeline).toBe(true);
    expect(result.data.parameterSets?.[1]?.valueFromPipelineByPropertyName).toBe(true);
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
      removed: false,
      parameterSets: [
        { mandatory: false },
        { set: "ByIdentity", mandatory: true, valueFromPipeline: true },
      ],
    });

    expect(result.success).toBe(true);
  });

  it("rejects multiple all-set fallbacks", () => {
    const result = PsParameterDialectSchema.safeParse({
      parameterSets: [{ mandatory: false }, { mandatory: true }],
    });

    expect(result.success).toBe(false);
  });
});

describe("PowerShell synthetic parameters", () => {
  it("marks a parameter as transform-synthesized without client-specific semantics", () => {
    const result = IRCommandParameterSourceSchema.safeParse({ kind: "synthetic" });

    expect(result.success).toBe(true);
  });
});

describe("PowerShell command identity references", () => {
  const identityId = { module: "Widgets", namespace: [], name: "WidgetIdentity" };
  const identityPropertyId = { module: "Widgets", namespace: [], name: "WidgetIdentity.resourceGroupName" };
  const serverId = { module: "Widgets", namespace: [], name: "server" };
  const operationId = { module: "Widgets", namespace: [], name: "get" };
  const pathParameterId = { module: "Widgets", namespace: [], name: "get.resourceGroupName" };
  const apiVersion = { value: "2025-01-01", location: "query", parameterName: "api-version" };
  const identityModel = {
    id: identityId,
    name: "WidgetIdentity",
    source: "Widgets",
    properties: [{
      id: identityPropertyId,
      name: "ResourceGroupName",
      wireName: "resourceGroupName",
      typeKind: "primitive" as const,
      type: { kind: "primitive" as const, name: "string" as const },
      optional: false,
      nullable: false,
    }],
  };

  function document({
    parameterType = identityId,
    sourceParameters = [pathParameterId],
    identityParameter = "InputObject",
    property = identityPropertyId,
  } = {}) {
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
      models: [identityModel],
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
        id: { module: "Widgets", subject: "Widget", action: "Get", variant: "ByIdentity" },
        name: "GetWidgetByIdentity",
        template: "atomic",
        server: serverId,
        steps: [{
          operation: operationId,
          request: { method: "GET", path: [], query: [], headers: [] },
          responses: [{ statusCodes: [{ kind: "exact", code: 200 }], isError: false }],
        }],
        parameters: [{
          name: "InputObject",
          source: { kind: "synthetic" },
          type: { kind: "model", id: parameterType },
          required: true,
          nullable: false,
        }],
        surface: { paginated: false },
        dialect: {
          ps: {
            verb: "Get",
            noun: "AzWidget",
            identity: {
              parameter: identityParameter,
              properties: [{ property, sourceParameters }],
            },
          },
        },
      }],
      target,
    };
  }

  it("rejects an identity that names no synthetic model parameter", () => {
    const result = safeParseTransformedIR(document({ identityParameter: "Missing" }));

    expect(result.success).toBe(false);
    if (!result.success) {
      const identityIssues = result.error.issues.filter((issue) => issue.path.includes("identity"));
      expect(identityIssues).toEqual([
        expect.objectContaining({ path: ["commands", 0, "dialect", "ps", "identity", "parameter"] }),
      ]);
    }
  });

  it("accepts a synthetic model parameter with command-specific identity bindings", () => {
    expect(safeParseTransformedIR(document()).success).toBe(true);
  });

  it("rejects an identity parameter whose model does not exist", () => {
    const result = safeParseTransformedIR(document({
      parameterType: { ...identityId, name: "OtherIdentity" },
    }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["commands", 0, "parameters", 0, "type", "id"] }),
      ]));
    }
  });

  it("rejects an identity property bound to a non-path declaration", () => {
    const result = safeParseTransformedIR(document({ sourceParameters: [operationId] }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({
          path: ["commands", 0, "dialect", "ps", "identity", "properties", 0, "sourceParameters", 0],
        }),
      ]));
    }
  });

  it("rejects an identity property outside the parameter model", () => {
    const result = safeParseTransformedIR(document({ property: pathParameterId }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({
          path: ["commands", 0, "dialect", "ps", "identity", "properties", 0, "property"],
        }),
      ]));
    }
  });
});

describe("PowerShell attribute metadata", () => {
  it.each([
    ["script", { script: "Get-Thing" }],
    ["name", { name: "LocationCompleter" }],
    ["description", { description: "Completes locations." }],
  ])("accepts a completer carrying only %s", (_field, value) => {
    expect(PSCompleterAttributeSchema.safeParse(value).success).toBe(true);
  });

  it("rejects an empty completer", () => {
    expect(PSCompleterAttributeSchema.safeParse({}).success).toBe(false);
  });

  it("requires a script for a default value", () => {
    expect(PSDefaultValueAttributeSchema.safeParse({ name: "Subscription" }).success).toBe(false);
  });
});