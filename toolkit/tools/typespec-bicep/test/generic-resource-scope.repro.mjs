import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { before, test } from "node:test";
import { getArmResources, resolveArmResources } from "@azure-tools/typespec-azure-resource-manager";
import { bicepTypes, compiler, getProviderDefinitionsChecked } from "../index.mjs";
import { upstream } from "../prepare.mjs";

// Opt-in: the generic-resource assertion intentionally fails until upstream is fixed.
const cases = new Map();
const { ScopeType } = bicepTypes;

before(async () => {
  const program = await compiler.compile(compiler.NodeHost,
    fileURLToPath(new URL("./generic-resource-scope.tsp", import.meta.url)), { noEmit: true });
  const errors = program.diagnostics.filter(({ severity, code }) =>
    severity === "error" || code === "incompatible-library");
  assert.deepEqual(errors.map(({ code, message }) => ({ code, message })), []);
  const services = compiler.listServices(program);
  assert.equal(services.length, 1);
  const namespace = services[0].type;
  const registered = getArmResources(program);
  const resolved = resolveArmResources(program).resources;
  assert.ok(resolved);
  const definitions = getProviderDefinitionsChecked({ program, options: {}, emitterOutputDir: "" }, "2025-07-01");
  assert.equal(definitions.length, 1);

  for (const [modelName, collection, parameter] of [
    ["StandardWidget", "standardWidgets", "standardWidgetName"],
    ["GenericWidget", "genericWidgets", "genericWidgetName"],
  ]) {
    const model = namespace.models.get(modelName);
    assert.ok(model);
    const registrations = registered.filter(({ typespecType }) => typespecType === model);
    assert.equal(registrations.length, 1);
    const resourceType = `Test.ScopeRepro/${collection}`;
    const matches = resolved.filter((resource) => resource.type === model &&
      [resource.resourceType.provider, ...resource.resourceType.types].join("/") === resourceType);
    assert.equal(matches.length, 1);
    const writes = matches[0].operations.lifecycle.createOrUpdate;
    assert.equal(writes?.length, 1);
    const write = writes[0];
    assert.equal(write.httpOperation.verb, "put");
    assert.equal(write.httpOperation.parameters.body?.type, model);
    assert.equal(write.path,
      `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Test.ScopeRepro/${collection}/{${parameter}}`);
    const emitted = definitions[0].resourcesByType[resourceType.toLowerCase()];
    assert.equal(emitted?.length, 1);
    assert.equal(emitted[0].getModel, model);

    cases.set(modelName, {
      upstreamRevision: upstream.revision,
      resourceType,
      registeredKind: registrations[0].kind,
      collectionName: registrations[0].collectionName ?? null,
      officialPut: write.path,
      expectedWritableScope: ScopeType.ResourceGroup,
      emittedReadableScope: emitted[0].descriptor.readableScopes,
      emittedWritableScope: emitted[0].descriptor.writableScopes,
    });
  }
});

test("control: standard resource retains its resource-group writable scope", (t) => {
  const standard = cases.get("StandardWidget");
  t.diagnostic(JSON.stringify(standard));
  assert.equal(standard.registeredKind, "Tracked");
  assert.equal(standard.emittedReadableScope, ScopeType.ResourceGroup);
  assert.equal(standard.emittedWritableScope, ScopeType.ResourceGroup);
});

test("generic resource retains its officially resolved PUT writable scope", (t) => {
  const generic = cases.get("GenericWidget");
  t.diagnostic(JSON.stringify(generic));
  assert.equal(generic.registeredKind, "Generic");
  assert.equal(generic.collectionName, null);
  assert.equal(generic.emittedReadableScope, ScopeType.ResourceGroup);
  assert.equal(generic.emittedWritableScope, ScopeType.ResourceGroup,
    "A registered generic resource with an official RG-scoped PUT must not become read-only.");
});
