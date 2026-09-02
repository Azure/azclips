import { describe, expect, it } from "vitest";
import {
  PSCommandAttributeSchema,
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  PSParameterAttributeSchema,
  PSModuleAttributeSchema,
  PSSchema,
} from "../src/ps.js";
import { IRSchema, emptyIR } from "../src/ir.js";

const opId = { module: "Test", name: "createOrUpdate", namespace: ["Fleets"] };
const paramId = { module: "Test", name: "createOrUpdate#name", namespace: ["Fleets"] };

/** A minimal valid command; individual tests override the fields they exercise. */
const command = (overrides: Record<string, unknown> = {}) => ({
  verb: "New",
  noun: "Fleet",
  subject: "Fleet",
  supportsShouldProcess: true,
  outputTypes: [],
  parameterSets: [{ name: "CreateOrUpdate", sourceOperationId: opId }],
  parameters: [],
  defaultParameterSetName: "CreateOrUpdate",
  sourceOperationIds: [opId],
  ...overrides,
});

describe("PSCommandAttribute", () => {
  it("accepts a single-source command", () => {
    expect(PSCommandAttributeSchema.safeParse(command({ subjectPrefix: "Az" })).success).toBe(true);
  });

  it("accepts multiple source operations (GetPut-style merge)", () => {
    const result = PSCommandAttributeSchema.safeParse(
      command({ sourceOperationIds: [opId, { ...opId, name: "get" }] }),
    );
    expect(result.success).toBe(true);
  });

  it("accepts nested parameters that reference a declared set", () => {
    const result = PSCommandAttributeSchema.safeParse(
      command({
        parameters: [
          { name: "Name", parameterSetName: "CreateOrUpdate", sourceParameterId: paramId },
        ],
      }),
    );
    expect(result.success).toBe(true);
  });

  it("rejects a parameter whose parameterSetName is not one of the command's sets", () => {
    const result = PSCommandAttributeSchema.safeParse(
      command({
        parameters: [{ name: "Name", parameterSetName: "List", sourceParameterId: paramId }],
      }),
    );
    expect(result.success).toBe(false);
  });

  it("rejects a per-set override keyed by an undeclared set", () => {
    const result = PSCommandAttributeSchema.safeParse(
      command({
        parameters: [
          { name: "Name", parameterSets: { List: { mandatory: true } }, sourceParameterId: paramId },
        ],
      }),
    );
    expect(result.success).toBe(false);
  });

  it("rejects a defaultParameterSetName that doesn't match any parameterSets entry", () => {
    const result = PSCommandAttributeSchema.safeParse(
      command({ verb: "Get", supportsShouldProcess: false, defaultParameterSetName: "List" }),
    );
    expect(result.success).toBe(false);
  });

  it("rejects an empty sourceOperationIds array", () => {
    expect(PSCommandAttributeSchema.safeParse(command({ sourceOperationIds: [] })).success).toBe(false);
  });
});

describe("PSParameterAttribute", () => {
  it("accepts per-parameter-set overrides", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "resourceGroupName",
      sourceParameterId: paramId,
      parameterSets: {
        Get: { mandatory: true },
        List: { mandatory: false },
      },
    });
    expect(result.success).toBe(true);
  });

  it("rejects a parameter-set override keyed by an empty string", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "resourceGroupName",
      sourceParameterId: paramId,
      parameterSets: { "": { mandatory: true } },
    });
    expect(result.success).toBe(false);
  });

  it("rejects an empty override map, which would name no set at all", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "resourceGroupName",
      sourceParameterId: paramId,
      parameterSets: {},
    });
    expect(result.success).toBe(false);
  });

  it("accepts a parameter in every set, which names neither field", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "SubscriptionId",
      sourceParameterId: paramId,
    });
    expect(result.success).toBe(true);
  });

  it("accepts a hidden parameter, which is kept but not exported", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "managedByResourceId",
      sourceParameterId: paramId,
      hidden: true,
    });
    expect(result.success).toBe(true);
  });

  it("accepts an identity parameter, which has no IR node to point at", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "InputObject",
      identity: true,
      mandatory: true,
      valueFromPipeline: true,
      parameterSetName: "GetViaIdentity",
    });
    expect(result.success).toBe(true);
  });

  it("rejects a parameter that is neither identity-bound nor sourced from IR", () => {
    expect(PSParameterAttributeSchema.safeParse({ name: "Orphan" }).success).toBe(false);
  });

  it("rejects a parameter that both names one set and spans several", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "Name",
      sourceParameterId: paramId,
      parameterSetName: "Get",
      parameterSets: { List: { mandatory: false } },
    });
    expect(result.success).toBe(false);
  });

  it("rejects a parameter claiming both an IR source and identity binding", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "InputObject",
      identity: true,
      sourceParameterId: paramId,
    });
    expect(result.success).toBe(false);
  });
});

describe("PSCompleterAttribute", () => {
  it.each([
    ["script", { script: "Get-Thing" }],
    ["name", { name: "LocationCompleter" }],
    ["description", { description: "Completes locations." }],
  ])("accepts a completer carrying only %s", (_field, value) => {
    expect(PSCompleterAttributeSchema.safeParse(value).success).toBe(true);
  });

  it("rejects a completer with none of script/name/description", () => {
    expect(PSCompleterAttributeSchema.safeParse({}).success).toBe(false);
  });
});

describe("PSDefaultValueAttribute", () => {
  it("accepts a default value carrying a script", () => {
    const result = PSDefaultValueAttributeSchema.safeParse({
      script: "(Get-AzContext).Subscription.Id",
      setCondition: "$PSBoundParameters.ContainsKey('SubscriptionId')",
    });
    expect(result.success).toBe(true);
  });

  it("rejects a default value with no script", () => {
    expect(PSDefaultValueAttributeSchema.safeParse({ name: "Subscription" }).success).toBe(false);
  });
});

describe("PSModuleAttribute", () => {
  it("accepts a module description", () => {
    const result = PSModuleAttributeSchema.safeParse({
      name: "AzureFleet",
      description: "Fleet management cmdlets.",
      sourceId: "Microsoft.AzureFleet",
      cmdletsToExport: ["New-AzFleet", "Get-AzFleet"],
    });
    expect(result.success).toBe(true);
  });

  it("accepts a synthesized identity whose properties span several operations", () => {
    const result = PSModuleAttributeSchema.safeParse({
      name: "AzureFleet",
      sourceId: "Microsoft.AzureFleet",
      cmdletsToExport: ["Get-AzFleet"],
      identity: {
        name: "AzureFleetIdentity",
        properties: [
          { name: "FleetName", helpMessage: "The name of the Fleet.", sourceParameterIds: [paramId] },
          // The ARM resource id stands for the whole path, so no single parameter backs it.
          { name: "Id", helpMessage: "Resource identity path", sourceParameterIds: [] },
        ],
      },
    });
    expect(result.success).toBe(true);
  });

  it("rejects a module with no name, which could not name a .psd1", () => {
    const result = PSModuleAttributeSchema.safeParse({
      sourceId: "Microsoft.AzureFleet",
      cmdletsToExport: ["Get-AzFleet"],
    });
    expect(result.success).toBe(false);
  });
});

describe("PS surface", () => {
  const surface = (overrides: Record<string, unknown> = {}) => ({
    commands: [command()],
    modules: [{ name: "AzureFleet", sourceId: "Microsoft.AzureFleet", cmdletsToExport: ["New-AzFleet"] }],
    ...overrides,
  });

  it("accepts commands and modules", () => {
    expect(PSSchema.safeParse(surface()).success).toBe(true);
  });

  it("accepts an empty surface", () => {
    expect(PSSchema.safeParse(surface({ commands: [], modules: [] })).success).toBe(true);
  });

  it("rejects a top-level parameter, which is only reachable through its command", () => {
    expect(PSSchema.safeParse(surface({ parameter: { name: "Name" } })).success).toBe(false);
  });

  it("hangs off an IR document, and is optional there", () => {
    // `emptyIR()` alone does not validate -- IRSchema requires at least one source.
    const ir = {
      ...emptyIR(),
      sources: [
        {
          id: "Microsoft.AzureFleet",
          entrypoint: "main.tsp",
          apiVersion: { value: "2025-04-01", location: "query" as const, parameterName: "api-version" },
          server: { kind: "literal" as const, url: "https://management.azure.com" },
          rootNamespace: ["Microsoft", "AzureFleet"],
        },
      ],
    };

    expect(IRSchema.safeParse(ir).success).toBe(true);
    expect(IRSchema.safeParse({ ...ir, ps: surface() }).success).toBe(true);
    expect(IRSchema.safeParse({ ...ir, ps: { commands: [] } }).success).toBe(false);
  });
});

