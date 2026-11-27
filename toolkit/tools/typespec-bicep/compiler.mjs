import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { createReadStream, existsSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const directory = dirname(fileURLToPath(import.meta.url));
export const compilerPin = JSON.parse(readFileSync(join(directory, "compiler.json"), "utf8"));
assert.equal(compilerPin.repository, "Azure/bicep");
assert.match(compilerPin.revision, /^[a-f0-9]{40}$/);
assert.match(compilerPin.asset.sha256, /^[a-f0-9]{64}$/);
const cache = resolve(directory, "..", "..", "..", "artifacts", "bicep-cli", compilerPin.version, compilerPin.asset.sha256);
export const compilerPath = join(cache, "bicep.exe");
const config = join(directory, "compiler-config.json");
const fixture = join(directory, "test", "compiler-association.bicep");
const selection = "Microsoft.Network/publicIPAddresses@2026-03-01";

async function hashFile(path) {
  const hash = createHash("sha256");
  for await (const chunk of createReadStream(path)) hash.update(chunk);
  return hash.digest("hex");
}

export async function verifyCompiler(executable = compilerPath) {
  assert.ok(process.platform === "win32" && process.arch === "x64", "The pinned compiler asset supports Windows x64 only");
  assert.ok(existsSync(executable), "Pinned Bicep CLI missing; run npm run compiler:setup in this tool package");
  assert.equal(statSync(executable).size, compilerPin.asset.size, "Pinned Bicep CLI size mismatch; refusing to execute");
  const sha256 = await hashFile(executable);
  assert.equal(sha256, compilerPin.asset.sha256, "Pinned Bicep CLI digest mismatch; refusing to execute");
  const version = execFileSync(executable, ["--version"], { encoding: "utf8", timeout: 30_000 }).trim();
  assert.equal(version, `Bicep CLI version ${compilerPin.version} (${compilerPin.revision.slice(0, 10)})`, "Pinned Bicep CLI identity mismatch");
  return { version, sha256, revision: compilerPin.revision, asset: compilerPin.asset.url };
}

async function installCompiler() {
  assert.ok(process.platform === "win32" && process.arch === "x64", "The pinned compiler asset supports Windows x64 only");
  if (!existsSync(compilerPath)) {
    mkdirSync(cache, { recursive: true });
    const partial = join(cache, `bicep-${process.pid}.partial`);
    try {
      execFileSync("gh", ["release", "download", `v${compilerPin.version}`, "--repo", compilerPin.repository,
        "--pattern", compilerPin.asset.name, "--output", partial], { stdio: "inherit", timeout: 600_000 });
      assert.equal(statSync(partial).size, compilerPin.asset.size, "Downloaded CLI size mismatch");
      assert.equal(await hashFile(partial), compilerPin.asset.sha256, "Downloaded CLI digest mismatch");
      renameSync(partial, compilerPath);
    } finally {
      if (existsSync(partial)) rmSync(partial);
    }
  }
  return verifyCompiler();
}

export async function compileBicep(input, executable = compilerPath) {
  await verifyCompiler(executable);
  const result = spawnSync(executable, ["build", resolve(input), "--stdout", "--no-restore",
    "--config", config, "--diagnostics-format", "Sarif"], {
    encoding: "utf8", timeout: 120_000, maxBuffer: 16 * 1024 * 1024,
  });
  if (result.error) throw result.error;
  assert.ok(Number.isInteger(result.status), `Compiler did not exit normally: ${result.signal}`);
  const sarif = JSON.parse(result.stderr);
  assert.equal(sarif.version, "2.1.0", "Compiler diagnostic format is unexpected");
  assert.ok(Array.isArray(sarif.runs) && sarif.runs.length === 1 && Array.isArray(sarif.runs[0].results),
    "Compiler diagnostic results are missing");
  return {
    exitCode: result.status,
    diagnostics: sarif.runs[0].results.map(({ ruleId, level, message }) => ({ code: ruleId, level: level ?? null, message: message.text })),
    ...(result.stdout.trim() ? { template: JSON.parse(result.stdout) } : {}),
  };
}

export function requireCleanCompilation(result) {
  assert.equal(result.exitCode, 0, `Compiler failed: ${JSON.stringify(result.diagnostics)}`);
  assert.deepEqual(result.diagnostics, [], "Compiler warnings/errors block association and asset qualification");
  assert.ok(result.template, "Compiler produced no ARM JSON");
  return result.template;
}

export async function verifyAssociation() {
  const identity = await verifyCompiler();
  const auditPath = resolve(directory, "..", "..", "..", "openspec", "changes", "pilot-public-ip-bicep-create",
    "public-ip-audit-after-retirement-2026-03-01.json");
  const audit = JSON.parse(readFileSync(auditPath, "utf8"));
  assert.equal(audit.catalogue, `${compilerPin.catalogue.package}@${compilerPin.catalogue.version}`);
  assert.equal(audit.catalogueSha256, compilerPin.catalogue.independentAssemblySha256, "Independent audit catalogue identity differs");
  assert.equal(audit.results[0].selection, selection);
  const positive = await compileBicep(fixture);
  const template = requireCleanCompilation(positive);
  const resources = Object.values(template.resources);
  assert.equal(resources.length, 1, "Association probe must declare exactly one selected resource");
  assert.equal(`${resources[0].type}@${resources[0].apiVersion}`, selection, "Compiler selected an unexpected resource/version");
  assert.equal(resources[0].properties.idleTimeoutInMinutes, 4, "Compiler probe body is unexpected");
  for (const [name, type] of Object.entries({ properties: "object", sku: "object", location: "string", name: "string" })) {
    assert.equal(template.parameters[name].type, type);
    assert.equal(template.parameters[name].metadata["__bicep_resource_derived_type!"].source, `${selection}#properties/${name}`);
  }
  mkdirSync(cache, { recursive: true });
  const scratch = mkdtempSync(join(cache, "association-"));
  const source = readFileSync(fixture, "utf8");
  const negatives = [];
  try {
    for (const [name, content, expectedCode] of [
      ["missing-version", source.replaceAll("2026-03-01", "2026-05-01"), "BCP081"],
      ["wrong-integer-type", source.replace("idleTimeoutInMinutes: 4", "idleTimeoutInMinutes: 'not-an-integer'"), "BCP036"],
      ["unknown-property", source.replace("idleTimeoutInMinutes: 4", "azclipsUnknownProperty: 'not-in-schema'"), "BCP037"],
    ]) {
      const input = join(scratch, `${name}.bicep`);
      writeFileSync(input, content);
      const result = await compileBicep(input);
      assert.equal(result.exitCode, 0, `${name} must reproduce warning-only compiler success`);
      assert.deepEqual(result.diagnostics.map(({ code }) => code), [expectedCode], `${name} did not exercise the required schema validation`);
      assert.throws(() => requireCleanCompilation(result), `${name} must fail the qualification gate`);
      negatives.push({ name, exitCode: result.exitCode, diagnostics: result.diagnostics });
    }
  } finally {
    rmSync(scratch, { recursive: true });
  }
  return {
    schemaVersion: "1.0.0", status: "verified-selected-resource-association", selection,
    compiler: identity, catalogue: compilerPin.catalogue,
    independentAuditSha256: await hashFile(auditPath),
    configSha256: await hashFile(config), probeSha256: await hashFile(fixture),
    templateSha256: createHash("sha256").update(JSON.stringify(template)).digest("hex"),
    positive: { exitCode: positive.exitCode, diagnostics: positive.diagnostics, resources: 1 },
    negatives,
    limitations: [
      "Association uses the official release digest/version, its pinned source package versions and selected resource declaration/property-derived type probes.",
      "The catalogue assembly inside the single-file CLI was not extracted or hashed independently.",
      "This is not a command-specific template, API/Bicep schema-compatibility pass, runtime or live deployment qualification.",
    ],
  };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [mode, output, ...rest] = process.argv.slice(2);
  assert.ok(["--install", "--verify"].includes(mode) && rest.length === 0 && (mode !== "--install" || !output),
    "Use --install or --verify [report.json]");
  const result = mode === "--install" ? await installCompiler() : await verifyAssociation();
  if (output) writeFileSync(resolve(output), JSON.stringify(result, null, 2) + "\n");
  console.log(JSON.stringify(result, null, 2));
}
