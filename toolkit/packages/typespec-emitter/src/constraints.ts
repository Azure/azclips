import {
  getMaxItemsAsNumeric, getMaxLengthAsNumeric, getMaxValueAsNumeric, getMaxValueExclusiveAsNumeric,
  getMinItemsAsNumeric, getMinLengthAsNumeric, getMinValueAsNumeric, getMinValueExclusiveAsNumeric,
  getMaxValueForScalar, getMinValueForScalar, getMaxValueExclusiveForScalar, getMinValueExclusiveForScalar,
  getPattern, getEncode, isSecret,
  type Numeric, type Program, type Type,
} from "@typespec/compiler";
import type { SdkType } from "@azure-tools/typespec-client-generator-core";
import { idKey, type IRDeploymentFacet, type IRId } from "@azure-client-engine/ir";
import { literalFromValue } from "./literals.js";

function fail(detail: string): never {
  throw new Error(detail);
}

function rawSources(raw: Type | undefined): Type[] {
  const sources: Type[] = [];
  const seen = new Set<Type>();
  while (raw && !seen.has(raw)) {
    sources.push(raw);
    seen.add(raw);
    raw = raw.kind === "ModelProperty" ? raw.type : raw.kind === "Scalar" ? raw.baseScalar : undefined;
  }
  return sources;
}

export function getSourceConstraints(program: Program, target: IRId, source: Type): IRDeploymentFacet {
  return sourceFacet(program, target, [], rawSources(source));
}

function sourceFacet(program: Program, target: IRId, typePath: IRDeploymentFacet["typePath"], sources: Type[]): IRDeploymentFacet {
  const first = <T>(getter: (type: Type) => T | undefined): T | undefined =>
    sources.map(getter).find((value) => value !== undefined);
  const numeric = (getter: (program: Program, type: Type) => Numeric | undefined): string | undefined =>
    first((type) => getter(program, type))?.toString();
  const count = (getter: (program: Program, type: Type) => Numeric | undefined): number | undefined => {
    const value = first((type) => getter(program, type));
    if (!value) return undefined;
    const number = value.asNumber();
    if (number === null || !Number.isSafeInteger(number) || number < 0 || number > 2147483647) {
      fail(`Length/items constraint exceeds the IR/.NET reader range at ${idKey(target)}`);
    }
    return number;
  };
  if (first((type) => getMinValueForScalar(program, type)) !== undefined ||
      first((type) => getMaxValueForScalar(program, type)) !== undefined ||
      first((type) => getMinValueExclusiveForScalar(program, type)) !== undefined ||
      first((type) => getMaxValueExclusiveForScalar(program, type)) !== undefined) {
    fail(`Non-numeric scalar bounds are not representable at ${idKey(target)}`);
  }
  if (sources.some((type) => (type.kind === "Scalar" || type.kind === "ModelProperty") &&
      getEncode(program, type) !== undefined)) {
    fail(`Explicit wire encoding cannot yet be represented faithfully at ${idKey(target)}`);
  }
  const minLength = count(getMinLengthAsNumeric);
  const maxLength = count(getMaxLengthAsNumeric);
  const minItems = count(getMinItemsAsNumeric);
  const maxItems = count(getMaxItemsAsNumeric);
  const minValue = numeric(getMinValueAsNumeric);
  const maxValue = numeric(getMaxValueAsNumeric);
  const minValueExclusive = numeric(getMinValueExclusiveAsNumeric);
  const maxValueExclusive = numeric(getMaxValueExclusiveAsNumeric);
  const pattern = first((type) => getPattern(program, type));
  return {
    target, typePath,
    ...(minLength !== undefined ? { minLength } : {}),
    ...(maxLength !== undefined ? { maxLength } : {}),
    ...(minItems !== undefined ? { minItems } : {}),
    ...(maxItems !== undefined ? { maxItems } : {}),
    ...(minValue !== undefined ? { minValue } : {}),
    ...(maxValue !== undefined ? { maxValue } : {}),
    ...(minValueExclusive !== undefined ? { minValueExclusive } : {}),
    ...(maxValueExclusive !== undefined ? { maxValueExclusive } : {}),
    ...(pattern !== undefined ? { pattern } : {}),
    ...(sources.some((type) => isSecret(program, type)) ? { sensitive: true } : {}),
  };
}

/** Preserve source-only facets of an observed input, without discovering or walking resource models. */
export function getSourceFacets(
  program: Program, target: IRId, type: SdkType, raw?: Type,
  path: IRDeploymentFacet["typePath"] = [],
): IRDeploymentFacet[] {
  if (type.kind === "nullable" && path.length > 0) {
    fail(`Nested collection nullability is not representable in canonical IR at ${idKey(target)}`);
  }
  while (type.kind === "nullable") type = type.type;
  const facet = sourceFacet(program, target, path, [...rawSources(raw), ...rawSources(type.__raw)]);
  const representableNumber = (value: unknown): void => {
    if (typeof value === "number" && Number.isInteger(value) && !Number.isSafeInteger(value)) {
      fail(`Non-representable numeric literal or enum member at ${idKey(target)}`);
    }
  };
  if (type.kind === "constant" || type.kind === "enumvalue") {
    representableNumber(type.value);
    facet.literalValue = literalFromValue(type.value);
  }
  if (type.kind === "enum") type.values.forEach((value) => representableNumber(value.value));
  const facets = Object.keys(facet).length > 2 ? [facet] : [];
  if (type.kind === "array") {
    facets.push(...getSourceFacets(program, target, type.valueType, undefined, [...path, "element"]));
  }
  if (type.kind === "dict") {
    facets.push(...getSourceFacets(program, target, type.valueType, undefined, [...path, "value"]));
  }
  if (type.kind === "union") {
    const hasFacets = (variant: SdkType, seen = new Set<SdkType>()): boolean => {
      if (seen.has(variant)) return false;
      seen.add(variant);
      if (Object.keys(sourceFacet(program, target, [], rawSources(variant.__raw))).length > 2) return true;
      if (variant.kind === "nullable") return hasFacets(variant.type, seen);
      if (variant.kind === "array" || variant.kind === "dict") return hasFacets(variant.valueType, seen);
      return variant.kind === "union" && variant.variantTypes.some((value) => hasFacets(value, seen));
    };
    if (type.variantTypes.some((variant) => hasFacets(variant))) {
      fail(`Constraints on structural union variants are not representable at ${idKey(target)}`);
    }
  }
  return facets;
}
