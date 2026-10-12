/**
 * The shared entry check.
 *
 * Every transform warns about keys it does not know through this one function, so
 * these pin its wording once: the full key, what is known, and what a stage that
 * takes nothing says.
 */

import { describe, expect, it } from "vitest";

import { NO_KEYS, warnUnknownKeys, type EntryKind } from "../src/entry.js";

interface Reported {
  severity: string;
  code: string;
  message: string;
}

function warnings(
  entry: Record<string, unknown>,
  known: ReadonlySet<string>,
  kind: EntryKind,
  prefix?: string,
): Reported[] {
  const reported: Reported[] = [];
  warnUnknownKeys(
    entry,
    known,
    kind,
    (severity, code, message) => reported.push({ severity, code, message }),
    prefix,
  );
  return reported;
}

describe("warnUnknownKeys", () => {
  it("warns about each key it does not know, naming the ones it does", () => {
    const known = new Set(["inliningThreshold", "noInline"]);

    expect(warnings({ inliningThreshold: 10, inliningTreshold: 10 }, known, "option")).toEqual([
      {
        severity: "warning",
        code: "unknown-option",
        message:
          "'inliningTreshold' is not a known option and is ignored. Known: inliningThreshold, noInline.",
      },
    ]);
  });

  it("names the full key of a nested entry", () => {
    const known = new Set(["models", "processors"]);

    expect(warnings({ model: "Types" }, known, "fact", "directories.")).toEqual([
      {
        severity: "warning",
        code: "unknown-fact",
        message:
          "'directories.model' is not a known fact and is ignored. " +
          "Known: directories.models, directories.processors.",
      },
    ]);
  });

  it("says so when a stage takes none", () => {
    expect(warnings({ rootNamespace: "Contoso" }, NO_KEYS, "option").map((w) => w.message)).toEqual([
      "'rootNamespace' is not a known option and is ignored. This stage takes no options.",
    ]);
  });
});
