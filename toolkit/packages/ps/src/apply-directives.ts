/**
 * Applies authoring directives to derived attributes. Runs after inference and never feeds back
 * into it, so the two layers stay independently reviewable.
 *
 * Anything a directive asks for that this layer cannot carry out is reported rather than dropped:
 * a silently ignored override is the failure mode that leaves real modules with directives their
 * authors believe are working.
 */

import type { PSCommandAttribute, PSParameterAttribute } from "./attributes.js";
import type { DerivedAttributes } from "./derive.js";
import type { PSDirective, PSDirectiveSelector, PSDirectiveSet } from "./directives.js";

export type PSDirectiveDiagnostic = {
  severity: "warning";
  code: "unmatched-directive" | "unsupported-directive" | "ineffective-directive";
  message: string;
  /** Index into the directive list, so a report can point at the offending entry. */
  directiveIndex: number;
};

export type ApplyDirectivesResult = {
  attributes: DerivedAttributes;
  diagnostics: PSDirectiveDiagnostic[];
};

/** Anchored so `subject: Volume` does not also match `VolumeGroup`, matching AutoRest's behaviour. */
function matches(pattern: string | undefined, value: string | undefined): boolean {
  if (pattern === undefined) return true;
  if (value === undefined) return pattern === "";
  return new RegExp(pattern).test(value);
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

/** The module prefix is not stored; it is whatever `noun` carries ahead of the known parts. */
function modulePrefixOf(command: PSCommandAttribute): string {
  const tail = (command.subjectPrefix ?? "") + command.subject;
  return command.noun.endsWith(tail) ? command.noun.slice(0, command.noun.length - tail.length) : "";
}

function applyCommandSet(command: PSCommandAttribute, set: PSDirectiveSet): PSCommandAttribute {
  const next = { ...command };
  if (set.verb !== undefined) next.verb = set.verb;
  if (set["command-description"] !== undefined) next.description = set["command-description"];
  if (set.alias !== undefined) next.alias = typeof set.alias === "string" ? [set.alias] : [...set.alias];
  if (set["suppress-should-process"] !== undefined) {
    next.supportsShouldProcess = !set["suppress-should-process"];
  }

  if (set.subject !== undefined || set["subject-prefix"] !== undefined) {
    const modulePrefix = modulePrefixOf(command);
    if (set.subject !== undefined) next.subject = set.subject;
    if (set["subject-prefix"] !== undefined) {
      next.subjectPrefix = set["subject-prefix"] === "" ? undefined : set["subject-prefix"];
    }
    next.noun = modulePrefix + (next.subjectPrefix ?? "") + next.subject;
  }

  return next;
}

function applyParameterSet(
  parameter: PSParameterAttribute,
  set: PSDirectiveSet,
): PSParameterAttribute {
  const next = { ...parameter };
  if (set["parameter-name"] !== undefined) next.name = set["parameter-name"];
  if (set["parameter-description"] !== undefined) next.helpMessage = set["parameter-description"];
  if (set.alias !== undefined) next.alias = typeof set.alias === "string" ? [set.alias] : [...set.alias];
  if (set.completer !== undefined) next.completer = set.completer;
  if (set.default !== undefined) next.defaultValue = set.default;
  return next;
}

export function applyDirectives(
  attributes: DerivedAttributes,
  directives: readonly PSDirective[],
): ApplyDirectivesResult {
  let commands = attributes.commands.map((command) => ({
    ...command,
    parameters: command.parameters.map((parameter) => ({ ...parameter })),
  }));
  const diagnostics: PSDirectiveDiagnostic[] = [];

  directives.forEach((directive, directiveIndex) => {
    const where = directive.where;
    const targetsParameters = where["parameter-name"] !== undefined && directive.select !== "command";
    let matched = 0;

    if (targetsParameters) {
      const kept: typeof commands = [];
      for (const command of commands) {
        if (!commandMatches(where, command)) {
          kept.push(command);
          continue;
        }

        const parameters: PSParameterAttribute[] = [];
        for (const parameter of command.parameters) {
          if (!matches(where["parameter-name"], parameter.name)) {
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
          if (directive.set !== undefined) next = applyParameterSet(next, directive.set);
          if (directive.hide === true) next = { ...next, hidden: true };
          parameters.push(next);
        }
        kept.push({ ...command, parameters });
      }
      commands = kept;
    } else {
      const kept: typeof commands = [];
      for (const command of commands) {
        const hit =
          commandMatches(where, command) &&
          (where["parameter-name"] === undefined ||
            command.parameters.some((p) => matches(where["parameter-name"], p.name)));
        if (!hit) {
          kept.push(command);
          continue;
        }
        matched++;

        if (directive.remove === true) continue;

        let next = command;
        if (directive["clear-alias"] === true) {
          const { alias: _dropped, ...rest } = next;
          next = rest;
        }
        if (directive.set !== undefined) next = applyCommandSet(next, directive.set);
        if (directive.hide === true) next = { ...next, hidden: true };
        kept.push(next);
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
        message: `\`${unsupported}\` is recognised but not yet applied by this stage.`,
        directiveIndex,
      });
    }
  });

  return { attributes: { ...attributes, commands }, diagnostics };
}

/** Parsed, but with nowhere to land yet -- reported so it cannot become a silent no-op. */
function unsupportedKeys(directive: PSDirective): string[] {
  const keys: string[] = [];
  if (directive.set?.variant !== undefined) keys.push("set.variant");
  if (directive.set?.["clientside-pagination"] !== undefined) keys.push("set.clientside-pagination");
  return keys;
}
