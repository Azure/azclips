import assert from "node:assert/strict";
import { cpSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { before, test } from "node:test";
import { fileURLToPath } from "node:url";
import { buildEmitter, loadToolchain, requireNoErrors, selectProjectedCreate } from "../project-public-ip.mjs";

const root = resolve(process.env.AZCLIPS_NEWER_TSP_ROOT ??
  join(dirname(fileURLToPath(import.meta.url)), "..", "dist", "newer-network-probe"));
const resourceType = "Test.Selection/widgets";
const apiVersion = "2026-01-01";
let tools, program, emitter, definitions, fixture;

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
using TypeSpec.Versioning;
using Azure.ResourceManager;
@armProviderNamespace
@service
@versioned(Versions)
namespace Test.Selection;
enum Versions { earlier: "2025-01-01", current: "2026-01-01" }
model WidgetProperties {
  kept?: string;
  @removed(Versions.current) obsolete?: string;
}
model Widget is TrackedResource<WidgetProperties> {
  ...ResourceNameParameter<Widget, KeyName = "widgetName", SegmentName = "widgets">;
}
@armResourceOperations
interface Widgets {
  get is ArmResourceRead<Widget>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Widget>;
}
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
