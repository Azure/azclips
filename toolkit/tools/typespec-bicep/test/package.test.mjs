import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { copyFileSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { buildPackage, manifestName, validateTemplate } from "../package-template.mjs";

const fixtures = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "..", "tests", "Cli.Tests", "Fixtures");
const bridge = JSON.parse(readFileSync(join(fixtures, "PublicIpBicepBindings.json"), "utf8"));

test("the immutable package reproduces verified compiled bytes and binds both variants to the current source and compiler", async () => {
  const { files, manifest } = await buildPackage(fixtures);
  for (const [name, content] of Object.entries(files))
    assert.equal(readFileSync(join(fixtures, name), "utf8"), content, `Regenerate the packaged asset: ${name}`);
  assert.deepEqual(manifest.source, bridge.provenance);
  assert.deepEqual(Object.keys(manifest.assets).sort(), ["TagsAbsent", "TagsPresent"]);
  assert.equal(manifest.compiler.sha256, "acfff0453c9bcf442aa97103acbcd845ef366634e6ea2a2ba5734514409ce9c3");
  assert.ok(Object.values(manifest.assets).every(({ compilation }) => compilation.exitCode === 0 && compilation.diagnostics.length === 0));
});

test("invalid scope, dependencies, extra resources and parameter drift cannot be packaged", () => {
  const valid = JSON.parse(readFileSync(join(fixtures, "PublicIpBicepCreate.TagsPresent.json"), "utf8"));
  for (const [change, message] of [
    [(copy) => { copy.resources.extra = { type: "Microsoft.Resources/resourceGroups", apiVersion: "2025-04-01" }; }, /Exactly one/],
    [(copy) => { copy.resources.publicIp.dependsOn = ["some-resource"]; }, /Unexpected resource fields/],
    [(copy) => { copy.resources.publicIp.scope = "/subscriptions/other"; }, /Unexpected resource fields/],
    [(copy) => { copy.resources.publicIp.type = "Microsoft.Resources/resourceGroups"; }, /Unexpected resource\/version/],
    [(copy) => { copy.resources.publicIp.apiVersion = "2026-05-01"; }, /Unexpected resource\/version/],
    [(copy) => { copy.outputs = {}; }, /Unexpected template sections/],
    [(copy) => { delete copy.parameters.tags; }, /parameter coverage/],
    [(copy) => { copy.parameters.skuTier.defaultValue = "Regional"; }, /defaults/],
    [(copy) => { delete copy.resources.publicIp.tags; }, /Unexpected resource fields/],
  ]) {
    const copy = structuredClone(valid);
    change(copy);
    assert.throws(() => validateTemplate(copy, bridge, true), message);
  }
});

test("a stale second source fails the actual packaging command before replacing any packaged file", () => {
  const scratch = mkdtempSync(join(tmpdir(), "azclips-package-"));
  const inputs = ["PublicIpBicepBindings.json", "PublicIpBicepCreate.TagsAbsent.bicep", "PublicIpBicepCreate.TagsPresent.bicep"];
  const outputs = [manifestName, "PublicIpBicepCreate.TagsAbsent.json", "PublicIpBicepCreate.TagsPresent.json"];
  try {
    for (const name of [...inputs, ...outputs]) copyFileSync(join(fixtures, name), join(scratch, name));
    const before = outputs.map((name) => readFileSync(join(scratch, name)));
    const path = join(scratch, "PublicIpBicepCreate.TagsPresent.bicep");
    writeFileSync(path, readFileSync(path, "utf8") + "\n// stale source\n");
    const result = spawnSync(process.execPath, [resolve(dirname(fileURLToPath(import.meta.url)), "..", "package-template.mjs"), scratch],
      { encoding: "utf8", timeout: 120_000 });
    if (result.error) throw result.error;
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Stale or modified Bicep source/);
    for (const [index, name] of outputs.entries()) assert.deepEqual(readFileSync(join(scratch, name)), before[index]);
  } finally {
    rmSync(scratch, { recursive: true });
  }
});
