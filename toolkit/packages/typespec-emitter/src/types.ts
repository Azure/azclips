/**
 * SdkType → IRTypeRef mapping.
 *
 * Ordinary types use TCGC's normalization. Discriminated unions are resolved
 * from TypeSpec before SDK enum/nullable normalization can erase their wire shape.
 *
 * Nullable handling: `SdkNullableType` is *not* a type-ref in IR.
 * Callers (`properties.ts`, `http.ts`, `operations.ts`) call
 * `unwrapNullable` first to lift the nullability flag, then pass the
 * unwrapped type here. A tagged null payload stays inside its union definition.
 */

import {
  getDiscriminatedUnion, getDoc, getLocationContext,
  getNamespaceFullName, getSummary, NoTarget,
  type Type, type Union, type UnionVariant,
} from "@typespec/compiler";
import type {
  SdkArrayType,
  SdkBuiltInType,
  SdkConstantType,
  SdkDateTimeType,
  SdkDictionaryType,
  SdkDurationType,
  SdkEnumType,
  SdkEnumValueType,
  SdkModelType,
  SdkNullableType,
  SdkType,
  SdkUnionType,
} from "@azure-tools/typespec-client-generator-core";
import {
  getClientTypeWithDiagnostics, getGeneratedName, getLibraryName, getNamespaceFromType,
} from "@azure-tools/typespec-client-generator-core";
import type {
  FormatKind,
  IRDiscriminatedUnion,
  IRDiscriminatedUnionVariant,
  IRId,
  IRTypeRef,
  IRTypeRefPrimitive,
  IRTypeRefDiscriminatedUnion,
  PrimitiveKind,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";
import { literalFromValue } from "./literals.js";
import { reportSdkDiagnostics } from "./discriminators.js";

const ANY_PRIMITIVE: IRTypeRefPrimitive = { kind: "primitive", name: "any" };

/**
 * Strip an outer SdkNullableType layer and return both the inner type and
 * whether a nullable wrapper was found. Repeated callers can spread the
 * result into their own `{ nullable }` field.
 */
export function unwrapNullable(ctx: BuildContext, t: SdkType): { type: SdkType; nullable: boolean } {
  if (t.kind === "nullable" && !discriminatedSource(ctx, t)) {
    const inner = (t as SdkNullableType).type;
    // TCGC normalizes flat nullable; defensively unwrap chains anyway.
    const next = unwrapNullable(ctx, inner);
    return { type: next.type, nullable: true };
  }
  return { type: t, nullable: false };
}

export function discriminatedSource(ctx: BuildContext, type: SdkType): Union | undefined {
  const raw = type.__raw;
  if (raw?.kind !== "Union") return undefined;
  const [resolved, diagnostics] = getDiscriminatedUnion(ctx.program, raw);
  ctx.program.reportDiagnostics(diagnostics);
  return resolved?.type;
}

function sourceId(ctx: BuildContext, raw: Type | undefined, name: string): IRId {
  const namespace = getNamespaceFromType(raw);
  const fqn = namespace !== undefined ? getNamespaceFullName(namespace) : "";
  return {
    module: sourceModule(ctx, raw),
    namespace: fqn.length === 0 ? [] : fqn.split("."),
    name,
  };
}

function sourceModule(ctx: BuildContext, raw: Type | undefined): string {
  const location = raw !== undefined ? getLocationContext(ctx.program, raw) : undefined;
  return location?.type === "library" ? location.metadata.name : ctx.options.sourceId;
}

/**
 * Resolve the IR "module" string for a nameable type. Per the IR contract
 * (see IRModel.source / IRId.module), types declared in the user's spec are
 * attributed to the chosen sourceId, while types that come from a TypeSpec
 * *library* (e.g. Azure.Core.Foundations.Error) are attributed to that
 * library's package name so library-shared types don't get duplicated under
 * each spec's id. We read the location context off the type's `__raw` node.
 */
export function moduleFor(
  ctx: BuildContext,
  t: SdkModelType | SdkEnumType | SdkUnionType,
): string {
  return sourceModule(ctx, t.__raw);
}

/**
 * Build an IRId for any *nameable* SdkType (model/enum/union). Uses the
 * id-cache so we hand out the same identity on subsequent calls. The
 * namespace comes from the original TypeSpec declaration, not SDK placement:
 *
 *   module     = the source-id (or the type's owning library package name
 *                for library-declared types — see `moduleFor`)
 *   namespace  = the original TypeSpec namespace's FQN segments
 *   name       = TCGC's libraryName for the type
 */
export function idFor(
  ctx: BuildContext,
  t: SdkModelType | SdkEnumType | SdkUnionType,
): IRId {
  if (t.kind === "model") ctx.models.add(t);
  if (t.kind === "enum") ctx.enums.add(t);
  const cached = ctx.idCache.get(t);
  if (cached) return cached;

  const name = nameOf(ctx, t);
  const id = sourceId(ctx, t.__raw, name);
  ctx.idCache.set(t, id);
  return id;
}

function discriminatedVariant(
  ctx: BuildContext,
  variant: UnionVariant,
  where: string,
): IRDiscriminatedUnionVariant {
  const docs = getDoc(ctx.program, variant);
  const summary = getSummary(ctx.program, variant);
  const metadata = {
    ...(typeof variant.name === "string" ? { value: variant.name } : {}),
    ...(docs !== undefined ? { docs } : {}),
    ...(summary !== undefined ? { summary } : {}),
  };
  if (variant.type.kind === "Intrinsic" && variant.type.name === "null") {
    return { ...metadata, nullable: true, literalValue: { kind: "null" } };
  }
  const [sdkType, diagnostics] = getClientTypeWithDiagnostics(ctx.sdkContext, variant.type);
  reportSdkDiagnostics(ctx.program, diagnostics);
  const { type, nullable } = unwrapNullable(ctx, sdkType);
  let literalValue = type.kind === "constant" || type.kind === "enumvalue"
    ? literalFromValue(type.value) : undefined;
  if (variant.type.kind === "Number" && !Number.isSafeInteger(variant.type.value)) {
    const integer = variant.type.numericValue.asBigInt();
    if (integer !== null) literalValue = literalFromValue(integer);
  }
  return {
    ...metadata,
    type: typeRefFor(ctx, type, where),
    nullable,
    ...(literalValue !== undefined ? { literalValue } : {}),
  };
}

function discriminatedUnionRef(ctx: BuildContext, raw: Union, where: string): IRTypeRefDiscriminatedUnion {
  const existing = ctx.discriminatedUnions.get(raw);
  if (existing) return { kind: "discriminatedUnion", id: existing.id };
  const [resolved, diagnostics] = getDiscriminatedUnion(ctx.program, raw);
  ctx.program.reportDiagnostics(diagnostics);
  if (!resolved) throw new Error(`Missing discriminator definition for ${where}`);
  const id = sourceId(ctx, raw, getLibraryName(ctx.sdkContext, raw) || getGeneratedName(ctx.sdkContext, raw));
  const docs = getDoc(ctx.program, raw);
  const summary = getSummary(ctx.program, raw);
  const definition: IRDiscriminatedUnion = {
    id,
    name: id.name,
    source: id.module,
    discriminatorPropertyName: resolved.options.discriminatorPropertyName,
    envelope: resolved.options.envelope,
    ...(resolved.options.envelope === "object" ? { envelopePropertyName: resolved.options.envelopePropertyName } : {}),
    variants: [],
    ...(docs !== undefined ? { docs } : {}),
    ...(summary !== undefined ? { summary } : {}),
  };
  // Register before visiting payloads so recursive unions remain references on the wire.
  ctx.discriminatedUnions.set(raw, definition);
  definition.variants = [...raw.variants.values()].map((variant, index) =>
    discriminatedVariant(ctx, variant, `${where}.variant[${index}]`));
  return { kind: "discriminatedUnion", id };
}

function nameOf(
  ctx: BuildContext,
  t: SdkModelType | SdkEnumType | SdkUnionType,
): string {
  if (t.__raw !== undefined) {
    const lib = getLibraryName(ctx.sdkContext, t.__raw as Parameters<typeof getLibraryName>[1]);
    if (lib) return lib;
  }
  if (t.name && t.name.length > 0) return t.name;
  reportDiagnostic(ctx.program, {
    code: "anonymous-model-no-name",
    format: { where: t.kind, name: "Anonymous" },
    target: NoTarget,
  });
  return "Anonymous";
}

/** Inner mapping from a TCGC builtin kind to our closed `PrimitiveKind` set. */
function primitiveKindOf(kind: string): PrimitiveKind | undefined {
  switch (kind) {
    case "string":
    case "url":
    case "uri":
    case "plainDate":
    case "plainTime":
      return "string";
    case "boolean":
      return "boolean";
    case "int8":
    case "int16":
    case "int32":
    case "int64":
    case "uint8":
    case "uint16":
    case "uint32":
    case "uint64":
      return kind as PrimitiveKind;
    case "integer":
    case "safeint":
    case "numeric":
      return "int64";
    case "float":
    case "float32":
    case "float64":
      return kind === "float" ? "float64" : (kind as PrimitiveKind);
    case "decimal":
    case "decimal128":
      return kind as PrimitiveKind;
    case "bytes":
      return "bytes";
    case "unknown":
    case "any":
      return "any";
    case "null":
    case "void":
    case "never":
      return "void";
    default:
      return undefined;
  }
}

/**
 * Azure.Core semantic string scalars (`armResourceIdentifier`, `uuid`, `ipV4/6Address`)
 * `extends string`, so TCGC flattens them to `kind: "string"` — but retains their
 * identity in `crossLanguageDefinitionId`. Map that identity onto the IR's format
 * hint so the semantic survives the flattening (source-faithful — nothing dropped).
 */
const SEMANTIC_SCALAR_FORMATS: Readonly<Record<string, FormatKind>> = {
  "Azure.Core.armResourceIdentifier": "arm-id",
  "Azure.Core.uuid": "uuid",
  "Azure.Core.ipV4Address": "ip-address",
  "Azure.Core.ipV6Address": "ip-address",
};

/** Compute a wire-format hint from a TCGC builtin/datetime/duration kind. */
function formatFor(t: SdkType): FormatKind | undefined {
  switch (t.kind) {
    case "utcDateTime":
    case "offsetDateTime":
      return "date-time";
    case "duration":
      return "duration";
    case "plainDate":
      return "date";
    case "plainTime":
      return "time";
    case "url":
      return "uri";
    case "bytes": {
      const enc = (t as SdkBuiltInType).encode;
      if (enc === "base64url") return "base64url";
      if (enc === "base64") return "base64";
      return undefined;
    }
    default: {
      // Semantic string scalars keep their identity in crossLanguageDefinitionId
      // even though TCGC reports kind "string".
      const xldi = (t as SdkBuiltInType).crossLanguageDefinitionId;
      return xldi !== undefined ? SEMANTIC_SCALAR_FORMATS[xldi] : undefined;
    }
  }
}

/**
 * Recursive structural mapping. The caller is responsible for unwrapping
 * `SdkNullableType` (see `unwrapNullable`) — IR carries nullability on
 * the *containing* node (property/parameter), not on the type-ref.
 */
export function typeRefFor(
  ctx: BuildContext,
  t: SdkType,
  where: string,
): IRTypeRef {
  // Inspect the source before TCGC's enum/nullable/single-variant normalization.
  const discriminated = discriminatedSource(ctx, t);
  if (discriminated) return discriminatedUnionRef(ctx, discriminated, where);
  switch (t.kind) {
    case "nullable": {
      // Defensive: should have been unwrapped by caller. Try anyway.
      return typeRefFor(ctx, (t as SdkNullableType).type, where);
    }

    case "model":
      return { kind: "model", id: idFor(ctx, t as SdkModelType) };

    case "enum":
      return { kind: "enum", id: idFor(ctx, t as SdkEnumType) };

    case "enumvalue": {
      const ev = t as SdkEnumValueType;
      // A member of an enum-normalized @discriminated union is its payload
      // literal, not a reference to the SDK's synthetic enum.
      if (discriminatedSource(ctx, ev.enumType)) {
        return typeRefFor(ctx, ev.enumType.valueType, where);
      }
      return { kind: "enum", id: idFor(ctx, ev.enumType) };
    }

    case "array": {
      const arr = t as SdkArrayType;
      return { kind: "array", element: typeRefFor(ctx, arr.valueType, where + ".element") };
    }

    case "dict": {
      const d = t as SdkDictionaryType;
      return { kind: "dictionary", value: typeRefFor(ctx, d.valueType, where + ".value") };
    }

    case "constant": {
      // The constant collapses to its wireType in the IR; the literal
      // value lives in the containing property's `defaultValue`.
      const c = t as SdkConstantType;
      return typeRefFor(ctx, c.valueType as SdkType, where);
    }

    case "union": {
      const u = t as SdkUnionType;
      const variants: IRTypeRef[] = u.variantTypes.map((v, i) =>
        typeRefFor(ctx, v as SdkType, `${where}.variant[${i}]`),
      );
      // Degenerate 1-variant unions are inlined (IR requires min(2)).
      if (variants.length === 1) {
        const v = variants[0];
        if (v) return v;
      }
      if (variants.length === 0) {
        reportDiagnostic(ctx.program, {
          code: "union-non-mappable",
          format: { name: u.name ?? where },
          target: NoTarget,
        });
        return ANY_PRIMITIVE;
      }
      return { kind: "union", variants };
    }

    case "utcDateTime":
    case "offsetDateTime": {
      const dt = t as SdkDateTimeType;
      // Encoded as the underlying wireType (usually string) plus a format.
      const inner = typeRefFor(ctx, dt.wireType as SdkType, where);
      if (inner.kind === "primitive") {
        const fmt = formatFor(dt);
        return fmt !== undefined ? { ...inner, format: fmt } : inner;
      }
      return inner;
    }

    case "duration": {
      const dur = t as SdkDurationType;
      const inner = typeRefFor(ctx, dur.wireType as SdkType, where);
      if (inner.kind === "primitive") {
        return { ...inner, format: "duration" };
      }
      return inner;
    }

    case "tuple": {
      reportDiagnostic(ctx.program, {
        code: "unsupported-type-kind",
        format: { kind: "tuple", where },
        target: NoTarget,
      });
      return ANY_PRIMITIVE;
    }

    case "credential": {
      reportDiagnostic(ctx.program, {
        code: "unsupported-type-kind",
        format: { kind: "credential", where },
        target: NoTarget,
      });
      return ANY_PRIMITIVE;
    }

    case "endpoint": {
      // Endpoints never appear as model property / parameter types in IR;
      // they're handled by server.ts. Treat as opaque string here.
      return { kind: "primitive", name: "string" };
    }

    default: {
      // All remaining cases are SdkBuiltInType variants.
      const b = t as SdkBuiltInType;
      const k = primitiveKindOf(b.kind);
      if (k === undefined) {
        reportDiagnostic(ctx.program, {
          code: "unsupported-type-kind",
          format: { kind: b.kind, where },
          target: NoTarget,
        });
        return ANY_PRIMITIVE;
      }
      const fmt = formatFor(b);
      return fmt !== undefined ? { kind: "primitive", name: k, format: fmt } : { kind: "primitive", name: k };
    }
  }
}
