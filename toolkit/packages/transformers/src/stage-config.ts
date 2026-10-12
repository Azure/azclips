/**
 * Checking a stage config: the part of the gen-time config the compiler hands one
 * stage, untouched.
 *
 * Every stage checks its own config the same way. A key it does not know is a
 * warning: the stage ignores the key, and the warning is how a misspelled one
 * surfaces. An invalid value is an error the stage reports itself, which fails it.
 *
 * Exported for the compiler too, which checks its own `stages` setting and runs
 * the two external stages with these same rules.
 */

/**
 * What a stage config's keys are: the facts `seed-ir` reads, a stage's options, or
 * the compiler's own settings for a stage (`disabled`, `options`).
 */
export type ConfigKeyKind = "fact" | "option" | "setting";

/** The known keys of a stage that takes none. */
export const NO_KEYS: ReadonlySet<string> = new Set();

/** True for a plain map: the shape of every stage config, and of every section. */
export function isMap(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** A value as a reader would recognize it in a message: `'abc'`, `the number 1`, `a list`. */
export function describeValue(value: unknown): string {
  if (typeof value === "string") return value.length === 0 ? "an empty string" : `'${value}'`;
  if (value === null) return "null";
  if (Array.isArray(value)) return "a list";
  if (typeof value === "object") return "a map";
  return `the ${typeof value} ${String(value)}`;
}

/**
 * Warn about every key of `config` that is not in `known`, as `unknown-<kind>`.
 *
 * `report` is the stage's own reporter, so the code carries the stage's prefix.
 * `prefix` places a nested map in the config (`directories.`), so a message names
 * the full key.
 */
export function warnUnknownKeys(
  config: Readonly<Record<string, unknown>>,
  known: ReadonlySet<string>,
  kind: ConfigKeyKind,
  report: (severity: "warning", code: string, message: string) => void,
  prefix = "",
): void {
  const expected =
    known.size > 0
      ? `Known: ${[...known].map((name) => `${prefix}${name}`).join(", ")}.`
      : `This stage takes no ${kind}s.`;

  for (const key of Object.keys(config)) {
    if (known.has(key)) continue;
    report(
      "warning",
      `unknown-${kind}`,
      `'${prefix}${key}' is not a known ${kind} and is ignored. ${expected}`,
    );
  }
}
