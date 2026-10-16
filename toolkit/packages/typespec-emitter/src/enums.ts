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
const INT64_MIN = -(2 ** 63);
/** Exclusive: `2 ** 63` is the first double past `int64`'s range. */
const INT64_END = 2 ** 63;

/**
 * The primitive kind of an enum's values.
 *
 * TCGC reports a union's declared scalar (`int64 | 1 | 2` is `int64`) and
 * otherwise guesses from how each value prints: `float32` once one has a decimal
 * point, else `int32`. A guess can be too narrow — `3000000000` overflows an
 * `int32`, `1e-7` prints without a point yet is no integer, and `1.23456789` is
 * more precise than a `float32` — so an `int32` or `float32` is widened to the
 * first of `int32`, `int64`, `float32`, `float64` that holds every value. A
 * reported kind is never narrowed.
 */
function valueTypeOf(ctx: BuildContext, e: SdkEnumType): EnumValueKind | undefined {
  const reported = primitiveKindOf(e.valueType.kind);
  const kind = reported === "int32" || reported === "float32" ? widen(reported, numbersOf(e)) : reported;
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

function numbersOf(e: SdkEnumType): number[] {
  return e.values.map((v) => v.value).filter((v): v is number => typeof v === "number");
}

function widen(kind: "int32" | "float32", values: readonly number[]): EnumValueKind {
  if (kind === "int32") {
    const integral = values.every((v) => Number.isInteger(v));
    if (integral && values.every((v) => v >= INT32_MIN && v <= INT32_MAX)) return "int32";
    if (integral && values.every((v) => v >= INT64_MIN && v < INT64_END)) return "int64";
  }
  return values.every(fitsFloat32) ? "float32" : "float64";
}

/**
 * Whether a `float32` keeps `value` as written: the shortest decimal that reads
 * back as the nearest `float32` is `value` itself. That is how a `float32` prints,
 * so `0.1` fits although no binary float holds it exactly, while `1.23456789` does
 * not — as a `float32` it prints `1.2345679`.
 */
function fitsFloat32(value: number): boolean {
  const single = Math.fround(value);
  if (!Number.isFinite(single)) return false;
  // Nine significant digits always identify a float32.
  for (let digits = 1; digits <= 9; digits++) {
    const printed = Number(single.toPrecision(digits));
    if (Math.fround(printed) === single) return printed === value;
  }
  return false;
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
