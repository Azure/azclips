/** Standalone schema projections for PowerShell runtime CLR attributes. */

import { z } from "zod";
import { defineCSharpAttribute, type CSharpAttributeProjection } from "./csharp-attribute.js";

export const ConfirmImpacts = ["None", "Low", "Medium", "High"] as const;
export type ConfirmImpact = (typeof ConfirmImpacts)[number];

export const LifecycleTags = ["Preview", "Deprecated", "Experimental", "GA"] as const;
export type LifecycleTag = (typeof LifecycleTags)[number];

const PSCommandContractDefinition = defineCSharpAttribute(
  "PSCommandAttribute",
  {
    verb: { schema: z.string().min(1), attribute: { kind: "constructor" } },
    noun: { schema: z.string().min(1), attribute: { kind: "constructor" } },
    defaultParameterSetName: { schema: z.string().min(1), attribute: { kind: "constructor" } },
    supportsShouldProcess: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
    confirmImpact: {
      schema: z.enum(ConfirmImpacts).optional(),
      attribute: { kind: "property", type: { clr: "PSConfirmImpact" } },
    },
    aliases: { schema: z.array(z.string().min(1)).optional(), attribute: { kind: "property", default: "[]" } },
    hidden: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
    summary: { schema: z.string().optional(), attribute: { kind: "property", default: "string.Empty" } },
    description: { schema: z.string().optional(), attribute: { kind: "property", default: "string.Empty" } },
    examples: { schema: z.array(z.string()).optional(), attribute: { kind: "property", default: "[]" } },
    tags: {
      schema: z.array(z.enum(LifecycleTags)).optional(),
      attribute: { kind: "property", type: { clr: "Core.Abstractions.HelpTag[]" }, default: "[]" },
    },
    supportsPaging: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
    helpUri: { schema: z.string().optional(), attribute: { kind: "property", default: "string.Empty" } },
  },
  {
    targets: ["Class"],
    allowMultiple: true,
    enums: [{ name: "PSConfirmImpact", members: ConfirmImpacts }],
    properties: [
      { name: "HttpPath", type: "string", default: "string.Empty" },
      { name: "ApiVersion", type: "string", default: "string.Empty" },
      { name: "OutputTypes", type: { clr: "Type[]" }, default: "[]" },
    ],
  },
);

export const PSCommandContractSchema = PSCommandContractDefinition.schema.strict();

const PSParameterSetContractDefinition = defineCSharpAttribute(
  "PSParameterSetAttribute",
  {
    name: { schema: z.string().min(1), attribute: { kind: "constructor" } },
  },
  {
    targets: ["Class"],
    allowMultiple: true,
    context: [{ name: "commandName", type: "string" }],
  },
);

export const PSParameterSetContractSchema = PSParameterSetContractDefinition.schema.strict();

const PSParameterContractDefinition = defineCSharpAttribute(
  "PSParameterAttribute",
  {
    name: { schema: z.string().min(1), attribute: { kind: "constructor" } },
    mandatory: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
    hidden: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
    helpMessage: { schema: z.string().optional(), attribute: { kind: "property", default: "string.Empty" } },
    position: { schema: z.number().int().optional(), attribute: { kind: "property", default: "int.MinValue" } },
    valueFromPipeline: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
    valueFromPipelineByPropertyName: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
    aliases: { schema: z.array(z.string().min(1)).optional(), attribute: { kind: "property", default: "[]" } },
    isIdentity: { schema: z.boolean().optional(), attribute: { kind: "property", default: "false" } },
  },
  {
    targets: ["Property"],
    allowMultiple: true,
    context: [
      { name: "commandName", type: "string" },
      { name: "parameterSetName", type: "string" },
    ],
  },
);

export const PSParameterContractSchema = PSParameterContractDefinition.schema.strict();

const PSModuleContractDefinition = defineCSharpAttribute(
  "PSModuleAttribute",
  {
    name: { schema: z.string().min(1), attribute: { kind: "constructor" } },
    sourceId: { schema: z.string().min(1), attribute: { kind: "constructor" } },
    description: { schema: z.string().optional(), attribute: { kind: "property", default: "string.Empty" } },
    cmdletsToExport: { schema: z.array(z.string().min(1)).optional(), attribute: { kind: "property", default: "[]" } },
  },
  { targets: ["Assembly"], allowMultiple: true, inherited: true },
);

export const PSModuleContractSchema = PSModuleContractDefinition.schema.strict();

const PSIdentityContractDefinition = defineCSharpAttribute(
  "PSIdentityAttribute",
  {
    name: { schema: z.string().min(1), attribute: { kind: "constructor" } },
  },
  { targets: ["Class"] },
);

export const PSIdentityContractSchema = PSIdentityContractDefinition.schema.strict();

const PSIdentityPropertyContractDefinition = defineCSharpAttribute(
  "PSIdentityPropertyAttribute",
  {
    name: { schema: z.string().min(1), attribute: { kind: "constructor" } },
    helpMessage: { schema: z.string().optional(), attribute: { kind: "property", default: "string.Empty" } },
  },
  { targets: ["Property"] },
);

export const PSIdentityPropertyContractSchema = PSIdentityPropertyContractDefinition.schema.strict();

const PSCompleterContractDefinition = defineCSharpAttribute(
  "PSCompleterAttribute",
  {
    script: { schema: z.string().min(1).optional(), attribute: { kind: "property", default: "string.Empty" } },
    name: { schema: z.string().min(1).optional(), attribute: { kind: "property", default: "string.Empty" } },
    description: { schema: z.string().min(1).optional(), attribute: { kind: "property", default: "string.Empty" } },
  },
  {
    targets: ["Class", "Property"],
    allowMultiple: true,
    context: [{ name: "commandName", type: "string" }],
    properties: [{ name: "ParameterSetName", type: "string", default: "string.Empty" }],
  },
);

export const PSCompleterContractSchema = PSCompleterContractDefinition.schema
  .strict()
  .refine((hint) => hint.script !== undefined || hint.name !== undefined || hint.description !== undefined, {
    message: "PSCompleterAttribute requires at least one of script/name/description.",
  });

const PSDefaultValueContractDefinition = defineCSharpAttribute(
  "PSDefaultValueAttribute",
  {
    script: { schema: z.string().min(1), attribute: { kind: "property", required: true } },
    name: { schema: z.string().min(1).optional(), attribute: { kind: "property", default: "string.Empty" } },
    description: { schema: z.string().min(1).optional(), attribute: { kind: "property", default: "string.Empty" } },
    setCondition: { schema: z.string().min(1).optional(), attribute: { kind: "property", default: "string.Empty" } },
  },
  {
    targets: ["Property"],
    allowMultiple: true,
    context: [
      { name: "commandName", type: "string" },
      { name: "parameterSetName", type: "string" },
    ],
  },
);

export const PSDefaultValueContractSchema = PSDefaultValueContractDefinition.schema.strict();

export const PowerShellContractSchemas = {
  PSCommandAttribute: PSCommandContractSchema,
  PSParameterSetAttribute: PSParameterSetContractSchema,
  PSParameterAttribute: PSParameterContractSchema,
  PSModuleAttribute: PSModuleContractSchema,
  PSIdentityAttribute: PSIdentityContractSchema,
  PSIdentityPropertyAttribute: PSIdentityPropertyContractSchema,
  PSCompleterAttribute: PSCompleterContractSchema,
  PSDefaultValueAttribute: PSDefaultValueContractSchema,
};

export const PowerShellContractProjections = {
  PSCommandAttribute: PSCommandContractDefinition.projection,
  PSParameterSetAttribute: PSParameterSetContractDefinition.projection,
  PSParameterAttribute: PSParameterContractDefinition.projection,
  PSModuleAttribute: PSModuleContractDefinition.projection,
  PSIdentityAttribute: PSIdentityContractDefinition.projection,
  PSIdentityPropertyAttribute: PSIdentityPropertyContractDefinition.projection,
  PSCompleterAttribute: PSCompleterContractDefinition.projection,
  PSDefaultValueAttribute: PSDefaultValueContractDefinition.projection,
} satisfies Record<keyof typeof PowerShellContractSchemas, CSharpAttributeProjection>;