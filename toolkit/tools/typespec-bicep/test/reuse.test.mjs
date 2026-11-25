import assert from "node:assert/strict";
import { cpSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { before, test } from "node:test";
import { buildRoot, packageRoot, verifySource } from "../prepare.mjs";
import { bicepTypes, compiler, emitter, getProviderDefinitionsChecked } from "../index.mjs";

const fixtures = join(buildRoot, "test-fixtures");
const identitySource = `
import "@typespec/http";
import "@typespec/rest";
import "@azure-tools/typespec-azure-resource-manager";
using TypeSpec.Http;
using TypeSpec.Rest;
using Azure.ResourceManager;
@armProviderNamespace
@service
namespace Test.Binding;
enum Versions { earlier: "2025-01-01", current: "2026-01-01" }
model Reference { id?: string; }
model WidgetProperties { reference?: Reference; }
model Widget is TrackedResource<WidgetProperties> {
  ...ResourceNameParameter<Widget, KeyName = "widgetName", SegmentName = "widgets">;
}
@armResourceOperations
interface Widgets {
  get is ArmResourceRead<Widget>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Widget>;
}
`;

before(() => {
  mkdirSync(fixtures, { recursive: true });
  for (const name of ["basic", "separate-bodies"]) {
    cpSync(join(packageRoot, "test", "integration", "specs", name), join(fixtures, name), { recursive: true });
  }
  writeFileSync(join(fixtures, "identity.tsp"), identitySource);
  writeFileSync(join(fixtures, "no-resources.tsp"), "model Plain { value: string; }");
});

async function context(file) {
  const program = await compiler.compile(compiler.NodeHost, join(fixtures, file), { noEmit: true });
  const errors = program.diagnostics.filter(({ severity }) => severity === "error");
  assert.deepEqual(errors.map(({ code, message }) => ({ code, message })), []);
  assert.deepEqual(program.diagnostics.filter(({ code }) => code === "incompatible-library"), []);
  return { program, emitterOutputDir: join(buildRoot, "unused"), options: {} };
}

function generate(context, version) {
  const definitions = getProviderDefinitionsChecked(context, version);
  assert.equal(definitions.length, 1);
  const sources = [];
  const types = emitter.generateTypes(context.program, definitions[0], (source) => sources.push(source));
  return { definition: definitions[0], types, sources };
}

test("upstream output matches its golden baseline with and without correspondence", async () => {
  const input = await context(join("basic", "main.tsp"));
  const { definition, types, sources } = generate(input, "2021-10-31");
  const json = bicepTypes.writeTypesJson(types);
  assert.equal(json, bicepTypes.writeTypesJson(emitter.generateTypes(input.program, definition)));
  const baseline = readFileSync(join(packageRoot, "test", "integration", "generated", "bicep",
    "basic", "test.rp1", "2021-10-31", "types.json"), "utf8");
  assert.deepEqual(JSON.parse(json), JSON.parse(baseline));
  const password = sources.find(({ name }) => name === "password");
  assert.equal(password.putProperty.kind, "ModelProperty");
  assert.equal(password.property.type, types[password.container.index].properties.password.type);
});

test("reused request properties retain distinct response context and recursive type links", async () => {
  const input = await context(join("separate-bodies", "main.tsp"));
  const { types, sources } = generate(input, "2024-01-01");
  const values = sources.filter(({ name, putProperty }) =>
    name === "value" && putProperty?.model.name === "RequestNested");
  const response = values.find(({ getProperty }) => getProperty?.model.name === "ResponseNested");
  const other = values.find(({ getProperty }) => getProperty?.model.name === "OtherResponseNested");
  assert.ok(response);
  assert.ok(other);
  assert.equal(response.putProperty, other.putProperty);
  assert.notDeepEqual(response.container, other.container);
  const recursive = sources.find(({ name, container }) => name === "next" &&
    container.index === response.container.index);
  assert.deepEqual(recursive.property.type, response.container);
  const discriminator = sources.find(({ role, putProperty, getProperty }) => role === "discriminator" &&
    putProperty?.model.name === "RequestVariant" && getProperty?.model.name === "ResponseVariant");
  assert.ok(discriminator);
  assert.equal(discriminator.name, "kind");
  assert.equal(discriminator.property, types[discriminator.container.index].properties.kind);
});

test("computed root identity remains distinct from ordinary nested reference IDs", async () => {
  const { sources } = generate(await context("identity.tsp"), "2026-01-01");
  const rootId = sources.find(({ name, role }) => name === "id" && role === "computedIdentity");
  const nestedId = sources.find(({ name, role }) => name === "id" && role === "property");
  assert.ok(rootId);
  assert.ok(nestedId);
  assert.notDeepEqual(rootId.container, nestedId.container);
  assert.ok(rootId.property.flags & bicepTypes.ObjectTypePropertyFlags.ReadOnly);
  assert.equal(nestedId.property.flags & bicepTypes.ObjectTypePropertyFlags.ReadOnly, 0);
  assert.equal(nestedId.putProperty.model.name, "Reference");
  assert.ok(sources.some(({ name, role }) => name === "name" && role === "resourceName"));
});

test("an explicit version mismatch fails rather than relabeling upstream output", async () => {
  const input = await context("identity.tsp");
  assert.throws(() => getProviderDefinitionsChecked(input, "2025-01-01"),
    /selected '2026-01-01'.*requested '2025-01-01'/);
  assert.throws(() => getProviderDefinitionsChecked(input, "latest"), /explicit concrete API version/);
});

test("missing ARM discovery is an explicit failure", async () => {
  const input = await context("no-resources.tsp");
  assert.throws(() => getProviderDefinitionsChecked(input, "2026-01-01"), /discovered no ARM resource definitions/);
});

test("unreviewed source additions cannot enter the upstream build", (t) => {
  const file = join(packageRoot, "src", "azclips-integration-unreviewed.ts");
  writeFileSync(file, "export const unexpected = true;\n", { flag: "wx" });
  t.after(() => unlinkSync(file));
  assert.throws(() => verifySource(), /Unexpected untracked upstream source files/);
});
