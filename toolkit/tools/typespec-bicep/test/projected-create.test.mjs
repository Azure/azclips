import assert from "node:assert/strict";
import { cpSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { before, test } from "node:test";
import { fileURLToPath } from "node:url";
import { buildEmitter, emitCanonical, loadToolchain, projectCommands, requireNoErrors, selectProjectedCreate } from "../project-public-ip.mjs";
import { classifyPublicIpEligibility, createBindings, sameSource } from "../create-bindings.mjs";

const root = resolve(process.env.AZCLIPS_NEWER_TSP_ROOT ??
  join(dirname(fileURLToPath(import.meta.url)), "..", "dist", "newer-network-probe"));
const resourceType = "Test.Selection/widgets";
const apiVersion = "2026-01-01";
let tools, program, emitter, definitions, fixture, bindingInput;

before(async () => {
  tools = await loadToolchain(root);
  emitter = await buildEmitter(root);
  const fixtures = join(root, "fixtures", "projected-create");
  mkdirSync(fixtures, { recursive: true });
  const entry = join(fixtures, "main.tsp");
  fixture = entry;
  writeFileSync(entry, `
import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-resource-manager";
import "@azure-tools/typespec-client-generator-core";
using TypeSpec.Versioning;
using Azure.ResourceManager;
@armProviderNamespace
@service
@versioned(Versions)
namespace Test.Selection;
enum Versions { earlier: "2025-01-01", current: "2026-01-01" }
model Shared { id?: string; @minLength(2) value?: string; }
model BaseProperties { inherited?: string; }
model WidgetProperties extends BaseProperties {
  kept?: string;
  @minValue(1) @maxValue(100) timeout?: int32;
  left?: Shared;
  right?: Shared;
  @visibility(Lifecycle.Read) serverOnly?: string;
  @removed(Versions.current) obsolete?: string;
}
model Widget is TrackedResource<WidgetProperties> {
  ...ResourceNameParameter<Widget, KeyName = "widgetName", SegmentName = "widgets">;
  @visibility(Lifecycle.Read, Lifecycle.Create, Lifecycle.Update) id?: Azure.Core.armResourceIdentifier;
}
@armResourceOperations
interface Widgets {
  get is ArmResourceRead<Widget>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Widget>;
}
@@Azure.ClientGenerator.Core.Legacy.flattenProperty(Widget.properties);
@@Azure.ClientGenerator.Core.clientName(Shared.value, "renamedValue");
model Other is TrackedResource<WidgetProperties> {
  ...ResourceNameParameter<Other, KeyName = "otherName", SegmentName = "others">;
}
@armResourceOperations
interface Others {
  get is ArmResourceRead<Other>;
}
`);
  program = await tools.compiler.compile(tools.compiler.NodeHost, entry, { noEmit: true });
  requireNoErrors(program);
  definitions = emitter.getProviderDefinitions({ program, options: {}, emitterOutputDir: "" });
  const selected = selectProjectedCreate(program, definitions, { resourceType, apiVersion }, tools);
  const correspondence = [];
  const types = emitter.generateTypes(program, selected.provider, (record) => correspondence.push(record));
  const canonical = await emitCanonical(program, root, apiVersion);
  const commands = await projectCommands(canonical.ir);
  bindingInput = { ir: commands.ir, sources: canonical.sources, selected, types, correspondence,
    program, getSourceConstraints: canonical.getSourceConstraints, bicep: tools.bicep };
});

test("source joins preserve inherited, flattened and reused property paths without using parameter names", () => {
  const bridge = createBindings(bindingInput);
  const fields = bridge.bindings.filter((binding) => binding.role === "field");
  const left = fields.find(({ wirePath }) => wirePath.join(".") === "properties.left.value");
  const right = fields.find(({ wirePath }) => wirePath.join(".") === "properties.right.value");
  assert.ok(left);
  assert.ok(right);
  assert.deepEqual(left.source.property, right.source.property);
  assert.notDeepEqual(left.source.via, right.source.via);
  assert.deepEqual(left.bicepPath, ["properties", "left", "value"]);
  assert.deepEqual(right.bicepPath, ["properties", "right", "value"]);
  assert.equal(left.constraints.minLength, 2);
  assert.ok(fields.some(({ wirePath }) => wirePath.join(".") === "properties.inherited"));
  const timeout = fields.find(({ wirePath }) => wirePath.join(".") === "properties.timeout");
  assert.equal(timeout.type.name, "int32");
  assert.equal(timeout.constraints.minValue, "1");
  assert.equal(timeout.constraints.maxValue, "100");
  assert.ok(!bridge.bindings.some(({ wirePath }) => wirePath?.includes("obsolete")));
  assert.ok(!bridge.bindings.some(({ wirePath }) => wirePath?.includes("serverOnly")));
  assert.ok(bridge.readOnlyFields.some(({ name }) => name === "serverOnly"));
  const nestedId = fields.find(({ wirePath }) => wirePath.join(".") === "properties.left.id");
  assert.ok(nestedId);
  assert.equal(nestedId.role, "field");
  const rootId = bridge.bindings.find(({ wirePath }) => wirePath?.join(".") === "id");
  assert.ok(rootId);
  assert.equal(rootId.role, "computedIdentity");
  assert.ok(rootId.bicepField.flags & tools.bicep.ObjectTypePropertyFlags.ReadOnly);
  assert.ok(left.parameter.includes("RenamedValue"));
  assert.equal(bridge.bindings.filter(({ role }) => role === "scope").length, 2);
  assert.equal(bridge.bindings.filter(({ role }) => role === "resourceName").length, 1);

  const renamed = structuredClone(bindingInput.ir);
  const command = renamed.commands.find(({ id }) => JSON.stringify(id) === JSON.stringify(bridge.command));
  for (const parameter of command.parameters) {
    parameter.name = `Renamed${parameter.name}`;
    parameter.dialect = { cli: { flag: "unrelated-cli-flag", aliases: ["unrelated-cli-alias"] } };
  }
  const changed = createBindings({ ...bindingInput, ir: renamed });
  assert.deepEqual(changed.bindings.map(({ source, bicepPath, role }) => ({ source, bicepPath, role })),
    bridge.bindings.map(({ source, bicepPath, role }) => ({ source, bicepPath, role })));
});

test("missing, ambiguous and broken source paths fail rather than drop inputs", () => {
  const bridge = createBindings(bindingInput);
  const target = bridge.bindings.find(({ wirePath }) => wirePath?.join(".") === "properties.timeout");
  const record = bindingInput.correspondence.find(({ container, name }) =>
    container.index === target.bicepField.container.index && name === target.bicepField.name);
  assert.throws(() => createBindings({ ...bindingInput,
    correspondence: bindingInput.correspondence.filter((candidate) => candidate !== record) }),
  /Missing or ambiguous Bicep field/);
  assert.throws(() => createBindings({ ...bindingInput, correspondence: [...bindingInput.correspondence, record] }),
    /Missing or ambiguous Bicep field/);
  const bad = structuredClone(bindingInput.ir);
  const command = bad.commands.find(({ id }) => JSON.stringify(id) === JSON.stringify(bridge.command));
  command.parameters.find(({ name }) => name === target.parameter).source.via = [];
  assert.throws(() => createBindings({ ...bindingInput, ir: bad }), /Broken canonical containing path/);
  const wrong = structuredClone(bindingInput.ir);
  wrong.operations.find(({ id }) => JSON.stringify(id) === JSON.stringify(bridge.operation)).apiVersion.value = "2025-01-01";
  assert.throws(() => createBindings({ ...bindingInput, ir: wrong }), /API versions differ/);
});

test("pilot field eligibility keeps exact path segments and explicit identity rejection", () => {
  const bridge = classifyPublicIpEligibility({
    bindings: [
      { role: "field", wirePath: ["sku", "name"] },
      { role: "field", wirePath: ["sku.name"] },
      { role: "field", wirePath: ["properties", "natGateway", "id"] },
      { role: "computedIdentity", wirePath: ["id"] },
      { role: "readOnly", wirePath: ["properties", "provisioningState"] },
      { role: "scope", wirePath: [] },
    ],
  });
  assert.deepEqual(bridge.bindings.map(({ eligibility }) => eligibility), [
    "eligible", "excluded", "excluded", "reject", "reject", "structural",
  ]);
});

test("template instances sharing a declaration node retain their distinct source identities", () => {
  const modelNode = {}, propertyNode = {}, stringNode = {}, integerNode = {};
  const make = (argument) => ({ kind: "ModelProperty", node: propertyNode,
    model: { kind: "Model", node: modelNode, templateMapper: { args: [argument] } } });
  const string = { kind: "Scalar", node: stringNode };
  const integer = { kind: "Scalar", node: integerNode };
  assert.ok(sameSource(make(string), make({ kind: "Scalar", node: stringNode })));
  assert.ok(!sameSource(make(string), make(integer)));
});

test("latest-version projection removes obsolete fields and selects only one create resource", () => {
  const rawSources = [];
  emitter.generateTypes(program, definitions[0], (record) => rawSources.push(record));
  assert.ok(rawSources.some(({ name }) => name === "obsolete"));
  const selected = selectProjectedCreate(program, definitions, { resourceType, apiVersion }, tools);
  const selectedSources = [];
  const schema = JSON.parse(tools.bicep.writeTypesJson(
    emitter.generateTypes(program, selected.provider, (record) => selectedSources.push(record))));
  const roots = schema.filter((type) => type.$type === "ResourceType");
  assert.deepEqual(roots.map(({ name }) => name), [`${resourceType}@${apiVersion}`]);
  assert.equal(roots[0].writableScopes, tools.bicep.ScopeType.ResourceGroup);
  assert.ok(selectedSources.some(({ name }) => name === "kept"));
  assert.ok(!selectedSources.some(({ name }) => name === "obsolete"));
  assert.equal(selected.create.httpOperation.parameters.body.type,
    selected.provider.resourcesByType[resourceType.toLowerCase()][0].putModel);
  requireNoErrors(program);
});

test("missing, duplicate and mismatched resource selections fail explicitly", () => {
  const select = (providers, selection) => selectProjectedCreate(program, providers, selection, tools);
  assert.throws(() => select(definitions, { resourceType: "Test.Selection/missing", apiVersion }),
    /upstream resource\/version must be unambiguous/);
  assert.throws(() => select([...definitions, ...definitions], { resourceType, apiVersion }),
    /upstream resource\/version must be unambiguous/);
  assert.throws(() => select(definitions, { resourceType, apiVersion: "2025-01-01" }),
    /upstream resource\/version must be unambiguous/);
  assert.throws(() => select(definitions, { resourceType, apiVersion: "latest" }),
    /explicit concrete API version/);
  assert.throws(() => select(definitions, { resourceType: "Test.Selection/others", apiVersion }),
    /exactly one create-or-update operation/);
});

test("ordinary canonical emission still loads with the newer ARM public API", async () => {
  const canonical = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "packages", "typespec-emitter");
  const stage = join(root, "public-ip-create", "canonical-emitter");
  mkdirSync(stage, { recursive: true });
  cpSync(join(canonical, "dist"), join(stage, "dist"), { recursive: true });
  cpSync(join(canonical, "package.json"), join(stage, "package.json"));
  const written = new Map();
  const result = await tools.compiler.compile({
    ...tools.compiler.NodeHost,
    writeFile: async (path, content) => { written.set(path, content); },
    mkdirp: async (path) => path,
  }, fixture, {
    emit: [tools.compiler.normalizePath(join(stage, "dist", "src", "index.js"))],
    options: {
      "@azure-client-engine/typespec-emitter": { "api-version": apiVersion, format: "json", outputFile: "canonical.json" },
    },
  });
  requireNoErrors(result);
  const content = [...written].find(([path]) => path.endsWith("/canonical.json"))?.[1];
  assert.ok(content, "Canonical emitter produced no IR");
  const ir = JSON.parse(content);
  assert.ok(ir.operations.some((operation) => operation.method === "PUT" && operation.apiVersion.value === apiVersion));
});
