import {
  getDiscriminatedUnion, getPagingOperation, isList, resolveEncodedName,
  walkPropertiesInherited,
  type ModelProperty, type Operation, type PagingOperation, type PagingProperty, type Type,
} from "@typespec/compiler";
import {
  createMetadataInfo, resolveRequestVisibility, Visibility,
  type HttpPayloadBody,
} from "@typespec/http";
import {
  getClientTypeWithDiagnostics, getDisablePageable, getGeneratedName, getLibraryName,
  getMarkAsPageable, getNextLinkVerb, getPropertyNames, isApiVersion,
  type SdkHttpOperation, type SdkServiceMethod,
} from "@azure-tools/typespec-client-generator-core";
import { getParameterizedNextLinkArguments } from "@azure-tools/typespec-azure-core";
import type {
  IRId, IRPageableSemantics, IRPagingBinding, IRPagingProperty, IRPagingSegment,
} from "@azure-client-engine/ir";
import type { BuildContext } from "./context.js";
import { reportSdkDiagnostics } from "./discriminators.js";
import { parameterIdFor, statusCodeFrom } from "./http.js";
import { reportDiagnostic } from "./lib.js";
import { operationIdFor } from "./operation-id.js";
import { sourceId, typeRefFor, unwrapNullable } from "./types.js";

const inputRoles = ["offset", "pageIndex", "pageSize", "continuationToken"] as const;
const outputRoles = ["pageItems", "nextLink", "prevLink", "firstLink", "lastLink", "continuationToken"] as const;
const roles = [...new Set([...inputRoles, ...outputRoles])];

function rootProperty(property: ModelProperty): ModelProperty {
  while (property.sourceProperty) property = property.sourceProperty;
  return property;
}

function sameProperty(left: ModelProperty | undefined, right: ModelProperty): boolean {
  return left !== undefined && rootProperty(left) === rootProperty(right);
}

function visitProperties(
  type: Type,
  visit: (property: ModelProperty, path: ModelProperty[]) => void,
  path: ModelProperty[] = [],
  visited = new Set<Type>(),
): void {
  if (visited.has(type)) return;
  visited.add(type);
  if (type.kind === "Model") {
    for (const property of walkPropertiesInherited(type)) {
      const propertyPath = [...path, property];
      visit(property, propertyPath);
      visitProperties(property.type, visit, propertyPath, visited);
    }
  } else if (type.kind === "Union") {
    for (const variant of type.variants.values()) visitProperties(variant.type, visit, path, visited);
  } else if (type.kind === "UnionVariant") {
    visitProperties(type.type, visit, path, visited);
  }
}

function sourcePaths(type: Type, target: ModelProperty): ModelProperty[][] {
  const paths: ModelProperty[][] = [];
  visitProperties(type, (property, path) => {
    if (sameProperty(property, target)) paths.push(path);
  });
  return paths;
}

/**
 * The compiler omits partial metadata when items are missing. Standard marker
 * state also lets us diagnose that case without treating every basic op as a
 * malformed list, and preserve remaining roles for Legacy.markAsPageable.
 */
function partialPaging(ctx: BuildContext, raw: Operation) {
  const input: Partial<Record<typeof inputRoles[number], PagingProperty>> = {};
  const output: Partial<Record<typeof outputRoles[number], PagingProperty>> = {};
  let hasMarkers = false;
  const marked = (property: ModelProperty, role: string) =>
    ctx.program.stateSet(Symbol.for(`TypeSpec.${role}`)).has(property);
  visitProperties(raw.parameters, (property, path) => {
    hasMarkers ||= roles.some((role) => marked(property, role));
    for (const role of inputRoles) {
      if (marked(property, role)) input[role] ??= { property, path };
    }
  });
  visitProperties(raw.returnType, (property, path) => {
    hasMarkers ||= roles.some((role) => marked(property, role));
    for (const role of outputRoles) {
      if (marked(property, role)) output[role] ??= { property, path };
    }
  });
  return { input, output, hasMarkers };
}

function sourceSegments(
  ctx: BuildContext, raw: Operation, operationId: IRId,
  path: ModelProperty[], direction: "input" | "output",
): IRPagingSegment[] {
  return path.map((property, index) => {
    let id: IRId;
    if (direction === "input" && index === 0) {
      id = { ...operationId, name: `${operationId.name}.${property.name}` };
    } else if (property.model) {
      const owner = property.model;
      const name = getLibraryName(ctx.sdkContext, owner) || getGeneratedName(ctx.sdkContext, owner, raw);
      id = sourceId(ctx, owner, `${name}.${getPropertyNames(ctx.sdkContext, property)[0]}`);
    } else {
      id = { ...operationId, name: `${operationId.name}.${direction}.${path.slice(0, index + 1).map((p) => p.name).join(".")}` };
    }
    return { id, name: property.name };
  });
}

function bodyPaths(
  ctx: BuildContext, body: HttpPayloadBody, target: ModelProperty,
  contentType: string, visibility: Visibility,
): string[][] {
  if (sameProperty(body.property, target)) return [[]];
  if (body.bodyKind !== "single") return [];
  return valuePaths(ctx, body.type, target, contentType, visibility, body.isExplicit);
}

function valuePaths(
  ctx: BuildContext, root: Type, target: ModelProperty, contentType: string,
  visibility: Visibility, inExplicitBody: boolean,
): string[][] {
  const metadata = createMetadataInfo(ctx.program);
  const result: string[][] = [];
  const reachable = new Map<Type, boolean>();
  function walk(type: Type, path: string[], ancestors: Set<Type>): void {
    if (ancestors.has(type)) return;
    let containsTarget = reachable.get(type);
    if (containsTarget === undefined) {
      containsTarget = sourcePaths(type, target).length !== 0;
      reachable.set(type, containsTarget);
    }
    if (!containsTarget) return;
    const next = new Set(ancestors).add(type);
    if (type.kind === "Model") {
      for (const property of walkPropertiesInherited(type)) {
        if (!metadata.isPayloadProperty(property, visibility, inExplicitBody)) continue;
        const propertyPath = [...path, resolveEncodedName(ctx.program, property, contentType)];
        if (sameProperty(property, target)) result.push(propertyPath);
        else walk(property.type, propertyPath, next);
      }
    } else if (type.kind === "Union") {
      const [union, diagnostics] = getDiscriminatedUnion(ctx.program, type);
      ctx.program.reportDiagnostics(diagnostics);
      const payloadPath = union?.options.envelope === "object"
        ? [...path, union.options.envelopePropertyName] : path;
      for (const variant of type.variants.values()) walk(variant.type, payloadPath, next);
    } else if (type.kind === "UnionVariant") {
      walk(type.type, path, next);
    }
  }
  walk(root, [], new Set());
  return result;
}

function bindingsFor(
  ctx: BuildContext, method: SdkServiceMethod<SdkHttpOperation>, operationId: IRId,
  target: ModelProperty, direction: "input" | "output",
): IRPagingBinding[] {
  const http = method.operation.__raw;
  const bindings: IRPagingBinding[] = [];
  if (direction === "input") {
    function parameterFor(property: ModelProperty): IRId | undefined {
      const parameter = method.operation.parameters.find((p) => sameProperty(p.__raw, property));
      return parameter && parameter.kind !== "cookie"
        && !(parameter.__raw && isApiVersion(ctx.sdkContext, parameter.__raw))
        ? parameterIdFor(operationId, parameter) : undefined;
    }
    const visibility = resolveRequestVisibility(ctx.program, http.operation, http.verb);
    for (const p of http.parameters.parameters) {
      const paths = sameProperty(p.param, target) ? [[]]
        : valuePaths(ctx, p.param.type, target, "application/json", visibility, true);
      const parameter = parameterFor(p.param);
      for (const path of paths) {
        bindings.push({ location: p.type, path: [p.name, ...path], ...(parameter ? { parameter } : {}) });
      }
    }
    const body = http.parameters.body;
    if (body && sameProperty(body.contentTypeProperty, target)) {
      const parameter = parameterFor(target);
      bindings.push({ location: "header", path: ["Content-Type"], ...(parameter ? { parameter } : {}) });
    }
    if (body) {
      for (const contentType of body.contentTypes) {
        for (const path of bodyPaths(ctx, body, target, contentType, visibility)) {
          bindings.push({ location: "body", path, contentType });
        }
      }
    }
  } else {
    for (const response of http.responses) {
      for (const content of response.responses) {
        const statusCode = statusCodeFrom(response.statusCodes);
        const contentTypes = content.body?.contentTypes ?? [undefined];
        for (const contentType of contentTypes) {
          const context = {
            response: { statusCode },
            ...(contentType !== undefined ? { contentType } : {}),
          };
          for (const [wireName, property] of Object.entries(content.headers ?? {})) {
            const paths = sameProperty(property, target) ? [[]]
              : valuePaths(ctx, property.type, target, "application/json", Visibility.Read, true);
            for (const path of paths) {
              bindings.push({ location: "header", path: [wireName, ...path], ...context });
            }
          }
          if (content.body && sameProperty(content.body.contentTypeProperty, target)) {
            bindings.push({ location: "header", path: ["Content-Type"], ...context });
          }
          if (content.body && contentType !== undefined) {
            for (const path of bodyPaths(ctx, content.body, target, contentType, Visibility.Read)) {
              bindings.push({ location: "body", path, ...context });
            }
          }
        }
      }
    }
  }
  const unique = [...new Map(bindings.map((binding) => [JSON.stringify(binding), binding])).values()];
  if (unique.length === 0) {
    reportDiagnostic(ctx.program, {
      code: "paging-property-unbound",
      target,
      format: { operation: http.operation.name, property: target.name },
    });
  }
  return unique;
}

function nextLinkArguments(ctx: BuildContext, type: Type, visited = new Set<Type>()): ModelProperty[] {
  if (visited.has(type)) return [];
  visited.add(type);
  if (type.kind === "UnionVariant") return nextLinkArguments(ctx, type.type, visited);
  if (type.kind === "Union") {
    const variants = [...type.variants.values()]
      .filter((variant) => !(variant.type.kind === "Intrinsic" && variant.type.name === "null"));
    if (variants.length === 1) return nextLinkArguments(ctx, variants[0]!.type, visited);
  }
  if (type.kind === "Scalar") {
    for (let scalar: typeof type | undefined = type; scalar; scalar = scalar.baseScalar) {
      const parameters = getParameterizedNextLinkArguments(ctx.program, scalar);
      if (parameters !== undefined) return parameters;
    }
  }
  return [];
}

export function pagingFor(
  ctx: BuildContext, method: SdkServiceMethod<SdkHttpOperation>, operationId: IRId,
): IRPageableSemantics | undefined {
  const raw = method.operation.__raw.operation;
  const [resolved, diagnostics] = getPagingOperation(ctx.program, raw);
  const marked = getMarkAsPageable(ctx.sdkContext, raw);
  const partial = resolved ? undefined : partialPaging(ctx, raw);
  const relevantDiagnostics = diagnostics.filter((d) =>
    d.code !== "missing-paging-items" || (!marked && (isList(ctx.program, raw) || partial?.hasMarkers)));
  ctx.program.reportDiagnostics(relevantDiagnostics);
  if (relevantDiagnostics.some((d) => d.severity === "error")) return undefined;

  let paging = resolved;
  if (!paging && marked) {
    const path = sourcePaths(raw.returnType, marked.itemsProperty)[0];
    if (!path) {
      reportDiagnostic(ctx.program, {
        code: "paging-property-unbound", target: marked.itemsProperty,
        format: { operation: raw.name, property: marked.itemsProperty.name },
      });
      return undefined;
    }
    paging = { input: partial!.input, output: { ...partial!.output, pageItems: { property: marked.itemsProperty, path } } };
  }
  if (!paging) return undefined;

  function propertyFrom(value: PagingProperty, direction: "input" | "output"): IRPagingProperty {
    const [sdkType, errors] = getClientTypeWithDiagnostics(ctx.sdkContext, value.property.type, raw);
    reportSdkDiagnostics(ctx.program, errors);
    const { type, nullable } = unwrapNullable(ctx, sdkType);
    return {
      sourcePath: sourceSegments(ctx, raw, operationId, value.path, direction),
      type: typeRefFor(ctx, type, `${raw.name}.paging.${value.property.name}`),
      optional: value.property.optional,
      nullable,
      bindings: bindingsFor(ctx, method, operationId, value.property, direction),
    };
  }

  const input: IRPageableSemantics["input"] = {};
  for (const role of inputRoles) {
    const value = paging.input[role];
    if (value) input[role] = propertyFrom(value, "input");
  }
  const pageItems = propertyFrom(paging.output.pageItems, "output");
  if (pageItems.type.kind !== "array") {
    reportDiagnostic(ctx.program, {
      code: "paging-items-not-array", target: paging.output.pageItems.property,
      format: { operation: raw.name },
    });
    return undefined;
  }
  const output: IRPageableSemantics["output"] = { pageItems };
  for (const role of outputRoles) {
    if (role === "pageItems") continue;
    const value = paging.output[role];
    if (value) output[role] = propertyFrom(value, "output");
  }
  const nextLink = paging.output.nextLink?.property;
  const reinjected = nextLink ? nextLinkArguments(ctx, nextLink.type) : [];
  const nextLinkReinjectedParameters = reinjected.map((property) =>
    propertyFrom({ property, path: sourcePaths(raw.parameters, property)[0] ?? [property] }, "input"));
  const sdkMetadata = method.kind === "paging" || method.kind === "lropaging" ? method.pagingMetadata : undefined;
  const nextOperation = sdkMetadata?.nextLinkOperation;
  return {
    isList: isList(ctx.program, raw),
    metadataSource: resolved ? "compiler" : "sdk",
    itemType: pageItems.type.element,
    input,
    output,
    sdk: {
      disabled: getDisablePageable(ctx.sdkContext, raw),
      forced: marked !== undefined,
      nextLinkVerb: getNextLinkVerb(ctx.sdkContext, raw),
      nextLinkReinjectedParameters,
      ...(nextOperation ? {
        nextLinkOperation: operationIdFor(ctx, nextOperation.__raw ?? nextOperation.operation.__raw.operation),
      } : {}),
    },
  };
}
