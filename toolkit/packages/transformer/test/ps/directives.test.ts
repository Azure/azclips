import { describe, expect, it } from "vitest";

import { PSDirectiveSchema } from "../../src/ps/directives.js";

describe("PSDirectiveSchema", () => {
  // Lifted verbatim from Az.ElasticSan's own README, which is the compatibility target.
  it.each([
    [
      "rename a command",
      { where: { verb: "Backup", subject: "VolumePre" }, set: { verb: "Test", subject: "VolumeBackup" } },
    ],
    ["hide a command", { where: { subject: "^PrivateEndpointConnection$|^PrivateLinkResource$" }, hide: true }],
    ["remove by variant", { where: { variant: "^(Create|Update)(?!.*?Expanded)" }, remove: true }],
    [
      "rename a parameter within a command",
      { where: { subject: "Volume", "parameter-name": "GroupName" }, set: { "parameter-name": "VolumeGroupName" } },
    ],
    ["clear an alias", { where: { subject: "Volume", "parameter-name": "GroupName" }, "clear-alias": true }],
    [
      "set an alias",
      { where: { subject: "ElasticSan", "parameter-name": "Name" }, set: { alias: "ElasticSanName" } },
    ],
    [
      "hide a parameter",
      { where: { verb: "^New$|^Update$", subject: "Volume", "parameter-name": "ManagedByResourceId" }, hide: true },
    ],
  ])("accepts a real directive: %s", (_label, directive) => {
    expect(PSDirectiveSchema.safeParse(directive).success).toBe(true);
  });

  it("accepts an empty subject-prefix, which is how modules clear an inherited prefix", () => {
    const result = PSDirectiveSchema.safeParse({
      where: { verb: "^Backup$|^Restore$", subject: "" },
      set: { "subject-prefix": "", subject: "CloudHsm" },
    });
    expect(result.success).toBe(true);
  });

  it("accepts select: command, which acts on the command a matched parameter belongs to", () => {
    const result = PSDirectiveSchema.safeParse({
      where: { "parameter-name": "ConfigStoreCreationParameter" },
      select: "command",
      hide: true,
    });
    expect(result.success).toBe(true);
  });

  // Each of these is a real directive from a shipped module that autorest.powershell silently
  // ignores, so the override its author intended has never taken effect.
  it.each([
    ["subjectPrefix instead of subject-prefix (Cdn)", { where: { verb: "Invoke", subjectPrefix: "Cdn" }, hide: true }],
    ["capitalised Subject (ConfidentialLedger)", { where: { verb: "Update", Subject: "Ledger" }, hide: true }],
    ["misspelled varinat (Relay)", { where: { verb: "Set", varinat: "^UpdateExpanded$" }, hide: true }],
    ["alias-name instead of alias (CloudService)", { where: { subject: "^CloudService$" }, set: { "alias-name": "X" } }],
    ["hide nested under set (RedisEnterpriseCache)", { where: { verb: "New", subject: "Database" }, set: { hide: true } }],
  ])("rejects a directive that is silently ignored today: %s", (_label, directive) => {
    expect(PSDirectiveSchema.safeParse(directive).success).toBe(false);
  });

  it("rejects a selector that is not a valid regular expression", () => {
    expect(PSDirectiveSchema.safeParse({ where: { subject: "^(unclosed" }, hide: true }).success).toBe(false);
  });

  it("rejects a directive with no selector", () => {
    expect(PSDirectiveSchema.safeParse({ where: {}, hide: true }).success).toBe(false);
  });

  it("rejects a directive with no action", () => {
    expect(PSDirectiveSchema.safeParse({ where: { subject: "Volume" } }).success).toBe(false);
  });

  it("rejects an empty set block", () => {
    expect(PSDirectiveSchema.safeParse({ where: { subject: "Volume" }, set: {} }).success).toBe(false);
  });
});
