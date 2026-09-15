/**
 * SdkEnumType → IREnum.
 *
 * Both closed enums (`isFixed: true`) and extensible enums (`isFixed: false`)
 * preserve their TCGC semantics in IR.
 *
 * Union-as-enum: TCGC turns string-literal unions into `SdkEnumType` with
 * `isUnionAsEnum: true`. They show up here exactly like any other enum.
 */

import { NoTarget } from "@typespec/compiler";
import { UsageFlags } from "@azure-tools/typespec-client-generator-core";
import type { SdkEnumType } from "@azure-tools/typespec-client-generator-core";
import type { IREnum, IREnumMember } from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { accessFrom, usageFrom } from "./annotations.js";
import { discriminatedSource, idFor } from "./types.js";

function valueTypeOf(e: SdkEnumType): "string" | "number" {
  const k = e.valueType.kind;
  // Anything that isn't string we coerce to number — IR enums only
  // have two value-types and TCGC's enum value-types are always numeric
  // or string-ish.
  return k === "string" ? "string" : "number";
}

export function enumFrom(ctx: BuildContext, e: SdkEnumType): IREnum | undefined {
  if (!e.isFixed && !ctx.flags.openEnumWarned) {
    reportDiagnostic(ctx.program, {
      code: "open-enum-as-closed",
      format: { enum: e.name },
      target: NoTarget,
    });
    ctx.flags.openEnumWarned = true;
  }

  const id = idFor(ctx, e);
  const members: IREnumMember[] = e.values.map((v) => ({
    name: v.name,
    value: v.value,
    ...(v.doc !== undefined ? { docs: v.doc } : {}),
    ...(v.summary !== undefined ? { summary: v.summary } : {}),
  }));
  if (members.length === 0) {
    // IR requires min(1). Skip silently; an empty enum is a meaningless
    // surface and almost certainly a TCGC quirk we can't help.
    return undefined;
  }
  const usage = usageFrom(e.usage);
  const access = accessFrom(e.access);
  return {
    id,
    name: id.name,
    valueType: valueTypeOf(e),
    isFixed: e.isFixed,
    members,
    // Library-shared enums are attributed to their owning library module
    // (carried on id.module), not the spec's source id. See IREnum.source.
    source: id.module,
    ...(e.doc !== undefined ? { docs: e.doc } : {}),
    ...(e.summary !== undefined ? { summary: e.summary } : {}),
    ...(usage !== undefined ? { usage } : {}),
    ...(access !== undefined ? { access } : {}),
  };
}

export function enumsFrom(ctx: BuildContext): IREnum[] {
  const out: IREnum[] = [];
  for (const e of ctx.enums) {
    if (discriminatedSource(ctx, e)) continue;
    // The @versioned api-version enum is denormalized onto IRSource.apiVersion;
    // it isn't a domain type, so don't surface it as a standalone IREnum.
    if ((e.usage & UsageFlags.ApiVersionEnum) !== 0) continue;
    const ir = enumFrom(ctx, e);
    if (ir) out.push(ir);
  }
  return out;
}
