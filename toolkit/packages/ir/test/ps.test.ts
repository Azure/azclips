import { assert, describe, expect, it } from "vitest";
import {
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  PsCommandDialectSchema,
  PsModuleDialectSchema,
  PsParameterDialectSchema,
  IRCommandIdentitySchema,
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

describe("IRCommandIdentity", () => {
  it("declares a synthesized InputObject shared by commands", () => {
    const result = IRCommandIdentitySchema.safeParse({
      kind: "identity",
      id: { module: "ElasticSan", namespace: [], name: "ElasticSanIdentity" },
      name: "ElasticSanIdentity",
      properties: [
        {
          name: "ResourceGroupName",
          sourceParameters: [
            { module: "ElasticSan", namespace: [], name: "resourceGroupName" },
          ],
        },
      ],
    });

    expect(result.success).toBe(true);
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
          source: {
            kind: "synthetic",
            purpose: "identity",
            type: { module: "Widgets", namespace: [], name: "WidgetIdentity" },
            parameters: [operation],
          },
          type: { kind: "primitive", name: "string" },
          required: true,
          nullable: false,
          dialect: { ps: { parameterSets: [{ set: "ByIdentity", mandatory: true }] } },
        },
      ],
      surface: { paginated: false },
      dialect: {
        ps: { verb: "Get", noun: "AzWidget", defaultParameterSet: "ByName" },
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
  it("records the canonical path parameters supplied by an InputObject", () => {
    const result = IRCommandParameterSourceSchema.safeParse({
      kind: "synthetic",
      purpose: "identity",
      type: { module: "Widgets", namespace: [], name: "WidgetIdentity" },
      parameters: [
        { module: "Widgets", namespace: [], name: "subscriptionId" },
        { module: "Widgets", namespace: [], name: "resourceGroupName" },
      ],
    });

    expect(result.success).toBe(true);
  });
});

describe("PowerShell shared identity references", () => {
  const identityId = { module: "Widgets", namespace: [], name: "WidgetIdentity" };
  const serverId = { module: "Widgets", namespace: [], name: "server" };
  const operationId = { module: "Widgets", namespace: [], name: "get" };
  const pathParameterId = { module: "Widgets", namespace: [], name: "get.resourceGroupName" };
  const apiVersion = { value: "2025-01-01", location: "query", parameterName: "api-version" };
  const identity = {
    kind: "identity" as const,
    id: identityId,
    name: "WidgetIdentity",
    properties: [],
  };

  function document({
    sharedTypes = [identity],
    parameterType = identityId,
    sourceParameters = [pathParameterId],
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
          source: {
            kind: "synthetic",
            purpose: "identity",
            type: identityId,
            parameters: sourceParameters,
          },
          type: { kind: "model", id: parameterType },
          required: true,
          nullable: false,
        }],
        surface: { paginated: false },
      }],
      sharedTypes,
      target,
    };
  }

  it("rejects duplicate shared type ids", () => {
    const result = safeParseTransformedIR({
      ...emptyIR(),
      commands: [],
      sharedTypes: [identity, identity],
      target,
    });

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["sharedTypes", 1, "id"] }),
      ]));
    }
  });

  it("rejects a synthetic parameter that references a missing shared identity", () => {
    const result = safeParseTransformedIR(document({ sharedTypes: [] }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["commands", 0, "parameters", 0, "source", "type"] }),
      ]));
    }
  });

  it("accepts a synthetic parameter bound to its shared identity and canonical path", () => {
    expect(safeParseTransformedIR(document()).success).toBe(true);
  });

  it("rejects a synthetic parameter whose model type differs from its shared identity", () => {
    const result = safeParseTransformedIR(document({
      parameterType: { ...identityId, name: "OtherIdentity" },
    }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["commands", 0, "parameters", 0, "type"] }),
      ]));
    }
  });

  it("rejects a synthetic parameter bound to a non-path declaration", () => {
    const result = safeParseTransformedIR(document({ sourceParameters: [operationId] }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({
          path: ["commands", 0, "parameters", 0, "source", "parameters", 0],
        }),
      ]));
    }
  });

  it("rejects a shared identity property bound to a non-path declaration", () => {
    const result = safeParseTransformedIR(document({
      sharedTypes: [{
        ...identity,
        properties: [{ name: "ResourceGroupName", sourceParameters: [operationId] }],
      }],
    }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({
          path: ["sharedTypes", 0, "properties", 0, "sourceParameters", 0],
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