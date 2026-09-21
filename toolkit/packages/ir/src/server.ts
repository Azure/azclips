/**
 * Shared server definitions and their endpoint expressions.
 * Sources and operations reference a definition's IRId.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";
import { IRLiteralValueSchema, type IRLiteralValue } from "./literal.js";
import { IRTypeRefSchema, type IRTypeRef } from "./type-ref.js";

export type IRServerParameter = {
  name: string;
  wireName: string;
  type: IRTypeRef;
  defaultValue?: IRLiteralValue | undefined;
  docs?: string | undefined;
};

export const IRServerParameterSchema: z.ZodType<IRServerParameter> = z
  .object({
    name: z.string().min(1),
    wireName: z.string().min(1),
    type: IRTypeRefSchema,
    defaultValue: IRLiteralValueSchema.optional(),
    docs: z.string().optional(),
  })
  .strict();

/**
 * Endpoint resolved from TypeSpec's @server declarations. A oneOf preserves
 * alternative endpoints within the same named server definition.
 */
export type IRServer =
  | { kind: "literal"; url: string }
  | { kind: "parameterized"; template: string; parameters: IRServerParameter[] }
  | { kind: "oneOf"; servers: IRServer[] };

export const IRServerSchema: z.ZodType<IRServer> = z.lazy(() =>
  z.discriminatedUnion("kind", [
    z.object({ kind: z.literal("literal"), url: z.string().url() }).strict(),
    z
      .object({
        kind: z.literal("parameterized"),
        template: z.string().min(1),
        parameters: z.array(IRServerParameterSchema).min(1),
      })
      .strict(),
    z
      .object({
        kind: z.literal("oneOf"),
        servers: z.array(IRServerSchema).min(2),
      })
      .strict(),
  ]),
);

export type IRServerDefinition = {
  id: IRId;
  endpoint: IRServer;
};

export const IRServerDefinitionSchema: z.ZodType<IRServerDefinition> = z
  .object({
    id: IRIdSchema,
    endpoint: IRServerSchema,
  })
  .strict();
