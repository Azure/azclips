/**
 * Property references: a property as reached from a root model.
 *
 * A property id alone does not say *where* a value lives — a shared model's
 * property (say `SubResource.id`) is reachable through many containers — so a
 * reference also names the containers walked to reach it, and both fields take
 * part in equality. Ids are invariant under renaming transforms, and the wire
 * path is each property's `wireName` along `via`, then `property`.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";

export type IRPropertyRef = {
  /** The property referenced; it supplies the type, nullability, and docs. */
  property: IRId;
  /**
   * The model-typed object properties walked from the root model to reach
   * `property`, outermost first. Empty when `property` belongs to the root model.
   */
  via: IRId[];
};

export const IRPropertyRefSchema: z.ZodType<IRPropertyRef> = z
  .object({
    property: IRIdSchema,
    via: z.array(IRIdSchema),
  })
  .strict();
