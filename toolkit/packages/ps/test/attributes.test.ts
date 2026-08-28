import { describe, expect, it } from "vitest";
import {
  PSCommandAttributeSchema,
  PSParameterAttributeSchema,
  PSModuleAttributeSchema,
} from "../src/attributes.js";

const opId = { module: "Test", name: "createOrUpdate", namespace: ["Fleets"] };

describe("PSCommandAttribute", () => {
  it("accepts a single-source command", () => {
    const result = PSCommandAttributeSchema.safeParse({
      verb: "New",
      noun: "Fleet",
      subjectPrefix: "Az",
      supportsShouldProcess: true,
      outputTypes: [],
      parameterSets: [{ name: "CreateOrUpdate", sourceOperationId: opId }],
      defaultParameterSetName: "CreateOrUpdate",
      sourceOperationIds: [opId],
    });
    expect(result.success).toBe(true);
  });

  it("accepts multiple source operations (GetPut-style merge)", () => {
    const result = PSCommandAttributeSchema.safeParse({
      verb: "New",
      noun: "Fleet",
      supportsShouldProcess: true,
      outputTypes: [],
      parameterSets: [{ name: "CreateOrUpdate", sourceOperationId: opId }],
      defaultParameterSetName: "CreateOrUpdate",
      sourceOperationIds: [opId, { ...opId, name: "get" }],
    });
    expect(result.success).toBe(true);
  });

  it("rejects a defaultParameterSetName that doesn't match any parameterSets entry", () => {
    const result = PSCommandAttributeSchema.safeParse({
      verb: "Get",
      noun: "Fleet",
      supportsShouldProcess: false,
      outputTypes: [],
      parameterSets: [{ name: "Get", sourceOperationId: opId }],
      defaultParameterSetName: "List",
      sourceOperationIds: [opId],
    });
    expect(result.success).toBe(false);
  });

  it("rejects an empty sourceOperationIds array", () => {
    const result = PSCommandAttributeSchema.safeParse({
      verb: "New",
      noun: "Fleet",
      supportsShouldProcess: true,
      outputTypes: [],
      parameterSets: [{ name: "CreateOrUpdate", sourceOperationId: opId }],
      defaultParameterSetName: "CreateOrUpdate",
      sourceOperationIds: [],
    });
    expect(result.success).toBe(false);
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
});

describe("PSModuleAttribute", () => {
  it("accepts a module description", () => {
    const result = PSModuleAttributeSchema.safeParse({
      description: "Fleet management cmdlets.",
      sourceId: "Microsoft.AzureFleet",
      cmdletsToExport: ["New-AzFleet", "Get-AzFleet"],
    });
    expect(result.success).toBe(true);
  });
});
