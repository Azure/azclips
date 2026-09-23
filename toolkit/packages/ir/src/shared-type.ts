import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";

/** One property exposed by a synthesized PowerShell InputObject type. */
export type IRCommandIdentityProperty = {
  name: string;
  description?: string | undefined;
  /** Canonical path parameters this property can satisfy. Empty for a synthesized resource id. */
  sourceParameters: IRId[];
};

export const IRCommandIdentityPropertySchema: z.ZodType<IRCommandIdentityProperty> = z
  .object({
    name: z.string().min(1),
    description: z.string().optional(),
    sourceParameters: z.array(IRIdSchema),
  })
  .strict();

/** A shared synthesized InputObject declaration used by command parameters. */
export type IRCommandIdentity = {
  kind: "identity";
  id: IRId;
  name: string;
  properties: IRCommandIdentityProperty[];
};

export const IRCommandIdentitySchema: z.ZodType<IRCommandIdentity> = z
  .object({
    kind: z.literal("identity"),
    id: IRIdSchema,
    name: z.string().min(1),
    properties: z.array(IRCommandIdentityPropertySchema),
  })
  .strict();

/** A type shared by commands in the transformed client surface. */
export type IRCommandSharedType = IRCommandIdentity;

export const IRCommandSharedTypeSchema: z.ZodType<IRCommandSharedType> =
  IRCommandIdentitySchema;