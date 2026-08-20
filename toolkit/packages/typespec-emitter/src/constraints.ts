import { getMaxLength, getMinLength, type ModelProperty } from "@typespec/compiler";
import type { IRLengthConstraints } from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";

export function lengthConstraintsFor(
  ctx: BuildContext,
  property: ModelProperty | undefined,
): IRLengthConstraints | undefined {
  if (property === undefined) return undefined;

  const minLength =
    getMinLength(ctx.program, property) ?? getMinLength(ctx.program, property.type);
  const maxLength =
    getMaxLength(ctx.program, property) ?? getMaxLength(ctx.program, property.type);

  return minLength !== undefined || maxLength !== undefined
    ? {
        ...(minLength !== undefined ? { minLength } : {}),
        ...(maxLength !== undefined ? { maxLength } : {}),
      }
    : undefined;
}