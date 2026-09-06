/**
 * SdkModelType → IRModel.
 *
 * Heavy lifting:
 *   1. Walk `baseModel` chain root-down. Prepend inherited copies of
 *      each ancestor's properties (`inheritedFrom` set).
 *   2. Append own properties. If an own property collides with an
 *      inherited one by `name`, mark the own copy with `overrides`
 *      pointing at the inherited id and *omit* the inherited copy
 *      (the own copy is the source of truth from this child's POV).
 *   3. Carry polymorphism if `discriminatedSubtypes` is non-empty;
 *      pick the discriminator property (must be resolvable to one of
 *      `properties[]`).
 *   4. Carry `additionalProperties` if TCGC tells us this is an open
 *      dictionary (e.g. `extends Record<X>`).
 *   5. Defensive cycle guard on `baseModel` chains.
 */

import { NoTarget } from "@typespec/compiler";
import type {
  SdkModelPropertyType,
  SdkModelType,
} from "@azure-tools/typespec-client-generator-core";
import type {
  IRId,
  IRModel,
  IRPolymorphism,
  IRProperty,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { propertyFrom } from "./properties.js";
import { accessFrom, usageFrom } from "./annotations.js";
import { idFor, typeRefFor, unwrapNullable } from "./types.js";

/**
 * Walk the inheritance chain from the root down. Returns the chain as
 * `[root, ..., direct-parent]` (own model NOT included).
 * Reports an `inheritance-chain-cycle` and returns the prefix up to the
 * cycle if it detects one.
 */
function ancestorChain(ctx: BuildContext, m: SdkModelType): SdkModelType[] {
  const out: SdkModelType[] = [];
  const seen = new Set<SdkModelType>();
  let cur = m.baseModel;
  while (cur) {
    if (seen.has(cur)) {
      reportDiagnostic(ctx.program, {
        code: "inheritance-chain-cycle",
        format: { model: m.name },
        target: NoTarget,
      });
      break;
    }
    seen.add(cur);
    out.unshift(cur); // prepend so root ends up first
    cur = cur.baseModel;
  }
  return out;
}

/**
 * Build the denormalized property list according to the rules pinned in
 * the plan. Returns the array in declaration order: root-defined first,
 * then each successive descendant. Own-overrides replace the inherited
 * copy in-place.
 *
 * For a property re-declared on an intermediate ancestor, the *defining*
 * ancestor (the root-most one that introduced it) is preserved as
 * `inheritedFrom` and inside the IR id, so the rematerialize pass and
 * downstream consumers can identify the source-of-truth model.
 */
function denormalizedProperties(
  ctx: BuildContext,
  ownModel: SdkModelType,
  ownId: IRId,
): IRProperty[] {
  const ancestors = ancestorChain(ctx, ownModel);
  // Map from logical name to its current IRProperty (so own overrides can find it).
  const byName = new Map<string, IRProperty>();
  // Tracks original (defining/root-most) id per name, to populate `overrides`.
  const definingId = new Map<string, IRId>();

  for (const anc of ancestors) {
    const ancId = idFor(ctx, anc);
    for (const sdkProp of anc.properties) {
      const built = propertyFrom(ctx, ancId, sdkProp);
      if (!built) continue;
      const existingDefining = definingId.get(built.name);
      const definingAncestorId = existingDefining ?? ancId;
      // Stamp inheritedFrom to the *defining* (root-most) ancestor, not the
      // closest re-declarer; this keeps the rematerialize pass anchored on
      // the original property source-of-truth.
      const inheritedCopy: IRProperty = {
        ...built,
        inheritedFrom: definingAncestorId,
      };
      if (!existingDefining) definingId.set(built.name, built.id);
      byName.set(built.name, inheritedCopy);
    }
  }

  // Now the model's own properties.
  for (const sdkProp of ownModel.properties) {
    const built = propertyFrom(ctx, ownId, sdkProp);
    if (!built) continue;
    const inheritedId = definingId.get(built.name);
    if (inheritedId !== undefined) {
      // An override of an inherited property. Drop the inherited copy.
      const overridden: IRProperty = { ...built, overrides: inheritedId };
      byName.set(built.name, overridden);
    } else {
      byName.set(built.name, built);
    }
  }

  return Array.from(byName.values());
}

/**
 * Resolve polymorphism for a model, if any. Returns `undefined` when
 * the model is not a polymorphic base (no `discriminatedSubtypes`).
 */
function polymorphismOf(
  ctx: BuildContext,
  m: SdkModelType,
  ownId: IRId,
  properties: IRProperty[],
): IRPolymorphism | undefined {
  void ownId;
  const subs = m.discriminatedSubtypes;
  if (!subs) return undefined;
  const subKeys = Object.keys(subs);
  if (subKeys.length === 0) return undefined;

  // Locate the discriminator property by name.
  const discProp = m.discriminatorProperty;
  if (!discProp) {
    reportDiagnostic(ctx.program, {
      code: "discriminator-property-not-found",
      format: { model: m.name },
      target: NoTarget,
    });
    return undefined;
  }
  // Match by the discriminator property's __raw if we can; fall back to name.
  let discId: IRId | undefined = ctx.propertyIdCache.get(discProp);
  if (!discId) {
    const wantName = discProp.name;
    const found = properties.find((p) => p.name === wantName);
    if (found) discId = found.id;
  }
  if (!discId) {
    // The discriminator must be one of the emitted properties; without a
    // referent the resulting IR would carry a dangling pointer. Skip
    // polymorphism rather than synthesize a fake id.
    reportDiagnostic(ctx.program, {
      code: "discriminator-property-not-found",
      format: { model: m.name },
      target: NoTarget,
    });
    return undefined;
  }

  const subtypes: IRPolymorphism["subtypes"] = [];
  for (const [value, sub] of Object.entries(subs)) {
    subtypes.push({ value, type: idFor(ctx, sub) });
  }
  if (subtypes.length === 0) return undefined;
  return { discriminator: discId, subtypes };
}

/** Top-level: build the IR model for one SdkModelType. */
export function modelFrom(ctx: BuildContext, m: SdkModelType): IRModel {
  const id = idFor(ctx, m);
  const properties = denormalizedProperties(ctx, m, id);
  const polymorphism = polymorphismOf(ctx, m, id, properties);

  // Parent + discriminatorValue
  let parent: IRId | undefined;
  let discriminatorValue: string | undefined;
  if (m.baseModel) {
    parent = idFor(ctx, m.baseModel);
    if (m.discriminatorValue !== undefined && m.discriminatorValue.length > 0) {
      discriminatorValue = m.discriminatorValue;
    }
  }

  // additionalProperties: open dictionary mapping (e.g. extends Record<T>).
  let additionalProperties: IRModel["additionalProperties"];
  if (m.additionalProperties) {
    const { type: ap } = unwrapNullable(m.additionalProperties);
    additionalProperties = typeRefFor(ctx, ap, `${id.name}.additionalProperties`);
  }

  const usage = usageFrom(m.usage);
  const access = accessFrom(m.access);

  const model: IRModel = {
    id,
    name: id.name,
    // Library-shared models are attributed to their owning library module
    // (carried on id.module), not the spec's source id. See IRModel.source.
    source: id.module,
    properties,
    ...(parent !== undefined ? { parent } : {}),
    ...(discriminatorValue !== undefined ? { discriminatorValue } : {}),
    ...(polymorphism !== undefined ? { polymorphism } : {}),
    ...(additionalProperties !== undefined ? { additionalProperties } : {}),
    ...(m.doc !== undefined ? { docs: m.doc } : {}),
    ...(m.summary !== undefined ? { summary: m.summary } : {}),
    ...(usage !== undefined ? { usage } : {}),
    ...(access !== undefined ? { access } : {}),
  };
  return model;
}

/** Convert all models in the sdkPackage to IR models. */
export function modelsFrom(ctx: BuildContext): IRModel[] {
  return ctx.sdkContext.sdkPackage.models.map((m) => modelFrom(ctx, m));
}

/** Re-export for the smoke test wiring. */
export { propertyFrom };
