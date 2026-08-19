/**
 * Directive-style (`select`/`where`/`set`) bulk overrides for
 * `create-ps-commands`, per ps-cmdlet-schema's design.md Decision 7. Modeled
 * on AutoRest.PowerShell's real `docs/directives.md` grammar, narrowed to
 * this schema's scope: only `command` and `parameter` selectors (no
 * `model`/`property` — those are out of scope, see design.md's Non-Goals).
 *
 * Precedence (Decision 7): an inline `hints.powershell` value an author
 * already set always wins; a directive only supplies a *fallback* default,
 * applied on top of plain inference, never overriding an explicit inline
 * value. Directive matching itself is evaluated against that same
 * "already-authored, else inferred" value — the operation's resolved
 * identity as it would be if no directive existed — mirroring how AutoRest's
 * own directives match already-resolved verb/subject from an earlier stage.
 */

import { z } from "zod";
import { ConfirmImpacts, type ConfirmImpact } from "@azure-client-engine/ir";

/** What a directive selects. Inferred from `where` when omitted (see `resolveSelector`). */
export const DirectiveSelectors = ["command", "parameter"] as const;
export type DirectiveSelector = (typeof DirectiveSelectors)[number];

/**
 * Filter criteria. `subject` is the bare noun (no subject prefix), matching
 * `hints.powershell.noun`'s own bare convention. `parameterName` filters a
 * `parameter` selector's own name, or (for a `command` selector) requires
 * the command to have a parameter with that name.
 */
export type DirectiveWhere = {
  verb?: string | undefined;
  subject?: string | undefined;
  subjectPrefix?: string | undefined;
  parameterName?: string | undefined;
};

export const DirectiveWhereSchema: z.ZodType<DirectiveWhere> = z
  .object({
    verb: z.string().min(1).optional(),
    subject: z.string().min(1).optional(),
    subjectPrefix: z.string().min(1).optional(),
    parameterName: z.string().min(1).optional(),
  })
  .strict();

/** Actions. Fields not relevant to the resolved selector are simply ignored. */
export type DirectiveSet = {
  /** Command-level: renames the verb. */
  verb?: string | undefined;
  /** Command-level: renames the bare noun (subject). */
  subject?: string | undefined;
  /** Command-level or parameter-level: additional alias(es). */
  alias?: string[] | undefined;
  /** Command-level: excludes the command from the generated module. */
  hidden?: boolean | undefined;
  /** Command-level. */
  confirmImpact?: ConfirmImpact | undefined;
  /** Parameter-level: excludes the parameter from tab-completion/help (mirrors `ParameterAttribute.DontShow`). */
  dontShow?: boolean | undefined;
};

export const DirectiveSetSchema: z.ZodType<DirectiveSet> = z
  .object({
    verb: z.string().min(1).optional(),
    subject: z.string().min(1).optional(),
    alias: z.array(z.string().min(1)).optional(),
    hidden: z.boolean().optional(),
    confirmImpact: z.enum(ConfirmImpacts).optional(),
    dontShow: z.boolean().optional(),
  })
  .strict();

export type Directive = {
  select?: DirectiveSelector | undefined;
  where: DirectiveWhere;
  set: DirectiveSet;
};

export const DirectiveSchema: z.ZodType<Directive> = z
  .object({
    select: z.enum(DirectiveSelectors).optional(),
    where: DirectiveWhereSchema,
    set: DirectiveSetSchema,
  })
  .strict();

export const DirectivesSchema: z.ZodType<Directive[]> = z.array(DirectiveSchema);

/**
 * Resolve which kind of node a directive targets. Mirrors AutoRest's own
 * inference: an explicit `select` always wins; absent that, a `parameterName`
 * filter with no `select` defaults to `"parameter"` (matching AutoRest's own
 * default — see `docs/directives.md`'s first example), otherwise `"command"`.
 */
export function resolveSelector(directive: Directive): DirectiveSelector {
  if (directive.select !== undefined) return directive.select;
  return directive.where.parameterName !== undefined ? "parameter" : "command";
}

/** The already-resolved (authored-or-inferred) identity a directive's `where` filter matches against. */
export type CommandIdentity = {
  verb: string;
  subject: string;
  subjectPrefix: string | undefined;
  parameterNames: readonly string[];
};

/** True if a command-selector directive's `where` filter matches this operation's resolved identity. */
export function matchesCommand(where: DirectiveWhere, identity: CommandIdentity): boolean {
  if (where.verb !== undefined && where.verb !== identity.verb) return false;
  if (where.subject !== undefined && where.subject !== identity.subject) return false;
  if (where.subjectPrefix !== undefined && where.subjectPrefix !== identity.subjectPrefix) return false;
  if (where.parameterName !== undefined && !identity.parameterNames.includes(where.parameterName)) {
    return false;
  }
  return true;
}

/** A parameter's identity plus its owning command's, for a parameter-selector directive's optional scoping filters. */
export type ParameterIdentity = {
  parameterName: string;
  owningCommand: CommandIdentity;
};

/** True if a parameter-selector directive's `where` filter matches this parameter (optionally scoped to its command). */
export function matchesParameter(where: DirectiveWhere, identity: ParameterIdentity): boolean {
  if (where.parameterName === undefined || where.parameterName !== identity.parameterName) return false;
  if (where.verb !== undefined && where.verb !== identity.owningCommand.verb) return false;
  if (where.subject !== undefined && where.subject !== identity.owningCommand.subject) return false;
  if (
    where.subjectPrefix !== undefined &&
    where.subjectPrefix !== identity.owningCommand.subjectPrefix
  ) {
    return false;
  }
  return true;
}

/** Merge multiple matching directives' `set` actions. Later directives win on scalars; `alias` arrays concatenate (deduped). */
export function mergeDirectiveSets(sets: readonly DirectiveSet[]): DirectiveSet {
  const merged: DirectiveSet = {};
  const aliases: string[] = [];

  for (const set of sets) {
    if (set.verb !== undefined) merged.verb = set.verb;
    if (set.subject !== undefined) merged.subject = set.subject;
    if (set.hidden !== undefined) merged.hidden = set.hidden;
    if (set.confirmImpact !== undefined) merged.confirmImpact = set.confirmImpact;
    if (set.dontShow !== undefined) merged.dontShow = set.dontShow;
    for (const alias of set.alias ?? []) {
      if (!aliases.includes(alias)) aliases.push(alias);
    }
  }

  if (aliases.length > 0) merged.alias = aliases;
  return merged;
}
