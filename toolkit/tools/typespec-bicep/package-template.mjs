import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { compileBicep, compilerPin, requireCleanCompilation, verifyCompiler } from "./compiler.mjs";
import { generateTemplates } from "./generate-template.mjs";

const directory = dirname(fileURLToPath(import.meta.url));
export const manifestName = "PublicIpBicepCreate.Assets.json";
const sha256 = (value) => createHash("sha256").update(value).digest("hex");
const text = (path) => readFileSync(path, "utf8").replaceAll("\r\n", "\n");

export function validateTemplate(template, bridge, tagsPresent) {
  assert.equal(template.$schema, "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#");
  assert.equal(template.languageVersion, "2.0", "Expected the compiler's symbolic-name template format");
  assert.ok(Object.keys(template).every((key) =>
    ["$schema", "languageVersion", "contentVersion", "metadata", "parameters", "resources"].includes(key)),
  "Unexpected template sections; dependency/extension/output orchestration is outside the pilot");
  assert.ok(template.resources && !Array.isArray(template.resources) && typeof template.resources === "object");
  const resources = Object.values(template.resources);
  assert.equal(resources.length, 1, "Exactly one Public IP must be declared");
  const resource = resources[0];
  assert.equal(`${resource.type}@${resource.apiVersion}`, bridge.resourceType, "Unexpected resource/version");
  const bindings = bridge.bindings.filter((binding) =>
    binding.role === "resourceName" || binding.role === "field" && binding.eligibility === "eligible");
  const optionalRoot = bindings.filter((binding) => binding.bicepPath.length === 1 && binding.normalizedRequired === false);
  assert.equal(optionalRoot.length, 1);
  const selected = bindings.filter((binding) => tagsPresent || binding !== optionalRoot[0]);
  assert.deepEqual(Object.keys(resource).sort(), [...new Set([
    "type", "apiVersion", ...selected.map(({ bicepPath }) => bicepPath[0]),
  ])].sort(), "Unexpected resource fields, dependencies, scope or nested resource declarations");
  assert.deepEqual(Object.keys(template.parameters).sort(), selected.map(({ deploymentParameter }) => deploymentParameter).sort(),
    "Compiled parameter coverage differs from the generated bridge");
  for (const binding of selected) {
    const parameter = template.parameters[binding.deploymentParameter];
    assert.equal(parameter.metadata["__bicep_resource_derived_type!"].source,
      `${bridge.resourceType}#properties/${binding.bicepPath.join("/properties/")}`, "Compiled parameter source differs");
    assert.ok(!Object.hasOwn(parameter, "defaultValue"), "Resource defaults must remain in customization");
    assert.equal(parameter.nullable === true, binding === optionalRoot[0] ? false : binding.normalizedRequired === false);
    if (binding.type.kind === "primitive" && binding.type.name === "int32") {
      assert.equal(parameter.type, "int");
      assert.equal(parameter.minValue, -2147483648);
      assert.equal(parameter.maxValue, 2147483647);
    }
    if (binding.bicepPath.length === 1)
      assert.equal(resource[binding.bicepPath[0]], `[parameters('${binding.deploymentParameter}')]`);
  }
}

export async function buildPackage(fixtures) {
  fixtures = resolve(fixtures);
  const bridgeText = text(join(fixtures, "PublicIpBicepBindings.json"));
  const bridge = JSON.parse(bridgeText);
  const sources = generateTemplates(bridge);
  // Validate all checked-in sources before compiling or publishing either variant.
  for (const [name, source] of Object.entries(sources))
    assert.equal(text(join(fixtures, name)), source, `Stale or modified Bicep source: ${name}; regenerate from the bridge`);
  const compiler = await verifyCompiler();
  const associationPath = resolve(directory, "..", "..", "..", "openspec", "changes", "pilot-public-ip-bicep-create",
    "public-ip-compiler-association.json");
  const associationText = text(associationPath);
  const association = JSON.parse(associationText);
  assert.equal(association.status, "verified-selected-resource-association", "Compiler association is unverified");
  assert.equal(association.selection, bridge.resourceType, "Compiler association selects a different resource");
  assert.deepEqual(association.compiler, compiler, "Compiler association does not match the verified executable");
  assert.deepEqual(association.catalogue, compilerPin.catalogue, "Compiler catalogue pins differ");
  const configSha256 = sha256(readFileSync(join(directory, "compiler-config.json")));
  assert.equal(association.configSha256, configSha256, "Compiler configuration differs from its association evidence");
  const files = {};
  const assets = {};
  for (const [name, source] of Object.entries(sources)) {
    const tagsPresent = name.endsWith(".TagsPresent.bicep");
    const result = await compileBicep(join(fixtures, name));
    const template = requireCleanCompilation(result);
    validateTemplate(template, bridge, tagsPresent);
    const fileName = name.replace(/\.bicep$/, ".json");
    const content = JSON.stringify(template, null, 2) + "\n";
    files[fileName] = content;
    assets[tagsPresent ? "TagsPresent" : "TagsAbsent"] = {
      fileName, sha256: sha256(content), sourceSha256: sha256(source),
      compilation: { exitCode: result.exitCode, diagnostics: result.diagnostics },
    };
  }
  const manifest = {
    schemaVersion: "1.0.0", resourceType: bridge.resourceType,
    bridgeSha256: sha256(bridgeText), source: bridge.provenance,
    compiler, catalogue: compilerPin.catalogue,
    compilerConfigSha256: configSha256, compilerAssociationSha256: sha256(associationText),
    textHashNormalization: "utf8-lf", compilerConfigHashNormalization: "raw-bytes",
    assets,
  };
  files[manifestName] = JSON.stringify(manifest, null, 2) + "\n";
  return { files, manifest };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [fixtures, ...rest] = process.argv.slice(2);
  assert.ok(fixtures && rest.length === 0, "Use package-template.mjs <existing-fixture-directory>");
  const { files, manifest } = await buildPackage(fixtures);
  // Publish the manifest last; partially written assets cannot pass its digest checks.
  for (const [name, content] of Object.entries(files)) writeFileSync(resolve(fixtures, name), content);
  console.log(JSON.stringify(manifest, null, 2));
}
