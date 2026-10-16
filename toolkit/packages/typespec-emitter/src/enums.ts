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
import {
  EnumValueKinds,
  type EnumValueKind,
  type IREnum,
  type IREnumMember,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { accessFrom, usageFrom } from "./annotations.js";
import { discriminatedSource, idFor, primitiveKindOf } from "./types.js";

const ENUM_VALUE_KINDS: ReadonlySet<string> = new Set(EnumValueKinds);
const INT32_MIN = -(2 ** 31);
const INT32_MAX = 2 ** 31 - 1;

/**
 * The primitive kind of an enum's values.
 *
 * TCGC reports a union's declared scalar (`int64 | 1 | 2` is `int64`) and
 * otherwise guesses from the values: `float32` once one has a decimal point,
 * else `int32`. An `int32` that a value does not fit is widened to `int64`.
 */
function valueTypeOf(ctx: BuildContext, e: SdkEnumType): EnumValueKind | undefined {
  const kind = primitiveKindOf(e.valueType.kind);
  if (
    kind === "int32" &&
    e.values.some((v) => typeof v.value === "number" && (v.value < INT32_MIN || v.value > INT32_MAX))
  ) {
    return "int64";
  }
  if (kind !== undefined && ENUM_VALUE_KINDS.has(kind)) {
    return kind as EnumValueKind;
  }
  reportDiagnostic(ctx.program, {
    code: "unsupported-enum-value-type",
    format: { enum: e.name, kind: e.valueType.kind },
    target: NoTarget,
  });
  return undefined;
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
  const valueType = valueTypeOf(ctx, e);
  if (valueType === undefined) {
    return undefined;
  }
  const usage = usageFrom(e.usage);
  const access = accessFrom(e.access);
  return {
    id,
    name: id.name,
    valueType,
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
