import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { compileParameters, requireCleanCompilation } from "../compiler.mjs";

const fixtures = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "..", "tests", "Cli.Tests", "Fixtures");
const evidence = JSON.parse(readFileSync(join(fixtures, "PublicIpBicepSnapshots.json"), "utf8"));
for (const [name, recorded] of Object.entries(evidence.cases)) {
  test(`the ${name} snapshot input compiles to the recorded parameters and exact packaged ARM JSON`, async () => {
    const content = readFileSync(join(fixtures, `PublicIpBicepCreate.${recorded.variant}.json`));
    assert.equal(createHash("sha256").update(content).digest("hex"), evidence.assetSha256[recorded.variant]);
    const result = await compileParameters(join(fixtures, `PublicIpBicepSnapshot.${name}.bicepparam`));
    const template = requireCleanCompilation(result);
    assert.deepEqual(template, JSON.parse(content.toString("utf8")), "Snapshot input must not recompile the resource source");
    assert.deepEqual(result.parameters.parameters, recorded.parameters);
  });
}
