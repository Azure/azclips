import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, it } from "vitest";
import { compile, NodeHost, normalizePath, type CompilerHost } from "@typespec/compiler";
import { idEquals, parseIR } from "@azure-client-engine/ir";
import { emitWithSources } from "../src/emitter.js";
import type { CanonicalSource } from "../src/context.js";

const emitter = "@azure-client-engine/typespec-emitter";
const entry = normalizePath(resolve(dirname(fileURLToPath(import.meta.url)), "fixtures", "deployment-legacy.tsp"));
const options = { format: "json" as const, outputFile: "ir.json", "api-version": "2024-01-01" };

async function emit(extra: Record<string, unknown> = {}) {
  const written = new Map<string, string>();
  const host: CompilerHost = {
    ...NodeHost,
    writeFile: async (path, content) => { written.set(normalizePath(path), content); },
    mkdirp: async (path) => path,
  };
  const program = await compile(host, entry, {
    emit: [emitter], options: { [emitter]: { ...options, ...extra } },
  });
  return { program, content: [...written.entries()].find(([path]) => path.endsWith("ir.json"))?.[1] };
}

it("observes canonical source identities and original SDK input types without changing ordinary IR", async () => {
  const original = await emit({ sourceCommit: "b".repeat(40) });
  expect(original.program.diagnostics.filter(({ severity }) => severity === "error")).toEqual([]);
  const ir = parseIR(JSON.parse(original.content!));
  expect(ir).not.toHaveProperty("deployment");
  const sources: CanonicalSource[] = [];
  const observed = await emitWithSources({
    program: original.program, options, emitterOutputDir: "observational-output",
  }, (source) => sources.push(source));
  expect(observed).toEqual(ir);
  const resource = observed!.models.find((model) => model.name === "Widget")!;
  expect(sources.some((record) => record.kind === "model" && idEquals(record.id, resource.id))).toBe(true);
  for (const property of resource.properties) {
    expect(sources.some((record) => record.kind === "property" && record.source.kind === "ModelProperty" &&
      record.sdkType !== undefined && idEquals(record.id, property.id))).toBe(true);
  }
  const create = observed!.operations.find((operation) => operation.method === "PUT")!;
  expect(sources.some((record) => record.kind === "operation" && idEquals(record.id, create.id))).toBe(true);
  expect(sources.some((record) => record.kind === "parameter" &&
    idEquals(record.id, create.pathParameters[0]!.id))).toBe(true);
});

it.each(["deploymentContract", "deploymentResources"])("rejects retired %s configuration explicitly", async (option) => {
  const result = await emit({ [option]: option === "deploymentContract" ? true : ["DeploymentLegacy/widgets"] });
  expect(result.content).toBeUndefined();
  expect(result.program.diagnostics.some(({ severity, message }) => severity === "error" && message.includes(option))).toBe(true);
});

it.each(["latest", "2099-01-01"])("rejects non-concrete or unresolved observer selection %s", async (version) => {
  const original = await emit();
  const observed = await emitWithSources({
    program: original.program, options: { ...options, "api-version": version }, emitterOutputDir: "observational-output",
  }, () => {});
  expect(observed).toBeUndefined();
  expect(original.program.diagnostics.some(({ code, severity }) =>
    severity === "error" && code.endsWith("/source-projection-incomplete"))).toBe(true);
});
