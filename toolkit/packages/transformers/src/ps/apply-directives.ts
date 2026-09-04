/**
 * Applies authoring directives to derived attributes. Runs after inference and never feeds back
 * into it, so the two layers stay independently reviewable.
 *
 * Anything a directive asks for that this layer cannot carry out is reported rather than dropped:
 * a silently ignored override is the failure mode that leaves real modules with directives their
 * authors believe are working.
 */

import type { PS, PSCommandAttribute, PSParameterAttribute } from "@azure-client-engine/ir";
import type { PSDirective, PSDirectiveSelector, PSDirectiveSet } from "./directives.js";

export type PSDirectiveDiagnostic = {
  severity: "warning";
  code: "unmatched-directive" | "unsupported-directive";
  message: string;
  /** Index into the directive list, so a report can point at the offending entry. */
  directiveIndex: number;
};

export type ApplyDirectivesResult = {
  attributes: PS;
  diagnostics: PSDirectiveDiagnostic[];
};

/**
 * Mirrors `getPatternToMatch` in modifiers-v2.ts: a selector of only letters and digits is
 * anchored, so `subject: Volume` cannot also match `VolumeGroup`, while a selector carrying regex
 * syntax is used as written. Matching is case-insensitive, and an empty selector is no constraint
 * at all -- modules author `subject: ''` as a placeholder next to the selector they mean.
 */
const patterns = new Map<string, RegExp>();
function patternFor(selector: string): RegExp {
  let pattern = patterns.get(selector);
  if (pattern === undefined) {
    // `g` matches AutoRest, whose `set` replacement rewrites every match rather than the first.
    pattern = /^[a-zA-Z0-9]+$/.test(selector)
      ? new RegExp(`^${selector}$`, "gi")
      : new RegExp(selector, "gi");
    patterns.set(selector, pattern);
  }
  return pattern;
}

function matches(selector: string | undefined, value: string): boolean {
  if (!selector) return true;
  return value.match(patternFor(selector)) !== null;
}

/** Replacement runs through the selector, so a `set` value can carry `$1`-style capture groups. */
function replaced(previous: string, selector: string | undefined, replacement: string): string {
  return selector ? previous.replace(patternFor(selector), replacement) : replacement;
}

function commandMatches(where: PSDirectiveSelector, command: PSCommandAttribute): boolean {
  return (
    matches(where.verb, command.verb) &&
    matches(where.subject, command.subject) &&
    matches(where["subject-prefix"], command.subjectPrefix ?? "") &&
    (where.variant === undefined ||
      command.parameterSets.some((set) => matches(where.variant, set.name)))
  );
}

/** The sets a parameter takes part in; one that names neither takes part in all of them. */
function setsOf(command: PSCommandAttribute, parameter: PSParameterAttribute): string[] {
  if (parameter.parameterSets !== undefined) return Object.keys(parameter.parameterSets);
  if (parameter.parameterSetName !== undefined) return [parameter.parameterSetName];
  return command.parameterSets.map((set) => set.name);
}

/**
 * A `variant` selector narrows which parameters a directive reaches, not just which commands.
 * `modifiers-v2.ts` gets this for free -- its unit is one cmdlet *per variant*, each with its own
 * parameter list -- whereas a command here holds every set at once, so the scoping is explicit.
 */
function parameterMatches(
  where: PSDirectiveSelector,
  command: PSCommandAttribute,
  parameter: PSParameterAttribute,
): boolean {
  if (!matches(where["parameter-name"], parameter.name)) return false;
  if (where.variant === undefined) return true;
  return setsOf(command, parameter).some((set) => matches(where.variant, set));
}

/** The module prefix is not stored; it is whatever `noun` carries ahead of the known parts. */
function modulePrefixOf(command: PSCommandAttribute): string {
  const tail = (command.subjectPrefix ?? "") + command.subject;
  return command.noun.endsWith(tail) ? command.noun.slice(0, command.noun.length - tail.length) : "";
}

function applyCommandSet(
  command: PSCommandAttribute,
  where: PSDirectiveSelector,
  set: PSDirectiveSet,
  matchedVariant: string | undefined,
): PSCommandAttribute {
  const next = { ...command };
  if (set.verb !== undefined) next.verb = replaced(command.verb, where.verb, set.verb);
  if (set["command-description"] !== undefined) next.description = set["command-description"];
  if (set.alias !== undefined) next.alias = unionAlias(command.alias, set.alias);
  if (set["suppress-should-process"] !== undefined) {
    next.supportsShouldProcess = !set["suppress-should-process"];
  }
  // The capability half lives in IR (`IRPageableSemantics`); this is the opt-in half.
  if (set["clientside-pagination"] !== undefined) next.supportsPaging = set["clientside-pagination"];

  if (set.subject !== undefined || set["subject-prefix"] !== undefined) {
    const modulePrefix = modulePrefixOf(command);
    if (set.subject !== undefined) {
      next.subject = replaced(command.subject, where.subject, set.subject);
    }
    if (set["subject-prefix"] !== undefined) {
      const prefix = replaced(
        command.subjectPrefix ?? "",
        where["subject-prefix"],
        set["subject-prefix"],
      );
      next.subjectPrefix = prefix === "" ? undefined : prefix;
    }
    next.noun = modulePrefix + (next.subjectPrefix ?? "") + next.subject;
  }

  if (set.variant !== undefined && matchedVariant !== undefined) {
    return renameParameterSet(
      next,
      matchedVariant,
      replaced(matchedVariant, where.variant, set.variant),
    );
  }
  return next;
}

/** Renames a set and every reference to it, so the schema's set-reference refinement still holds. */
function renameParameterSet(
  command: PSCommandAttribute,
  from: string,
  to: string,
): PSCommandAttribute {
  return {
    ...command,
    parameterSets: command.parameterSets.map((set) => (set.name === from ? { ...set, name: to } : set)),
    defaultParameterSetName:
      command.defaultParameterSetName === from ? to : command.defaultParameterSetName,
    parameters: command.parameters.map((parameter) => {
      const next = { ...parameter };
      if (next.parameterSetName === from) next.parameterSetName = to;
      if (next.parameterSets !== undefined && from in next.parameterSets) {
        const { [from]: moved, ...rest } = next.parameterSets;
        next.parameterSets = { ...rest, [to]: moved! };
      }
      return next;
    }),
  };
}

/** AutoRest adds to the aliases already on the attribute rather than replacing them. */
function unionAlias(existing: readonly string[] | undefined, added: string | string[]): string[] {
  return [...new Set([...(existing ?? []), ...(typeof added === "string" ? [added] : added)])];
}

/**
 * Drops the sets a `variant` selector names, along with any parameter left with no set to belong
 * to. In `modifiers-v2.ts` each variant is its own cmdlet, so `remove` deletes that one variant;
 * here every variant lives on a single command, so the same directive has to cut sets out of it.
 * Returns undefined when no set survives, which is the case that really does drop the command.
 */
function removeParameterSets(
  command: PSCommandAttribute,
  variant: string,
): PSCommandAttribute | undefined {
  const removed = new Set(
    command.parameterSets.filter((set) => matches(variant, set.name)).map((set) => set.name),
  );
  const parameterSets = command.parameterSets.filter((set) => !removed.has(set.name));
  if (parameterSets.length === 0) return undefined;

  const parameters: PSParameterAttribute[] = [];
  for (const parameter of command.parameters) {
    const next = { ...parameter };
    if (next.parameterSets !== undefined) {
      const kept = Object.entries(next.parameterSets).filter(([name]) => !removed.has(name));
      if (kept.length === 0) continue;
      next.parameterSets = Object.fromEntries(kept);
    } else if (next.parameterSetName !== undefined && removed.has(next.parameterSetName)) {
      continue;
    }
    parameters.push(next);
  }

  return {
    ...command,
    parameterSets,
    parameters,
    defaultParameterSetName: removed.has(command.defaultParameterSetName)
      ? parameterSets[0]!.name
      : command.defaultParameterSetName,
  };
}

function applyParameterSet(
  parameter: PSParameterAttribute,
  where: PSDirectiveSelector,
  set: PSDirectiveSet,
): PSParameterAttribute {
  const next = { ...parameter };
  if (set["parameter-name"] !== undefined) {
    next.name = replaced(parameter.name, where["parameter-name"], set["parameter-name"]);
  }
  if (set["parameter-description"] !== undefined) next.helpMessage = set["parameter-description"];
  if (set.alias !== undefined) next.alias = unionAlias(parameter.alias, set.alias);
  if (set.completer !== undefined) next.completer = set.completer;
  if (set.default !== undefined) next.defaultValue = set.default;
  return next;
}

export function applyDirectives(
  attributes: PS,
  directives: readonly PSDirective[],
): ApplyDirectivesResult {
  let commands = attributes.commands.map((command) => ({
    // Kept alongside so `cmdletsToExport` can follow a rename; directives may rename the same
    // command more than once, so the starting name is what the module list is keyed by.
    origin: `${command.verb}-${command.noun}`,
    command: {
      ...command,
      parameters: command.parameters.map((parameter) => ({ ...parameter })),
    },
  }));
  const diagnostics: PSDirectiveDiagnostic[] = [];

  directives.forEach((directive, directiveIndex) => {
    const where = directive.where;
    const targetsParameters = where["parameter-name"] !== undefined && directive.select !== "command";
    let matched = 0;

    if (targetsParameters) {
      const kept: typeof commands = [];
      for (const { origin, command } of commands) {
        if (!commandMatches(where, command)) {
          kept.push({ origin, command });
          continue;
        }

        const parameters: PSParameterAttribute[] = [];
        for (const parameter of command.parameters) {
          if (!parameterMatches(where, command, parameter)) {
            parameters.push(parameter);
            continue;
          }

          // One attribute carries one name, so a set-scoped rename cannot reach a parameter that
          // also lives outside those sets -- autorest.powershell splits it in two (`-Name` in Get,
          // `-SearchName` in List on Get-AzMLWorkspaceDatastore). Report rather than rename both.
          if (
            where.variant !== undefined &&
            directive.set?.["parameter-name"] !== undefined &&
            setsOf(command, parameter).some((set) => !matches(where.variant, set))
          ) {
            diagnostics.push({
              severity: "warning",
              code: "unsupported-directive",
              message:
                `Cannot rename '${parameter.name}' on ${command.verb}-${command.noun} for variant ` +
                `'${where.variant}' alone: the parameter also belongs to sets the variant does not match.`,
              directiveIndex,
            });
            parameters.push(parameter);
            continue;
          }

          matched++;

          if (directive.remove === true) continue;

          let next = parameter;
          if (directive["clear-alias"] === true) {
            const { alias: _dropped, ...rest } = next;
            next = rest;
          }
          if (directive.set !== undefined) next = applyParameterSet(next, where, directive.set);
          if (directive.hide === true) next = { ...next, hidden: true };
          parameters.push(next);
        }
        kept.push({ origin, command: { ...command, parameters } });
      }
      commands = kept;
    } else {
      const kept: typeof commands = [];
      for (const { origin, command } of commands) {
        const hit =
          commandMatches(where, command) &&
          (where["parameter-name"] === undefined ||
            command.parameters.some((p) => parameterMatches(where, command, p)));
        if (!hit) {
          kept.push({ origin, command });
          continue;
        }
        matched++;

        if (directive.remove === true) {
          if (where.variant === undefined) continue;
          const trimmed = removeParameterSets(command, where.variant);
          if (trimmed === undefined) continue;
          kept.push({ origin, command: trimmed });
          continue;
        }

        let next = command;
        if (directive["clear-alias"] === true) {
          const { alias: _dropped, ...rest } = next;
          next = rest;
        }
        if (directive.set !== undefined) {
          const matchedVariant =
            where.variant === undefined
              ? undefined
              : command.parameterSets.find((s) => matches(where.variant, s.name))?.name;
          next = applyCommandSet(next, where, directive.set, matchedVariant);
        }
        if (directive.hide === true) next = { ...next, hidden: true };
        kept.push({ origin, command: next });
      }
      commands = kept;
    }

    if (matched === 0) {
      diagnostics.push({
        severity: "warning",
        code: "unmatched-directive",
        message: `Directive matched nothing: ${JSON.stringify(where)}.`,
        directiveIndex,
      });
    }

    for (const unsupported of unsupportedKeys(directive)) {
      diagnostics.push({
        severity: "warning",
        code: "unsupported-directive",
        message: `${unsupported}.`,
        directiveIndex,
      });
    }
  });

  // A directive can rename or remove a command, which `cmdletsToExport` has to follow or the
  // document contradicts itself.
  const renamed = new Map(
    commands.map(({ origin, command }) => [origin, `${command.verb}-${command.noun}`]),
  );
  const modules = attributes.modules.map((module) => ({
    ...module,
    cmdletsToExport: module.cmdletsToExport
      .map((name) => renamed.get(name))
      .filter((name): name is string => name !== undefined),
  }));

  return {
    attributes: { ...attributes, commands: commands.map(({ command }) => command), modules },
    diagnostics,
  };
}

const COMMAND_ONLY_SET_KEYS = [
  "verb",
  "subject",
  "subject-prefix",
  "variant",
  "command-description",
  "suppress-should-process",
  "clientside-pagination",
] as const satisfies readonly (keyof PSDirectiveSet)[];

const PARAMETER_ONLY_SET_KEYS = [
  "parameter-name",
  "parameter-description",
  "completer",
  "default",
] as const satisfies readonly (keyof PSDirectiveSet)[];

/**
 * A directive selects either commands or parameters, and AutoRest quietly ignores whichever
 * setters do not belong to the side it picked. Reporting them keeps the failure visible instead.
 */
function unsupportedKeys(directive: PSDirective): string[] {
  const set = directive.set;
  if (set === undefined) return [];

  const targetsParameters =
    directive.where["parameter-name"] !== undefined && directive.select !== "command";
  const target = targetsParameters ? "parameter" : "command";
  const inapplicable = targetsParameters ? COMMAND_ONLY_SET_KEYS : PARAMETER_ONLY_SET_KEYS;

  const reasons = inapplicable
    .filter((key) => set[key] !== undefined)
    .map((key) => `\`set.${key}\` does not apply to a ${target} directive`);

  if (!targetsParameters && set.variant !== undefined && directive.where.variant === undefined) {
    reasons.push("`set.variant` needs a `variant` selector to know which set to rename");
  }
  return reasons;
}
