import type {
  IRCommand,
  IRCommandParameter,
  PsCommandDialect,
  PsParameterDialect,
  TransformedIR,
} from "@azure-client-engine/ir";
import type { PSDirective, PSDirectiveSelector, PSDirectiveSet } from "./directives.js";

export type PSDirectiveDiagnostic = {
  severity: "warning";
  code: "unmatched-directive" | "unsupported-directive";
  message: string;
  directiveIndex: number;
};

export type ApplyDirectivesResult = {
  ir: TransformedIR;
  diagnostics: PSDirectiveDiagnostic[];
};

const patterns = new Map<string, RegExp>();

function patternFor(selector: string): RegExp {
  let pattern = patterns.get(selector);
  if (pattern === undefined) {
    pattern = /^[a-zA-Z0-9]+$/.test(selector)
      ? new RegExp(`^${selector}$`, "gi")
      : new RegExp(selector, "gi");
    patterns.set(selector, pattern);
  }
  pattern.lastIndex = 0;
  return pattern;
}

function matches(selector: string | undefined, value: string): boolean {
  if (!selector) return true;
  return patternFor(selector).test(value);
}

function replaced(previous: string, selector: string | undefined, replacement: string): string {
  return selector ? previous.replace(patternFor(selector), replacement) : replacement;
}

function psOf(command: IRCommand): PsCommandDialect | undefined {
  return command.dialect?.ps;
}

function nounPrefixOf(command: IRCommand): string {
  const noun = psOf(command)?.noun ?? "";
  return noun.endsWith(command.id.subject)
    ? noun.slice(0, noun.length - command.id.subject.length)
    : "";
}

function commandMatches(where: PSDirectiveSelector, command: IRCommand): boolean {
  const ps = psOf(command);
  return (
    ps !== undefined &&
    matches(where.verb, ps.verb) &&
    matches(where.subject, command.id.subject) &&
    matches(where["subject-prefix"], nounPrefixOf(command)) &&
    matches(where.variant, command.id.variant ?? "")
  );
}

function displayName(parameter: IRCommandParameter): string {
  return parameter.dialect?.ps?.name ?? parameter.name;
}

function parameterMatches(
  where: PSDirectiveSelector,
  parameter: IRCommandParameter,
): boolean {
  if (!matches(where["parameter-name"], displayName(parameter))) return false;
  if (where.variant === undefined) return true;
  const memberships = parameter.dialect?.ps?.parameterSets;
  return memberships === undefined || memberships.some(
    (membership) => membership.set === undefined || matches(where.variant, membership.set),
  );
}

function unionAliases(existing: readonly string[] | undefined, added: string | string[]): string[] {
  return [...new Set([...(existing ?? []), ...(typeof added === "string" ? [added] : added)])];
}

function renameSets(
  command: IRCommand,
  where: PSDirectiveSelector,
  replacement: string,
): IRCommand {
  if (where.variant === undefined) return command;
  const rename = (set: string): string => replaced(set, where.variant, replacement);
  const commandPs = psOf(command)!;
  const parameters = command.parameters.map((parameter) => {
    const memberships = parameter.dialect?.ps?.parameterSets;
    if (memberships === undefined) return parameter;
    return {
      ...parameter,
      dialect: {
        ...parameter.dialect,
        ps: {
          ...parameter.dialect!.ps!,
          parameterSets: memberships.map((membership) =>
            membership.set !== undefined && matches(where.variant, membership.set)
              ? { ...membership, set: rename(membership.set) }
              : membership,
          ),
        },
      },
    };
  });

  return {
    ...command,
    parameters,
    dialect: {
      ...command.dialect,
      ps: {
        ...commandPs,
        ...(commandPs.defaultParameterSet !== undefined &&
        matches(where.variant, commandPs.defaultParameterSet)
          ? { defaultParameterSet: rename(commandPs.defaultParameterSet) }
          : {}),
      },
    },
  };
}

function applyCommandSet(
  command: IRCommand,
  where: PSDirectiveSelector,
  set: PSDirectiveSet,
): IRCommand {
  const current = psOf(command)!;
  const ps: PsCommandDialect = { ...current };
  if (set.verb !== undefined) ps.verb = replaced(current.verb, where.verb, set.verb);
  if (set["command-description"] !== undefined) ps.description = set["command-description"];
  if (set.alias !== undefined) ps.aliases = unionAliases(current.aliases, set.alias);
  if (set["suppress-should-process"] !== undefined) {
    ps.supportsShouldProcess = !set["suppress-should-process"];
  }
  if (set.subject !== undefined || set["subject-prefix"] !== undefined) {
    const subject = set.subject !== undefined
      ? replaced(command.id.subject, where.subject, set.subject)
      : command.id.subject;
    const prefix = set["subject-prefix"] !== undefined
      ? replaced(nounPrefixOf(command), where["subject-prefix"], set["subject-prefix"])
      : nounPrefixOf(command);
    ps.noun = prefix + subject;
  }

  let next: IRCommand = { ...command, dialect: { ...command.dialect, ps } };
  if (set.variant !== undefined) next = renameSets(next, where, set.variant);
  return next;
}

function applyParameterSet(
  parameter: IRCommandParameter,
  where: PSDirectiveSelector,
  set: PSDirectiveSet,
): IRCommandParameter {
  const current = parameter.dialect?.ps ?? {};
  const ps: PsParameterDialect = { ...current };
  if (set["parameter-name"] !== undefined) {
    ps.name = replaced(displayName(parameter), where["parameter-name"], set["parameter-name"]);
  }
  if (set["parameter-description"] !== undefined) ps.description = set["parameter-description"];
  if (set.alias !== undefined) ps.aliases = unionAliases(current.aliases, set.alias);
  if (set.completer !== undefined) ps.completer = set.completer;
  if (set.default !== undefined) ps.defaultValue = set.default;
  return { ...parameter, dialect: { ...parameter.dialect, ps } };
}

function cloneCommand(command: IRCommand): IRCommand {
  return {
    ...command,
    dialect: command.dialect === undefined ? undefined : {
      ...command.dialect,
      ps: command.dialect.ps === undefined ? undefined : { ...command.dialect.ps },
    },
    parameters: command.parameters.map((parameter) => ({
      ...parameter,
      dialect: parameter.dialect === undefined ? undefined : {
        ...parameter.dialect,
        ps: parameter.dialect.ps === undefined ? undefined : {
          ...parameter.dialect.ps,
          parameterSets: parameter.dialect.ps.parameterSets?.map((membership) => ({ ...membership })),
        },
      },
    })),
  };
}

export function applyDirectives(
  input: TransformedIR,
  directives: readonly PSDirective[],
): ApplyDirectivesResult {
  let commands = input.commands.map(cloneCommand);
  const diagnostics: PSDirectiveDiagnostic[] = [];

  directives.forEach((directive, directiveIndex) => {
    const where = directive.where;
    const targetsParameters = where["parameter-name"] !== undefined && directive.select !== "command";
    let matched = 0;

    if (targetsParameters) {
      commands = commands.map((command) => {
        if (!commandMatches(where, command)) return command;
        const parameters: IRCommandParameter[] = [];
        for (const parameter of command.parameters) {
          if (!parameterMatches(where, parameter)) {
            parameters.push(parameter);
            continue;
          }
          matched++;
          if (directive.remove === true) {
            parameters.push(parameter);
            continue;
          }

          let next = parameter;
          if (directive["clear-alias"] === true && next.dialect?.ps !== undefined) {
            const { aliases: _aliases, ...ps } = next.dialect.ps;
            next = { ...next, dialect: { ...next.dialect, ps } };
          }
          if (directive.set !== undefined) next = applyParameterSet(next, where, directive.set);
          if (directive.hide === true) {
            next = { ...next, dialect: { ...next.dialect, ps: { ...next.dialect?.ps, hidden: true } } };
          }
          parameters.push(next);
        }
        return { ...command, parameters };
      });
    } else {
      const kept: IRCommand[] = [];
      for (const command of commands) {
        const hit = commandMatches(where, command) &&
          (where["parameter-name"] === undefined ||
            command.parameters.some((parameter) => parameterMatches(where, parameter)));
        if (!hit) {
          kept.push(command);
          continue;
        }
        matched++;
        if (directive.remove === true) continue;

        let next = command;
        if (directive["clear-alias"] === true) {
          const { aliases: _aliases, ...ps } = psOf(next)!;
          next = { ...next, dialect: { ...next.dialect, ps } };
        }
        if (directive.set !== undefined) next = applyCommandSet(next, where, directive.set);
        if (directive.hide === true) {
          next = { ...next, dialect: { ...next.dialect, ps: { ...psOf(next)!, hidden: true } } };
        }
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
        message: `${unsupported}.`,
        directiveIndex,
      });
    }
  });

  return { ir: { ...input, commands }, diagnostics };
}

const COMMAND_ONLY_SET_KEYS = [
  "verb",
  "subject",
  "subject-prefix",
  "variant",
  "command-description",
  "suppress-should-process",
] as const satisfies readonly (keyof PSDirectiveSet)[];

const PARAMETER_ONLY_SET_KEYS = [
  "parameter-name",
  "parameter-description",
  "completer",
  "default",
] as const satisfies readonly (keyof PSDirectiveSet)[];

function unsupportedKeys(directive: PSDirective): string[] {
  const targetsParameters =
    directive.where["parameter-name"] !== undefined && directive.select !== "command";
  const reasons: string[] = [];
  if (targetsParameters && directive.remove === true) {
    reasons.push(
      "Parameter removal has no presentation-only storage; removing the canonical parameter would invalidate request bindings",
    );
  }

  const set = directive.set;
  if (set === undefined) return reasons;
  const target = targetsParameters ? "parameter" : "command";
  const inapplicable = targetsParameters ? COMMAND_ONLY_SET_KEYS : PARAMETER_ONLY_SET_KEYS;
  reasons.push(...inapplicable
    .filter((key) => set[key] !== undefined)
    .map((key) => `\`set.${key}\` does not apply to a ${target} directive`));

  if (set["clientside-pagination"] !== undefined) {
    reasons.push("`set.clientside-pagination` has no canonical command dialect storage");
  }
  if (!targetsParameters && set.variant !== undefined && directive.where.variant === undefined) {
    reasons.push("`set.variant` needs a `variant` selector to know which set to rename");
  }
  return reasons;
}
