import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { cpSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { checkout, packageRoot, upstream, verifySource } from "./prepare.mjs";
import { compile as compileCommands } from "@azure-client-engine/compiler";
import { createAuditContract, createBindings, classifyPublicIpEligibility, createBindingFixture } from "./create-bindings.mjs";

const apiVersion = "2026-03-01";
const resourceType = "Microsoft.Network/publicIPAddresses";
const sourceCommit = "965a33b4c632fb1aaf5f2ea02e6d603e1c81baa6";

export async function loadToolchain(root) {
  const expected = {
    "@typespec/compiler": "1.18.0",
    "@typespec/http": "1.18.0",
    "@typespec/openapi": "1.18.0",
    "@typespec/rest": "0.88.0",
    "@azure-tools/typespec-azure-core": "0.74.0",
    "@azure-tools/typespec-azure-resource-manager": "0.74.0",
    "@azure-tools/typespec-client-generator-core": "0.74.0",
    "@typespec/versioning": "0.88.0",
    "@azure/bicep-types": "0.6.129",
  };
  const require = createRequire(join(root, "package.json"));
  for (const [name, version] of Object.entries(expected)) {
    const installed = JSON.parse(readFileSync(join(root, "node_modules", name, "package.json"), "utf8"));
    assert.equal(installed.version, version, `Experimental toolchain mismatch for ${name}`);
  }
  const load = (name) => import(pathToFileURL(require.resolve(name)).href);
  const [compiler, arm, bicep, versioning] = await Promise.all([
    load("@typespec/compiler"),
    load("@azure-tools/typespec-azure-resource-manager"),
    load("@azure/bicep-types"),
    load("@typespec/versioning"),
  ]);
  return { compiler, arm, bicep, versioning, versions: expected };
}

export function selectProjectedCreate(program, definitions, selection, { compiler, arm, versioning }) {
  assert.ok(typeof selection.apiVersion === "string" && selection.apiVersion.trim() === selection.apiVersion &&
    !["", "latest", "all", "default"].includes(selection.apiVersion.toLowerCase()),
  "An explicit concrete API version is required");
  const key = selection.resourceType.toLowerCase();
  const candidates = definitions
    .flatMap((provider) => (provider.resourcesByType[key] ?? []).map((definition) => ({ provider, definition })));
  assert.equal(candidates.length, 1, "Selected upstream resource/version must be unambiguous");
  const { provider, definition } = candidates[0];
  assert.equal(definition.descriptor.apiVersion, provider.apiVersion, "Upstream descriptor version is inconsistent");
  const versions = (versioning.getAllVersions(program, definition.getModel ?? definition.putModel) ?? [])
    .filter((version) => version.value === selection.apiVersion);
  assert.equal(versions.length, 1, "Selected API version must have unambiguous registered source metadata");
  const version = versions[0];

  const resources = (arm.resolveArmResources(program, {
    serviceVersions: [{ namespace: version.namespace, version }],
  }).resources ?? [])
    .filter(({ resourceType }) => [resourceType.provider, ...resourceType.types].join("/").toLowerCase() === key);
  assert.equal(resources.length, 1, "Selected projected resource must be unambiguous");
  const resource = resources[0];
  const creates = resource.operations.lifecycle.createOrUpdate ?? [];
  assert.equal(creates.length, 1, "Selected resource must have exactly one create-or-update operation");
  const create = creates[0];
  assert.equal(create.httpOperation.verb, "put");
  assert.equal(create.httpOperation.path, resource.resourceInstancePath);
  const putModel = create.httpOperation.parameters.body?.type;
  assert.equal(putModel?.kind, "Model", "Selected PUT must have a model request body");
  assert.ok(definition.nameProperty, "Upstream resource-name correspondence is required");
  const names = [...compiler.walkPropertiesInherited(resource.type)].filter((property) =>
    property === definition.nameProperty ||
    (definition.nameProperty.node && property.node === definition.nameProperty.node));
  assert.equal(names.length, 1, "Projected resource-name source must be unambiguous");

  return {
    resource,
    create,
    provider: {
      ...provider,
      apiVersion: version.value,
      resourcesByType: {
        [key]: [{ ...definition, descriptor: { ...definition.descriptor, apiVersion: version.value },
          putModel, getModel: resource.type, nameProperty: names[0] }],
      },
      resourceActions: [],
      providerOperations: [],
    },
  };
}

export async function buildEmitter(root) {
  verifySource();
  const stage = join(root, "public-ip-create", "upstream");
  mkdirSync(stage, { recursive: true });
  for (const name of ["src", "package.json", "tsconfig.json"]) {
    cpSync(join(packageRoot, name), join(stage, name), { recursive: true });
  }
  cpSync(join(checkout, "LICENSE"), join(stage, "LICENSE"));
  const require = createRequire(import.meta.url);
  execFileSync(process.execPath, [require.resolve("typescript/bin/tsc"), "-p", stage], {
    stdio: "inherit",
    timeout: 120_000,
  });
  verifySource();
  return import(pathToFileURL(join(stage, "dist", "src", "index.js")).href);
}

export function requireNoErrors(program) {
  assert.deepEqual(program.diagnostics
    .filter(({ severity, code }) => severity === "error" || code === "incompatible-library")
    .map(({ code, message }) => ({ code, message })), []);
}

export async function emitCanonical(program, root, apiVersion, options = {}) {
  const canonicalRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "packages", "typespec-emitter");
  const stage = join(root, "public-ip-create", "canonical-emitter");
  mkdirSync(stage, { recursive: true });
  cpSync(join(canonicalRoot, "dist"), join(stage, "dist"), { recursive: true });
  cpSync(join(canonicalRoot, "package.json"), join(stage, "package.json"));
  const canonical = await import(pathToFileURL(join(stage, "dist", "src", "index.js")).href);
  const sources = [];
  const ir = await canonical.emitWithSources({
    program,
    options: { outputFile: "canonical.json", ...options, "api-version": apiVersion, format: "json" },
    emitterOutputDir: join(root, "public-ip-create"),
  }, (record) => sources.push(record));
  requireNoErrors(program);
  assert.ok(ir, "Canonical emitter produced no IR");
  return { ir, sources, getSourceConstraints: canonical.getSourceConstraints, getSourceFacets: canonical.getSourceFacets };
}

function sourceProvenance(program, tools) {
  const files = [...program.sourceFiles.values()].map(({ file }) => {
    const path = file.path.replaceAll("\\", "/");
    const dependency = path.lastIndexOf("/node_modules/");
    const identity = dependency >= 0 ? path.slice(dependency + 14) : relative(program.projectRoot, file.path).replaceAll("\\", "/");
    return [identity, createHash("sha256").update(file.text).digest("hex")];
  }).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0);
  const canonical = JSON.parse(readFileSync(resolve(dirname(fileURLToPath(import.meta.url)),
    "..", "..", "packages", "typespec-emitter", "package.json"), "utf8"));
  const emitter = JSON.parse(readFileSync(join(packageRoot, "package.json"), "utf8"));
  return {
    sourceHash: createHash("sha256").update(JSON.stringify(files)).digest("hex"),
    sourceCommit,
    tools: [
      ...Object.entries(tools.versions).map(([name, version]) => ({ name, version })),
      { name: canonical.name, version: canonical.version },
      { name: emitter.name, version: `${emitter.version}+${upstream.revision}` },
    ].sort((a, b) => a.name.localeCompare(b.name)),
  };
}

export async function projectCommands(ir, config = {}) {
  const result = await compileCommands({
    entrypoint: ir.sources[0].entrypoint, apiVersion: ir.sources[0].apiVersion.value,
    config: { ...config, stages: { ...config.stages, csharp: { disabled: true } } },
  }, { emit: { name: "observed-canonical-ir", produce: async () => ({ ir }) } });
  assert.ok(result.success, JSON.stringify(result.diagnostics));
  return result;
}

async function main(root, source, fixtureOutput, mode) {
  assert.ok(root && source, "Usage: node project-public-ip.mjs <isolated-toolchain-root> <network-source-checkout> [output.json] [--audit-ir]");
  assert.ok(mode === undefined || mode === "--audit-ir", "Unsupported probe mode");
  root = resolve(root);
  source = resolve(source);
  const commit = execFileSync("git", ["-C", source, "rev-parse", "HEAD"], { encoding: "utf8" }).trim();
  assert.equal(commit, sourceCommit, "Unexpected experimental Network source revision");
  assert.equal(execFileSync("git", ["-C", source, "status", "--porcelain", "--untracked-files=no"], {
    encoding: "utf8",
  }).trim(), "", "Experimental source checkout must have no tracked modifications");
  const tools = await loadToolchain(root);
  const entrypoint = join(source, "specification", "network", "resource-manager", "Microsoft.Network", "Network", "main.tsp");
  const program = await tools.compiler.compile(tools.compiler.NodeHost, entrypoint, { noEmit: true });
  requireNoErrors(program);
  const emitter = await buildEmitter(root);
  const definitions = emitter.getProviderDefinitions({ program, options: {}, emitterOutputDir: "" });
  const selected = selectProjectedCreate(program, definitions, { resourceType, apiVersion }, tools);
  assert.equal(selected.resource.scope, "ResourceGroup");
  const correspondence = [];
  const types = emitter.generateTypes(program, selected.provider, (record) => correspondence.push(record));
  requireNoErrors(program);
  const schema = tools.bicep.writeTypesJson(types);
  const roots = JSON.parse(schema).filter((type) => type.$type === "ResourceType");
  assert.equal(roots.length, 1);
  assert.equal(roots[0].name, `${resourceType}@${apiVersion}`);
  assert.equal(roots[0].writableScopes, tools.bicep.ScopeType.ResourceGroup);

  const canonical = await emitCanonical(program, root, apiVersion,
    mode === "--audit-ir" ? { outputFile: "audit-canonical.json" } : {});
  if (mode === "--audit-ir") {
    assert.ok(fixtureOutput, "Full audit mode requires an output JSON path");
    const deployment = createAuditContract({
      ...canonical, selected, types, correspondence, program, bicep: tools.bicep,
    }, sourceProvenance(program, tools));
    writeFileSync(resolve(fixtureOutput), JSON.stringify({ ...canonical.ir, deployment }, null, 2));
    console.log(JSON.stringify({ apiVersion, sourceCommit, models: canonical.ir.models.length,
      operations: canonical.ir.operations.length, facets: deployment.facets.length,
      output: resolve(fixtureOutput) }, null, 2));
    return;
  }
  const configRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "packages", "cli");
  const { loadAceConfig } = await import(pathToFileURL(join(configRoot, "dist", "src", "aceconfig.js")).href);
  const config = await loadAceConfig(resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..",
    "src", "Extensions", "Network", "aceconfig.yaml"), (diagnostic) => {
    assert.notEqual(diagnostic.severity, "error", diagnostic.message);
  });
  assert.ok(config, "Normal Network configuration failed to load");
  const commands = await projectCommands(canonical.ir, config.config);
  const bridge = classifyPublicIpEligibility(createBindings({
    ir: commands.ir, sources: canonical.sources, selected, types, correspondence,
    program, getSourceConstraints: canonical.getSourceConstraints, bicep: tools.bicep,
  }));

  const describe = (property) => {
    if (!property) return undefined;
    const location = tools.compiler.getSourceLocation(property);
    return {
      model: tools.compiler.getNamespaceFullName(property.model?.namespace) + "." + property.model?.name,
      name: property.name,
      file: relative(source, location.file.path).replaceAll("\\", "/"),
      pos: location.pos,
      end: location.end,
    };
  };
  const report = {
    status: "create-bindings-generated-runtime-pending",
    resourceType, apiVersion, sourceCommit, upstreamRevision: upstream.revision,
    toolchain: tools.versions,
    toolchainLockSha256: createHash("sha256").update(readFileSync(join(root, "package-lock.json"))).digest("hex"),
    upstreamPeerRangeQualified: false,
    selection: "Public IP create-or-update and its schema dependencies only; no C# generation",
    projection: "Official resolveArmResources(program, { serviceVersions }) with registered version metadata",
    create: { method: selected.create.httpOperation.verb, path: selected.create.httpOperation.path },
    readableScopes: roots[0].readableScopes,
    writableScopes: roots[0].writableScopes,
    typeCount: types.length,
    correspondenceCount: correspondence.length,
    commandName: bridge.commandName,
    bindingCount: bridge.bindings.length,
    roles: Object.fromEntries([...new Set(bridge.bindings.map(({ role }) => role))].map((role) =>
      [role, bridge.bindings.filter((binding) => binding.role === role).length])),
    commandDiagnostics: commands.diagnostics,
    schemaSha256: createHash("sha256").update(schema).digest("hex"),
    bindingSha256: createHash("sha256").update(JSON.stringify(bridge)).digest("hex"),
    diagnostics: program.diagnostics.map(({ severity, code, message }) => ({ severity, code, message })),
  };
  const output = join(root, "public-ip-create");
  if (fixtureOutput) {
    const fixture = createBindingFixture(bridge, {
      sourceCommit, upstreamRevision: upstream.revision,
      toolchainLockSha256: report.toolchainLockSha256,
      schemaSha256: report.schemaSha256, bindingSha256: report.bindingSha256,
    });
    writeFileSync(resolve(fixtureOutput), JSON.stringify(fixture, null, 2) + "\n");
  }
  writeFileSync(join(output, "types.json"), schema);
  writeFileSync(join(output, "bindings.json"), JSON.stringify(bridge, null, 2));
  writeFileSync(join(output, "correspondence.json"), JSON.stringify(correspondence.map((record) => ({
    container: record.container, name: record.name, role: record.role,
    type: record.property.type, flags: record.property.flags,
    putSource: describe(record.putProperty), getSource: describe(record.getProperty),
  })), null, 2));
  writeFileSync(join(output, "report.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ ...report, diagnostics: { count: report.diagnostics.length }, output }, null, 2));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main(...process.argv.slice(2));
}
