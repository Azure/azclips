import { z } from "zod";
import { IRIdSchema } from "./id.js";
import { IRLiteralValueSchema } from "./literal.js";

export const IRDeploymentBindingSchema = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("parameter"), parameter: IRIdSchema }).strict(),
  z.object({ kind: z.literal("literal"), value: z.string().min(1) }).strict(),
]);
export type IRDeploymentBinding = z.infer<typeof IRDeploymentBindingSchema>;

export const IRDeploymentResourceSchema = z.object({
  operation: IRIdSchema,
  resourceType: z.string().regex(/^[\w.-]+(?:\/[\w.-]+)+$/),
  apiVersion: z.string().min(1),
  writableScope: z.enum(["tenant", "managementGroup", "subscription", "resourceGroup", "resource"]),
  name: z.array(IRDeploymentBindingSchema).min(1),
  scopeParameters: z.array(IRIdSchema),
}).strict();
export type IRDeploymentResource = z.infer<typeof IRDeploymentResourceSchema>;

const numericBound = z.string().regex(/^-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?$/);
export const IRDeploymentFacetSchema = z.object({
  target: IRIdSchema,
  typePath: z.array(z.enum(["element", "value", "additionalProperties"])),
  minLength: z.number().int().nonnegative().max(2147483647).optional(),
  maxLength: z.number().int().nonnegative().max(2147483647).optional(),
  pattern: z.string().optional(),
  minValue: numericBound.optional(),
  maxValue: numericBound.optional(),
  minValueExclusive: numericBound.optional(),
  maxValueExclusive: numericBound.optional(),
  minItems: z.number().int().nonnegative().max(2147483647).optional(),
  maxItems: z.number().int().nonnegative().max(2147483647).optional(),
  sensitive: z.boolean().optional(),
  literalValue: IRLiteralValueSchema.optional(),
}).strict();
export type IRDeploymentFacet = z.infer<typeof IRDeploymentFacetSchema>;

export const IRDeploymentToolSchema = z.object({
  name: z.string().min(1),
  version: z.string().min(1),
}).strict();
export const IRDeploymentProvenanceSchema = z.object({
  sourceHash: z.string().regex(/^[a-f0-9]{64}$/),
  sourceCommit: z.string().regex(/^[a-f0-9]{40}$/).optional(),
  tools: z.array(IRDeploymentToolSchema).min(1),
}).strict();
export const IRDeploymentContractSchema = z.object({
  schemaVersion: z.literal("1.0.0"),
  resources: z.array(IRDeploymentResourceSchema),
  selection: z.array(z.string().regex(/^[\w.-]+(?:\/[\w.-]+)+@[^@\s]+$/)).min(1).optional(),
  facets: z.array(IRDeploymentFacetSchema),
  provenance: IRDeploymentProvenanceSchema,
}).strict();
export type IRDeploymentContract = z.infer<typeof IRDeploymentContractSchema>;
