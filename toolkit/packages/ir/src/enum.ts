/**
 * Enums.
 *
 * TypeSpec enums map to a name, a value type (string or numeric), known
 * members, and whether values outside that set are accepted.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";
import { AccessKinds, UsageKinds, type IRAccess, type IRUsage } from "./primitives.js";
import { EnumHintsSchema, type EnumHints } from "./hints.js";

export type IREnumMember = {
  name: string;
  /** Wire value. Strings carry strings; numeric enums use number. */
  value: string | number;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
};

export const IREnumMemberSchema: z.ZodType<IREnumMember> = z
  .object({
    name: z.string().min(1),
    value: z.union([z.string(), z.number()]),
    docs: z.string().optional(),
    summary: z.string().optional(),
  })
  .strict();

export type IREnum = {
  id: IRId;
  name: string;
  valueType: "string" | "number";
  /** True when the declared members are exhaustive; false for extensible enums. */
  isFixed: boolean;
  members: IREnumMember[];
  /**
   * The source (IRSource.id) this enum was declared in. Library-shared
   * enums use the library's module string here so they aren't tagged
   * to any single source.
   */
  source: string;
  docs?: string | undefined;
  /** Short-form summary from `@summary`, distinct from the long-form `docs` (`@doc`). */
  summary?: string | undefined;
  /** Data-flow usage (`@usage`/derived). Omitted when empty (`Usage.None`). */
  usage?: IRUsage[] | undefined;
  /** Generated-code visibility (`@access`). Omitted when `public` (the default). */
  access?: IRAccess | undefined;
  rendering?: EnumHints | undefined;
};

export const IREnumSchema: z.ZodType<IREnum> = z
  .object({
    id: IRIdSchema,
    name: z.string().min(1),
    valueType: z.enum(["string", "number"]),
    isFixed: z.boolean(),
    members: z.array(IREnumMemberSchema).min(1),
    source: z.string().min(1),
    docs: z.string().optional(),
    summary: z.string().optional(),
    usage: z.enum(UsageKinds).array().optional(),
    access: z.enum(AccessKinds).optional(),
    rendering: EnumHintsSchema.optional(),
  })
  .strict();
