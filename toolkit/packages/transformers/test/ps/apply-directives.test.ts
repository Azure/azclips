import { describe, expect, it } from "vitest";
import { parseIR, parseTransformedIR, type IR, type TransformedIR } from "@azure-client-engine/ir";
import type { TransformContext } from "@azure-client-engine/compiler";

import { createCommandStep } from "../../src/create-command-step.js";
import { applyDirectives } from "../../src/ps/apply-directives.js";
import { deriveDialect } from "../../src/ps/derive.js";
import type { PSDirective } from "../../src/ps/directives.js";

const MODULE = "M";
const SERVER = { module: MODULE, namespace: ["N"], name: "server" };
const API_VERSION = { value: "2026-01-01", location: "query" as const, parameterName: "api-version" };
const context: TransformContext = { reportDiagnostic: () => {}, config: {} };

const id = (name: string, iface = "Volumes") => ({
  module: MODULE,
  namespace: ["N"],
  interface: iface,
  name,
});

const parameter = (operation: string, name: string, optional = false) => ({
  id: id(`${operation}.${name}`),
  name,
  wireName: name,
  type: { kind: "primitive" as const, name: "string" as const },
  optional,
  nullable: false,
});

const operation = (over: Record<string, unknown> = {}) => ({
  id: id("get"),
  name: "get",
  source: MODULE,
  method: "GET",
  pathTemplate: "/volumes",
  pathParameters: [],
  queryParameters: [],
  headerParameters: [],
  responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
  apiVersion: API_VERSION,
  server: SERVER,
  ...over,
});

function document(operations: Record<string, unknown>[]): IR {
  return parseIR({
    schemaVersion: "0.12.0",
    sources: [{ id: MODULE, entrypoint: "main.tsp", apiVersion: API_VERSION, server: SERVER, rootNamespace: ["Contoso"] }],
    servers: [{ id: SERVER, endpoint: { kind: "literal", url: "https://example.com" } }],
    models: [],
    enums: [],
    operations,
  });
}

function transform(operations: Record<string, unknown>[], subjectPrefix?: string): TransformedIR {
  const ir = createCommandStep().run(document(operations), context);
  return deriveDialect(ir, { modulePrefix: "Az", subjectPrefix });
}

function fixture(): TransformedIR {
  return transform([
    operation({ pathTemplate: "/volumes/{volumesName}", pathParameters: [parameter("get", "volumesName")] }),
    operation({ id: id("delete"), name: "delete", method: "DELETE", responses: [{ statusCode: { kind: "exact", code: 204 }, isError: false }] }),
  ]);
}

function siblingFixture(): TransformedIR {
  const top = parameter("list", "top", true);
  return transform([
    operation({ id: id("list"), name: "list", queryParameters: [top] }),
    operation({
      id: id("listByResourceGroup"),
      name: "listByResourceGroup",
      pathTemplate: "/groups/{resourceGroupName}/volumes",
      pathParameters: [parameter("listByResourceGroup", "resourceGroupName")],
      queryParameters: [{ ...top, id: id("listByResourceGroup.top") }],
    }),
  ]);
}

const apply = (directives: PSDirective[]) => applyDirectives(fixture(), directives);

function command(ir: TransformedIR, verb: string) {
  return ir.commands.find((candidate) => candidate.dialect?.ps?.verb === verb)!;
}

describe("applyDirectives: canonical commands", () => {
  it("renames only the PowerShell verb and keeps canonical identity and name unchanged", () => {
    const input = fixture();
    const original = input.commands.find((candidate) => candidate.id.action === "Delete")!;
    const { ir, diagnostics } = applyDirectives(input, [
      { where: { verb: "Remove", subject: "Volumes" }, set: { verb: "Test" } },
    ]);
    const renamed = command(ir, "Test");

    expect(renamed.id).toEqual(original.id);
    expect(renamed.name).toBe(original.name);
    expect(diagnostics).toEqual([]);
  });

  it("removes the matching IRCommand; there is no module cmdletsToExport metadata", () => {
    const { ir } = apply([{ where: { verb: "Remove" }, remove: true }]);
    expect(ir.commands.map((candidate) => candidate.dialect?.ps?.verb)).toEqual(["Get"]);
    expect(ir).not.toHaveProperty("ps");
  });

  it("writes command description, aliases, hidden, and SupportsShouldProcess to dialect.ps", () => {
    const { ir } = apply([
      { where: { verb: "Get" }, set: { "command-description": "Gets one.", alias: ["Read-AzVolume"], "suppress-should-process": false }, hide: true },
    ]);
    expect(command(ir, "Get").dialect?.ps).toEqual(expect.objectContaining({
      description: "Gets one.",
      aliases: ["Read-AzVolume"],
      hidden: true,
      supportsShouldProcess: true,
    }));
  });

  it("rewrites the dialect noun for subject rename without changing command.id.subject", () => {
    const { ir } = apply([{ where: { verb: "Get", subject: "Volumes" }, set: { subject: "Disks" } }]);
    expect(command(ir, "Get").dialect?.ps?.noun).toBe("AzDisks");
    expect(command(ir, "Get").id.subject).toBe("Volumes");
  });

  it("treats the composed noun prefix as subject-prefix because module metadata is absent", () => {
    const input = transform([operation()], "ElasticSan");
    const { ir } = applyDirectives(input, [
      { where: { "subject-prefix": "AzElasticSan" }, set: { "subject-prefix": "AzStorage" } },
    ]);
    expect(ir.commands[0]!.dialect?.ps?.noun).toBe("AzStorageVolumes");
  });

  it("matches parameter display name from dialect.ps.name and keeps the neutral name", () => {
    const { ir } = apply([{ where: { "parameter-name": "Name" }, set: { "parameter-name": "VolumeName" } }]);
    const renamed = command(ir, "Get").parameters[0]!;
    expect(renamed.name).toBe("VolumesName");
    expect(renamed.dialect?.ps?.name).toBe("VolumeName");
  });

  it("matches a neutral parameter name when no PS parameter dialect exists", () => {
    const input = transform([operation({ queryParameters: [parameter("get", "filter", true)] })]);
    const { ir } = applyDirectives(input, [
      { where: { "parameter-name": "Filter" }, set: { "parameter-description": "Filter text." } },
    ]);
    expect(ir.commands[0]!.parameters[0]!.dialect?.ps?.description).toBe("Filter text.");
  });

  it("writes all authored parameter dialect fields", () => {
    const { ir } = apply([{
      where: { "parameter-name": "Name" },
      set: {
        alias: "Id",
        "parameter-description": "Volume identifier.",
        completer: { name: "VolumeCompleter" },
        default: { script: "'default'" },
      },
      hide: true,
    }]);
    expect(command(ir, "Get").parameters[0]!.dialect?.ps).toEqual(expect.objectContaining({
      aliases: expect.arrayContaining(["VolumesName", "Id"]),
      description: "Volume identifier.",
      completer: { name: "VolumeCompleter" },
      defaultValue: { script: "'default'" },
      hidden: true,
    }));
  });

  it("removes a parameter only from the PowerShell surface", () => {
    const { ir, diagnostics } = apply([{ where: { "parameter-name": "Name" }, remove: true }]);
    const parameters = command(ir, "Get").parameters;
    expect(parameters.find((parameter) => parameter.name === "VolumesName")!.dialect?.ps?.removed).toBe(true);
    expect(parameters.find((parameter) => parameter.source.kind === "synthetic")).toBeDefined();
    expect(diagnostics).toEqual([]);
  });

  it("select: command removes the command containing the parameter", () => {
    const { ir } = apply([{ where: { "parameter-name": "Name" }, select: "command", remove: true }]);
    expect(ir.commands.map((candidate) => candidate.dialect?.ps?.verb)).toEqual(["Remove"]);
  });

  it("reports an unmatched directive", () => {
    const { diagnostics } = apply([{ where: { verb: "Restart" }, remove: true }]);
    expect(diagnostics).toEqual([expect.objectContaining({ code: "unmatched-directive", directiveIndex: 0 })]);
  });

  it("stores client-side pagination on the PowerShell command dialect", () => {
    const { ir, diagnostics } = apply([{ where: { verb: "Get" }, set: { "clientside-pagination": true } }]);
    expect(diagnostics).toEqual([]);
    expect(command(ir, "Get").dialect?.ps?.supportsPaging).toBe(true);
    expect(command(ir, "Get").surface.paginated).toBe(false);
  });

  it("applies directives in order against the updated dialect", () => {
    const { ir } = apply([
      { where: { verb: "Remove" }, set: { verb: "Test" } },
      { where: { verb: "Test" }, set: { verb: "Stop" } },
    ]);
    expect(command(ir, "Stop")).toBeDefined();
  });

  it("does not mutate the input and preserves canonical schema for dialect-only changes", () => {
    const input = fixture();
    const before = JSON.stringify(input);
    const { ir } = applyDirectives(input, [{ where: { verb: "Get" }, set: { verb: "Find" } }]);
    expect(JSON.stringify(input)).toBe(before);
    expect(() => parseTransformedIR(ir)).not.toThrow();
  });
});

describe("applyDirectives: sibling command variants", () => {
  it("variant-scoped removal removes the matching sibling IRCommand", () => {
    const { ir } = applyDirectives(siblingFixture(), [
      { where: { verb: "Get", variant: "ByResourceGroup" }, remove: true },
    ]);
    expect(ir.commands.map((candidate) => candidate.id.variant)).toEqual(["List"]);
  });

  it("renames parameter memberships and defaultParameterSet without changing id.variant", () => {
    const input = siblingFixture();
    const { ir } = applyDirectives(input, [
      { where: { verb: "Get", variant: "List" }, set: { variant: "All" } },
    ]);
    const list = ir.commands.find((candidate) => candidate.id.variant === "List")!;
    expect(list.id.variant).toBe("List");
    expect(list.dialect?.ps?.defaultParameterSet).toBe("All");
    expect(ir.commands.every((command) => command.dialect?.ps?.defaultParameterSet === "All")).toBe(true);
    expect(list.parameters[0]!.dialect?.ps?.parameterSets).toEqual([{ set: "All", mandatory: false }]);
    expect(() => parseTransformedIR(ir)).not.toThrow();
  });

  it("uses parameter memberships when a variant scopes a parameter directive", () => {
    const { ir, diagnostics } = applyDirectives(siblingFixture(), [
      { where: { variant: "ByResourceGroup", "parameter-name": "Top" }, hide: true },
    ]);
    const byGroup = ir.commands.find((candidate) => candidate.id.variant === "ByResourceGroup")!;
    const list = ir.commands.find((candidate) => candidate.id.variant === "List")!;
    expect(byGroup.parameters.find((candidate) => candidate.name === "Top")?.dialect?.ps?.hidden).toBe(true);
    expect(list.parameters.find((candidate) => candidate.name === "Top")?.dialect?.ps?.hidden).toBeUndefined();
    expect(diagnostics).toEqual([]);
  });
});
