import { describe, expect, it } from "vitest";

import { deconstruct, deduplicateNoun, inferCommandNames, pascalCase, removeSequentialDuplicates } from "../../src/ps/naming.js";

/** Most action words yield exactly one name; assert that and unwrap it. */
function inferOne(group: string, actionWord: string) {
  const names = inferCommandNames(group, actionWord);
  expect(names).toHaveLength(1);
  return names[0]!;
}

describe("deconstruct", () => {
  it("splits camelCase, digits, and acronym boundaries into lower-cased words", () => {
    expect(deconstruct("listByResourceGroup")).toEqual(["list", "by", "resource", "group"]);
    expect(deconstruct("PreBackup")).toEqual(["pre", "backup"]);
    expect(deconstruct("VolumeSnapshots")).toEqual(["volume", "snapshots"]);
    expect(deconstruct("ElasticSans")).toEqual(["elastic", "sans"]);
  });
});

describe("removeSequentialDuplicates", () => {
  it("drops adjacent repeats and adjacent repeated pairs", () => {
    expect(removeSequentialDuplicates(["a", "a", "b"])).toEqual(["a", "b"]);
    expect(removeSequentialDuplicates(["a", "b", "a", "b", "c"])).toEqual(["a", "b", "c"]);
  });
});

describe("deduplicateNoun", () => {
  // Ported verbatim from autorest.powershell's own test/noun-cleaning.ts expectations.
  it.each([
    ["AppConfiguration", "ConfigurationStore", "App", "ConfigurationStore"],
    ["AppConfiguration", "ConfigurationStoreKey", "App", "ConfigurationStoreKey"],
    ["ContainerService", "ContainerService", "", "ContainerService"],
    ["ContainerService", "ContainerServiceSomething", "", "ContainerServiceSomething"],
    ["WebFoo", "FooBarBlah", "Web", "FooBarBlah"],
    ["Hi", "HelloHiHolaHallo", "Hi", "HelloHiHolaHallo"],
  ])("splits %s + %s into %s + %s", (prefix, subject, expectedPrefix, expectedSubject) => {
    expect(deduplicateNoun(prefix, subject)).toEqual({
      subjectPrefix: expectedPrefix,
      subject: expectedSubject,
    });
  });
});

describe("inferCommandNames", () => {
  // Every expectation below is a name autorest.powershell demonstrably produced for the real
  // ElasticSan spec, read off the `where:` clauses of that module's directive block.
  it.each([
    ["ElasticSans", "get", "Get", "ElasticSan", ""],
    ["ElasticSans", "create", "New", "ElasticSan", ""],
    ["ElasticSans", "update", "Update", "ElasticSan", ""],
    ["ElasticSans", "delete", "Remove", "ElasticSan", ""],
    ["VolumeSnapshots", "get", "Get", "VolumeSnapshot", ""],
    ["VolumeGroups", "get", "Get", "VolumeGroup", ""],
    ["Skus", "list", "Get", "Sku", ""],
    ["PrivateLinkResources", "listByElasticSan", "Get", "PrivateLinkResource", "ElasticSan"],
    ["Volumes", "preBackup", "Backup", "VolumePre", ""],
    ["Volumes", "preRestore", "Restore", "VolumePre", ""],
    ["ElasticSans", "listByResourceGroup", "Get", "ElasticSan", "ResourceGroup"],
    ["ElasticSans", "listBySubscription", "Get", "ElasticSan", "Subscription"],
    ["ElasticSan", "restoreVolume", "Restore", "ElasticSanVolume", ""],
  ] as const)("%s.%s -> %s / subject %s", (group, actionWord, verb, subject, variant) => {
    expect(inferOne(group, actionWord)).toMatchObject({ verb, subject, variant });
  });

  it("reports the word the verb resolved from, since `list` and `get` share a verb but not an action", () => {
    expect(inferOne("ElasticSans", "listBySubscription").action).toBe("list");
    expect(inferOne("ElasticSans", "get").action).toBe("get");
  });

  it("fans an X-or-Y action word out into one command per branch", () => {
    expect(inferCommandNames("ResourceGroups", "createOrUpdate")).toMatchObject([
      { verb: "New", subject: "ResourceGroup", variant: "" },
      { verb: "Update", subject: "ResourceGroup", variant: "" },
    ]);
  });

  it("falls back to Invoke, never to the HTTP method, and keeps the unmatched word", () => {
    // `credentials` is in the pluralizer's uninflective list, so the group never collapses onto
    // the action's own `credential` and both survive the sequential-duplicate pass.
    expect(inferOne("Credentials", "issueCredential")).toMatchObject({
      verb: "Invoke",
      subject: "IssueCredentialsCredential",
      variant: "",
    });
  });

  it("keeps an unrecognized action word in the subject so sibling actions stay distinct", () => {
    expect(inferOne("Volumes", "preBackup").verb).not.toBe(inferOne("Volumes", "preRestore").verb);
  });

  it("singularizes only the last word of a multi-word group", () => {
    expect(inferOne("VolumeGroups", "get").subject).toBe("VolumeGroup");
    expect(pascalCase(deconstruct("VolumeGroups"))).toBe("VolumeGroups");
  });
});
