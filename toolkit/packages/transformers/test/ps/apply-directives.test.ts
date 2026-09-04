import { describe, expect, it } from "vitest";
import { emptyIR, PSCommandAttributeSchema, type IROperation } from "@azure-client-engine/ir";

import { applyDirectives } from "../../src/ps/apply-directives.js";
import { deriveAttributes } from "../../src/ps/derive.js";
import type { PSDirective } from "../../src/ps/directives.js";

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

/** Two resources whose subjects share a prefix, so anchoring is observable. */
function siblingFixture() {
  const volume = makeOperation({
    id: { module: "<root>", namespace: ["Volumes"], name: "get" },
    method: "GET",
  });
  const group = makeOperation({
    id: { module: "<root>", namespace: ["VolumeGroups"], name: "get" },
    method: "GET",
  });
  return deriveAttributes({ ...emptyIR(), operations: [volume, group] }, { modulePrefix: "Az" });
}

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
      { where: { verb: "^Get$" }, set: { variant: "Renamed" } },
    ]);
    expect(diagnostics.map((d) => d.code)).toContain("unsupported-directive");
  });

  it("opts a command into paging, which derivation never does on its own", () => {
    const derived = fixture();
    expect(derived.commands.every((c) => c.supportsPaging === undefined)).toBe(true);

    const { attributes } = applyDirectives(derived, [
      { where: { verb: "^Get$" }, set: { "clientside-pagination": true } },
    ]);

    expect(attributes.commands.find((c) => c.verb === "Get")?.supportsPaging).toBe(true);
  });

  it("renames a parameter set and every reference to it", () => {
    const { attributes } = apply([
      { where: { verb: "^Get$", variant: "^Get$" }, set: { variant: "Identity" } },
    ]);

    const command = attributes.commands.find((c) => c.verb === "Get")!;
    expect(command.parameterSets.map((s) => s.name)).toContain("Identity");
    expect(command.parameterSets.map((s) => s.name)).not.toContain("Get");
    expect(command.defaultParameterSetName).toBe("Identity");
    // A parameter bound to the old name must follow it, or the schema refinement would reject.
    expect(command.parameters.every((p) => p.parameterSetName !== "Get")).toBe(true);
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

  it("anchors a plain selector, so `Volume` does not also match `VolumeGroup`", () => {
    const { attributes } = applyDirectives(siblingFixture(), [
      { where: { subject: "Volume" }, set: { verb: "Find" } },
    ]);

    const verbByNoun = Object.fromEntries(attributes.commands.map((c) => [c.noun, c.verb]));
    expect(verbByNoun["AzVolume"]).toBe("Find");
    expect(verbByNoun["AzVolumeGroup"]).toBe("Get");
  });

  it("leaves a selector carrying regex syntax unanchored", () => {
    const { attributes } = applyDirectives(siblingFixture(), [
      { where: { subject: "Volume.*" }, set: { verb: "Find" } },
    ]);
    expect(attributes.commands.every((c) => c.verb === "Find")).toBe(true);
  });

  it("matches case-insensitively", () => {
    const { attributes, diagnostics } = apply([{ where: { verb: "^get$" }, set: { verb: "Find" } }]);

    expect(attributes.commands.map((c) => c.verb).sort()).toEqual(["Find", "Remove"]);
    expect(diagnostics).toHaveLength(0);
  });

  it("treats an empty selector as no constraint, the way modules author it", () => {
    const { attributes } = apply([{ where: { verb: "^Get$", subject: "" }, set: { verb: "Find" } }]);
    expect(attributes.commands.map((c) => c.verb).sort()).toEqual(["Find", "Remove"]);
  });

  it("replaces through the selector, so a set value can carry capture groups", () => {
    const { attributes } = applyDirectives(siblingFixture(), [
      { where: { subject: "Volume(.*)" }, set: { subject: "Disk$1" } },
    ]);
    expect(attributes.commands.map((c) => c.noun).sort()).toEqual(["AzDisk", "AzDiskGroup"]);
  });

  it("adds aliases rather than replacing the ones already there", () => {
    const { attributes } = apply([
      { where: { verb: "^Get$" }, set: { alias: "Get-AzVol" } },
      { where: { verb: "^Get$" }, set: { alias: ["Get-AzV"] } },
    ]);

    expect(attributes.commands.find((c) => c.verb === "Get")!.alias).toEqual([
      "Get-AzVol",
      "Get-AzV",
    ]);
  });

  it("reports a command setter on a parameter directive instead of dropping it", () => {
    const { diagnostics } = apply([
      { where: { "parameter-name": "^GroupName$" }, set: { verb: "Find" } },
    ]);

    expect(diagnostics.map((d) => d.message)).toContain(
      "`set.verb` does not apply to a parameter directive.",
    );
  });

  it("reports a parameter setter on a command directive instead of dropping it", () => {
    const { diagnostics } = apply([
      { where: { verb: "^Get$" }, set: { "parameter-description": "Which group." } },
    ]);

    expect(diagnostics.map((d) => d.message)).toContain(
      "`set.parameter-description` does not apply to a command directive.",
    );
  });

  it("does not mutate the input attributes", () => {
    const derived = fixture();
    const before = JSON.stringify(derived);
    applyDirectives(derived, [{ where: { verb: "^Get$" }, set: { verb: "Find" } }]);
    expect(JSON.stringify(derived)).toBe(before);
  });
});

/** One command whose Get set carries a parameter the List set does not. */
function twoSetFixture() {
  const get = makeOperation({
    id: { module: "<root>", namespace: ["Volumes"], name: "get" },
    method: "GET",
    pathTemplate: "/subscriptions/{subscriptionId}/volumes/{volumeName}",
    pathParameters: [pathParam("get", "subscriptionId"), pathParam("get", "volumeName")],
  });
  const list = makeOperation({
    id: { module: "<root>", namespace: ["Volumes"], name: "listByResourceGroup" },
    method: "GET",
    pathTemplate: "/subscriptions/{subscriptionId}/volumes",
    pathParameters: [pathParam("list", "subscriptionId")],
  });
  return deriveAttributes({ ...emptyIR(), operations: [get, list] }, { modulePrefix: "Az" });
}

describe("applyDirectives: variant scoping", () => {
  it("leaves a parameter alone when the variant names sets it does not belong to", () => {
    const attributes = twoSetFixture();
    expect(attributes.commands[0]!.parameterSets.map((s) => s.name)).toEqual([
      "Get",
      "GetViaIdentity",
      "List",
    ]);

    // `Name` exists only in the Get set, so a List-scoped directive must not reach it.
    const result = applyDirectives(attributes, [
      { where: { subject: "Volume", variant: "^List$", "parameter-name": "^Name$" }, hide: true },
    ]);

    expect(result.attributes.commands[0]!.parameters.find((p) => p.name === "Name")?.hidden).toBeUndefined();
    expect(result.diagnostics.map((d) => d.code)).toContain("unmatched-directive");
  });

  it("applies to a parameter that does belong to the named set", () => {
    const result = applyDirectives(twoSetFixture(), [
      { where: { subject: "Volume", variant: "^Get$", "parameter-name": "^Name$" }, hide: true },
    ]);

    expect(result.attributes.commands[0]!.parameters.find((p) => p.name === "Name")?.hidden).toBe(true);
    expect(result.diagnostics).toEqual([]);
  });

  it("reports a rename it cannot scope rather than renaming every set", () => {
    // `SubscriptionId` spans Get and List, so renaming it for List alone would need two attributes.
    const result = applyDirectives(twoSetFixture(), [
      {
        where: { subject: "Volume", variant: "^List$", "parameter-name": "^SubscriptionId$" },
        set: { "parameter-name": "Subscription" },
      },
    ]);

    const names = result.attributes.commands[0]!.parameters.map((p) => p.name);
    expect(names).toContain("SubscriptionId");
    expect(names).not.toContain("Subscription");
    expect(result.diagnostics.map((d) => d.code)).toContain("unsupported-directive");
  });
});

describe("applyDirectives: module exports", () => {
  /** `fixture()` declares no IR source, so it derives no module to export from. */
  function moduleFixture() {
    const get = makeOperation({
      id: { module: "<root>", namespace: ["Volumes"], name: "get" },
      method: "GET",
    });
    const remove = makeOperation({
      id: { module: "<root>", namespace: ["Volumes"], name: "delete" },
      method: "DELETE",
      responses: [],
    });
    return deriveAttributes(
      {
        ...emptyIR(),
        sources: [
          {
            id: "<root>",
            entrypoint: "main.tsp",
            apiVersion: { value: "2025-04-01", location: "query", parameterName: "api-version" },
            server: { kind: "literal" as const, url: "https://management.azure.com" },
            rootNamespace: ["Test"],
          },
        ],
        operations: [get, remove],
      },
      { modulePrefix: "Az" },
    );
  }

  const applyToModule = (directives: PSDirective[]) => applyDirectives(moduleFixture(), directives);

  it("follows a renamed command into cmdletsToExport", () => {
    const { attributes } = applyToModule([{ where: { verb: "^Get$" }, set: { verb: "Find" } }]);

    expect(attributes.modules[0]!.cmdletsToExport).toContain("Find-AzVolume");
    expect(attributes.modules[0]!.cmdletsToExport).not.toContain("Get-AzVolume");
  });

  it("drops a removed command from cmdletsToExport", () => {
    const { attributes } = applyToModule([{ where: { verb: "^Get$" }, remove: true }]);

    expect(attributes.modules[0]!.cmdletsToExport).not.toContain("Get-AzVolume");
    expect(attributes.commands.map((c) => `${c.verb}-${c.noun}`)).not.toContain("Get-AzVolume");
  });

  it("keeps cmdletsToExport agreeing with the commands it ships", () => {
    const { attributes } = applyToModule([
      { where: { verb: "^Get$" }, set: { verb: "Find" } },
      { where: { verb: "^Remove$" }, remove: true },
    ]);

    const actual = attributes.commands.map((c) => `${c.verb}-${c.noun}`).sort();
    expect([...attributes.modules[0]!.cmdletsToExport].sort()).toEqual(actual);
  });
});

/**
 * A resource reachable by name or by piped identity, so every command carries a plain set and a
 * `ViaIdentity` one -- the shape the corpus's `remove` + `variant` directives actually target.
 */
function variantFixture() {
  const withGroup = (name: string, method: IROperation["method"]) =>
    makeOperation({
      id: { module: "<root>", namespace: ["Volumes"], name },
      method,
      pathTemplate: "/subscriptions/{subscriptionId}/groups/{groupName}/volumes/{volumeName}",
      pathParameters: [pathParam(name, "groupName")],
    });

  return deriveAttributes(
    { ...emptyIR(), operations: [withGroup("get", "GET"), withGroup("createOrUpdate", "PUT")] },
    { modulePrefix: "Az" },
  );
}

const applyToVariants = (directives: PSDirective[]) =>
  applyDirectives(variantFixture(), directives);

describe("applyDirectives: remove scoped by variant", () => {
  it("cuts only the named sets out of the command, keeping the rest of the cmdlet", () => {
    const { attributes, diagnostics } = applyToVariants([
      { where: { variant: "^CreateViaIdentity$" }, remove: true },
    ]);

    const create = attributes.commands.find((c) => c.verb === "New")!;
    expect(create.parameterSets.map((s) => s.name)).toEqual(["Create"]);
    expect(create.parameters.map((p) => p.name)).not.toContain("InputObject");
    expect(diagnostics).toHaveLength(0);

    // A command whose sets the selector does not name is untouched.
    const get = attributes.commands.find((c) => c.verb === "Get")!;
    expect(get.parameterSets.map((s) => s.name)).toEqual(["Get", "GetViaIdentity"]);
  });

  it("moves defaultParameterSetName off a set it just removed", () => {
    const { attributes } = applyToVariants([
      { where: { verb: "^Get$", variant: "^Get$" }, remove: true },
    ]);

    const get = attributes.commands.find((c) => c.verb === "Get")!;
    expect(get.parameterSets.map((s) => s.name)).toEqual(["GetViaIdentity"]);
    expect(get.defaultParameterSetName).toBe("GetViaIdentity");
  });

  it("drops the command only when no set survives", () => {
    const { attributes } = applyToVariants([
      { where: { verb: "^Get$", variant: "^Get" }, remove: true },
    ]);

    expect(attributes.commands.map((c) => c.verb)).not.toContain("Get");
  });

  it("leaves a document the schema still accepts", () => {
    const { attributes } = applyToVariants([
      { where: { variant: "ViaIdentity$" }, remove: true },
    ]);

    for (const command of attributes.commands) {
      expect(PSCommandAttributeSchema.safeParse(command).success).toBe(true);
    }
  });
});
