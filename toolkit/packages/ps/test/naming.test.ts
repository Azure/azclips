import { describe, expect, it } from "vitest";

import { deconstruct, deduplicateNoun, inferCommandName, pascalCase, removeSequentialDuplicates } from "../src/naming.js";

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

describe("inferCommandName", () => {
  // Every expectation below is a name autorest.powershell demonstrably produced for the real
  // ElasticSan spec, read off the `where:` clauses of that module's directive block.
  it.each([
    ["ElasticSans", "get", "GET", "Get", "ElasticSan", ""],
    ["ElasticSans", "create", "PUT", "New", "ElasticSan", ""],
    ["ElasticSans", "update", "PATCH", "Update", "ElasticSan", ""],
    ["ElasticSans", "delete", "DELETE", "Remove", "ElasticSan", ""],
    ["VolumeSnapshots", "get", "GET", "Get", "VolumeSnapshot", ""],
    ["VolumeGroups", "get", "GET", "Get", "VolumeGroup", ""],
    ["Skus", "list", "GET", "Get", "Sku", ""],
    ["PrivateLinkResources", "listByElasticSan", "GET", "Get", "PrivateLinkResource", "ElasticSan"],
    ["Volumes", "preBackup", "POST", "Backup", "VolumePre", ""],
    ["Volumes", "preRestore", "POST", "Restore", "VolumePre", ""],
    ["ElasticSans", "listByResourceGroup", "GET", "Get", "ElasticSan", "ResourceGroup"],
    ["ElasticSans", "listBySubscription", "GET", "Get", "ElasticSan", "Subscription"],
  ] as const)(
    "%s.%s -> %s / subject %s",
    (group, actionWord, method, verb, subject, variant) => {
      expect(inferCommandName(group, actionWord, method)).toEqual({ verb, subject, variant });
    },
  );

  it("keeps an unrecognized action word in the subject so sibling actions stay distinct", () => {
    const backup = inferCommandName("Volumes", "preBackup", "POST");
    const restore = inferCommandName("Volumes", "preRestore", "POST");
    expect(backup.verb).not.toBe(restore.verb);
  });

  it("singularizes only the last word of a multi-word group", () => {
    expect(inferCommandName("VolumeGroups", "get", "GET").subject).toBe("VolumeGroup");
    expect(pascalCase(deconstruct("VolumeGroups"))).toBe("VolumeGroups");
  });
});
