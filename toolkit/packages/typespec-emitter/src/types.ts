/**
 * SdkType → IRTypeRef mapping.
 *
 * Pure mechanical translation. The only "choice" baked in here is the
 * primitive-kind mapping (see `primitiveKindOf` and `formatFor`); even
 * that is dictated by TCGC's own normalization of TypeSpec scalars.
 *
 * Nullable handling: `SdkNullableType` is *not* a type-ref in IR.
 * Callers (`properties.ts`, `http.ts`, `operations.ts`) call
 * `unwrapNullable` first to lift the nullability flag, then pass the
 * unwrapped type here.
 */

import { getLocationContext, getNamespaceFullName, NoTarget } from "@typespec/compiler";
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
import { getLibraryName, getNamespaceFromType } from "@azure-tools/typespec-client-generator-core";
import type {
  FormatKind,
  IRId,
  IRTypeRef,
  IRTypeRefPrimitive,
  PrimitiveKind,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { reportDiagnostic } from "./lib.js";

const ANY_PRIMITIVE: IRTypeRefPrimitive = { kind: "primitive", name: "any" };

/**
 * Strip an outer SdkNullableType layer and return both the inner type and
 * whether a nullable wrapper was found. Repeated callers can spread the
 * result into their own `{ nullable }` field.
 */
export function unwrapNullable(t: SdkType): { type: SdkType; nullable: boolean } {
  if (t.kind === "nullable") {
    const inner = (t as SdkNullableType).type;
    // TCGC normalizes flat nullable; defensively unwrap chains anyway.
    const next = unwrapNullable(inner);
    return { type: next.type, nullable: true };
  }
  return { type: t, nullable: false };
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
  const raw = t.__raw;
  if (raw !== undefined) {
    const loc = getLocationContext(ctx.program, raw);
    if (loc.type === "library") return loc.metadata.name;
  }
  return ctx.options.sourceId;
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
  const cached = ctx.idCache.get(t);
  if (cached) return cached;

  const name = nameOf(ctx, t);
  const namespace = getNamespaceFromType(t.__raw);
  const fqn = namespace !== undefined ? getNamespaceFullName(namespace) : "";
  const segments = fqn.length === 0 ? [] : fqn.split(".");

  const moduleStr = moduleFor(ctx, t);
  const id: IRId = {
    module: moduleStr,
    namespace: segments,
    name,
  };
  ctx.idCache.set(t, id);
  return id;
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
      // A single enum value used as a type — treat as the parent enum.
      const ev = t as SdkEnumValueType;
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
      // discriminated unions still get emitted as a union *type-ref*;
      // generators consult IRModel for envelope semantics if any.
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
