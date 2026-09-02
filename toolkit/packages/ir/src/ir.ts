/**
 * Root IR document.
 *
 * Validation strategy:
 *   - `IRSchema.parse(json)` performs a full Zod validation. Use it at
 *     pipeline boundaries (emit→pipeline, pipeline→generator) and in tests.
 *   - For per-stage validation inside `runPipeline`, the same parse is
 *     fast enough at our expected sizes (low-MB IR); don't pre-optimize.
 */

import { z } from "zod";
import { IR_SCHEMA_VERSION } from "./version.js";
import { IRModelSchema, type IRModel } from "./model.js";
import { IREnumSchema, type IREnum } from "./enum.js";
import { IROperationSchema, type IROperation } from "./operation.js";
import { IRSourceSchema, type IRSource } from "./source.js";
import { PSSchema, type PS } from "./ps.js";

export type IR = {
  schemaVersion: string;
  sources: IRSource[];
  models: IRModel[];
  enums: IREnum[];
  operations: IROperation[];
  /** PowerShell surface, populated by a transform; absent until one runs. */
  ps?: PS | undefined;
};

export const IRSchema: z.ZodType<IR> = z
  .object({
    schemaVersion: z.string().min(1),
    sources: z.array(IRSourceSchema).min(1),
    models: z.array(IRModelSchema),
    enums: z.array(IREnumSchema),
    operations: z.array(IROperationSchema),
    ps: PSSchema.optional(),
  })
  .strict();

/**
 * Parse-and-validate an arbitrary value as an IR document. Throws a
 * ZodError on failure; the error's `issues` array pinpoints the path
 * of every violation.
 */
export function parseIR(value: unknown): IR {
  return IRSchema.parse(value);
}

/**
 * Non-throwing variant. Returns `{ success: true, data }` or
 * `{ success: false, error }`.
 */
export function safeParseIR(value: unknown): z.SafeParseReturnType<unknown, IR> {
  return IRSchema.safeParse(value);
}

/** Construct an empty IR scaffold, useful as a transform starting point and in tests. */
export function emptyIR(): IR {
  return {
    schemaVersion: IR_SCHEMA_VERSION,
    sources: [],
    models: [],
    enums: [],
    operations: [],
  };
}
