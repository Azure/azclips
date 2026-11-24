import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { beforeAll, describe, expect, it } from "vitest";
import { compile, createSourceFile, NodeHost, normalizePath, type CompilerHost } from "@typespec/compiler";
import { idEquals, parseIR, type IR, type IRDeploymentResource } from "@azure-client-engine/ir";
import type { EmitterOptions } from "../src/options.js";

const emitter = "@azure-client-engine/typespec-emitter";
const here = dirname(fileURLToPath(import.meta.url));

async function emit(options: EmitterOptions, fixture = "deployment-service.tsp", extraProperty?: string) {
  const written = new Map<string, string>();
  const entry = normalizePath(resolve(here, "fixtures", fixture));
  const host: CompilerHost = {
    ...NodeHost,
    readFile: async (path) => {
      const file = await NodeHost.readFile(path);
      return normalizePath(path) === entry && extraProperty !== undefined
        ? createSourceFile(file.text.replace("model WidgetProperties {", `model WidgetProperties {\n  ${extraProperty}`), file.path)
        : file;
    },
    writeFile: async (path, content) => { written.set(normalizePath(path), content); },
    mkdirp: async (path) => path,
  };
  const program = await compile(host, entry, {
    emit: [emitter],
    options: { [emitter]: { format: "json", outputFile: "ir.json", ...options } },
  });
  const content = [...written.entries()].find(([path]) => path.endsWith("ir.json"))?.[1];
  return {
    ir: content === undefined ? undefined : parseIR(JSON.parse(content)),
    errors: program.diagnostics.filter((diagnostic) => diagnostic.severity === "error")
      .map(({ code, message }) => ({ code, message })),
  };
}

describe("version-aligned deployment contracts", () => {
  let older: IR;
  let newer: IR;
  let disabled: IR;
  beforeAll(async () => {
    const oldResult = await emit({ deploymentContract: true, "api-version": "2024-01-01" });
    const newResult = await emit({ deploymentContract: true, "api-version": "2025-01-01" });
    const offResult = await emit({ "api-version": "2024-01-01" });
    expect(oldResult.errors).toEqual([]);
    expect(newResult.errors).toEqual([]);
    expect(offResult.errors).toEqual([]);
    older = oldResult.ir!;
    newer = newResult.ir!;
    disabled = offResult.ir!;
  }, 60000);

  it("uses the exact selected API version and excludes newer-only properties", () => {
    expect(older.models.flatMap((model) => model.properties).some((property) => property.wireName === "newerOnly")).toBe(false);
    expect(newer.models.flatMap((model) => model.properties).some((property) => property.wireName === "newerOnly")).toBe(true);
    for (const resource of older.deployment!.resources) {
      const operation = older.operations.find((candidate) => idEquals(candidate.id, resource.operation))!;
      expect(resource.apiVersion).toBe("2024-01-01");
      expect(operation.apiVersion.value).toBe(resource.apiVersion);
    }
    expect(newer.deployment!.resources.every((resource) => resource.apiVersion === "2025-01-01")).toBe(true);
  });

  it("keeps default canonical IR identical, with the section absent", () => {
    const { deployment, ...canonical } = older;
    expect(deployment).toBeDefined();
    expect(disabled).not.toHaveProperty("deployment");
    expect(disabled).toEqual(canonical);
  });

  it("binds child names in parent-to-child order and scope inputs to canonical parameters", () => {
    const bindings = (resource: IRDeploymentResource) => {
      const operation = older.operations.find((candidate) => idEquals(candidate.id, resource.operation))!;
      const wire = (id: typeof operation.id) => operation.pathParameters.find((parameter) => idEquals(parameter.id, id))!.wireName;
      return {
        names: resource.name.map((binding) => binding.kind === "parameter" ? wire(binding.parameter) : binding.value),
        scope: resource.scopeParameters.map(wire),
      };
    };
    const child = older.deployment!.resources.find((resource) => resource.resourceType === "DeploymentService/widgets/children")!;
    expect(child.writableScope).toBe("resourceGroup");
    expect(bindings(child)).toEqual({ names: ["widgetName", "childName"], scope: ["subscriptionId", "resourceGroupName"] });
    const account = older.deployment!.resources.find((resource) => resource.resourceType === "DeploymentService/accounts")!;
    expect(account.writableScope).toBe("subscription");
    expect(bindings(account)).toEqual({ names: ["accountName"], scope: ["subscriptionId"] });
  });

  it("retains encoded wire identity and constrained sensitive inputs without a second schema", () => {
    const properties = older.models.find((model) => model.name === "WidgetProperties")!.properties;
    const display = properties.find((property) => property.wireName === "display_name")!;
    const password = properties.find((property) => property.wireName === "password")!;
    expect(older.deployment!.facets.find((facet) => idEquals(facet.target, display.id)))
      .toMatchObject({ typePath: [], minLength: 2, maxLength: 30 });
    expect(older.deployment!.facets.find((facet) => idEquals(facet.target, password.id)))
      .toMatchObject({ typePath: [], minLength: 8, maxLength: 128, sensitive: true });
  });

  it("records deterministic content/tool provenance while leaving commit absent for local sources", async () => {
    const repeat = await emit({ deploymentContract: true, "api-version": "2024-01-01" });
    expect(repeat.errors).toEqual([]);
    expect(repeat.ir!.deployment).toEqual(older.deployment);
    expect(older.deployment!.provenance.sourceHash).toMatch(/^[a-f0-9]{64}$/);
    expect(older.deployment!.provenance).not.toHaveProperty("sourceCommit");
    expect(older.deployment!.provenance.tools.map((tool) => tool.name)).toEqual([
      "@azure-client-engine/typespec-emitter",
      "@azure-tools/typespec-azure-resource-manager",
      "@azure-tools/typespec-client-generator-core",
      "@typespec/compiler",
    ]);
  }, 30000);
});

it.each([undefined, "latest", "all", "2099-01-01"])("rejects non-concrete or unresolved selection %s only when enabled", async (version) => {
  const result = await emit({ deploymentContract: true, ...(version !== undefined ? { "api-version": version } : {}) });
  expect(result.ir).toBeUndefined();
  expect(result.errors.some((diagnostic) => diagnostic.code.endsWith("/deployment-contract-invalid"))).toBe(true);
}, 30000);

it("distinguishes enabled discovery with no ARM resources from a disabled feature", async () => {
  const result = await emit({ deploymentContract: true, "api-version": "2024-01-01" }, "deployment-empty.tsp");
  expect(result.errors).toEqual([]);
  expect(result.ir!.deployment!.resources).toEqual([]);
}, 30000);

it("records the resolved source commit supplied by the acquiring host", async () => {
  const sourceCommit = "b".repeat(40);
  const result = await emit({ deploymentContract: true, "api-version": "2024-01-01", sourceCommit });
  expect(result.errors).toEqual([]);
  expect(result.ir!.deployment!.provenance.sourceCommit).toBe(sourceCommit);
}, 30000);

it.each([
  ["@encode(string) extra?: int64;", "Explicit wire encoding"],
  ["@minLength(2147483648) extra?: string;", "reader range"],
  ["extra?: (string | null)[];", "Nested collection nullability"],
])("rejects lossy input %s only when metadata is enabled", async (property, message) => {
  const options = { "api-version": "2024-01-01" };
  const disabled = await emit(options, "deployment-service.tsp", property);
  expect(disabled.errors).toEqual([]);
  expect(disabled.ir).toBeDefined();
  const enabled = await emit({ ...options, deploymentContract: true }, "deployment-service.tsp", property);
  expect(enabled.ir).toBeUndefined();
  expect(enabled.errors).toContainEqual(expect.objectContaining({
    code: expect.stringMatching(/\/deployment-contract-invalid$/),
    message: expect.stringContaining(message),
  }));
}, 30000);
