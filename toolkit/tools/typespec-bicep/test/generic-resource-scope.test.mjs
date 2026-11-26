import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { before, test } from "node:test";
import { getArmResources, resolveArmResources } from "@azure-tools/typespec-azure-resource-manager";
import { bicepTypes, compiler, emitter, getProviderDefinitionsChecked } from "../index.mjs";
import { upstream } from "../prepare.mjs";

const cases = new Map();
const { ScopeType } = bicepTypes;
let guardedProgram;
let guardedDefinitions;

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
  const generated = JSON.parse(bicepTypes.writeTypesJson(emitter.generateTypes(program, definitions[0])));

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
    const resourceTypeSchema = generated.find((type) =>
      type.$type === "ResourceType" && type.name === `${resourceType}@2025-07-01`);
    assert.ok(resourceTypeSchema);

    cases.set(modelName, {
      upstreamRevision: upstream.revision,
      resourceType,
      registeredKind: registrations[0].kind,
      collectionName: registrations[0].collectionName ?? null,
      officialPut: write.path,
      expectedWritableScope: ScopeType.ResourceGroup,
      emittedReadableScope: emitted[0].descriptor.readableScopes,
      emittedWritableScope: emitted[0].descriptor.writableScopes,
      generatedReadableScope: resourceTypeSchema.readableScopes,
      generatedWritableScope: resourceTypeSchema.writableScopes,
    });
  }

  guardedProgram = await compiler.compile(compiler.NodeHost,
    fileURLToPath(new URL("./generic-resource-guards.tsp", import.meta.url)), { noEmit: true });
  assert.deepEqual(guardedProgram.diagnostics.filter(({ severity, code }) =>
    severity === "error" || code === "incompatible-library").map(({ code, message }) => ({ code, message })), []);
  const guarded = getProviderDefinitionsChecked(
    { program: guardedProgram, options: {}, emitterOutputDir: "" }, "2025-07-01");
  assert.equal(guarded.length, 1);
  guardedDefinitions = guarded[0].resourcesByType;
});

test("control: standard resource retains its resource-group writable scope", (t) => {
  const standard = cases.get("StandardWidget");
  t.diagnostic(JSON.stringify(standard));
  assert.equal(standard.registeredKind, "Tracked");
  assert.equal(standard.emittedReadableScope, ScopeType.ResourceGroup);
  assert.equal(standard.emittedWritableScope, ScopeType.ResourceGroup);
  assert.equal(standard.generatedReadableScope, ScopeType.ResourceGroup);
  assert.equal(standard.generatedWritableScope, ScopeType.ResourceGroup);
});

test("generic resource retains its officially resolved PUT writable scope", (t) => {
  const generic = cases.get("GenericWidget");
  t.diagnostic(JSON.stringify(generic));
  assert.equal(generic.registeredKind, "Generic");
  assert.equal(generic.collectionName, null);
  assert.equal(generic.emittedReadableScope, ScopeType.ResourceGroup);
  assert.equal(generic.emittedWritableScope, ScopeType.ResourceGroup,
    "A registered generic resource with an official RG-scoped PUT must not become read-only.");
  assert.equal(generic.generatedReadableScope, ScopeType.ResourceGroup);
  assert.equal(generic.generatedWritableScope, ScopeType.ResourceGroup);
});

test("reused generic models retain per-type and per-scope write eligibility", () => {
  const writable = guardedDefinitions["test.scoperepro/genericwidgets"];
  assert.equal(writable?.length, 1);
  assert.equal(writable[0].descriptor.readableScopes, ScopeType.ResourceGroup | ScopeType.Subscription);
  assert.equal(writable[0].descriptor.writableScopes, ScopeType.ResourceGroup);
  const readOnly = guardedDefinitions["test.scoperepro/readonlywidgets"];
  assert.equal(readOnly?.length, 1);
  assert.equal(readOnly[0].descriptor.readableScopes, ScopeType.Subscription);
  assert.equal(readOnly[0].descriptor.writableScopes, ScopeType.None);
});

test("unrelated success bodies and foreign providers do not become generic resource instances", () => {
  assert.ok(!Object.hasOwn(guardedDefinitions, "test.scoperepro/genericwidgets/backups"));
  assert.ok(!Object.hasOwn(guardedDefinitions, "test.scoperepro/foreignwidgets"));
});

test("generic resources preserve a distinct PUT request model", () => {
  const resources = guardedDefinitions["test.scoperepro/separatewidgets"];
  assert.equal(resources?.length, 1);
  const namespace = compiler.listServices(guardedProgram)[0].type;
  assert.ok(resources[0].getModel === namespace.models.get("SeparateWidget"));
  assert.ok(resources[0].putModel === namespace.models.get("CreateSeparateWidget"));
  assert.equal(resources[0].descriptor.writableScopes, ScopeType.ResourceGroup);
});

test("conflicting PUT models for the same generic type are diagnosed rather than selected", () => {
  assert.equal(guardedDefinitions["test.scoperepro/ambiguouswidgets"], undefined);
  assert.ok(guardedProgram.diagnostics.some(({ code, message }) =>
    code === "@azure-tools/typespec-bicep/emitter-warning" &&
    message.includes("ambiguousWidgets") && message.includes("PUT request models")));
});

test("unknown generic scope paths are diagnosed rather than defaulted to resource group", () => {
  const unknown = resolveArmResources(guardedProgram).resources.filter(({ resourceType }) =>
    resourceType.types.join("/") === "unknownWidgets");
  assert.equal(unknown.length, 1);
  assert.equal(unknown[0].operations.lifecycle.createOrUpdate?.length, 1);
  assert.ok(!Object.hasOwn(guardedDefinitions, "test.scoperepro/unknownwidgets"));
  assert.ok(guardedProgram.diagnostics.some(({ code, message }) =>
    code === "@azure-tools/typespec-bicep/no-resource-scopes" && message.includes("unknownWidgets")));
});
