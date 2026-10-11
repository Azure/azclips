/**
 * Cmdlet name inference, ported from autorest.powershell so generated names match what the
 * existing PowerShell generator produces for the same API.
 *
 * The shape of a name is `verb + "-" + modulePrefix + subjectPrefix + subject`, where
 * `subjectPrefix` is authored configuration rather than anything derived, and `subject` comes
 * from the operation *group* (an operation's declaring namespace) -- never from its response
 * model. Sources: `plugins/create-commands-v2.ts` (`inferCommand`, `createCommandVariant`),
 * `plugins/ps-namer-v2.ts` (`getDeduplicatedNoun`), and `@azure-tools/codegen`'s
 * `text-manipulation` helpers.
 *
 * Authoring overrides (AutoRest's `directive:` block) are deliberately not modelled here: real
 * modules lean on them heavily, but they are a separate layer applied on top of inference.
 */

import { resolveVerb, isKnownActionWord } from "./verb-mapping.js";
import { isActionWord } from "./action-words.js";

/**
 * Split an identifier into lower-cased words. Mirrors `@azure-tools/codegen`'s `deconstruct`,
 * except that its `[a-z|A-Z]` class (which matches a literal `|`) is written `[a-zA-Z]` here --
 * identifiers cannot contain `|`, so this is the same behaviour without the stray alternation.
 */
export function deconstruct(identifier: string): string[] {
  return identifier
    .replace(/([a-z]+)([A-Z])/g, "$1 $2")
    .replace(/(\d+)([a-zA-Z]+)/g, "$1 $2")
    .replace(/\b([A-Z]+)([A-Z])([a-z])/, "$1 $2$3")
    .split(/[\W_]+/)
    .filter((word) => word.length > 0)
    .map((word) => word.toLowerCase());
}

/** AutoRest's `isEqual`: case- and accent-insensitive, and false for empty/absent words. */
function wordsEqual(a: string | undefined, b: string | undefined): boolean {
  return b !== undefined && a !== undefined && a.length > 0 && a.localeCompare(b, undefined, { sensitivity: "base" }) === 0;
}

/** Drop adjacent repeats, and adjacent repeated *pairs* (`a b a b` -> `a b`). */
export function removeSequentialDuplicates(words: readonly string[]): string[] {
  const ids = [...words].filter((word) => word.length > 0);
  for (let i = 0; i < ids.length; i++) {
    while (wordsEqual(ids[i], ids[i - 1])) ids.splice(i, 1);
    while (wordsEqual(ids[i], ids[i - 2]) && wordsEqual(ids[i + 1], ids[i - 1])) ids.splice(i, 2);
  }
  return ids;
}

/** The only acronyms `@azure-tools/codegen` upper-cases; `dns` and `sas` deliberately are not. */
const ACRONYMS: ReadonlySet<string> = new Set(["ip", "os", "ms", "vm"]);

function capitalize(word: string): string {
  if (ACRONYMS.has(word)) return word.toUpperCase();
  return word.length > 0 ? word[0]!.toUpperCase() + word.slice(1) : word;
}

export function pascalCase(words: readonly string[]): string {
  return removeSequentialDuplicates(words).map(capitalize).join("");
}

/** Strip a trailing plural, handling the common English suffix patterns. */
import { singularize } from "./pluralization.js";

export { singularize };

/** Words that are a preposition's object rather than a split point (`signIn`, not `sign | in`). */
const SPECIAL_WORDS: Readonly<Record<string, readonly string[]>> = { in: ["sign"] };

const PREPOSITIONS: readonly string[] = ["with", "at", "by", "for", "in", "of"];

function indexOfPreposition(preposition: string, parts: readonly string[]): number {
  const special = SPECIAL_WORDS[preposition];
  if (special === undefined) return parts.indexOf(preposition);

  let start = 0;
  for (;;) {
    const index = parts.indexOf(preposition, start);
    if (index <= 0 || !special.includes(parts[index - 1]!)) return index;
    start = index + 1;
  }
}

function splitOnPreposition(
  preposition: string,
  parts: readonly string[],
): [string[], string[]] | undefined {
  const index = indexOfPreposition(preposition, parts);
  return index > 0 ? [parts.slice(0, index), parts.slice(index + 1)] : undefined;
}

function splitOnAnyPreposition(parts: readonly string[]): [string[], string[]] | undefined {
  for (const preposition of PREPOSITIONS) {
    const result = splitOnPreposition(preposition, parts);
    if (result !== undefined && result[0].length > 0) return result;
  }
  return undefined;
}

/** An inferred cmdlet name, before the subject prefix is applied. */
export type InferredName = {
  verb: string;
  /** Bare subject, e.g. "VolumeSnapshot". */
  subject: string;
  /** Parameter-set discriminator taken from a trailing preposition ("ListByResourceGroup" -> "ResourceGroup"). */
  variant: string;
  /** The word the verb resolved from, before mapping -- `list` and `get` both become `Get`. */
  action: string;
};

/**
 * `createCommandVariant`: an action that resolves to no known verb keeps its own word in the
 * subject, so `preBackup`-style operations stay distinguishable instead of colliding on `Invoke`.
 */
function buildName(action: string, subject: readonly string[], variant: readonly string[]): InferredName {
  const verb = resolveVerb(action);
  const subjectWords =
    verb === "Invoke" && action.toLowerCase() !== "post" ? [action, ...subject] : [...subject];

  return {
    verb,
    subject: pascalCase(removeSequentialDuplicates(subjectWords.map(singularize))),
    variant: pascalCase(variant),
    action,
  };
}

function inferFromWords(
  words: readonly string[],
  group: string,
  suffix: readonly string[] = [],
): InferredName[] {
  const parts = words.filter((word) => word !== "all");

  if (parts.length === 0) return [buildName(group, [group], suffix)];
  if (parts.length === 1) return [buildName(parts[0]!, [group], suffix)];

  if (parts.length === 2) {
    if (isActionWord(parts[0]!)) return [buildName(parts[0]!, [group, parts[1]!], suffix)];
    if (isActionWord(parts[1]!)) return [buildName(parts[1]!, [group, parts[0]!], suffix)];
    return [buildName(parts[0]!, [group, parts[1]!], suffix)];
  }

  // "createOrUpdate" fans out into both commands, as AutoRest does: New-* from the left branch
  // and Update-* from the right.
  const either = splitOnPreposition("or", parts);
  if (either !== undefined) {
    return [
      ...inferFromWords([...either[0], ...either[1].slice(1)], group, suffix),
      ...inferFromWords(either[1], group, suffix),
    ];
  }

  // "listByResourceGroup" -> action "list", variant "ResourceGroup".
  const prepositional = splitOnAnyPreposition(parts);
  if (prepositional !== undefined) {
    return inferFromWords(prepositional[0], group, [...suffix, ...prepositional[1]]);
  }

  for (let i = 0; i < parts.length; i++) {
    if (!isActionWord(parts[i]!)) continue;
    if (i === 0) return [buildName(parts[i]!, [group, ...parts.slice(1)], suffix)];
    if (i === parts.length - 1) return [buildName(parts[i]!, [group, ...parts.slice(0, i)], suffix)];
    return [buildName(parts[i]!, [group], [...suffix, ...parts.slice(i + 1)])];
  }

  return [buildName(parts[0]!, [group, ...parts.slice(1)], suffix)];
}

/**
 * Infer verb/subject/variant from an operation's declaring group and action word --
 * AutoRest's `inferCommandNames`. Usually one name, but an `X`-or-`Y` action word fans out
 * into one command per branch.
 */
export function inferCommandNames(group: string, actionWord: string): InferredName[] {
  const groupWords = removeSequentialDuplicates(deconstruct(group));
  if (groupWords.length > 0) {
    groupWords[groupWords.length - 1] = singularize(groupWords[groupWords.length - 1]!);
  }
  const groupName = pascalCase(groupWords);

  // A whole-word match against the *verb map* -- not the verb dictionary -- names the command
  // after the group alone.
  if (isKnownActionWord(actionWord)) return [buildName(actionWord, [groupName], [])];

  return inferFromWords(deconstruct(actionWord), groupName);
}

/**
 * Split a noun between prefix and subject so the shared words appear once --
 * AutoRest's `getDeduplicatedNoun`. "ElasticSan" + "ElasticSan" -> subject "", giving
 * `Get-AzElasticSan` rather than `Get-AzElasticSanElasticSan`.
 */
export function deduplicateNoun(
  subjectPrefix: string,
  subject: string,
): { subjectPrefix: string; subject: string } {
  const prefixWords = removeSequentialDuplicates(deconstruct(subjectPrefix));
  const subjectWords = removeSequentialDuplicates(deconstruct(subject));
  const merged = removeSequentialDuplicates([...prefixWords, ...subjectWords]);

  const reversedSubject: string[] = [];
  for (let m = merged.length - 1, s = subjectWords.length - 1; s >= 0 && m >= 0; m--, s--) {
    if (merged[m] !== subjectWords[s]) break;
    reversedSubject.push(merged.pop()!);
  }

  return { subjectPrefix: pascalCase(merged), subject: pascalCase(reversedSubject.reverse()) };
}
