/**
 * Client dialects: how one command is *spoken* by one client.
 *
 * A command is client-neutral — a name, a parameter surface, a sequence of calls.
 * A dialect is everything a particular client needs to present it: PowerShell's
 * verb-noun and parameter sets, the CLI's command path and flags.
 *
 * ## Why this is its own contract
 *
 * The same shape is needed in three independent places, each with its own type
 * system, and each generated from this single source of truth:
 *
 *   1. **The compiler pipeline** (ir / emitter / transformers) — these Zod schemas.
 *   2. **The generator's POCOs** — via `ir.schema.json`, the same Zod → JSON Schema
 *      → C# chain that produces `Ir.Generated.cs`.
 *   3. **The generated extension at runtime** — the emitted `ExtensionIndex` feeds
 *      `CommandMetadata` / `ParameterMetadata` to `Core.ArgumentParsing`, whose
 *      providers (`PSArgumentParsingProvider`, `CLIArgumentParsingProvider`) resolve
 *      argument combinations from it.
 *
 * Keeping dialects in a file of their own is what makes (2) and (3) mechanical
 * projections rather than hand-maintained parallel definitions.
 *
 * ## Ownership
 *
 * The command transform does **not** write dialects — it knows nothing about
 * clients. Each client's own transform fills in its own member, later in the
 * sequence. A command with no dialect is complete and valid; the field is simply
 * absent.
 *
 * ## The base is an intersection, not a union
 *
 * `clientDialectFields` holds only what *every* client has. A field belongs there
 * when no client can do without it; anything one client needs goes in that client's
 * own type. Letting client-specific fields drift into the base would hand every
 * client a surface that means nothing to it.
 */

import { z } from "zod";
import { IRLiteralValueSchema, type IRLiteralValue } from "./literal.js";
import { IRTypeRefSchema, type IRTypeRef } from "./type-ref.js";
import { HttpMethods, type HttpMethod } from "./operation.js";

// ─── Shared base ────────────────────────────────────────────────────────

/** What every client dialect carries, whatever the client. */
export type IRClientDialect = {
  /** Client-facing name, when it differs from the neutral command or parameter name. */
  name?: string | undefined;
  /** Alternate names that also resolve here. */
  aliases?: string[] | undefined;
  /** Usable but not advertised in help or completion. */
  hidden?: boolean | undefined;
  /** Client-facing description, when it differs from the neutral docs. */
  description?: string | undefined;
};

/**
 * Spread into every client's dialect, the way `multipartValueFields` is spread into
 * the multipart types. Not exported as a schema on its own: a bare base is never a
 * valid dialect, only the client-specific types are.
 */
const clientDialectFields = {
  name: z.string().min(1).optional(),
  aliases: z.array(z.string().min(1)).optional(),
  hidden: z.boolean().optional(),
  description: z.string().optional(),
};

// ─── PowerShell ─────────────────────────────────────────────────────────

/** PowerShell `ConfirmImpact` level. */
export const ConfirmImpacts = ["None", "Low", "Medium", "High"] as const;
export type ConfirmImpact = (typeof ConfirmImpacts)[number];

/** Deprecation announcement emitted for a PowerShell command or parameter. */
export type PSDeprecated = {
  message: string;
};

export const PSDeprecatedSchema: z.ZodType<PSDeprecated> = z
  .object({ message: z.string().min(1) })
  .strict();

/** Preview announcement emitted for a PowerShell command or parameter. */
export type PSPreview = {
  message: string;
  estimatedGaDate?: string | undefined;
};

export const PSPreviewSchema: z.ZodType<PSPreview> = z
  .object({
    message: z.string().min(1),
    estimatedGaDate: z.string().min(1).optional(),
  })
  .strict();

/** PowerShell metadata shared by every command emitted into one module. */
export type PsModuleDialect = {
  name: string;
  description?: string | undefined;
  /** Module-owned noun prefix, for example `Az`. */
  prefix?: string | undefined;
  /** Service/subject prefix appended after `prefix`, for example `ApplicationInsights`. */
  subjectPrefix?: string | undefined;
};

export const PsModuleDialectSchema: z.ZodType<PsModuleDialect> = z
  .object({
    name: z.string().min(1),
    description: z.string().optional(),
    prefix: z.string().min(1).optional(),
    subjectPrefix: z.string().min(1).optional(),
  })
  .strict();

/**
 * A command as a PowerShell cmdlet.
 *
 * `verb` and `subject` are the cmdlet's identity, so sibling commands (same
 * `id.subject` and `id.action`, different `id.variant`) normally share them and fold
 * into one cmdlet — each sibling contributing the parameter sets its own parameters
 * declare.
 */
export type PsCommandDialect = IRClientDialect & {
  /** Approved PowerShell verb (`Get`, `New`, `Set`, `Remove`, …). */
  verb: string;
  /** Cmdlet subject, prefix included (`AzElasticSan`). */
  subject: string;
  /**
   * Set chosen when an invocation does not identify one. A family-level fact, so
   * sibling commands folded into one cmdlet must agree on it — nothing in this
   * schema can check that, since it spans commands.
   *
   * Note this is the *dialect's* notion of a set, not necessarily a literal
   * PowerShell parameter set; how it maps to `[Parameter(ParameterSetName = …)]` is
   * the PS transform's business.
   *
   * Which command the extension index dispatches to is **not** here: that is
   * `IRCommandId.variant`, which is client-neutral and already distinguishes
   * siblings.
   */
  defaultParameterSet?: string | undefined;
  /** Emit `SupportsShouldProcess` (`-WhatIf` / `-Confirm`). */
  supportsShouldProcess?: boolean | undefined;
  confirmImpact?: ConfirmImpact | undefined;
  examples?: string[] | undefined;
  deprecated?: PSDeprecated | undefined;
  preview?: PSPreview | undefined;
  helpUri?: string | undefined;
  /** PowerShell output types when the cmdlet writes more than the canonical primary output. */
  outputTypes?: IRTypeRef[] | undefined;
  /** Runtime API-version metadata when the cmdlet must surface a dynamic version. */
  httpPath?: { path: string; apiVersion: string } | undefined;
};

export const PsCommandDialectSchema: z.ZodType<PsCommandDialect> = z
  .object({
    ...clientDialectFields,
    verb: z.string().min(1),
    subject: z.string().min(1),
    defaultParameterSet: z.string().min(1).optional(),
    supportsShouldProcess: z.boolean().optional(),
    confirmImpact: z.enum(ConfirmImpacts).optional(),
    examples: z.array(z.string().min(1)).optional(),
    deprecated: PSDeprecatedSchema.optional(),
    preview: PSPreviewSchema.optional(),
    helpUri: z.string().url().optional(),
    outputTypes: z.array(IRTypeRefSchema).min(1).optional(),
    httpPath: z
      .object({ path: z.string().min(1), apiVersion: z.string().min(1) })
      .strict()
      .optional(),
  })
  .strict();

/**
 * A parameter's membership in one parameter set.
 *
 * Mirrors a single `[Parameter(...)]` attribute, so **every field here is per-set**:
 * a parameter can be mandatory in one set and optional in another, or positional in
 * only one. A parameter carries one of these per set it belongs to, which is how a
 * property ends up with several `[Parameter]` attributes.
 */
export type PsParameterSetMembership = {
  /** Set name. Absent means every set — PowerShell's `__AllParameterSets`. */
  set?: string | undefined;
  /**
   * Whether *this set* requires a value. May differ from the command-neutral
   * `IRCommandParameter.required`, which is why it lives here and not there.
   */
  mandatory: boolean;
  /** Positional index within this set, when the value may be supplied unnamed. */
  position?: number | undefined;
  /** Bind from the pipeline object itself. */
  valueFromPipeline?: boolean | undefined;
  /** Bind from a property of the pipeline object with the same name. */
  valueFromPipelineByPropertyName?: boolean | undefined;
};

export const PsParameterSetMembershipSchema: z.ZodType<PsParameterSetMembership> = z
  .object({
    set: z.string().min(1).optional(),
    mandatory: z.boolean(),
    position: z.number().int().min(0).optional(),
    valueFromPipeline: z.boolean().optional(),
    valueFromPipelineByPropertyName: z.boolean().optional(),
  })
  .strict();

/** A command parameter as a cmdlet parameter. */
export type PsParameterDialect = IRClientDialect & {
  /**
   * One entry per set this parameter belongs to. Absent means it belongs to every
   * set, taking its requiredness from `IRCommandParameter.required` — so a dialect
   * that only renames a parameter need not restate its membership.
   */
  parameterSets?: PsParameterSetMembership[] | undefined;
  /** Fixed completion values offered for this parameter. */
  completer?: IRLiteralValue[] | undefined;
  /** PowerShell-specific default value. */
  defaultValue?: IRLiteralValue | undefined;
  /** Closed set of valid string values, projected from a closed enum. */
  validateSet?: string[] | undefined;
  deprecated?: PSDeprecated | undefined;
  preview?: PSPreview | undefined;
};

export const PsParameterDialectSchema: z.ZodType<PsParameterDialect> = z
  .object({
    ...clientDialectFields,
    parameterSets: z.array(PsParameterSetMembershipSchema).min(1).optional(),
    completer: z.array(IRLiteralValueSchema).min(1).optional(),
    defaultValue: IRLiteralValueSchema.optional(),
    validateSet: z.array(z.string().min(1)).min(1).optional(),
    deprecated: PSDeprecatedSchema.optional(),
    preview: PSPreviewSchema.optional(),
  })
  .strict()
  .superRefine((dialect, ctx) => {
    const completer = dialect.completer;
    if (completer !== undefined) {
      const values = new Set<string>();
      completer.forEach((value, index) => {
        const key = JSON.stringify(value);
        if (values.has(key)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `Completer value ${key} is declared more than once.`,
            path: ["completer", index],
          });
        }
        values.add(key);
      });
    }

    const validateSet = dialect.validateSet;
    if (validateSet !== undefined) {
      const values = new Set<string>();
      validateSet.forEach((value, index) => {
        if (values.has(value)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `ValidateSet value '${value}' is declared more than once.`,
            path: ["validateSet", index],
          });
        }
        values.add(value);
      });
    }

    const memberships = dialect.parameterSets;
    if (memberships === undefined) return;

    const named = new Set<string>();
    let hasFallback = false;
    memberships.forEach((membership, index) => {
      if (membership.set === undefined) {
        if (hasFallback) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: "A parameter can declare at most one unnamed fallback membership.",
            path: ["parameterSets", index],
          });
        }
        hasFallback = true;
        return;
      }
      if (named.has(membership.set)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Parameter set '${membership.set}' is declared more than once.`,
          path: ["parameterSets", index, "set"],
        });
      }
      named.add(membership.set);
    });
  });

// ─── CLI ────────────────────────────────────────────────────────────────

/**
 * Stored CLI spellings are bare: no leading dashes, matching the runtime's parameter
 * and alias dictionaries. A command-name token or long option is lowercase kebab-case;
 * a short option is a single ASCII letter or digit.
 */
export const CLI_TOKEN_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
export const CLI_SHORT_OPTION_PATTERN = /^[A-Za-z0-9]$/;
/** A command name: tokens separated by single spaces, without the executable. */
export const CLI_COMMAND_NAME_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*(?: [a-z0-9]+(?:-[a-z0-9]+)*)*$/;

/**
 * Preview marker. Field names follow `PSPreview` so the two can later move into a
 * shared parent; `message` is optional here because the CLI runtime can print a
 * standard notice. GA is the default when neither `preview` nor `deprecated` is set.
 */
export type CliPreview = {
  message?: string | undefined;
  /** Expected GA date, ISO 8601 (`2027-03-31`). */
  estimatedGaDate?: string | undefined;
};

export const CliPreviewSchema: z.ZodType<CliPreview> = z
  .object({
    message: z.string().min(1).optional(),
    estimatedGaDate: z.string().date().optional(),
  })
  .strict();

/**
 * Deprecation marker. A superset of `PSDeprecated`: Azure CLI deprecations also
 * name a replacement and when the item goes away. Hiding a deprecated item is the
 * existing `hidden` field.
 */
export type CliDeprecated = {
  message?: string | undefined;
  /** Replacement command name or long option, bare. */
  replacement?: string | undefined;
  /** ISO 8601 date after which the item may be removed. */
  removalDate?: string | undefined;
  /** Release in which the item is removed. */
  removalVersion?: string | undefined;
};

export const CliDeprecatedSchema: z.ZodType<CliDeprecated> = z
  .object({
    message: z.string().min(1).optional(),
    replacement: z.string().min(1).optional(),
    removalDate: z.string().date().optional(),
    removalVersion: z.string().min(1).optional(),
  })
  .strict();

/**
 * An old spelling that keeps working while users move to the parameter's `name`, as
 * Azure CLI's `c.deprecate(target='--old-name', redirect='--name')` inside an
 * `options_list`. The replacement is always the parameter's `name`; the other fields
 * follow `CliDeprecated`.
 */
export type CliDeprecatedAlias = {
  /** The old spelling, bare, under the same rules as `aliases`. */
  name: string;
  /** Keep the old spelling out of help and completion; it still parses. */
  hidden?: boolean | undefined;
  message?: string | undefined;
  /** ISO 8601 date after which the spelling may be removed. */
  removalDate?: string | undefined;
  /** Release in which the spelling is removed. */
  removalVersion?: string | undefined;
};

export const CliDeprecatedAliasSchema: z.ZodType<CliDeprecatedAlias> = z
  .object({
    name: z.string().min(1),
    hidden: z.boolean().optional(),
    message: z.string().min(1).optional(),
    removalDate: z.string().date().optional(),
    removalVersion: z.string().min(1).optional(),
  })
  .strict();

/**
 * One source API a command calls. Split into the same parts `IROperation` stores, so
 * the transformer can copy them from `steps[].operation`; a handwritten customization
 * writes them itself. A `getPut` command or an orchestrator lists several. Same concept
 * as PowerShell's `httpPath` (AutoRest's `[HttpPath]`); proposed as one shared field.
 */
export type ApiReference = {
  method: HttpMethod;
  path: string;
  apiVersion: string;
};

export const ApiReferenceSchema: z.ZodType<ApiReference> = z
  .object({
    method: z.enum(HttpMethods),
    path: z.string().min(1),
    apiVersion: z.string().min(1),
  })
  .strict();

/**
 * One command-line example. Structured because the help consumer receives
 * `"title | command"` strings and splits them at the first pipe, so a shell pipe in an
 * untitled raw string would be misread as a title separator. Regular code generation
 * leaves examples empty; a build-time step fills them.
 */
export type CliExample = {
  /** Short description of what the example does. Must not contain `|`. */
  title?: string | undefined;
  /** The full command line. May contain shell pipes. */
  command: string;
};

export const CliExampleSchema: z.ZodType<CliExample> = z
  .object({
    title: z
      .string()
      .min(1)
      .refine((title) => !title.includes("|"), {
        message: "An example title cannot contain `|`; help splits title and command at the first pipe.",
      })
      .optional(),
    command: z.string().min(1),
  })
  .strict();

/**
 * A command as a CLI command.
 *
 * Every command is one atomic processor, and its `id.variant` is its parameter set.
 * Sibling commands (same `commandActionKey`) with the same `name` form one public
 * command, one set per sibling. Siblings may also take different names, as
 * scope-specific deployment commands do.
 *
 * Two ways not to be offered:
 *   - no `dialect.cli` at all: the CLI has nothing to do with the command;
 *   - `hidden: true`: the CLI metadata exists and stays usable internally, for example
 *     by a customization wrapping the processor, but users cannot invoke it.
 */
export type CliCommandDialect = IRClientDialect & {
  /** Identifier and invocation: `elastic-san list`, without the executable. */
  name: string;
  /**
   * Set chosen when selection cannot decide between candidate sets. A family-level
   * fact: same-name siblings must agree on it, and it must name one sibling's
   * `id.variant`. Checked across commands, in `checkCliCommands`.
   */
  defaultParameterSet?: string | undefined;
  /** CLI command-line examples, not API request samples. */
  examples?: CliExample[] | undefined;
  preview?: CliPreview | undefined;
  deprecated?: CliDeprecated | undefined;
  /** The source API(s) behind this command: API → IR operation → IR command → dialect. */
  api?: ApiReference[] | undefined;
};

export const CliCommandDialectSchema: z.ZodType<CliCommandDialect> = z
  .object({
    ...clientDialectFields,
    name: z
      .string()
      .regex(CLI_COMMAND_NAME_PATTERN, "A CLI command name is lowercase kebab-case tokens separated by single spaces."),
    defaultParameterSet: z.string().min(1).optional(),
    examples: z.array(CliExampleSchema).min(1).optional(),
    preview: CliPreviewSchema.optional(),
    deprecated: CliDeprecatedSchema.optional(),
    api: z.array(ApiReferenceSchema).min(1).optional(),
  })
  .strict()
  .superRefine((dialect, ctx) => {
    // Aliases are alternate complete names, space-joined like `CommandMetadata.Aliases`.
    const seen = new Set<string>([dialect.name]);
    dialect.aliases?.forEach((alias, index) => {
      if (!CLI_COMMAND_NAME_PATTERN.test(alias)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `CLI command alias '${alias}' must be lowercase kebab-case tokens separated by single spaces.`,
          path: ["aliases", index],
        });
      }
      if (seen.has(alias)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `CLI command alias '${alias}' repeats the name or another alias.`,
          path: ["aliases", index],
        });
      }
      seen.add(alias);
    });
  });

/** Command name tokens, outermost first. */
export function cliCommandTokens(dialect: CliCommandDialect): string[] {
  return dialect.name.split(" ");
}

/**
 * A parameter's membership in its command's parameter set — the CLI subset of
 * `PsParameterSetMembership`. PowerShell's `position`, `valueFromPipeline` and
 * `valueFromPipelineByPropertyName` have no CLI meaning: generated commands take no
 * positionals and the CLI has no object pipeline.
 */
export type CliParameterSetMembership = {
  /** The command's `id.variant`. Absent means the command's own (only) set. */
  set?: string | undefined;
  /** Whether this set requires a value. Overrides `IRCommandParameter.required`. */
  mandatory: boolean;
};

export const CliParameterSetMembershipSchema: z.ZodType<CliParameterSetMembership> = z
  .object({
    set: z.string().min(1).optional(),
    mandatory: z.boolean(),
  })
  .strict();

/**
 * A command parameter as a CLI argument.
 *
 * Every non-internal parameter of an exposed command carries one: either presented
 * through `name`, or kept out of the syntax with `hidden`. Omitting it is a validation
 * error, because silently dropping a parameter changes what the command does.
 */
export type CliParameterDialect = IRClientDialect & {
  /**
   * Membership in the command's set. A generated command is one processor, so at most
   * one entry, naming the command's own variant. Absent means a member, with shared
   * `IRCommandParameter.required` as its requiredness. Extra sets declared by
   * customizations live on their C# attributes and are merged at build time.
   */
  parameterSets?: CliParameterSetMembership[] | undefined;
  /**
   * Help section only, never parameter-set membership: arguments from different groups
   * are combined in one invocation. Transformer default, as in AAZ: the name of the body
   * object that was flattened into this flag (`source.via`), e.g. `Properties`, `Sku`.
   * Stored bare, as legacy `arg_group` is; the generator appends ` Arguments` for
   * `[ParameterGroup]`. `Global` is reserved for the global arguments.
   */
  group?: string | undefined;
  /**
   * Values the CLI accepts, nothing else: a closed enum, projected by the transformer
   * from the shared enum as PowerShell's is. Never an open enum's members, which the
   * service may extend; those go in `completer`.
   */
  validateSet?: string[] | undefined;
  /**
   * Values offered for completion without restricting input: an open enum's known
   * members. Same shape as PowerShell's `completer`. The alternative to `validateSet`.
   */
  completer?: IRLiteralValue[] | undefined;
  preview?: CliPreview | undefined;
  deprecated?: CliDeprecated | undefined;
  /** Old spellings that still parse but are deprecated in favour of `name`. */
  deprecatedAliases?: CliDeprecatedAlias[] | undefined;
};
// Inherited: `name` is the canonical long option, required unless `hidden`; `aliases`
// are extra spellings, one character meaning a short option; `hidden` keeps the
// parameter out of the CLI syntax while its shared binding stays; `description`.

export const CliParameterDialectSchema: z.ZodType<CliParameterDialect> = z
  .object({
    ...clientDialectFields,
    parameterSets: z.array(CliParameterSetMembershipSchema).min(1).max(1).optional(),
    group: z
      .string()
      .min(1)
      .refine((group) => group === group.trim(), { message: "A CLI group has no leading or trailing spaces." })
      .refine((group) => !/(^|\s)arguments$/i.test(group), {
        message: "A CLI group is stored bare (`Properties`); the generator appends ` Arguments`.",
      })
      .refine((group) => group.toLowerCase() !== "global", {
        message: "The CLI group `Global` is reserved for the global arguments.",
      })
      .optional(),
    validateSet: z.array(z.string().min(1)).min(1).optional(),
    completer: z.array(IRLiteralValueSchema).min(1).optional(),
    preview: CliPreviewSchema.optional(),
    deprecated: CliDeprecatedSchema.optional(),
    deprecatedAliases: z.array(CliDeprecatedAliasSchema).min(1).optional(),
  })
  .strict()
  .superRefine((dialect, ctx) => {
    if (dialect.validateSet !== undefined && dialect.completer !== undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message:
          "A CLI parameter has either a ValidateSet (closed values) or a completer (suggestions), not both; " +
          "a ValidateSet already completes.",
        path: ["completer"],
      });
    }

    if (dialect.hidden === true) {
      dialect.parameterSets?.forEach((membership, index) => {
        if (membership.mandatory) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: "A hidden CLI parameter cannot be mandatory: the CLI offers no way to supply it.",
            path: ["parameterSets", index, "mandatory"],
          });
        }
      });
    } else if (dialect.name === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "An exposed CLI parameter needs a `name`; use `hidden: true` to keep it out of the syntax.",
        path: ["name"],
      });
    }

    if (dialect.deprecatedAliases !== undefined && (dialect.hidden === true || dialect.name === undefined)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Deprecated aliases redirect to the parameter's `name`, so a hidden or unnamed CLI parameter cannot have them.",
        path: ["deprecatedAliases"],
      });
    }

    const values = new Set<string>();
    dialect.validateSet?.forEach((value, index) => {
      if (values.has(value.toLowerCase())) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `ValidateSet value '${value}' is declared more than once; the CLI compares values case-insensitively.`,
          path: ["validateSet", index],
        });
      }
      values.add(value.toLowerCase());
    });

    if (dialect.name === undefined) return;
    if (!CLI_TOKEN_PATTERN.test(dialect.name) || dialect.name.length < 2) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `CLI parameter name '${dialect.name}' must be bare lowercase kebab-case, at least two characters.`,
        path: ["name"],
      });
    }

    // One character means a short option; anything longer is a long option.
    const seen = new Set<string>([dialect.name.toLowerCase()]);
    const checkAlias = (alias: string, path: (string | number)[]): void => {
      const valid = alias.length === 1 ? CLI_SHORT_OPTION_PATTERN.test(alias) : CLI_TOKEN_PATTERN.test(alias);
      if (!valid) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `CLI alias '${alias}' must be bare: one letter or digit, or lowercase kebab-case.`,
          path,
        });
      }
      // Case-insensitive on purpose: conservatively forbids pairs such as `g` and `G`.
      if (seen.has(alias.toLowerCase())) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `CLI alias '${alias}' repeats another spelling of the same parameter.`,
          path,
        });
      }
      seen.add(alias.toLowerCase());
    };
    dialect.aliases?.forEach((alias, index) => checkAlias(alias, ["aliases", index]));
    dialect.deprecatedAliases?.forEach((alias, index) => checkAlias(alias.name, ["deprecatedAliases", index, "name"]));
  });

/**
 * Every spelling an exposed CLI parameter answers to, bare, deprecated ones included.
 * Empty when hidden.
 */
export function cliSpellings(dialect: CliParameterDialect): string[] {
  if (dialect.hidden === true || dialect.name === undefined) return [];
  return [dialect.name, ...(dialect.aliases ?? []), ...(dialect.deprecatedAliases ?? []).map((alias) => alias.name)];
}

/** Whether the CLI requires this parameter in its command's set. */
export function cliMandatory(dialect: CliParameterDialect, sharedRequired: boolean): boolean {
  return dialect.parameterSets?.[0]?.mandatory ?? sharedRequired;
}
