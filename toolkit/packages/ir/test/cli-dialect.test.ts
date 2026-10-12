import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  CliCommandDialectSchema,
  CliParameterDialectSchema,
  safeParseTransformedIR,
  type CliCommandDialect,
  type CliParameterDialect,
  type IRCommand,
  type TransformedIR,
} from "../src/index.js";

// The generator's ElasticSan fixture: real `create-command` output, including the
// ElasticSans List family (BySubscription, ByResourceGroup).
const fixture = fileURLToPath(
  new URL("../../generator/test/Ace.Generator.Tests/TestData/elasticsan.ir.json", import.meta.url),
);

function load(): TransformedIR {
  return JSON.parse(readFileSync(fixture, "utf8")) as TransformedIR;
}

function kebab(name: string): string {
  return name
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1-$2")
    .toLowerCase();
}

/** A minimal CLI transform: the subscription rule, kebab names, internal parameters skipped. */
function expose(
  command: IRCommand,
  dialect: CliCommandDialect,
  overrides: Record<string, CliParameterDialect> = {},
): void {
  command.dialect = { ...command.dialect, cli: dialect };
  command.parameters.forEach((parameter) => {
    if (parameter.access === "internal") return;
    const cli: CliParameterDialect =
      overrides[parameter.name] ??
      (parameter.name === "SubscriptionId"
        ? { name: "subscription-id", parameterSets: [{ mandatory: false }] }
        : { name: kebab(parameter.name) });
    parameter.dialect = { ...parameter.dialect, cli };
  });
}

function find(doc: TransformedIR, name: string): IRCommand {
  const command = doc.commands.find((candidate) => candidate.name === name);
  if (command === undefined) throw new Error(`No command ${name}`);
  return command;
}

const list = { name: "elastic-san list" } satisfies CliCommandDialect;
const rg = { name: "resource-group", aliases: ["g"] } satisfies CliParameterDialect;

function listFamily(
  bySubscription: Partial<CliCommandDialect> = {},
  byResourceGroup: Partial<CliCommandDialect> = {},
  rgOverride: CliParameterDialect = rg,
): TransformedIR {
  const doc = load();
  expose(find(doc, "ListBySubscriptionElasticSans"), { ...list, ...bySubscription });
  expose(find(doc, "ListByResourceGroupElasticSans"), { ...list, ...byResourceGroup }, { ResourceGroupName: rgOverride });
  return doc;
}

function subscriptionOf(command: IRCommand): IRCommand["parameters"][number] {
  return command.parameters.find((parameter) => parameter.name === "SubscriptionId")!;
}

function messages(doc: TransformedIR): string[] {
  const result = safeParseTransformedIR(doc);
  return result.success ? [] : result.error.issues.map((issue) => issue.message);
}

describe("CLI parameter dialect", () => {
  it.each<[string, CliParameterDialect]>([
    ["an exposed parameter", { name: "resource-group", aliases: ["g", "resource-group-name"], group: "Resource Id" }],
    ["a requiredness override in its set", { name: "subscription-id", parameterSets: [{ set: "ByResourceGroup", mandatory: false }] }],
    ["a hidden parameter", { hidden: true }],
    ["a hidden parameter keeping its name for a customization", { hidden: true, name: "edge-zone", parameterSets: [{ mandatory: false }] }],
    ["a closed value set", { name: "sku", validateSet: ["Premium", "PayAsYouGo"] }],
    ["open-enum suggestions", { name: "tier", completer: [{ kind: "string", value: "Hot" }, { kind: "string", value: "Cool" }] }],
    ["preview and deprecation details", {
      name: "old-size",
      preview: { estimatedGaDate: "2027-03-31" },
      deprecated: { message: "Use --size.", replacement: "size", removalDate: "2027-06-30", removalVersion: "3.0.0" },
    }],
    ["a deprecated old spelling, hidden", { name: "target", deprecatedAliases: [{ name: "target-id", hidden: true, removalVersion: "3.0.0" }] }],
    ["a deprecated short option", { name: "location", deprecatedAliases: [{ name: "l", message: "Use --location." }] }],
  ])("accepts %s", (_label, dialect) => {
    expect(CliParameterDialectSchema.safeParse(dialect).success).toBe(true);
  });

  it.each<[string, CliParameterDialect, string]>([
    ["no name", {}, "needs a `name`"],
    ["a dashed name", { name: "--resource-group" }, "lowercase kebab-case"],
    ["an uppercase name", { name: "ResourceGroup" }, "lowercase kebab-case"],
    ["a one-character name", { name: "g" }, "at least two characters"],
    ["a dashed short alias", { name: "name", aliases: ["-n"] }, "must be bare"],
    ["an alias repeating a short option by case", { name: "resource-group", aliases: ["g", "G"] }, "repeats another spelling"],
    ["an alias repeating the name", { name: "resource-group", aliases: ["resource-group"] }, "repeats another spelling"],
    ["a hidden mandatory parameter", { hidden: true, parameterSets: [{ mandatory: true }] }, "cannot be mandatory"],
    ["two memberships", { name: "size", parameterSets: [{ set: "A", mandatory: true }, { set: "B", mandatory: false }] }, "at most 1"],
    ["a repeated value, by case", { name: "sku", validateSet: ["Premium", "premium"] }, "declared more than once"],
    ["both a ValidateSet and a completer", { name: "sku", validateSet: ["Premium"], completer: [{ kind: "string", value: "Premium" }] }, "not both"],
    ["a malformed preview date", { name: "size", preview: { estimatedGaDate: "next year" } }, "Invalid date"],
    ["a malformed removal date", { name: "size", deprecated: { removalDate: "soon" } }, "Invalid date"],
    ["a group with the Arguments suffix", { name: "sku", group: "Sku Arguments" }, "stored bare"],
    ["a bare Arguments group", { name: "sku", group: "arguments" }, "stored bare"],
    ["the reserved Global group", { name: "sku", group: "Global" }, "reserved"],
    ["a group with surrounding spaces", { name: "sku", group: " Sku" }, "leading or trailing"],
    ["deprecated aliases on a hidden parameter", { hidden: true, deprecatedAliases: [{ name: "old" }] }, "cannot have them"],
    ["a deprecated alias repeating an alias", { name: "target", aliases: ["t"], deprecatedAliases: [{ name: "T" }] }, "repeats another spelling"],
    ["a dashed deprecated alias", { name: "target", deprecatedAliases: [{ name: "--target-id" }] }, "must be bare"],
    ["a replacement on a deprecated alias", { name: "target", deprecatedAliases: [{ name: "target-id", replacement: "x" } as never] }, "Unrecognized key"],
    ["a PowerShell-only membership field", { name: "size", parameterSets: [{ mandatory: true, position: 0 } as never] }, "Unrecognized key"],
  ])("rejects %s", (_label, dialect, message) => {
    const result = CliParameterDialectSchema.safeParse(dialect);
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.message).join("\n")).toContain(message);
  });
});

describe("CLI command dialect", () => {
  it("accepts name, aliases, structured examples, preview, deprecated and api", () => {
    const dialect: CliCommandDialect = {
      name: "group create",
      aliases: ["group new"],
      examples: [{ title: "Create a group", command: "az group create -n rg -l westus | jq .id" }],
      preview: { message: "Preview while the API is in preview.", estimatedGaDate: "2027-03-31" },
      deprecated: { replacement: "group new", removalVersion: "3.0.0" },
      api: [{ method: "PUT", path: "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}", apiVersion: "2024-03-01" }],
    };
    expect(CliCommandDialectSchema.safeParse(dialect).success).toBe(true);
  });

  it.each<[string, CliCommandDialect, string]>([
    ["an uppercase token", { name: "Group create" }, "lowercase kebab-case"],
    ["a name with the executable's dashes", { name: "group --create" }, "lowercase kebab-case"],
    ["a doubled space", { name: "group  create" }, "single spaces"],
    ["an alias repeating the name", { name: "group create", aliases: ["group create"] }, "repeats the name"],
    ["a title containing a pipe", { name: "group list", examples: [{ title: "List | filter", command: "az group list" }] }, "cannot contain `|`"],
    ["an unknown HTTP method", { name: "group list", api: [{ method: "FETCH" as never, path: "/x", apiVersion: "1" }] }, "Invalid enum value"],
  ])("rejects %s", (_label, dialect, message) => {
    const result = CliCommandDialectSchema.safeParse(dialect);
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.message).join("\n")).toContain(message);
  });
});

describe("CLI families across commands", () => {
  it("leaves a document with no CLI dialects valid", () => {
    expect(messages(load())).toEqual([]);
  });

  it("folds the ElasticSan list variants into one command that needs no default set", () => {
    // BySubscription accepts {subscription-id}; ByResourceGroup accepts
    // {subscription-id, resource-group} and requires resource-group. Option A selects
    // BySubscription with no arguments and ByResourceGroup with -g.
    expect(messages(listFamily())).toEqual([]);
  });

  it("allows offering only some variants", () => {
    const doc = load();
    expose(find(doc, "ListByResourceGroupElasticSans"), list, { ResourceGroupName: rg });
    expect(messages(doc)).toEqual([]);
  });

  it("requires every non-internal parameter of an offered command to be accounted for", () => {
    const doc = listFamily();
    const command = find(doc, "ListByResourceGroupElasticSans");
    delete command.parameters.find((parameter) => parameter.name === "ResourceGroupName")!.dialect;
    expect(messages(doc).join("\n")).toContain("has no CLI dialect");
  });

  it("rejects presenting an internal parameter", () => {
    const doc = listFamily();
    const accept = find(doc, "ListBySubscriptionElasticSans").parameters.find((parameter) => parameter.name === "Accept")!;
    accept.dialect = { cli: { name: "accept" } };
    expect(messages(doc).join("\n")).toContain("presents internal parameter");
  });

  it("requires same-path siblings to agree on command-level fields", () => {
    expect(messages(listFamily({ description: "List Elastic SANs." })).join("\n")).toContain("disagrees");
  });

  it("rejects one spelling meaning different arguments in different sets", () => {
    const doc = listFamily();
    find(doc, "ListBySubscriptionElasticSans").parameters[0]!.dialect = {
      cli: { name: "subscription-id", aliases: ["g"], parameterSets: [{ mandatory: false }] },
    };
    expect(messages(doc).join("\n")).toMatch(/spelling 'g' means '--(subscription-id|resource-group)' in one set and '--(subscription-id|resource-group)' in another/);
  });

  it("counts deprecated spellings when checking one spelling per argument", () => {
    const doc = listFamily();
    subscriptionOf(find(doc, "ListByResourceGroupElasticSans")).dialect = {
      cli: { name: "subscription-id", deprecatedAliases: [{ name: "resource-group" }], parameterSets: [{ mandatory: false }] },
    };
    expect(messages(doc).join("\n")).toContain("CLI spelling 'resource-group' is used by both");
  });

  it("requires deprecated spellings to agree across sets", () => {
    const doc = listFamily();
    subscriptionOf(find(doc, "ListBySubscriptionElasticSans")).dialect = {
      cli: { name: "subscription-id", deprecatedAliases: [{ name: "subscription", hidden: true }], parameterSets: [{ mandatory: false }] },
    };
    expect(messages(doc).join("\n")).toContain("'--subscription-id' is presented differently");
  });

  it("rejects one flag with different shared types in different sets", () => {
    const doc = listFamily();
    find(doc, "ListBySubscriptionElasticSans").parameters[0]!.type = { kind: "primitive", name: "int32" } as never;
    expect(messages(doc).join("\n")).toContain("different shared types");
  });

  it("reports an ambiguous family that has no default", () => {
    // With -g optional, every ByResourceGroup invocation also fits BySubscription's
    // arguments, and BySubscription can never win.
    const optionalRg = { ...rg, parameterSets: [{ mandatory: false }] };
    const issues = messages(listFamily({}, {}, optionalRg)).join("\n");
    expect(issues).toContain("no default parameter set resolves it");
    expect(issues).toContain("can never be selected");
  });

  it("resolves that ambiguity with the right default, and only the right one", () => {
    const optionalRg = { ...rg, parameterSets: [{ mandatory: false }] };
    const bySubscription = { defaultParameterSet: "BySubscription" };
    expect(messages(listFamily(bySubscription, bySubscription, optionalRg))).toEqual([]);

    const byResourceGroup = { defaultParameterSet: "ByResourceGroup" };
    expect(messages(listFamily(byResourceGroup, byResourceGroup, optionalRg)).join("\n")).toContain(
      "set 'BySubscription' can never be selected",
    );
  });

  it("rejects a default that names no variant, or a default on a single command", () => {
    const unknown = { defaultParameterSet: "ByTenant" };
    expect(messages(listFamily(unknown, unknown)).join("\n")).toContain("is not one of its variants");

    const doc = load();
    expose(find(doc, "ListByResourceGroupElasticSans"), { ...list, defaultParameterSet: "ByResourceGroup" }, { ResourceGroupName: rg });
    expect(messages(doc).join("\n")).toContain("one implicit set");
  });

  it("rejects a path claimed by commands from different actions", () => {
    const doc = listFamily();
    const other = doc.commands.find((command) => command.id.action !== "List")!;
    expose(other, list);
    expect(messages(doc).join("\n")).toContain("Only same-action siblings may share a name");
  });

  it("keeps a hidden command out of the public surface but still accounts for its parameters", () => {
    // Like the generated `vm create` processor behind a customization: same name, not public.
    const doc = listFamily();
    const other = doc.commands.find((command) => command.id.action !== "List")!;
    expose(other, { ...list, hidden: true });
    expect(messages(doc)).toEqual([]);

    delete other.parameters.find((parameter) => parameter.access !== "internal")!.dialect;
    expect(messages(doc).join("\n")).toContain("has no CLI dialect");
  });

  it("leaves a hidden parameter out of selection", () => {
    // Hiding -g makes ByResourceGroup's arguments identical to BySubscription's.
    const hiddenRg = { hidden: true, parameterSets: [{ mandatory: false }] } satisfies CliParameterDialect;
    expect(messages(listFamily({}, {}, hiddenRg)).join("\n")).toContain("can never be selected");
  });

  it("keeps every membership in the command's own set", () => {
    const doc = listFamily();
    const rgParameter = find(doc, "ListByResourceGroupElasticSans").parameters.find((parameter) => parameter.name === "ResourceGroupName")!;
    rgParameter.dialect = { cli: { ...rg, parameterSets: [{ set: "ByResourceGroup", mandatory: true }] } };
    expect(messages(doc)).toEqual([]);

    rgParameter.dialect = { cli: { ...rg, parameterSets: [{ set: "BySubscription", mandatory: true }] } };
    expect(messages(doc).join("\n")).toContain("the command's set is its variant 'ByResourceGroup'");
  });

  it("checks ValidateSet against the shared enum", () => {
    // PublicNetworkAccess is an open enum {Enabled, Disabled}; narrowing it is allowed.
    const doc = load();
    const create = find(doc, "CreateElasticSans");
    expose(create, { name: "elastic-san create" }, {
      PublicNetworkAccess: { name: "public-network-access", validateSet: ["enabled"] },
    });
    expect(messages(doc)).toEqual([]);

    create.parameters.find((parameter) => parameter.name === "PublicNetworkAccess")!.dialect = {
      cli: { name: "public-network-access", validateSet: ["Enabled", "Blocked"] },
    };
    expect(messages(doc).join("\n")).toContain("ValidateSet value 'Blocked' is not a member");
  });
});
