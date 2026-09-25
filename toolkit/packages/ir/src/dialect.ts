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
import { IRIdSchema, type IRId } from "./id.js";
import {
  ConfirmImpacts,
  LifecycleTags,
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  type ConfirmImpact,
  type LifecycleTag,
  type PSCompleterAttribute,
  type PSDefaultValueAttribute,
} from "./ps.js";
import { IRTypeRefSchema, type IRTypeRef } from "./type-ref.js";

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

export type PsCommandIdentityProperty = {
  /** Property on the synthesized model used by the InputObject parameter. */
  property: IRId;
  /** Canonical path parameters this property supplies. Empty for a synthesized resource id. */
  sourceParameters: IRId[];
};

export const PsCommandIdentityPropertySchema: z.ZodType<PsCommandIdentityProperty> = z
  .object({
    property: IRIdSchema,
    sourceParameters: z.array(IRIdSchema),
  })
  .strict();

export type PsCommandIdentity = {
  /** Name of the synthetic command parameter whose model is the InputObject type. */
  parameter: string;
  /** Command-specific mapping from identity-model properties to canonical path parameters. */
  properties: PsCommandIdentityProperty[];
};

export const PsCommandIdentitySchema: z.ZodType<PsCommandIdentity> = z
  .object({
    parameter: z.string().min(1),
    properties: z.array(PsCommandIdentityPropertySchema).min(1),
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
 * `verb` and `noun` are the cmdlet's identity, so sibling commands (same
 * `id.subject` and `id.action`, different `id.variant`) normally share them and fold
 * into one cmdlet — each sibling contributing the parameter sets its own parameters
 * declare.
 */
export type PsCommandDialect = IRClientDialect & {
  /** Approved PowerShell verb (`Get`, `New`, `Set`, `Remove`, …). */
  verb: string;
  /** Cmdlet noun, prefix included (`AzElasticSan`). */
  noun: string;
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
  completer?: PSCompleterAttribute | undefined;
  examples?: string[] | undefined;
  lifecycleTag?: LifecycleTag | undefined;
  helpUri?: string | undefined;
  /** Opt in to client-side paging when the canonical operation is not pageable. */
  supportsPaging?: boolean | undefined;
  /** PowerShell output types when the cmdlet writes more than the canonical primary output. */
  outputTypes?: IRTypeRef[] | undefined;
  /** Runtime API-version metadata when the cmdlet must surface a dynamic version. */
  httpPath?: { path: string; apiVersion: string } | undefined;
  /** InputObject metadata over a synthetic model-typed command parameter. */
  identity?: PsCommandIdentity | undefined;
};

export const PsCommandDialectSchema: z.ZodType<PsCommandDialect> = z
  .object({
    ...clientDialectFields,
    verb: z.string().min(1),
    noun: z.string().min(1),
    defaultParameterSet: z.string().min(1).optional(),
    supportsShouldProcess: z.boolean().optional(),
    confirmImpact: z.enum(ConfirmImpacts).optional(),
    completer: PSCompleterAttributeSchema.optional(),
    examples: z.array(z.string().min(1)).optional(),
    lifecycleTag: z.enum(LifecycleTags).optional(),
    helpUri: z.string().min(1).optional(),
    supportsPaging: z.boolean().optional(),
    outputTypes: z.array(IRTypeRefSchema).min(1).optional(),
    httpPath: z
      .object({ path: z.string().min(1), apiVersion: z.string().min(1) })
      .strict()
      .optional(),
    identity: PsCommandIdentitySchema.optional(),
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
  /** Omit this parameter from the PowerShell surface without deleting its canonical binding. */
  removed?: boolean | undefined;
  completer?: PSCompleterAttribute | undefined;
  defaultValue?: PSDefaultValueAttribute | undefined;
};

export const PsParameterDialectSchema: z.ZodType<PsParameterDialect> = z
  .object({
    ...clientDialectFields,
    parameterSets: z.array(PsParameterSetMembershipSchema).min(1).optional(),
    removed: z.boolean().optional(),
    completer: PSCompleterAttributeSchema.optional(),
    defaultValue: PSDefaultValueAttributeSchema.optional(),
  })
  .strict()
  .superRefine((dialect, ctx) => {
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
 * A command as a CLI verb.
 *
 * Where PowerShell folds sibling variants into one cmdlet with several parameter
 * sets, the CLI generally surfaces them as one command whose flags differ — so
 * `path` is normally shared across siblings too.
 */
export type CliCommandDialect = IRClientDialect & {
  /** Invocation path, outermost first (`["elastic-san", "create"]`). */
  path: string[];
};

export const CliCommandDialectSchema: z.ZodType<CliCommandDialect> = z
  .object({
    ...clientDialectFields,
    path: z.array(z.string().min(1)).min(1),
  })
  .strict();

/** A command parameter as a CLI flag. */
export type CliParameterDialect = IRClientDialect & {
  /** Long flag, without leading dashes (`base-size-tib`). */
  flag: string;
  /** Single-character short flag, without its dash (`n`). */
  shorthand?: string | undefined;
  /** Help grouping, for commands with a large flag surface. */
  group?: string | undefined;
};

export const CliParameterDialectSchema: z.ZodType<CliParameterDialect> = z
  .object({
    ...clientDialectFields,
    flag: z.string().min(1),
    shorthand: z.string().length(1).optional(),
    group: z.string().min(1).optional(),
  })
  .strict();
