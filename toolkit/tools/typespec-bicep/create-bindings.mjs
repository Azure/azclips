import assert from "node:assert/strict";
import { idKey, idEquals } from "@azure-client-engine/ir";

// Projection realms share declaration nodes, not cloned Type objects.
export function sameSource(left, right, seen = new Map()) {
  if (!left || !right) return false;
  if (left === right) return true;
  if (!left || !right || left.kind !== right.kind || !left.node || left.node !== right.node) return false;
  if (seen.get(left)?.has(right)) return true;
  if (!seen.has(left)) seen.set(left, new Set());
  seen.get(left).add(right);
  if (left.kind === "ModelProperty" && !sameSource(left.model, right.model, seen)) return false;
  const leftArgs = left.templateMapper?.args ?? [];
  const rightArgs = right.templateMapper?.args ?? [];
  return leftArgs.length === rightArgs.length && leftArgs.every((arg, i) => sameSource(arg, rightArgs[i], seen));
}

function sourceChain(source) {
  const chain = [];
  for (let current = source; current && !chain.includes(current); current = current.sourceProperty) {
    chain.push(current);
  }
  return chain;
}

function correspondingSource(left, right) {
  return sourceChain(left).some((a) => sourceChain(right).some((b) => sameSource(a, b)));
}

function only(items, message) {
  assert.equal(items.length, 1, `${message} (found ${items.length})`);
  return items[0];
}

export function createBindings({ ir, sources, selected, types, correspondence, program, getSourceConstraints, bicep }) {
  const sourceOf = (kind, id) => {
    const matches = sources.filter((record) => record.kind === kind && idEquals(record.id, id));
    const unique = [...new Set(matches.map(({ source }) => source))];
    return only(unique, `Missing or ambiguous canonical ${kind} source for ${idKey(id)}`);
  };
  const modelOf = (id) => only(ir.models.filter((model) => idEquals(model.id, id)), `Unresolved canonical model ${idKey(id)}`);
  const operationSources = sources.filter((record) =>
    record.kind === "operation" && correspondingSource(record.source, selected.create.operation));
  const operationIds = new Map(operationSources.map(({ id }) => [idKey(id), id]));
  const operationId = only([...operationIds.values()], "Missing or ambiguous canonical create operation");
  const operation = only(ir.operations.filter(({ id }) => idEquals(id, operationId)), "Canonical create operation is missing");
  assert.equal(operation.method, "PUT");
  assert.equal(operation.apiVersion.value, selected.provider.apiVersion, "Canonical and Bicep API versions differ");
  assert.equal(operation.pathTemplate, selected.create.httpOperation.path, "Canonical and ARM create paths differ");
  assert.equal(operation.requestBody?.type?.kind, "model", "Canonical create requires a model body");
  const command = only(ir.commands.filter((candidate) => candidate.template === "atomic" &&
    candidate.steps.length === 1 && idEquals(candidate.steps[0].operation, operationId)),
  "Missing or ambiguous atomic create command");
  const definition = only(Object.values(selected.provider.resourcesByType).flat(), "Exactly one resource must be selected");
  const resourceName = `${definition.descriptor.namespace}/${definition.descriptor.typeSegments.join("/")}@${definition.descriptor.apiVersion}`;
  const resourceType = only(types.filter((type) => type.type === bicep.TypeBaseKind.ResourceType && type.name === resourceName),
    "Generated resource type is missing or ambiguous");
  const bodyId = operation.requestBody.type.id;
  assert.ok(sameSource(sourceOf("model", bodyId), definition.putModel), "Canonical and projected PUT body sources differ");

  const atContainer = (index, source) => correspondence.filter((record) =>
    record.container.index === index && record.putProperty && correspondingSource(record.putProperty, source));
  const walk = (reference, input) => {
    let model = modelOf(bodyId);
    let container = resourceType.body.index;
    const wirePath = [];
    const bicepPath = [];
    const hops = [...reference.via, reference.property];
    let record;
    for (const [position, id] of hops.entries()) {
      const property = only(model.properties.filter((property) => idEquals(property.id, id)),
        `Broken canonical containing path for ${input}`);
      const source = sourceOf("property", property.id);
      record = only(atContainer(container, source), `Missing or ambiguous Bicep field for ${input} at ${wirePath.join(".")}`);
      const actual = types[container]?.properties?.[record.name] ?? types[container]?.baseProperties?.[record.name];
      assert.ok(actual, `Bicep property observer has no field at container ${container}`);
      assert.deepEqual(actual.type, record.property.type, "Stale Bicep property correspondence");
      assert.equal(actual.flags, record.property.flags, "Stale Bicep property flags");
      wirePath.push(property.wireName);
      bicepPath.push(record.name);
      if (position < hops.length - 1) {
        assert.equal(property.type.kind, "model", `Non-model canonical container for ${input}`);
        model = modelOf(property.type.id);
        container = record.property.type.index;
        assert.ok([bicep.TypeBaseKind.ObjectType, bicep.TypeBaseKind.DiscriminatedObjectType].includes(types[container]?.type),
          `Unsupported Bicep container for ${input}; cannot guess a union branch`);
      }
    }
    return { wirePath, bicepPath, record };
  };

  const nameSegment = selected.resource.resourceInstancePath.split("/").at(-1);
  assert.ok(/^\{[^{}]+\}$/.test(nameSegment), "Create binding requires a parameterized resource name");
  const nameWire = nameSegment.slice(1, -1);
  assert.equal(selected.resource.scope, "ResourceGroup", "Only resource-group create bindings are supported");
  assert.equal(definition.descriptor.typeSegments.length, 1, "Nested resource scope/name bindings are not supported");
  const officialPaths = selected.create.httpOperation.parameters.parameters.filter(({ type }) => type === "path");
  assert.equal(officialPaths.length, 3, "Resource-group create must have two scope inputs and one resource name");
  const rootName = only(correspondence.filter((record) =>
    record.container.index === resourceType.body.index && record.role === "resourceName"), "Missing Bicep resource name");
  assert.ok(correspondingSource(rootName.putProperty, definition.nameProperty),
    "Bicep resource-name source differs from the selected ARM name property");

  const bindings = command.parameters.map((parameter) => {
    const base = {
      parameter: parameter.name, source: parameter.source,
      type: parameter.type, required: parameter.required, nullable: parameter.nullable,
      ...(parameter.defaultValue !== undefined ? { defaultValue: parameter.defaultValue } : {}),
    };
    if (parameter.source.kind === "body") {
      const result = walk(parameter.source, parameter.name);
      const source = sourceOf("property", parameter.source.property);
      const constraints = getSourceConstraints(program, parameter.source.property, source);
      let role = "field";
      if (result.record.role === "computedIdentity") role = "computedIdentity";
      else if (result.record.property.flags & bicep.ObjectTypePropertyFlags.ReadOnly) role = "readOnly";
      else if (result.record.role === "resourceName") role = "resourceName";
      return {
        ...base, role, wirePath: result.wirePath, bicepPath: result.bicepPath,
        bicepField: { container: result.record.container, name: result.record.name,
          type: result.record.property.type, flags: result.record.property.flags },
        constraints,
      };
    }
    if (parameter.source.kind === "path") {
      const source = sourceOf("parameter", parameter.source.parameter);
      const official = only(officialPaths.filter(({ param }) => correspondingSource(param, source)),
        `Missing or ambiguous official path parameter for ${parameter.name}`);
      const name = official.name === nameWire;
      return { ...base, role: name ? "resourceName" : "scope", wireName: official.name,
        ...(name ? { bicepPath: [rootName.name], bicepField: { container: rootName.container,
          name: rootName.name, type: rootName.property.type, flags: rootName.property.flags } } : {}) };
    }
    assert.equal(parameter.source.kind, "header", `Unsupported non-body input ${parameter.name}`);
    const header = only(operation.headerParameters.filter(({ id }) => idEquals(id, parameter.source.parameter)),
      `Missing header for ${parameter.name}`);
    assert.ok(parameter.access === "internal" &&
      ["content-type", "accept"].includes(header.wireName.toLowerCase()), `Unhandled HTTP input ${parameter.name}`);
    return { ...base, role: "protocol", wireName: header.wireName };
  });
  return {
    schemaVersion: "1.0.0", resourceType: resourceName,
    operation: operationId, command: command.id, commandName: command.name,
    bindings,
    readOnlyFields: correspondence.filter((record) => record.property.flags & bicep.ObjectTypePropertyFlags.ReadOnly)
      .map(({ container, name, role, property }) => ({ container, name, role, type: property.type, flags: property.flags })),
  };
}

export function classifyPublicIpEligibility(bridge) {
  const allowed = new Set([
    ["location"], ["tags"], ["sku", "name"], ["sku", "tier"], ["properties", "publicIPAllocationMethod"],
    ["properties", "publicIPAddressVersion"], ["properties", "idleTimeoutInMinutes"],
  ].map((path) => JSON.stringify(path)));
  return {
    ...bridge,
    bindings: bridge.bindings.map((binding) => ({
      ...binding,
      eligibility: binding.role === "field"
        ? (allowed.has(JSON.stringify(binding.wirePath)) ? "eligible" : "excluded")
        : binding.role === "computedIdentity" || binding.role === "readOnly" ? "reject" : "structural",
    })),
  };
}

export function createBindingFixture(bridge, provenance) {
  const names = new Set();
  const bindings = bridge.bindings.map((binding) => {
    if (binding.eligibility !== "eligible" && binding.role !== "resourceName") return binding;
    const deploymentParameter = binding.parameter[0].toLowerCase() + binding.parameter.slice(1);
    assert.ok(!names.has(deploymentParameter), `Duplicate deployment parameter ${deploymentParameter}`);
    names.add(deploymentParameter);
    return { ...binding, deploymentParameter };
  });
  return {
    schemaVersion: bridge.schemaVersion, resourceType: bridge.resourceType,
    operation: bridge.operation, command: bridge.command, commandName: bridge.commandName,
    provenance, bindings,
  };
}
