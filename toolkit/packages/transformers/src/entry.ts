/**
 * Checking a stage's entry: the part of the gen-time config the compiler hands one
 * stage, untouched.
 *
 * Every transform checks its own entry the same way. A key it does not know is a
 * warning: the stage ignores the key, and the warning is how a misspelled one
 * surfaces. An invalid value is an error the stage reports itself, which fails it.
 */

/** What an entry's keys are: the facts `seed-ir` reads, or a stage's options. */
export type EntryKind = "fact" | "option";

/** The known keys of a stage that takes none. */
export const NO_KEYS: ReadonlySet<string> = new Set();

/**
 * Warn about every key of `entry` that is not in `known`, as `unknown-<kind>`.
 *
 * `report` is the stage's own reporter, so the code carries the stage's prefix.
 * `prefix` places a nested entry in the config (`directories.`), so a message names
 * the full key.
 */
export function warnUnknownKeys(
  entry: Readonly<Record<string, unknown>>,
  known: ReadonlySet<string>,
  kind: EntryKind,
  report: (severity: "warning", code: string, message: string) => void,
  prefix = "",
): void {
  const expected =
    known.size > 0
      ? `Known: ${[...known].map((name) => `${prefix}${name}`).join(", ")}.`
      : `This stage takes no ${kind}s.`;

  for (const key of Object.keys(entry)) {
    if (known.has(key)) continue;
    report(
      "warning",
      `unknown-${kind}`,
      `'${prefix}${key}' is not a known ${kind} and is ignored. ${expected}`,
    );
  }
}
