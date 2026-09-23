import { assert, describe, expect, it } from "vitest";
import {
  PSCompleterAttributeSchema,
  PSCompleterContractSchema,
  PSDefaultValueAttributeSchema,
  PSDefaultValueContractSchema,
  PowerShellContractProjections,
  PowerShellContractSchemas,
  PsCommandDialectSchema,
  PsModuleDialectSchema,
  PsParameterDialectSchema,
  IRCommandIdentitySchema,
  IRCommandSchema,
  IRCommandParameterSourceSchema,
  emptyIR,
  safeParseTransformedIR,
  TransformedIRSchema,
  type TransformedIR,
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
      properties: [{
        name: "ResourceGroupName",
        sourceParameters: [
          { module: "ElasticSan", namespace: [], name: "resourceGroupName" },
        ],
      }],
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
      steps: [{
        operation,
        request: { method: "GET", path: [], query: [], headers: [] },
        responses: [{ statusCodes: [{ kind: "exact", code: 200 }], isError: false }],
      }],
      parameters: [{
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
      }],
      surface: { paginated: false },
      dialect: { ps: { verb: "Get", noun: "AzWidget", defaultParameterSet: "ByName" } },
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
  const identity = {
    kind: "identity" as const,
    id: identityId,
    name: "WidgetIdentity",
    properties: [],
  };

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
    const operation = { module: "Widgets", namespace: [], name: "get" };
    const result = safeParseTransformedIR({
      ...emptyIR(),
      commands: [{
        id: { module: "Widgets", subject: "Widget", action: "Get", variant: "ByIdentity" },
        name: "GetWidgetByIdentity",
        template: "atomic",
        server: { module: "Widgets", namespace: [], name: "server" },
        steps: [{
          operation,
          request: { method: "GET", path: [], query: [], headers: [] },
          responses: [{ statusCodes: [{ kind: "exact", code: 200 }], isError: false }],
        }],
        parameters: [{
          name: "InputObject",
          source: {
            kind: "synthetic",
            purpose: "identity",
            type: identityId,
            parameters: [operation],
          },
          type: { kind: "primitive", name: "string" },
          required: true,
          nullable: false,
        }],
        surface: { paginated: false },
      }],
      sharedTypes: [],
      target,
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues).toEqual(expect.arrayContaining([
        expect.objectContaining({ path: ["commands", 0, "parameters", 0, "source", "type"] }),
      ]));
    }
  });
});

describe("PowerShell CLR contract projections", () => {
  const expectedContracts = [
    "PSCommandAttribute",
    "PSParameterSetAttribute",
    "PSParameterAttribute",
    "PSModuleAttribute",
    "PSIdentityAttribute",
    "PSIdentityPropertyAttribute",
    "PSCompleterAttribute",
    "PSDefaultValueAttribute",
  ];

  it("registers all eight standalone contract schemas and projections", () => {
    expect(Object.keys(PowerShellContractSchemas)).toEqual(expectedContracts);
    expect(Object.keys(PowerShellContractProjections)).toEqual(expectedContracts);
    expect(Object.values(PowerShellContractProjections).map((projection) => projection.name)).toEqual(
      expectedContracts,
    );
  });

  it("uses the projected completer and default schemas in canonical dialects", () => {
    expect(PSCompleterAttributeSchema).toBe(PSCompleterContractSchema);
    expect(PSDefaultValueAttributeSchema).toBe(PSDefaultValueContractSchema);
  });

  it("preserves contextual constructor fields for later attribute emission", () => {
    expect(PowerShellContractProjections.PSParameterAttribute.constructor).toEqual([
      { name: "commandName", type: "string" },
      { name: "parameterSetName", type: "string" },
      { name: "name", source: "name" },
    ]);
    expect(PowerShellContractProjections.PSDefaultValueAttribute.constructor).toEqual([
      { name: "commandName", type: "string" },
      { name: "parameterSetName", type: "string" },
    ]);
  });
  it.each([
    ["script", { script: "Get-Thing" }],
    ["name", { name: "LocationCompleter" }],
    ["description", { description: "Completes locations." }],
  ])("accepts a completer carrying only %s", (_field, value) => {
    expect(PSCompleterAttributeSchema.safeParse(value).success).toBe(true);
  });

  it("rejects an empty completer and a default value without a script", () => {
    expect(PSCompleterAttributeSchema.safeParse({}).success).toBe(false);
    expect(PSDefaultValueAttributeSchema.safeParse({ name: "Subscription" }).success).toBe(false);
  });
});

describe("TransformedIR PowerShell ownership", () => {
  it("has no ps property in either its TypeScript contract or strict root schema", () => {
    const transformedIrHasPs: "ps" extends keyof TransformedIR ? true : false = false;
    expect(transformedIrHasPs).toBe(false);

    const result = TransformedIRSchema.safeParse({ ps: { commands: [], modules: [] } });
    expect(result.success).toBe(false);
    assert(!result.success);
    expect(result.error.issues).toContainEqual(
      expect.objectContaining({ code: "unrecognized_keys", keys: ["ps"] }),
    );
  });
});