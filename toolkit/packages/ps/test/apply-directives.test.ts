import { describe, expect, it } from "vitest";
import { emptyIR, type IROperation } from "@azure-client-engine/ir";

import { applyDirectives } from "../src/apply-directives.js";
import { deriveAttributes } from "../src/derive.js";
import type { PSDirective } from "../src/directives.js";

function makeOperation(overrides: Partial<IROperation> & Pick<IROperation, "id" | "method">): IROperation {
  return {
    name: overrides.id.name,
    source: "<root>",
    pathTemplate: "/subscriptions/{subscriptionId}/volumes/{volumeName}",
    pathParameters: [],
    queryParameters: [],
    headerParameters: [],
    responses: [
      {
        statusCode: { kind: "exact", code: 200 },
        isError: false,
        type: { kind: "model", id: { module: "<root>", namespace: [], name: "Volume" } },
      },
    ],
    apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
    server: { kind: "literal", url: "https://management.azure.com" },
    ...overrides,
  };
}

const pathParam = (operationName: string, name: string) => ({
  id: { module: "<root>", namespace: ["Volumes"], name: `${operationName}#${name}` },
  name,
  wireName: name,
  type: { kind: "primitive" as const, name: "string" as const },
  optional: false,
  nullable: false,
});

/** Two commands over one resource, with a parameter that only the Get set carries. */
function fixture() {
  const get = makeOperation({
    id: { module: "<root>", namespace: ["Volumes"], name: "get" },
    method: "GET",
    pathParameters: [pathParam("get", "groupName")],
  });
  const remove = makeOperation({
    id: { module: "<root>", namespace: ["Volumes"], name: "delete" },
    method: "DELETE",
    responses: [],
  });
  return deriveAttributes({ ...emptyIR(), operations: [get, remove] }, { modulePrefix: "Az" });
}

const apply = (directives: PSDirective[]) => applyDirectives(fixture(), directives);

describe("applyDirectives", () => {
  it("renames a command's verb", () => {
    const { attributes, diagnostics } = apply([
      { where: { verb: "^Remove$", subject: "^Volume$" }, set: { verb: "Test" } },
    ]);

    expect(attributes.commands.map((c) => `${c.verb}-${c.noun}`).sort()).toEqual([
      "Get-AzVolume",
      "Test-AzVolume",
    ]);
    expect(diagnostics).toHaveLength(0);
  });

  it("removes a matched command outright", () => {
    const { attributes } = apply([{ where: { verb: "^Remove$" }, remove: true }]);
    expect(attributes.commands.map((c) => c.verb)).toEqual(["Get"]);
  });

  it("hides a command without dropping it, since a handwritten cmdlet may own the name", () => {
    const { attributes } = apply([{ where: { verb: "^Remove$" }, hide: true }]);
    const removed = attributes.commands.find((c) => c.verb === "Remove");
    expect(removed?.hidden).toBe(true);
  });

  it("renames a parameter only within the commands the selector matches", () => {
    const { attributes } = apply([
      { where: { subject: "^Volume$", "parameter-name": "^GroupName$" }, set: { "parameter-name": "VolumeGroupName" } },
    ]);

    const get = attributes.commands.find((c) => c.verb === "Get")!;
    expect(get.parameters.map((p) => p.name)).toContain("VolumeGroupName");
  });

  it("leaves parameters alone when the command selector does not match", () => {
    const { attributes, diagnostics } = apply([
      { where: { subject: "^Snapshot$", "parameter-name": "^GroupName$" }, set: { "parameter-name": "X" } },
    ]);

    const names = attributes.commands.flatMap((c) => c.parameters.map((p) => p.name));
    expect(names).toContain("GroupName");
    expect(names).not.toContain("X");
    expect(diagnostics.map((d) => d.code)).toEqual(["unmatched-directive"]);
  });

  it("hides a parameter rather than removing it", () => {
    const { attributes } = apply([
      { where: { "parameter-name": "^GroupName$" }, hide: true },
    ]);

    const parameter = attributes.commands
      .flatMap((c) => c.parameters)
      .find((p) => p.name === "GroupName");
    expect(parameter?.hidden).toBe(true);
  });

  it("acts on the containing command when select is command", () => {
    const { attributes } = apply([
      { where: { "parameter-name": "^GroupName$" }, select: "command", remove: true },
    ]);

    // Only the Get command carries that parameter.
    expect(attributes.commands.map((c) => c.verb)).toEqual(["Remove"]);
  });

  it("reports a directive that matches nothing instead of failing quietly", () => {
    const { diagnostics } = apply([{ where: { verb: "^Restart$" }, remove: true }]);
    expect(diagnostics).toEqual([
      expect.objectContaining({ code: "unmatched-directive", directiveIndex: 0 }),
    ]);
  });

  it("reports a recognised setter this stage cannot yet apply", () => {
    const { diagnostics } = apply([
      { where: { verb: "^Get$" }, set: { "clientside-pagination": true } },
    ]);
    expect(diagnostics.map((d) => d.code)).toContain("unsupported-directive");
  });

  it("applies directives in order, so a later one sees the earlier one's result", () => {
    const { attributes } = apply([
      { where: { verb: "^Remove$" }, set: { verb: "Test" } },
      { where: { verb: "^Test$" }, set: { verb: "Stop" } },
    ]);
    expect(attributes.commands.map((c) => c.verb).sort()).toEqual(["Get", "Stop"]);
  });

  it("rewrites the subject and recomposes the noun around the module prefix", () => {
    const { attributes } = apply([
      { where: { verb: "^Get$", subject: "^Volume$" }, set: { subject: "VolumeList" } },
    ]);

    const command = attributes.commands.find((c) => c.verb === "Get")!;
    expect(command.subject).toBe("VolumeList");
    expect(command.noun).toBe("AzVolumeList");
  });

  it("adds a subject prefix and keeps it out of the subject", () => {
    const { attributes } = apply([
      { where: { verb: "^Get$" }, set: { "subject-prefix": "ElasticSan" } },
    ]);

    const command = attributes.commands.find((c) => c.verb === "Get")!;
    expect(command.subjectPrefix).toBe("ElasticSan");
    expect(command.subject).toBe("Volume");
    expect(command.noun).toBe("AzElasticSanVolume");
  });

  it("does not mutate the input attributes", () => {
    const derived = fixture();
    const before = JSON.stringify(derived);
    applyDirectives(derived, [{ where: { verb: "^Get$" }, set: { verb: "Find" } }]);
    expect(JSON.stringify(derived)).toBe(before);
  });
});
