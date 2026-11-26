import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { beforeAll, describe, expect, it } from "vitest";
import { compile, createSourceFile, NodeHost, normalizePath, type CompilerHost } from "@typespec/compiler";
import { getArmResource, getArmResourceOperationList } from "@azure-tools/typespec-azure-resource-manager";
import { idEquals, parseIR, type IR, type IRDeploymentResource } from "@azure-client-engine/ir";
import type { EmitterOptions } from "../src/options.js";
import { emitWithSources } from "../src/emitter.js";
import type { CanonicalSource } from "../src/context.js";

const emitter = "@azure-client-engine/typespec-emitter";
const here = dirname(fileURLToPath(import.meta.url));

it("observes canonical source identities without changing ordinary emitted IR", async () => {
  const options = { format: "json" as const, outputFile: "ir.json", "api-version": "2024-01-01" };
  const original = await emit(options, "deployment-legacy.tsp");
  expect(original.errors).toEqual([]);
  const sources: CanonicalSource[] = [];
  const observed = await emitWithSources({
    program: original.program, options, emitterOutputDir: "observational-output",
  }, (source) => sources.push(source));
  expect(observed).toEqual(original.ir);
  const resource = observed!.models.find((model) => model.name === "Widget")!;
  expect(sources.some((record) => record.kind === "model" && idEquals(record.id, resource.id))).toBe(true);
  for (const property of resource.properties) {
    expect(sources.some((record) => record.kind === "property" && record.source.kind === "ModelProperty" &&
      idEquals(record.id, property.id))).toBe(true);
  }
  const create = observed!.operations.find((operation) => operation.method === "PUT")!;
  expect(sources.some((record) => record.kind === "operation" && idEquals(record.id, create.id))).toBe(true);
  expect(sources.some((record) => record.kind === "parameter" &&
    idEquals(record.id, create.pathParameters[0]!.id))).toBe(true);
});

async function emit(options: EmitterOptions, fixture = "deployment-service.tsp", extraProperty?: string, transform?: (text: string) => string) {
  const written = new Map<string, string>();
  const entry = normalizePath(resolve(here, "fixtures", fixture));
  const host: CompilerHost = {
    ...NodeHost,
    readFile: async (path) => {
      const file = await NodeHost.readFile(path);
      if (normalizePath(path) !== entry) return file;
      const text = extraProperty === undefined ? file.text
        : file.text.replace("model WidgetProperties {", `model WidgetProperties {\n  ${extraProperty}`);
      return createSourceFile(transform ? transform(text) : text, file.path);
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
    program,
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

  it("discovers legacy ARM associations without modern registration at the selected version", async () => {
    const older = await emit({ deploymentContract: true, "api-version": "2024-01-01" }, "deployment-legacy.tsp");
    const namespace = older.program.getGlobalNamespaceType().namespaces.get("DeploymentLegacy")!;
    const widget = namespace.models.get("Widget")!;
    const raw = namespace.interfaces.get("Widgets")!.operations.get("createOrUpdate")!;
    expect(getArmResource(older.program, widget)).toBeUndefined();
    expect([...getArmResourceOperationList(older.program, widget)].some((entry) => entry.operation === raw)).toBe(true);
    expect(older.errors).toEqual([]);
    const ir = older.ir!;
    expect(ir.deployment!.resources).toHaveLength(1);
    const resource = ir.deployment!.resources[0]!;
    const operation = ir.operations.find((candidate) => idEquals(candidate.id, resource.operation))!;
    const wire = (id: typeof operation.id) => operation.pathParameters.find((parameter) => idEquals(parameter.id, id))!.wireName;
    expect(resource).toMatchObject({
      resourceType: "DeploymentLegacy/widgets", apiVersion: "2024-01-01", writableScope: "resourceGroup",
    });
    expect(operation.apiVersion.value).toBe(resource.apiVersion);
    expect(resource.name.map((binding) => binding.kind === "parameter" ? wire(binding.parameter) : binding.value)).toEqual(["widgetName"]);
    expect(resource.scopeParameters.map(wire)).toEqual(["subscriptionId", "resourceGroupName"]);
    expect(ir.models.flatMap((model) => model.properties).some((property) => property.wireName === "newerOnly")).toBe(false);
    const display = ir.models.find((model) => model.name === "WidgetProperties")!.properties.find((property) => property.wireName === "displayName")!;
    expect(ir.deployment!.facets.find((facet) => idEquals(facet.target, display.id))).toMatchObject({ minLength: 2 });
    const newer = await emit({ deploymentContract: true, "api-version": "2025-01-01" }, "deployment-legacy.tsp");
    expect(newer.errors).toEqual([]);
    expect(newer.ir!.deployment!.resources[0]!.apiVersion).toBe("2025-01-01");
    expect(newer.ir!.models.flatMap((model) => model.properties).some((property) => property.wireName === "newerOnly")).toBe(true);
    const disabled = await emit({ "api-version": "2024-01-01" }, "deployment-legacy.tsp");
    expect(disabled.errors).toEqual([]);
    const { deployment, ...canonical } = ir;
    expect(disabled.ir).toEqual(canonical);
  }, 60000);

  it("rejects conflicting legacy associations without affecting disabled emission", async () => {
    const transform = (text: string) => text
      .replace("interface Widgets {", `interface Widgets {
    @get @route("/otherWidgets")
    readOther(): OtherWidget;`)
      .replace("@armResourceCreateOrUpdate(Widget)", "@armResourceCreateOrUpdate(OtherWidget)\n  @armResourceCreateOrUpdate(Widget)")
      + "\nmodel OtherWidget extends Widget {}";
    const options = { "api-version": "2024-01-01" };
    const disabled = await emit(options, "deployment-legacy.tsp", undefined, transform);
    expect(disabled.errors).toEqual([]);
    expect(disabled.ir).toBeDefined();
    const enabled = await emit({ ...options, deploymentContract: true }, "deployment-legacy.tsp", undefined, transform);
    expect(enabled.ir).toBeUndefined();
    expect(enabled.errors).toContainEqual(expect.objectContaining({
      code: expect.stringMatching(/\/deployment-contract-invalid$/),
      message: expect.stringContaining("ARM resource association is unresolved or ambiguous"),
    }));
    const selected = await emit({
      ...options, deploymentContract: true, deploymentResources: ["DeploymentLegacy/widgets"],
    }, "deployment-legacy.tsp", undefined, transform);
    expect(selected.ir).toBeUndefined();
    expect(selected.errors).toContainEqual(expect.objectContaining({
      message: expect.stringContaining("ARM resource association is unresolved or ambiguous"),
    }));
  }, 30000);

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

it("selects a complete writable closure without pruning canonical IR or provisioning nested models", async () => {
  const options = { "api-version": "2024-01-01" };
  const full = await emit({ ...options, deploymentContract: true }, "deployment-scoped.tsp");
  expect(full.ir).toBeUndefined();
  expect(full.errors).toContainEqual(expect.objectContaining({ message: expect.stringContaining("scope bindings") }));
  const result = await emit({
    ...options, deploymentContract: true, deploymentResources: ["deploymentscoped/WIDGETS"],
  }, "deployment-scoped.tsp");
  expect(result.errors).toEqual([]);
  const ir = result.ir!;
  expect(ir.deployment!.selection).toEqual(["DeploymentScoped/widgets@2024-01-01"]);
  expect(ir.deployment!.resources.map((resource) => resource.resourceType)).toEqual(["DeploymentScoped/widgets"]);
  const property = (model: string, wire: string) => ir.models.find((value) => value.name === model)!.properties.find((value) => value.wireName === wire)!;
  const facet = (model: string, wire: string, path: string[]) => ir.deployment!.facets.find((value) =>
    idEquals(value.target, property(model, wire).id) && JSON.stringify(value.typePath) === JSON.stringify(path));
  expect(facet("Settings", "values", [])).toMatchObject({ minItems: 1 });
  expect(facet("Settings", "values", ["element"])).toMatchObject({ minLength: 3 });
  expect(facet("Settings", "labels", ["value"])).toMatchObject({ minLength: 3 });
  expect(facet("Settings", "password", [])).toMatchObject({ sensitive: true });
  expect(property("Settings", "next").type.kind).toBe("model");
  expect(property("WidgetProperties", "computed").visibility).toEqual(["read"]);
  expect(facet("WidgetProperties", "computed", [])).toBeUndefined();
  expect(facet("OtherProperties", "value", [])).toBeUndefined();
  const disabled = await emit(options, "deployment-scoped.tsp");
  expect(disabled.errors).toEqual([]);
  const { deployment, ...canonical } = ir;
  expect(disabled.ir).toEqual(canonical);
  expect(ir.operations.some((operation) => operation.pathTemplate.includes("/others/"))).toBe(true);
}, 60000);

it("still rejects unsupported writable facets in the selected graph", async () => {
  const result = await emit({
    deploymentContract: true, "api-version": "2024-01-01", deploymentResources: ["DeploymentScoped/widgets"],
  }, "deployment-scoped.tsp", "@encode(string) extra?: int64;");
  expect(result.ir).toBeUndefined();
  expect(result.errors).toContainEqual(expect.objectContaining({
    code: expect.stringMatching(/\/deployment-contract-invalid$/), message: expect.stringContaining("Explicit wire encoding"),
  }));
}, 30000);

it("follows embedded resource fields without selecting their create operation", async () => {
  const options = { deploymentContract: true, "api-version": "2024-01-01", deploymentResources: ["DeploymentScoped/widgets"] };
  const failed = await emit(options, "deployment-scoped.tsp", "embedded?: OtherWidget;");
  expect(failed.ir).toBeUndefined();
  expect(failed.errors).toContainEqual(expect.objectContaining({ message: expect.stringContaining("Explicit wire encoding") }));
  const result = await emit(options, "deployment-scoped.tsp", "embedded?: OtherWidget;", (text) =>
    text.replace(/@encode\(string\)\s+value\?: int64;/, "@minLength(7) value?: string;"));
  expect(result.errors).toEqual([]);
  expect(result.ir!.deployment!.resources.map((resource) => resource.resourceType)).toEqual(["DeploymentScoped/widgets"]);
  const property = result.ir!.models.find((model) => model.name === "OtherProperties")!.properties.find((value) => value.wireName === "value")!;
  expect(result.ir!.deployment!.facets.find((facet) => idEquals(facet.target, property.id))).toMatchObject({ minLength: 7 });
}, 30000);

it.each([
  { deploymentContract: true, deploymentResources: [] },
  { deploymentContract: true, deploymentResources: ["DeploymentLegacy/missing"] },
  { deploymentContract: true, deploymentResources: ["DeploymentLegacy/widgets", "deploymentlegacy/WIDGETS"] },
  { deploymentResources: ["DeploymentLegacy/widgets"] },
])("rejects invalid deployment selection %j", async (options) => {
  const result = await emit({ ...options, "api-version": "2024-01-01" }, "deployment-legacy.tsp");
  expect(result.ir).toBeUndefined();
  expect(result.errors.length).toBeGreaterThan(0);
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
