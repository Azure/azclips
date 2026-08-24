/**
 * Milestone 1: hints.powershell vocabulary richness — round-trip tests for
 * ParameterHints' new powershell namespace and OperationHints' new fields.
 */

import { describe, expect, it } from "vitest";

import {
  ModelHintsSchema,
  ParameterHintsSchema,
  OperationHintsSchema,
  type ParameterHints,
  type OperationHints,
} from "../src/index.js";

describe("hints.powershell (ParameterHints) — Milestone 1", () => {
  it("round-trips a fully populated parameter powershell hint", () => {
    const hint: ParameterHints = {
      powershell: {
        mandatory: true,
        parameterSetName: "Get",
        helpMessage: "The name of the resource group.",
        position: 0,
        valueFromPipeline: true,
        valueFromPipelineByPropertyName: true,
        valueFromRemainingArguments: false,
        supportsWildcards: false,
        aliases: ["Name"],
        allowedValues: ["Incremental", "Complete"],
        completer: { script: "Get-Foo", name: "FooCompleter" },
        create: true,
        update: false,
        readOnly: false,
        dontShow: false,
        defaultValue: { script: "(Get-AzContext).Subscription.Id", name: "DefaultSubscriptionId" },
      },
    };

    expect(ParameterHintsSchema.parse(hint)).toEqual(hint);
  });

  it("rejects mutability flags of the wrong type", () => {
    expect(() => ParameterHintsSchema.parse({ powershell: { readOnly: "yes" } })).toThrow();
  });

  it("rejects a non-integer position", () => {
    expect(() => ParameterHintsSchema.parse({ powershell: { position: 1.5 } })).toThrow();
  });

  it("round-trips per-parameter-set mandatory/position/helpMessage overrides, keyed by set name", () => {
    const hint: ParameterHints = {
      powershell: {
        mandatory: false,
        parameterSets: {
          Get: { mandatory: true, position: 1, helpMessage: "The subscription to get." },
          List: { mandatory: false },
        },
      },
    };

    expect(ParameterHintsSchema.parse(hint)).toEqual(hint);
  });

  it("accepts a parameter hint with only flat mandatory/position/helpMessage (no parameterSets)", () => {
    const parsed = ParameterHintsSchema.parse({ powershell: { mandatory: true, position: 0 } });

    expect(parsed.powershell?.parameterSets).toBeUndefined();
  });

  it("rejects a parameterSets entry with the wrong field type", () => {
    expect(() =>
      ParameterHintsSchema.parse({ powershell: { parameterSets: { Get: { mandatory: "yes" } } } }),
    ).toThrow();
  });

  it("does not leak the parameter powershell namespace onto unrelated node kinds", () => {
    expect(() => ModelHintsSchema.parse({ powershell: { mandatory: true } })).toThrow();
  });
});

describe("hints.powershell (OperationHints) — Milestone 1 extensions", () => {
  it("round-trips alias/hidden/help/lifecycle/httpPath fields", () => {
    const hint: OperationHints = {
      powershell: {
        verb: "New",
        noun: "ResourceGroup",
        alias: ["nrg"],
        hidden: false,
        summary: "Creates a resource group.",
        description: "Creates a resource group in the specified location.",
        examples: ["New-AzResourceGroup -Name rg1 -Location eastus"],
        lifecycleTag: "GA",
        httpPath: {
          path: "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}",
          apiVersion: "2025-04-01",
        },
        supportsPaging: false,
        helpUri: "https://learn.microsoft.com/powershell/module/az.resources/new-azresourcegroup",
      },
    };

    expect(OperationHintsSchema.parse(hint)).toEqual(hint);
  });

  it("rejects an unrecognized lifecycle tag", () => {
    expect(() => OperationHintsSchema.parse({ powershell: { lifecycleTag: "Beta" } })).toThrow();
  });
});
