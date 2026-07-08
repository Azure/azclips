/**
 * SdkModelPropertyType → IRProperty.
 *
 * The 5-way dispatch (primitive/object/collection/enum/union) mirrors
 * `IRProperty`'s discriminated union. Each path resolves:
 *   - logical/wire names (TCGC's `getPropertyNames`)
 *   - nullability (from unwrapping SdkNullableType)
 *   - readOnly/writeOnly (from `visibility[]`)
 *   - default value (from SdkConstantType — wireType narrow + literal)
 *   - inheritedFrom / overrides (populated by `models.ts`, not here)
 */

import { NoTarget } from "@typespec/compiler";
import type {
  SdkConstantType,
  SdkModelPropertyType,
  SdkType,
} from "@azure-tools/typespec-client-generator-core";
import { getPropertyNames } from "@azure-tools/typespec-client-generator-core";
import { Visibility } from "@typespec/http";
import type {
  IRId,
  IRLiteralValue,
  IRProperty,
  IRTypeRef,
  IRTypeRefArray,
  IRTypeRefDictionary,
  IRTypeRefEnum,
  IRTypeRefModel,
  IRTypeRefPrimitive,
  IRTypeRefUnion,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { pascalCase } from "./naming.js";
import { typeRefFor, unwrapNullable } from "./types.js";

/** Lift the read/write visibility flags onto IR's boolean pair. */
function visibilityFlags(prop: SdkModelPropertyType): { readOnly: boolean; writeOnly: boolean } {
  const vis = prop.visibility;
  if (!vis || vis.length === 0) {
    return { readOnly: false, writeOnly: false };
  }
  // `Visibility` is a numeric flags enum from @typespec/http; compare by value, not string form.
  const hasRead = vis.includes(Visibility.Read);
  const hasCreate = vis.includes(Visibility.Create);
  const hasUpdate = vis.includes(Visibility.Update);
  const readOnly = hasRead && !hasCreate && !hasUpdate;
  const writeOnly = (hasCreate || hasUpdate) && !hasRead;
  return { readOnly, writeOnly };
}

/** Build a literal value from a `SdkConstantType`. Returns undefined if not representable. */
function literalFromConstant(c: SdkConstantType): IRLiteralValue | undefined {
  const v = c.value;
  switch (typeof v) {
    case "string":
      return { kind: "string", value: v };
    case "boolean":
      return { kind: "boolean", value: v };
    case "number":
      return { kind: "number", value: v };
    case "bigint":
      return { kind: "bigint", value: (v as bigint).toString(10) };
    default:
      return undefined;
  }
}

/** Build the IR id for a SdkModelPropertyType, interned. */
export function propertyIdFor(
  ctx: BuildContext,
  containingModelId: IRId,
  prop: SdkModelPropertyType,
  libraryName: string,
): IRId {
  const cached = ctx.propertyIdCache.get(prop);
  if (cached) return cached;
  // Property ids: same module/namespace as the containing model, but the
  // name field has `<Model>.<property>` so two properties on different
  // models stay distinct without colliding with model ids.
  const id: IRId = {
    module: containingModelId.module,
    namespace: containingModelId.namespace,
    name: `${containingModelId.name}.${libraryName}`,
  };
  ctx.propertyIdCache.set(prop, id);
  return id;
}

/**
 * Convert a single property. Caller passes the *containing* model's IRId
 * so we can interpolate the property's id consistently across runs.
 *
 * Returns `undefined` if the property maps to an unsupported shape
 * (e.g. tuple-typed); a diagnostic is reported in that case.
 */
export function propertyFrom(
  ctx: BuildContext,
  containingModelId: IRId,
  prop: SdkModelPropertyType,
): IRProperty | undefined {
  // `getPropertyNames` requires a ModelProperty (the raw TypeSpec node).
  // For TCGC-synthesized properties (no __raw), fall back to TCGC's own name.
  let libName: string;
  let wireName: string;
  if (prop.__raw !== undefined) {
    const [lib, wire] = getPropertyNames(ctx.sdkContext, prop.__raw);
    libName = lib;
    wireName = wire;
  } else {
    libName = prop.name;
    wireName = prop.serializedName ?? prop.name;
  }

  const { type: unwrapped, nullable } = unwrapNullable(prop.type);

  // Resolve default value & wire-effective type if this is a constant.
  let defaultValue: IRLiteralValue | undefined;
  let valueType: SdkType = unwrapped;
  if (unwrapped.kind === "constant") {
    const c = unwrapped as SdkConstantType;
    const lit = literalFromConstant(c);
    if (lit) defaultValue = lit;
    valueType = c.valueType as SdkType;
  }

  const irType = typeRefFor(ctx, valueType, `${containingModelId.name}.${libName}`);

  const id = propertyIdFor(ctx, containingModelId, prop, libName);
  const { readOnly, writeOnly } = visibilityFlags(prop);

  const common = {
    id,
    name: pascalCase(libName),
    wireName,
    optional: prop.optional,
    nullable,
    readOnly,
    writeOnly,
    ...(defaultValue !== undefined ? { defaultValue } : {}),
    ...(prop.doc !== undefined ? { docs: prop.doc } : {}),
  };

  // Dispatch on the IR type-kind so the right discriminant lands on the
  // property. The narrowing here mirrors `IRProperty`'s 5 variants.
  return dispatchByTypeKind(common, irType);
}

function dispatchByTypeKind(
  common: Omit<IRProperty, "typeKind" | "type" | "rendering">,
  irType: IRTypeRef,
): IRProperty | undefined {
  switch (irType.kind) {
    case "primitive":
      return { ...common, typeKind: "primitive", type: irType as IRTypeRefPrimitive };
    case "model":
      return { ...common, typeKind: "object", type: irType as IRTypeRefModel };
    case "dictionary":
      return { ...common, typeKind: "object", type: irType as IRTypeRefDictionary };
    case "array":
      return { ...common, typeKind: "collection", type: irType as IRTypeRefArray };
    case "enum":
      return { ...common, typeKind: "enum", type: irType as IRTypeRefEnum };
    case "union":
      return { ...common, typeKind: "union", type: irType as IRTypeRefUnion };
  }
  // Should be unreachable; keep tsc honest.
  void common;
  return undefined;
}

// Re-export for the smoke test wiring; consumers can ignore.
export { NoTarget };
