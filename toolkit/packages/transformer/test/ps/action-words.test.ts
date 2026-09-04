import { describe, expect, it } from "vitest";

import { isActionWord } from "../../src/ps/action-words.js";
import { isKnownActionWord } from "../../src/ps/verb-mapping.js";
import { inferCommandNames } from "../../src/ps/naming.js";

/**
 * Two dictionaries answer two different questions, and the generator disagrees with itself about
 * which words belong in each. These lock in that separation.
 */
describe("action words", () => {
  it("recognises English verbs the verb map has no entry for", () => {
    for (const word of ["issue", "begin", "migrate", "attach", "abandon"]) {
      expect(isActionWord(word)).toBe(true);
    }
    expect(isKnownActionWord("issue")).toBe(false);
    expect(isKnownActionWord("begin")).toBe(false);
  });

  it("omits words the verb map maps but the dictionary never listed", () => {
    // Upstream's own asymmetry: `reimage` resolves to a verb but is not scanned for as one.
    for (const word of ["reimage", "reboot", "poweron", "failover", "unregister"]) {
      expect(isKnownActionWord(word)).toBe(true);
      expect(isActionWord(word)).toBe(false);
    }
  });

  it("locates the action word by the dictionary, not the verb map", () => {
    // `begin` is a dictionary verb, so the words after it are the subject; consulting the verb
    // map instead would have found `migrate` and produced a different subject and variant.
    expect(inferCommandNames("Volumes", "beginMigrateVolume")).toMatchObject([
      { verb: "Invoke", subject: "BeginVolumeMigrateVolume", variant: "" },
    ]);
  });

  it("still resolves the PowerShell verb from the verb map", () => {
    expect(inferCommandNames("Volumes", "delete")).toMatchObject([
      { verb: "Remove", subject: "Volume", variant: "" },
    ]);
  });
});
