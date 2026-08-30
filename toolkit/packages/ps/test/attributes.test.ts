import { describe, expect, it } from "vitest";
import {
  PSCommandAttributeSchema,
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  PSParameterAttributeSchema,
  PSModuleAttributeSchema,
} from "../src/attributes.js";

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
      sourceParameterId: opId,
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
      sourceParameterId: opId,
      parameterSets: { "": { mandatory: true } },
    });
    expect(result.success).toBe(false);
  });

  it("accepts a hidden parameter, which is kept but not exported", () => {
    const result = PSParameterAttributeSchema.safeParse({
      name: "managedByResourceId",
      sourceParameterId: opId,
      hidden: true,
    });
    expect(result.success).toBe(true);
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
  it("accepts a module description", () => {    const result = PSModuleAttributeSchema.safeParse({
      description: "Fleet management cmdlets.",
      sourceId: "Microsoft.AzureFleet",
      cmdletsToExport: ["New-AzFleet", "Get-AzFleet"],
    });
    expect(result.success).toBe(true);
  });
});
