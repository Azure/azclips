import {
  getMaxLength,
  getMaxValue,
  getMinLength,
  getMinValue,
  getPattern,
  type ModelProperty,
} from "@typespec/compiler";
import type { IRConstraints } from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";

function isNonBlank(value: string | undefined): value is string {
  return value !== undefined && value.trim().length > 0;
}

export function constraintsFor(
  ctx: BuildContext,
  property: ModelProperty | undefined,
): IRConstraints | undefined {
  if (property === undefined) return undefined;

  const minLength =
    getMinLength(ctx.program, property) ?? getMinLength(ctx.program, property.type);
  const maxLength =
    getMaxLength(ctx.program, property) ?? getMaxLength(ctx.program, property.type);
  const minValue =
    getMinValue(ctx.program, property) ?? getMinValue(ctx.program, property.type);
  const maxValue =
    getMaxValue(ctx.program, property) ?? getMaxValue(ctx.program, property.type);
  const pattern = [
    getPattern(ctx.program, property),
    getPattern(ctx.program, property.type),
  ].find(isNonBlank);

  return minLength !== undefined ||
    maxLength !== undefined ||
    pattern !== undefined ||
    minValue !== undefined ||
    maxValue !== undefined
    ? {
        ...(minLength !== undefined ? { minLength } : {}),
        ...(maxLength !== undefined ? { maxLength } : {}),
        ...(pattern !== undefined ? { pattern } : {}),
        ...(minValue !== undefined ? { minValue } : {}),
        ...(maxValue !== undefined ? { maxValue } : {}),
      }
    : undefined;
}