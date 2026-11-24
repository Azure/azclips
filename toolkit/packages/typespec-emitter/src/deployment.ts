import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import {
  getMaxItemsAsNumeric, getMaxLengthAsNumeric, getMaxValueAsNumeric, getMaxValueExclusiveAsNumeric,
  getMinItemsAsNumeric, getMinLengthAsNumeric, getMinValueAsNumeric, getMinValueExclusiveAsNumeric,
  getMaxValueForScalar, getMinValueForScalar, getMaxValueExclusiveForScalar, getMinValueExclusiveForScalar,
  getPattern, getEncode, isSecret,
  type Numeric, type Type,
} from "@typespec/compiler";
import {
  getArmResource, getArmResourceOperationData, getArmResourceOperationList,
  getResourceBaseType, getResourcePathElements, ResourceBaseType,
} from "@azure-tools/typespec-azure-resource-manager";
import type { SdkClientType, SdkHttpOperation, SdkType } from "@azure-tools/typespec-client-generator-core";
import { isApiVersion } from "@azure-tools/typespec-client-generator-core";
import {
  idKey, type IR, type IRDeploymentBinding, type IRDeploymentContract,
  type IRDeploymentFacet, type IRDeploymentResource, type IRId,
} from "@azure-client-engine/ir";
import type { BuildContext } from "./context.js";
import { operationIdFor } from "./operation-id.js";
import { parameterIdFor } from "./http.js";
import { literalFromValue } from "./literals.js";

export class DeploymentContractError extends Error {}

function fail(detail: string): never {
  throw new DeploymentContractError(detail);
}

function compareText(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
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

function sourceFacet(ctx: BuildContext, target: IRId, typePath: IRDeploymentFacet["typePath"], sources: Type[]): IRDeploymentFacet {
  const first = <T>(getter: (type: Type) => T | undefined): T | undefined =>
    sources.map(getter).find((value) => value !== undefined);
  const numeric = (getter: (program: BuildContext["program"], type: Type) => Numeric | undefined): string | undefined =>
    first((type) => getter(ctx.program, type))?.toString();
  const count = (getter: (program: BuildContext["program"], type: Type) => Numeric | undefined): number | undefined => {
    const value = first((type) => getter(ctx.program, type));
    if (!value) return undefined;
    const number = value.asNumber();
    if (number === null || !Number.isSafeInteger(number) || number < 0 || number > 2147483647) {
      fail(`Length/items constraint exceeds the IR/.NET reader range at ${idKey(target)}`);
    }
    return number;
  };
  if (first((type) => getMinValueForScalar(ctx.program, type)) !== undefined ||
      first((type) => getMaxValueForScalar(ctx.program, type)) !== undefined ||
      first((type) => getMinValueExclusiveForScalar(ctx.program, type)) !== undefined ||
      first((type) => getMaxValueExclusiveForScalar(ctx.program, type)) !== undefined) {
    fail(`Non-numeric scalar bounds are not representable at ${idKey(target)}`);
  }
  if (sources.some((type) => (type.kind === "Scalar" || type.kind === "ModelProperty") &&
      getEncode(ctx.program, type) !== undefined)) {
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
  const pattern = first((type) => getPattern(ctx.program, type));
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
    ...(sources.some((type) => isSecret(ctx.program, type)) ? { sensitive: true } : {}),
  };
}

function collectFacets(ctx: BuildContext): IRDeploymentFacet[] {
  const facets = new Map<string, IRDeploymentFacet>();
  for (const enumeration of ctx.enums) {
    if (enumeration.values.some((value) => typeof value.value === "number" &&
        Number.isInteger(value.value) && !Number.isSafeInteger(value.value))) {
      fail(`Non-representable numeric enum member in ${enumeration.name}`);
    }
  }
  const variantHasFacets = (type: SdkType, target: IRId, seen = new Set<SdkType>()): boolean => {
    if (seen.has(type)) return false;
    seen.add(type);
    if (Object.keys(sourceFacet(ctx, target, [], rawSources(type.__raw))).length > 2) return true;
    if (type.kind === "nullable") return variantHasFacets(type.type, target, seen);
    if (type.kind === "array" || type.kind === "dict") return variantHasFacets(type.valueType, target, seen);
    return type.kind === "union" && type.variantTypes.some((variant) => variantHasFacets(variant, target, seen));
  };
  const collect = (target: IRId, type: SdkType, raw: Type | undefined, path: IRDeploymentFacet["typePath"]): void => {
    if (type.kind === "nullable" && path.length > 0) {
      fail(`Nested collection nullability is not representable in canonical IR at ${idKey(target)}`);
    }
    while (type.kind === "nullable") type = type.type;
    const facet = sourceFacet(ctx, target, path, [...rawSources(raw), ...rawSources(type.__raw)]);
    if (type.kind === "constant" || type.kind === "enumvalue") {
      if (typeof type.value === "number" && Number.isInteger(type.value) && !Number.isSafeInteger(type.value)) {
        fail(`Non-representable numeric literal at ${idKey(target)}`);
      }
      facet.literalValue = literalFromValue(type.value);
    }
    if (Object.keys(facet).length > 2) {
      const key = `${idKey(target)}:${path.join("/")}`;
      const existing = facets.get(key);
      if (existing && JSON.stringify(existing) !== JSON.stringify(facet)) fail(`Ambiguous source facets at ${key}`);
      facets.set(key, facet);
    }
    if (type.kind === "array") collect(target, type.valueType, undefined, [...path, "element"]);
    if (type.kind === "dict") collect(target, type.valueType, undefined, [...path, "value"]);
    if (type.kind === "union") {
      for (const variant of type.variantTypes) {
        if (variantHasFacets(variant, target)) fail(`Constraints on structural union variants are not representable at ${idKey(target)}`);
      }
    }
  };
  for (const [property, id] of ctx.propertyIdCache) collect(id, property.type, property.__raw, []);
  for (const model of ctx.models) {
    const id = ctx.idCache.get(model);
    if (id && model.additionalProperties) collect(id, model.additionalProperties, undefined, ["additionalProperties"]);
  }
  const visit = (client: SdkClientType<SdkHttpOperation>): void => {
    for (const method of client.methods) {
      const operation = operationIdFor(ctx, method.__raw ?? method.operation.__raw.operation);
      for (const parameter of method.operation.parameters) {
        if ((parameter.kind === "path" || parameter.kind === "query" || parameter.kind === "header") &&
            !(parameter.__raw && isApiVersion(ctx.sdkContext, parameter.__raw))) {
          const id = parameterIdFor(operation, parameter);
          collect(id, parameter.type, parameter.__raw, []);
        }
      }
    }
    client.children?.forEach(visit);
  };
  ctx.sdkContext.sdkPackage.clients.forEach(visit);
  return [...facets.values()].sort((left, right) =>
    compareText(`${idKey(left.target)}:${left.typePath.join("/")}`, `${idKey(right.target)}:${right.typePath.join("/")}`));
}

function resourcesFrom(ctx: BuildContext, ir: IR): IRDeploymentResource[] {
  const resources: IRDeploymentResource[] = [];
  const visit = (client: SdkClientType<SdkHttpOperation>): void => {
    for (const method of client.methods) {
      const raw = method.__raw ?? method.operation.__raw.operation;
      if (getArmResourceOperationData(ctx.program, raw)?.kind !== "createOrUpdate") continue;
      const id = operationIdFor(ctx, raw);
      const operation = ir.operations.find((candidate) => idKey(candidate.id) === idKey(id));
      if (!operation || operation.method !== "PUT" || operation.requestBody?.type?.kind !== "model") {
        fail(`ARM create-or-update operation has no faithful PUT/model representation: ${idKey(id)}`);
      }
      const matches = [...ctx.models].filter((model) => model.__raw?.kind === "Model" &&
        getArmResource(ctx.program, model.__raw) &&
        [...getArmResourceOperationList(ctx.program, model.__raw)].some((entry) => entry.operation === raw));
      const model = matches[0]?.__raw;
      if (matches.length !== 1 || model?.kind !== "Model") fail(`ARM resource association is unresolved or ambiguous: ${idKey(id)}`);
      const resolved = getResourcePathElements(method.operation.path, "createOrUpdate");
      if (!resolved) fail(`ARM resource path is unresolved: ${method.operation.path}`);
      const { provider, types } = resolved.resourceType;
      const marker = `/providers/${provider}/`;
      const boundary = resolved.resourceInstancePath.lastIndexOf(marker);
      if (boundary < 0) fail(`ARM scope/name boundary is unresolved: ${resolved.resourceInstancePath}`);
      const prefix = resolved.resourceInstancePath.slice(0, boundary);
      const segments = resolved.resourceInstancePath.slice(boundary + marker.length).split("/");
      const binding = (segment: string): IRDeploymentBinding => {
        const match = /^\{\+?([^{}]+)\}$/.exec(segment);
        if (!match) {
          if (!segment || /[{}%]/.test(segment)) fail(`Non-representable ARM name segment '${segment}'`);
          return { kind: "literal", value: segment };
        }
        const parameters = operation.pathParameters.filter((parameter) => parameter.wireName === match[1]);
        if (parameters.length !== 1) fail(`Unresolved ARM input '${match[1]}' in ${idKey(id)}`);
        return { kind: "parameter", parameter: parameters[0]!.id };
      };
      if (segments.length !== types.length * 2 ||
          types.some((type, index) => type !== segments[index * 2])) {
        fail(`Non-representable ARM name hierarchy: ${resolved.resourceInstancePath}`);
      }
      const name = types.map((_, index) => binding(segments[index * 2 + 1]!));
      const scopeParameters = [...prefix.matchAll(/\{\+?([^{}]+)\}/g)].map((match) => {
        const value = binding(match[0]);
        if (value.kind !== "parameter") fail(`Unresolved ARM scope in ${idKey(id)}`);
        return value.parameter;
      });
      let writableScope: IRDeploymentResource["writableScope"];
      switch (getResourceBaseType(ctx.program, model)) {
        case ResourceBaseType.Tenant: writableScope = "tenant"; break;
        case ResourceBaseType.Subscription: writableScope = "subscription"; break;
        case ResourceBaseType.ResourceGroup: writableScope = "resourceGroup"; break;
        case ResourceBaseType.Extension: writableScope = "resource"; break;
        default: fail(`Unsupported ARM deployment scope in ${idKey(id)}`);
      }
      const expected = { tenant: 0, subscription: 1, resourceGroup: 2, resource: 1, managementGroup: 1 }[writableScope];
      if (scopeParameters.length !== expected) fail(`Unresolved ${writableScope} scope bindings in ${idKey(id)}`);
      resources.push({
        operation: id, resourceType: `${provider}/${types.join("/")}`,
        apiVersion: operation.apiVersion.value, writableScope, name, scopeParameters,
      });
    }
    client.children?.forEach(visit);
  };
  ctx.sdkContext.sdkPackage.clients.forEach(visit);
  return resources.sort((left, right) => compareText(idKey(left.operation), idKey(right.operation)));
}

async function toolVersion(name: string): Promise<{ name: string; version: string }> {
  let directory = dirname(fileURLToPath(import.meta.resolve(name)));
  for (;;) {
    let text: string;
    try {
      text = await readFile(join(directory, "package.json"), "utf8");
    } catch (error) {
      if (!(error instanceof Error) || !("code" in error) || error.code !== "ENOENT") throw error;
      const parent = dirname(directory);
      if (parent === directory) fail(`Cannot resolve toolchain identity for ${name}`);
      directory = parent;
      continue;
    }
    const metadata: unknown = JSON.parse(text);
    if (typeof metadata === "object" && metadata !== null && "name" in metadata && metadata.name === name &&
        "version" in metadata && typeof metadata.version === "string") {
      return { name, version: metadata.version };
    }
    const parent = dirname(directory);
    if (parent === directory) fail(`Cannot resolve toolchain version for ${name}`);
    directory = parent;
  }
}

export async function deploymentFrom(ctx: BuildContext, ir: IR, sourceCommit?: string): Promise<IRDeploymentContract> {
  const resources = resourcesFrom(ctx, ir);
  const inputIds = new Set([
    ...ir.models.map((model) => idKey(model.id)),
    ...ir.models.flatMap((model) => model.properties.map((property) => idKey(property.id))),
    ...ir.operations.flatMap((operation) =>
      [...operation.pathParameters, ...operation.queryParameters, ...operation.headerParameters].map((parameter) => idKey(parameter.id))),
  ]);
  const facets = collectFacets(ctx);
  for (const facet of facets) {
    if (!inputIds.has(idKey(facet.target))) fail(`Source facet has no canonical input: ${idKey(facet.target)}`);
  }
  const files = [...ctx.program.sourceFiles.values()].map(({ file }) => {
    const path = file.path.replace(/\\/g, "/");
    const dependency = path.lastIndexOf("/node_modules/");
    const identity = dependency >= 0 ? path.slice(dependency + 14) : relative(ctx.program.projectRoot, file.path).replace(/\\/g, "/");
    return [identity, createHash("sha256").update(file.text).digest("hex")];
  }).sort((left, right) => compareText(left[0]!, right[0]!));
  const tools = await Promise.all([
    "@typespec/compiler", "@azure-tools/typespec-azure-resource-manager",
    "@azure-tools/typespec-client-generator-core", "@azure-client-engine/typespec-emitter",
  ].map(toolVersion));
  return {
    schemaVersion: "1.0.0", resources, facets,
    provenance: {
      sourceHash: createHash("sha256").update(JSON.stringify(files)).digest("hex"),
      ...(sourceCommit !== undefined ? { sourceCommit } : {}),
      tools: tools.sort((left, right) => left.name.localeCompare(right.name)),
    },
  };
}
