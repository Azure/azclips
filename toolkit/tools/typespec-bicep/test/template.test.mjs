import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { compileBicep, requireCleanCompilation } from "../compiler.mjs";
import { generateTemplates } from "../generate-template.mjs";

const fixtures = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "..", "tests", "Cli.Tests", "Fixtures");
const bridge = JSON.parse(readFileSync(join(fixtures, "PublicIpBicepBindings.json"), "utf8"));
const selection = "Microsoft.Network/publicIPAddresses@2026-03-01";
const sku = "[shallowMerge(createArray(createObject('name', parameters('skuName')), if(not(equals(parameters('skuTier'), null())), createObject('tier', parameters('skuTier')), createObject())))]";

test("both generated variants compile to exact single-resource assignments with real tags omission and no resource defaults", async () => {
  const templates = generateTemplates(bridge);
  for (const [name, source] of Object.entries(templates)) {
    assert.equal(readFileSync(join(fixtures, name), "utf8").replaceAll("\r\n", "\n"), source, "Regenerate the checked-in template");
    const template = requireCleanCompilation(await compileBicep(join(fixtures, name)));
    const tags = name.includes("TagsPresent");
    assert.equal(template.$schema, "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#");
    assert.deepEqual(Object.values(template.resources), [{
      type: "Microsoft.Network/publicIPAddresses", apiVersion: "2026-03-01",
      name: "[parameters('publicIpAddressName')]", location: "[parameters('location')]",
      ...(tags ? { tags: "[parameters('tags')]" } : {}),
      properties: {
        publicIPAllocationMethod: "[parameters('publicIPAllocationMethod')]",
        publicIPAddressVersion: "[parameters('publicIPAddressVersion')]",
        idleTimeoutInMinutes: "[parameters('idleTimeoutInMinutes')]",
      },
      sku,
    }]);
    assert.deepEqual(Object.keys(template.parameters).sort(), [
      "publicIpAddressName", "location", "publicIPAllocationMethod", "publicIPAddressVersion",
      "idleTimeoutInMinutes", "skuName", "skuTier", ...(tags ? ["tags"] : []),
    ].sort());
    assert.deepEqual(Object.keys(template.parameters).filter((name) => template.parameters[name].nullable), ["skuTier"]);
    for (const parameter of Object.values(template.parameters)) assert.ok(!Object.hasOwn(parameter, "defaultValue"));
    assert.equal(template.parameters.idleTimeoutInMinutes.type, "int");
    assert.equal(template.parameters.idleTimeoutInMinutes.minValue, -2147483648);
    assert.equal(template.parameters.idleTimeoutInMinutes.maxValue, 2147483647);
    assert.equal(template.parameters.skuTier.metadata["__bicep_resource_derived_type!"].source,
      `${selection}#properties/sku/properties/tier`);
    assert.equal(template.parameters.publicIPAllocationMethod.metadata["__bicep_resource_derived_type!"].source,
      `${selection}#properties/properties/properties/publicIPAllocationMethod`);
  }
});

test("template assignments consume generated identifiers rather than a second command-name mapping", async () => {
  const changed = structuredClone(bridge);
  changed.bindings.find(({ deploymentParameter }) => deploymentParameter === "skuName").deploymentParameter = "renamedSku";
  changed.bindings.find(({ deploymentParameter }) => deploymentParameter === "tags").deploymentParameter = "renamedTags";
  const scratch = mkdtempSync(join(tmpdir(), "azclips-template-"));
  try {
    for (const [name, source] of Object.entries(generateTemplates(changed))) {
      const path = join(scratch, name);
      writeFileSync(path, source);
      const template = requireCleanCompilation(await compileBicep(path));
      assert.equal(template.resources.publicIp.sku, sku.replaceAll("skuName", "renamedSku"));
      assert.ok(!Object.hasOwn(template.parameters, "skuName"));
      if (name.includes("TagsPresent")) {
        assert.equal(template.resources.publicIp.tags, "[parameters('renamedTags')]");
        assert.ok(!Object.hasOwn(template.parameters, "tags"));
      }
    }
  } finally {
    rmSync(scratch, { recursive: true });
  }
});

test("ambiguous paths, missing normalization policy and nullable or read-only assignments fail generation", () => {
  for (const [change, message] of [
    [(_, field) => { field.nullable = true; }, /Nullable source/],
    [(_, field) => { delete field.normalizedRequired; }, /Normalized input requiredness/],
    [(_, field) => { field.bicepField.flags |= 2; }, /Read-only fields/],
    [(_, field) => { field.constraints.minValue = "1"; }, /Additional source constraints/],
    [(copy, field) => { copy.bindings.push(structuredClone(field)); }, /Duplicate.*parameter/],
    [(copy, field) => {
      const other = copy.bindings.find(({ deploymentParameter }) => deploymentParameter === "skuTier");
      other.bicepPath = [...field.bicepPath];
      other.bicepField.name = field.bicepField.name;
    }, /Duplicate or overlapping.*path/],
    [(_, field) => { field.eligibility = "unreviewed"; }, /Unaccounted binding/],
  ]) {
    const copy = structuredClone(bridge);
    const field = copy.bindings.find(({ deploymentParameter }) => deploymentParameter === "skuName");
    change(copy, field);
    assert.throws(() => generateTemplates(copy), message);
  }
});

test("actual declaration and nested conditional assignments retain compiler schema validation", async () => {
  const source = generateTemplates(bridge)["PublicIpBicepCreate.TagsPresent.bicep"];
  const scratch = mkdtempSync(join(tmpdir(), "azclips-template-negative-"));
  try {
    for (const [name, original, replacement] of [
      ["timeout", "idleTimeoutInMinutes: idleTimeoutInMinutes", "idleTimeoutInMinutes: 'not-an-integer'"],
      ["tier", "tier: skuTier", "tier: 123"],
    ]) {
      assert.ok(source.includes(original));
      const path = join(scratch, `${name}.bicep`);
      writeFileSync(path, source.replace(original, replacement));
      const result = await compileBicep(path);
      assert.ok(result.diagnostics.some(({ code }) => code === "BCP036"), `${name} did not exercise resource schema checking`);
      assert.throws(() => requireCleanCompilation(result));
    }
  } finally {
    rmSync(scratch, { recursive: true });
  }
});
