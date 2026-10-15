/**
 * The shared stage-config check.
 *
 * Every stage warns about keys it does not know through this one function, so these
 * pin its wording once: the full key, what is known, and what a stage that takes
 * nothing says.
 */

import { describe, expect, it } from "vitest";

import { NO_KEYS, warnUnknownKeys, type ConfigKeyKind } from "../src/stage-config.js";

interface Reported {
  severity: string;
  code: string;
  message: string;
}

function warnings(
  config: Record<string, unknown>,
  known: ReadonlySet<string>,
  kind: ConfigKeyKind,
  prefix?: string,
): Reported[] {
  const reported: Reported[] = [];
  warnUnknownKeys(
    config,
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

  it("names the full key in a nested map", () => {
    const known = new Set(["path", "api-version"]);

    expect(warnings({ apiVersion: "2024-05-01" }, known, "setting", "entrypoints[0].")).toEqual([
      {
        severity: "warning",
        code: "unknown-setting",
        message:
          "'entrypoints[0].apiVersion' is not a known setting and is ignored. " +
          "Known: entrypoints[0].path, entrypoints[0].api-version.",
      },
    ]);
  });

  it("says so when a stage takes none", () => {
    expect(warnings({ rootNamespace: "Contoso" }, NO_KEYS, "option").map((w) => w.message)).toEqual([
      "'rootNamespace' is not a known option and is ignored. This stage takes no options.",
    ]);
  });
});
