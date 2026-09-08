import {
  getEncode, getLifecycleVisibilityEnum, getVisibilityForClass, isNeverType, isVoidType,
  type ModelProperty, type Type,
} from "@typespec/compiler";
import {
  addEncodeInfo,
  createDiagnostic,
  getClientTypeWithDiagnostics,
  getPropertyNames,
  getSdkModelPropertyType,
  isInScope,
  type SdkHeaderParameter,
  type SdkModelPropertyTypeBase,
  type SdkModelType,
  type SdkType,
} from "@azure-tools/typespec-client-generator-core";
import type {
  HttpOperationMultipartBody,
  HttpOperationPart,
} from "@typespec/http";
import { isStatusCode } from "@typespec/http";
import {
  CollectionFormats,
  type CollectionFormat,
  type IRMultipartBody,
  type IRMultipartHeader,
  type IRMultipartPart,
  type IRMultipartPartBody,
  type IRMultipartValue,
} from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";
import { pascalCase } from "./naming.js";
import { literalFromConstant, propertyIdFor } from "./properties.js";
import { idFor, typeRefFor, unwrapNullable } from "./types.js";

export function sdkTypeFromHttp(ctx: BuildContext, type: Type): SdkType {
  const [sdkType, diagnostics] = getClientTypeWithDiagnostics(ctx.sdkContext, type);
  ctx.program.reportDiagnostics(diagnostics);
  return sdkType;
}

function sdkProperty(ctx: BuildContext, property: ModelProperty) {
  const [sdk, diagnostics] = getSdkModelPropertyType(ctx.sdkContext, property);
  ctx.program.reportDiagnostics(diagnostics);
  return sdk;
}

function completePayloadType(ctx: BuildContext, type: SdkType): void {
  if (ctx.multipartPayloadTypes.has(type)) return;
  ctx.multipartPayloadTypes.add(type);
  switch (type.kind) {
    case "model": {
      // TCGC's multipart-response branch skips ordinary payload models. Recover
      // their declarations with its public property converter, without passing
      // the enclosing multipart operation and re-entering that branch.
      if (type.properties.every((property) => property.discriminator) && type.__raw?.kind === "Model") {
        const lifecycle = getLifecycleVisibilityEnum(ctx.program);
        for (const property of type.__raw.properties.values()) {
          if (type.properties.some((existing) => existing.__raw === property)
            || isStatusCode(ctx.program, property) || isNeverType(property.type) || isVoidType(property.type)
            || !isInScope(ctx.sdkContext, property)
            || getVisibilityForClass(ctx.program, property, lifecycle).size === 0) continue;
          type.properties.push(sdkProperty(ctx, property));
        }
      }
      type.properties.forEach((property) => completePayloadType(ctx, property.type));
      if (type.baseModel) completePayloadType(ctx, type.baseModel);
      if (type.additionalProperties) completePayloadType(ctx, type.additionalProperties);
      Object.values(type.discriminatedSubtypes ?? {}).forEach((subtype) => completePayloadType(ctx, subtype));
      break;
    }
    case "array":
    case "dict":
      completePayloadType(ctx, type.valueType);
      break;
    case "nullable":
      completePayloadType(ctx, type.type);
      break;
    case "union":
      type.variantTypes.forEach((variant) => completePayloadType(ctx, variant));
      break;
  }
}

function valueFrom(
  ctx: BuildContext,
  property: SdkModelPropertyTypeBase,
  where: string,
  valueType: SdkType = property.type,
): IRMultipartValue {
  completePayloadType(ctx, valueType);
  const { type, nullable } = unwrapNullable(valueType);
  const defaultValue = type.kind === "constant" ? literalFromConstant(type) : undefined;
  return {
    name: pascalCase(property.name),
    type: typeRefFor(ctx, type, where),
    optional: property.optional,
    nullable,
    ...(defaultValue !== undefined ? { defaultValue } : {}),
    ...(property.doc !== undefined ? { docs: property.doc } : {}),
    ...(property.summary !== undefined ? { summary: property.summary } : {}),
  };
}

function headerFrom(
  ctx: BuildContext,
  header: HttpOperationPart["headers"][number],
  where: string,
  sdkHeader?: SdkHeaderParameter,
): IRMultipartHeader {
  const property = sdkHeader ?? sdkProperty(ctx, header.property);
  const value = valueFrom(ctx, property, where);
  let collectionFormat: CollectionFormat | undefined;
  if (sdkHeader) {
    collectionFormat = CollectionFormats.find((format) => format === sdkHeader.collectionFormat);
  } else if (value.type.kind === "array") {
    const encode = getEncode(ctx.program, header.property)?.encoding;
    if (header.options.explode) collectionFormat = "multi";
    else if (encode === "ArrayEncoding.pipeDelimited") collectionFormat = "pipes";
    else if (encode === "ArrayEncoding.spaceDelimited") collectionFormat = "ssv";
    else {
      collectionFormat = "csv";
      if (encode !== undefined) {
        ctx.program.reportDiagnostic(createDiagnostic({
          code: "invalid-encode-for-collection-format",
          target: header.property,
        }));
      }
    }
  }
  return {
    ...value,
    wireName: header.options.name,
    ...(collectionFormat !== undefined ? { collectionFormat } : {}),
  };
}

function modelProperty(model: SdkModelType | undefined, raw: ModelProperty | undefined) {
  if (raw === undefined) return undefined;
  for (let current = model; current !== undefined; current = current.baseModel) {
    const property = current.properties.find((candidate) => candidate.__raw === raw);
    if (property) return { model: current, property };
  }
  return undefined;
}

/** Apply MIME-body encoding without mutating TCGC's shared model/property types. */
function encodedType(
  ctx: BuildContext,
  sdkType: SdkType,
  target: ModelProperty,
  rawType: Type,
  contentType: string | undefined,
): SdkType {
  const { type, nullable } = unwrapNullable(sdkType);
  if (type.kind === "model" || type.kind === "enum" || type.kind === "enumvalue" || type.kind === "union") {
    return sdkType;
  }
  const encoded = { ...type };
  const encodeTarget = getEncode(ctx.program, target) === undefined
    && rawType.kind === "Scalar" && getEncode(ctx.program, rawType) !== undefined
    ? rawType : target;
  const [, diagnostics] = addEncodeInfo(ctx.sdkContext, encodeTarget, encoded, contentType);
  ctx.program.reportDiagnostics(diagnostics);
  return nullable && sdkType.kind === "nullable" ? { ...sdkType, type: encoded } : encoded;
}

function partFrom(
  ctx: BuildContext,
  multipart: HttpOperationMultipartBody,
  part: HttpOperationPart,
  model: SdkModelType | undefined,
  where: string,
): IRMultipartPart {
  const binding = modelProperty(model, part.property);
  const sdkPart = binding?.property;
  const options = sdkPart?.serializationOptions.multipart;
  let sdkType = sdkPart?.type;
  if (sdkType?.kind === "array" && part.multi) sdkType = sdkType.valueType;
  sdkType ??= sdkTypeFromHttp(ctx, part.body.type);
  completePayloadType(ctx, sdkType);
  const encoded = encodedType(
    ctx, sdkType, part.body.property ?? part.property ?? multipart.property,
    part.body.type, part.body.contentTypes[0],
  );
  const { type, nullable } = unwrapNullable(encoded);
  const contentTypeProperty = options?.contentType
    ?? (part.body.contentTypeProperty ? sdkProperty(ctx, part.body.contentTypeProperty) : undefined);
  const filenameProperty = options?.filename
    ?? (part.filename ? sdkProperty(ctx, part.filename) : undefined);
  const defaultValue = type.kind === "constant" ? literalFromConstant(type) : undefined;
  const common = {
    type: typeRefFor(ctx, type, `${where}.body`),
    nullable,
    contentTypes: [...part.body.contentTypes],
    ...(contentTypeProperty ? { contentType: valueFrom(ctx, contentTypeProperty, `${where}.contentType`) } : {}),
    ...(defaultValue !== undefined ? { defaultValue } : {}),
  };
  let body: IRMultipartPartBody;
  if (part.body.bodyKind === "file") {
    const contents = sdkProperty(ctx, part.body.contents);
    // Http.File contents are raw bytes/text; their JSON/base64 model encoding is separate.
    const contentsType = encodedType(
      ctx, contents.type, part.body.contents, part.body.contents.type,
      part.body.isText ? "text/plain" : "application/octet-stream",
    );
    body = {
      ...common,
      kind: "file",
      isText: part.body.isText,
      contents: valueFrom(ctx, contents, `${where}.contents`, contentsType),
    };
  } else {
    body = { ...common, kind: "single" };
  }
  const property = binding
    ? propertyIdFor(
        ctx, idFor(ctx, binding.model), binding.property,
        binding.property.__raw ? getPropertyNames(ctx.sdkContext, binding.property.__raw)[0] : binding.property.name,
      )
    : undefined;
  return {
    ...(part.name !== undefined ? { name: part.name } : {}),
    ...(property !== undefined ? { property } : {}),
    body,
    headers: part.headers.map((header) => headerFrom(
      ctx, header, `${where}.header.${header.options.name}`,
      options?.headers.find((candidate) => candidate.__raw === header.property),
    )),
    optional: part.optional,
    multi: part.multi,
    ...(filenameProperty ? { filename: valueFrom(ctx, filenameProperty, `${where}.filename`) } : {}),
    ...(sdkPart?.doc !== undefined ? { docs: sdkPart.doc } : {}),
    ...(sdkPart?.summary !== undefined ? { summary: sdkPart.summary } : {}),
  };
}

export function multipartFrom(
  ctx: BuildContext,
  body: HttpOperationMultipartBody,
  sdkType: SdkType,
  where: string,
): IRMultipartBody {
  const { type } = unwrapNullable(sdkType);
  return {
    kind: body.multipartKind,
    contentTypes: [...body.contentTypes],
    parts: body.parts.map((part, index) => partFrom(
      ctx, body, part, type.kind === "model" ? type : undefined, `${where}.parts[${index}]`,
    )),
  };
}
