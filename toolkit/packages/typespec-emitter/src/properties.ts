/**
 * SdkModelPropertyType → IRProperty.
 *
 * The 5-way dispatch (primitive/object/collection/enum/union) mirrors
 * `IRProperty`'s discriminated union. Each path resolves:
 *   - logical/wire names (TCGC's `getPropertyNames`)
 *   - nullability (from unwrapping SdkNullableType)
 *   - visibility lifecycle phases (from TCGC's `visibility[]`, omitted when unconstrained)
 *   - flatten intent (from TCGC's `flatten`, recorded as an object-property hint)
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
  IRVisibility,
  FlattenHint,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { pascalCase } from "./naming.js";
import { accessFrom } from "./annotations.js";
import { typeRefFor, unwrapNullable } from "./types.js";

/** Map TCGC's resolved visibility (a `@typespec/http` `Visibility` flags array)
 * onto IR's lifecycle phases. Returns `undefined` when the property is
 * *unconstrained* — visible in every phase (TypeSpec's default when no
 * `@visibility` is declared) — or has no visibility, so the IR omits the field
 * (absent = all phases). TCGC only ever surfaces the five lifecycle flags here. */
function visibilityPhases(prop: SdkModelPropertyType): IRVisibility[] | undefined {
  const vis = prop.visibility;
  if (!vis || vis.length === 0) return undefined;
  // `Visibility` is a numeric flags enum from @typespec/http; compare by value, not string form.
  const phases: IRVisibility[] = [];
  if (vis.includes(Visibility.Read)) phases.push("read");
  if (vis.includes(Visibility.Create)) phases.push("create");
  if (vis.includes(Visibility.Update)) phases.push("update");
  if (vis.includes(Visibility.Delete)) phases.push("delete");
  if (vis.includes(Visibility.Query)) phases.push("query");
  // Unconstrained (all five phases) carries no information relative to the
  // default, so omit it and keep the IR terse.
  if (phases.length === 0 || phases.length === 5) return undefined;
  return phases;
}

/**
 * Lift TCGC's `@Legacy.flattenProperty` intent onto the IR flatten hint.
 * `prop.flatten` is TCGC's scope-resolved boolean (true when the decorator
 * applies to ace's client scope). We record the source's *intent* only —
 * `{ into: "self" }` — and never actually hoist here; a later transform/generator
 * decides the rendering. Omitted when false so absent = "not flattened".
 */
function flattenHintFor(prop: SdkModelPropertyType): FlattenHint | undefined {
  return prop.flatten ? { into: "self" } : undefined;
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
  const visibility = visibilityPhases(prop);
  const flatten = flattenHintFor(prop);
  const access = accessFrom(prop.access);

  const common = {
    id,
    name: pascalCase(libName),
    wireName,
    optional: prop.optional,
    nullable,
    ...(visibility !== undefined ? { visibility } : {}),
    ...(defaultValue !== undefined ? { defaultValue } : {}),
    ...(prop.doc !== undefined ? { docs: prop.doc } : {}),
    ...(prop.summary !== undefined ? { summary: prop.summary } : {}),
    ...(access !== undefined ? { access } : {}),
  };

  // Dispatch on the IR type-kind so the right discriminant lands on the
  // property. The narrowing here mirrors `IRProperty`'s 5 variants.
  return dispatchByTypeKind(common, irType, flatten);
}

function dispatchByTypeKind(
  common: Omit<IRProperty, "typeKind" | "type" | "rendering">,
  irType: IRTypeRef,
  flatten: FlattenHint | undefined,
): IRProperty | undefined {
  // Flatten is an object-property hint (the ARM `properties` bag); only the
  // object variants carry `ObjectPropertyHints`. Omitted entirely when unset.
  const objectRendering = flatten !== undefined ? { rendering: { flatten } } : {};
  switch (irType.kind) {
    case "primitive":
      return { ...common, typeKind: "primitive", type: irType as IRTypeRefPrimitive };
    case "model":
      return { ...common, typeKind: "object", type: irType as IRTypeRefModel, ...objectRendering };
    case "dictionary":
      return { ...common, typeKind: "object", type: irType as IRTypeRefDictionary, ...objectRendering };
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
