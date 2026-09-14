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
import { IRServerDefinitionSchema, type IRServerDefinition } from "./server.js";
import { IRDiscriminatedUnionSchema, type IRDiscriminatedUnion } from "./discriminated-union.js";
import { idKey, serializeId, type IRId } from "./id.js";

export type IR = {
  schemaVersion: string;
  sources: IRSource[];
  servers: IRServerDefinition[];
  models: IRModel[];
  enums: IREnum[];
  discriminatedUnions?: IRDiscriminatedUnion[] | undefined;
  operations: IROperation[];
};

export const IRSchema: z.ZodType<IR> = z
  .object({
    schemaVersion: z.string().min(1),
    sources: z.array(IRSourceSchema).min(1),
    servers: z.array(IRServerDefinitionSchema),
    models: z.array(IRModelSchema),
    enums: z.array(IREnumSchema),
    discriminatedUnions: z.array(IRDiscriminatedUnionSchema).optional(),
    operations: z.array(IROperationSchema),
  })
  .strict()
  .superRefine((ir, ctx) => {
    // Keyed rather than pairwise: `idKey` agrees with `idEquals`, so this is one
    // pass instead of the quadratic scan a `some()` over earlier entries needs.
    const defined = new Set<string>();
    ir.servers.forEach((server, index) => {
      const key = idKey(server.id);
      if (defined.has(key)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Duplicate server definition ${serializeId(server.id)}`,
          path: ["servers", index, "id"],
        });
      }
      defined.add(key);
    });
    const checkReference = (id: IRId, path: (string | number)[]): void => {
      if (!defined.has(idKey(id))) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Server reference ${serializeId(id)} has no definition in IR.servers`,
          path,
        });
      }
    };
    ir.sources.forEach((source, index) => checkReference(source.server, ["sources", index, "server"]));
    ir.operations.forEach((operation, index) => checkReference(operation.server, ["operations", index, "server"]));
  });

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
    servers: [],
    models: [],
    enums: [],
    discriminatedUnions: [],
    operations: [],
  };
}
