/**
 * CLI rules that span commands, run from `TransformedIRSchema`.
 *
 * A single dialect can be checked on its own (`CliCommandDialectSchema`,
 * `CliParameterDialectSchema`). What cannot is anything about the *public* command:
 * same-name siblings fold into one CLI command, each contributing one parameter set,
 * so agreement, spelling conflicts and set selection are family-level facts. Hidden
 * commands are not public, so they join no family; their parameters are still
 * accounted for, because a customization may build on them.
 *
 * Each command is one atomic processor and one parameter set, named by its variant.
 *
 * Selection model (Option A): an invocation keeps the sets that accept every argument
 * given, then drops sets whose mandatory arguments are missing. One survivor wins;
 * several are resolved by `defaultParameterSet` if it survives. Configured or
 * profile-supplied values never take part.
 */

import { z } from "zod";
import { commandActionKey, type IRCommand } from "./command.js";
import { cliMandatory, cliSpellings, type CliParameterDialect } from "./dialect.js";
import type { IREnum } from "./enum.js";
import { idKey } from "./id.js";
import type { IRTypeRef } from "./type-ref.js";

type CliMember = {
  index: number;
  command: IRCommand;
  /** Set name: the command's `id.variant`. */
  set: string | undefined;
  /** Flags this set accepts, lowercased. */
  accepts: Set<string>;
  /** Flags this set requires, lowercased. */
  requires: Set<string>;
};

/** Command-level fields that same-name siblings must agree on. */
const familyFields = ["aliases", "description", "examples", "preview", "deprecated", "defaultParameterSet"] as const;

/** Parameter presentation that must agree wherever one name appears in a family. */
const argumentFields = [
  "aliases",
  "deprecatedAliases",
  "group",
  "description",
  "preview",
  "deprecated",
  "validateSet",
  "completer",
] as const;

function canonical(value: unknown): string {
  return JSON.stringify(value, (_key, inner: unknown) =>
    inner !== null && typeof inner === "object" && !Array.isArray(inner)
      ? Object.fromEntries(Object.entries(inner as Record<string, unknown>).sort(([a], [b]) => a.localeCompare(b)))
      : inner,
  );
}

function isSubset(inner: Set<string>, outer: Set<string>): boolean {
  for (const value of inner) if (!outer.has(value)) return false;
  return true;
}

function union(a: Set<string>, b: Set<string>): Set<string> {
  return new Set([...a, ...b]);
}

function intersection(a: Set<string>, b: Set<string>): Set<string> {
  return new Set([...a].filter((value) => b.has(value)));
}

function setName(member: CliMember): string {
  return member.set ?? "(no variant)";
}

/** The enum behind a parameter's type, looking through arrays. */
function enumOf(type: IRTypeRef, enums: ReadonlyMap<string, IREnum>): IREnum | undefined {
  if (type.kind === "enum") return enums.get(idKey(type.id));
  if (type.kind === "array") return enumOf(type.element, enums);
  return undefined;
}

export function checkCliCommands(
  ir: { commands: readonly IRCommand[]; enums: readonly IREnum[] },
  ctx: z.RefinementCtx,
): void {
  const { commands } = ir;
  const enums = new Map(ir.enums.map((declaration) => [idKey(declaration.id), declaration]));
  const families = new Map<string, CliMember[]>();
  const invocationOwners = new Map<string, { actionKey: string; index: number; isAlias: boolean }>();

  commands.forEach((command, index) => {
    const dialect = command.dialect?.cli;
    if (dialect === undefined) return; // Not offered by the CLI.

    // ── Parameter accounting ────────────────────────────────────────────
    const accepts = new Set<string>();
    const requires = new Set<string>();
    const spellings = new Map<string, string>();
    command.parameters.forEach((parameter, parameterIndex) => {
      const cli: CliParameterDialect | undefined = parameter.dialect?.cli;
      const path = ["commands", index, "parameters", parameterIndex, "dialect", "cli"];
      if (parameter.access === "internal") {
        if (cli !== undefined && cli.hidden !== true) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `Command '${command.name}' presents internal parameter '${parameter.name}' on the CLI.`,
            path,
          });
        }
        return;
      }
      if (cli === undefined) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            `Command '${command.name}' is offered by the CLI, but parameter '${parameter.name}' has no CLI dialect. ` +
            "Give it a `name`, or keep it out of the syntax with `hidden: true`.",
          path,
        });
        return;
      }
      // A command is one processor, so its parameters can only be in its own set.
      cli.parameterSets?.forEach((membership, membershipIndex) => {
        if (membership.set !== undefined && membership.set !== command.id.variant) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message:
              `Command '${command.name}': parameter '${parameter.name}' names set '${membership.set}', ` +
              `but the command's set is its variant '${command.id.variant ?? "(none)"}'.`,
            path: [...path, "parameterSets", membershipIndex, "set"],
          });
        }
      });

      // ValidateSet narrows the shared enum; it cannot accept values the service does not know.
      const declaration = enumOf(parameter.type, enums);
      if (cli.validateSet !== undefined && declaration !== undefined) {
        const known = new Set(declaration.members.map((member) => String(member.value).toLowerCase()));
        cli.validateSet.forEach((value, valueIndex) => {
          if (!known.has(value.toLowerCase())) {
            ctx.addIssue({
              code: z.ZodIssueCode.custom,
              message: `Command '${command.name}': ValidateSet value '${value}' is not a member of the parameter's enum.`,
              path: [...path, "validateSet", valueIndex],
            });
          }
        });
      }

      if (cli.hidden === true || cli.name === undefined) return;

      const flag = cli.name.toLowerCase();
      accepts.add(flag);
      if (cliMandatory(cli, parameter.required)) requires.add(flag);
      cliSpellings(cli).forEach((spelling) => {
        const key = spelling.toLowerCase();
        const owner = spellings.get(key);
        if (owner !== undefined) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `Command '${command.name}': CLI spelling '${spelling}' is used by both '${owner}' and '${parameter.name}'.`,
            path,
          });
        }
        spellings.set(key, parameter.name);
      });
    });

    if (dialect.hidden === true) return; // Usable internally, not public.

    // ── Name ownership ──────────────────────────────────────────────────
    const actionKey = commandActionKey(command.id);
    const invocation = dialect.name;
    [invocation, ...(dialect.aliases ?? [])].forEach((spoken, position) => {
      const owner = invocationOwners.get(spoken);
      const isAlias = position > 0;
      if (owner !== undefined && (owner.actionKey !== actionKey || owner.isAlias !== isAlias)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            `CLI invocation '${spoken}' is claimed by command '${command.name}' and by command ` +
            `'${commands[owner.index]!.name}'. Only same-action siblings may share a name.`,
          path: ["commands", index, "dialect", "cli", isAlias ? "aliases" : "name"],
        });
      }
      if (owner === undefined) invocationOwners.set(spoken, { actionKey, index, isAlias });
    });

    const members = families.get(invocation) ?? [];
    members.push({ index, command, set: command.id.variant, accepts, requires });
    families.set(invocation, members);
  });

  families.forEach((members, invocation) => checkFamily(invocation, members, ctx));
}

function checkFamily(invocation: string, members: CliMember[], ctx: z.RefinementCtx): void {
  const [first] = members;
  if (first === undefined) return;
  const firstDialect = first.command.dialect!.cli!;

  // ── Command-level agreement ───────────────────────────────────────────
  members.slice(1).forEach((member) => {
    const dialect = member.command.dialect!.cli!;
    familyFields.forEach((field) => {
      if (canonical(dialect[field]) !== canonical(firstDialect[field])) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            `CLI command '${invocation}': sibling '${member.command.name}' disagrees with ` +
            `'${first.command.name}' on '${field}'. Same-name siblings form one public command.`,
          path: ["commands", member.index, "dialect", "cli", field],
        });
      }
    });
  });

  const defaultSet = firstDialect.defaultParameterSet;
  if (members.length === 1) {
    if (defaultSet !== undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `CLI command '${invocation}' has one implicit set, so it cannot name a default parameter set.`,
        path: ["commands", first.index, "dialect", "cli", "defaultParameterSet"],
      });
    }
    return;
  }

  // ── Set names ─────────────────────────────────────────────────────────
  members.forEach((member) => {
    if (member.set === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message:
          `CLI command '${invocation}' folds several commands into parameter sets named by ` +
          `id.variant, but '${member.command.name}' has no variant.`,
        path: ["commands", member.index, "id", "variant"],
      });
    }
  });
  const byName = new Map(members.map((member) => [member.set, member]));
  const fallback = defaultSet === undefined ? undefined : byName.get(defaultSet);
  if (defaultSet !== undefined && fallback === undefined) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      message: `CLI command '${invocation}' names default parameter set '${defaultSet}', which is not one of its variants.`,
      path: ["commands", first.index, "dialect", "cli", "defaultParameterSet"],
    });
  }

  // ── One flag, one argument ────────────────────────────────────────────
  // Across sets a spelling must keep meaning the same argument: same flag, same
  // presentation, same shared value type. Requiredness and binding may differ per set.
  const argumentOwners = new Map<string, string>();
  const argumentShape = new Map<string, { member: CliMember; presentation: string; type: string }>();
  members.forEach((member) => {
    member.command.parameters.forEach((parameter, parameterIndex) => {
      const cli = parameter.dialect?.cli;
      if (cli === undefined || cli.hidden === true || cli.name === undefined) return;
      const flag = cli.name.toLowerCase();
      const path = ["commands", member.index, "parameters", parameterIndex, "dialect", "cli"];

      cliSpellings(cli).forEach((spelling) => {
        const owner = argumentOwners.get(spelling.toLowerCase());
        if (owner !== undefined && owner !== flag) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `CLI command '${invocation}': spelling '${spelling}' means '--${owner}' in one set and '--${flag}' in another.`,
            path,
          });
        }
        argumentOwners.set(spelling.toLowerCase(), flag);
      });

      const presentation = canonical(argumentFields.map((field) => cli[field]));
      const type = canonical(parameter.type);
      const seen = argumentShape.get(flag);
      if (seen === undefined) {
        argumentShape.set(flag, { member, presentation, type });
        return;
      }
      if (seen.presentation !== presentation) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            `CLI command '${invocation}': '--${flag}' is presented differently in sets ` +
            `'${setName(seen.member)}' and '${setName(member)}'.`,
          path,
        });
      }
      if (seen.type !== type) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            `CLI command '${invocation}': '--${flag}' has different shared types in sets ` +
            `'${setName(seen.member)}' and '${setName(member)}'.`,
          path: ["commands", member.index, "parameters", parameterIndex, "type"],
        });
      }
    });
  });

  // ── Selection (Option A) ──────────────────────────────────────────────
  members.forEach((a, i) => {
    members.slice(i + 1).forEach((b) => {
      const shared = intersection(a.accepts, b.accepts);
      const minimal = union(a.requires, b.requires);

      // Every invocation `a` accepts is also accepted by `b` (or the reverse), so the
      // dominated set can only ever be reached as the default.
      for (const [inner, outer] of [[a, b], [b, a]] as const) {
        if (isSubset(inner.accepts, outer.accepts) && isSubset(outer.requires, inner.requires) && fallback !== inner) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message:
              `CLI command '${invocation}': set '${setName(inner)}' can never be selected, because set ` +
              `'${setName(outer)}' accepts every invocation it does. Make it the default or distinguish it.`,
            path: ["commands", inner.index, "dialect", "cli"],
          });
        }
      }

      // Invocations between `minimal` and `shared` satisfy both sets. The default
      // resolves all of them only if it accepts `shared` and requires nothing beyond
      // `minimal`.
      if (!isSubset(minimal, shared)) return;
      const resolved =
        fallback !== undefined && isSubset(shared, fallback.accepts) && isSubset(fallback.requires, minimal);
      if (!resolved) {
        const example = [...minimal].map((flag) => `--${flag}`).join(" ") || "(no arguments)";
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            `CLI command '${invocation}': sets '${setName(a)}' and '${setName(b)}' both accept ` +
            `'${example}', and no default parameter set resolves it.`,
          path: ["commands", a.index, "dialect", "cli", "defaultParameterSet"],
        });
      }
    });
  });
}
