/**
 * Authoring directives, in autorest.powershell's own `directive:` shape so a module's existing
 * block ports across unchanged. Selector values are regular expressions, matched against the
 * derived attributes -- these run *after* inference and never feed back into it.
 *
 * Scope is the command/parameter surface that maps onto `PSCommandAttribute`/`PSParameterAttribute`.
 * Deliberately absent, with usage counts from a survey of all 217 generated Azure PowerShell
 * modules, so their omission is a decision rather than an oversight:
 *
 * - `from:` + `transform:` (1158 against the spec, 97 against generated C#) -- these rewrite the
 *   *input* or patch emitted text, neither of which is an attribute override. The spec-level
 *   equivalent here is an IR transform step.
 * - `model-name`/`property-name` selectors and `set: property-name` (425/239/238) -- the model
 *   layer, not the PS attribute layer.
 * - `set: format-table`/`properties` (231/228) and the breaking-change, deprecation and preview
 *   metadata families (~350 combined) -- real, but with no home in the schema yet.
 * - `model-cmdlet` (63) -- synthesises a `New-*Object` cmdlet; belongs with handwritten
 *   customization rather than with overrides.
 */

import { z } from "zod";

import {
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  type PSCompleterAttribute,
  type PSDefaultValueAttribute,
} from "@azure-client-engine/ir";

/**
 * A regex source string. Invalid patterns are rejected rather than silently never matching.
 * Empty is allowed because real modules author it (13 selectors across the 217-module corpus,
 * on `subject`, `subject-prefix` and `variant`); like AutoRest, it constrains nothing.
 */
const PatternSchema = z.string().refine(
  (value) => {
    try {
      new RegExp(value);
      return true;
    } catch {
      return false;
    }
  },
  { message: "Selector must be a valid regular expression." },
);

/**
 * Which derived attributes a directive applies to. Every field is a regex; omitted fields match
 * anything. A directive naming `parameter-name` targets parameters, otherwise it targets commands.
 */
export type PSDirectiveSelector = {
  verb?: string | undefined;
  subject?: string | undefined;
  "subject-prefix"?: string | undefined;
  /** Parameter-set name, e.g. `ResourceGroup` for `listByResourceGroup`. */
  variant?: string | undefined;
  "parameter-name"?: string | undefined;
};

export const PSDirectiveSelectorSchema: z.ZodType<PSDirectiveSelector> = z
  .object({
    verb: PatternSchema.optional(),
    subject: PatternSchema.optional(),
    "subject-prefix": PatternSchema.optional(),
    variant: PatternSchema.optional(),
    "parameter-name": PatternSchema.optional(),
  })
  .strict()
  .refine((where) => Object.values(where).some((value) => value !== undefined), {
    message: "A directive's `where` must constrain at least one field.",
  });

/** Replacement values -- the setter surface `modifiers-v2.ts` recognises for commands and parameters. */
export type PSDirectiveSet = {
  verb?: string | undefined;
  subject?: string | undefined;
  "subject-prefix"?: string | undefined;
  variant?: string | undefined;
  "parameter-name"?: string | undefined;
  "parameter-description"?: string | undefined;
  "command-description"?: string | undefined;
  alias?: string | string[] | undefined;
  completer?: PSCompleterAttribute | undefined;
  default?: PSDefaultValueAttribute | undefined;
  "suppress-should-process"?: boolean | undefined;
  "clientside-pagination"?: boolean | undefined;
};

export const PSDirectiveSetSchema: z.ZodType<PSDirectiveSet> = z
  .object({
    verb: z.string().min(1).optional(),
    // Empty clears the value, which is how modules drop an inherited subject prefix.
    subject: z.string().optional(),
    "subject-prefix": z.string().optional(),
    variant: z.string().min(1).optional(),
    "parameter-name": z.string().min(1).optional(),
    "parameter-description": z.string().min(1).optional(),
    "command-description": z.string().min(1).optional(),
    alias: z.union([z.string().min(1), z.array(z.string().min(1)).min(1)]).optional(),
    completer: PSCompleterAttributeSchema.optional(),
    default: PSDefaultValueAttributeSchema.optional(),
    "suppress-should-process": z.boolean().optional(),
    "clientside-pagination": z.boolean().optional(),
  })
  .strict()
  .refine((set) => Object.values(set).some((value) => value !== undefined), {
    message: "A directive's `set` must assign at least one field.",
  });

export type PSDirective = {
  where: PSDirectiveSelector;
  /** Disambiguates intent when a selector could match either; AutoRest's `select:`. */
  select?: "command" | "parameter" | undefined;
  set?: PSDirectiveSet | undefined;
  /** Drop the match entirely. */
  remove?: boolean | undefined;
  /** Keep the match but exclude it from the exported surface; a handwritten cmdlet may own the name. */
  hide?: boolean | undefined;
  "clear-alias"?: boolean | undefined;
};

export const PSDirectiveSchema: z.ZodType<PSDirective> = z
  .object({
    where: PSDirectiveSelectorSchema,
    select: z.enum(["command", "parameter"]).optional(),
    set: PSDirectiveSetSchema.optional(),
    remove: z.boolean().optional(),
    hide: z.boolean().optional(),
    "clear-alias": z.boolean().optional(),
  })
  .strict()
  .refine(
    (directive) =>
      directive.set !== undefined ||
      directive.remove !== undefined ||
      directive.hide !== undefined ||
      directive["clear-alias"] !== undefined,
    { message: "A directive must carry an action: set, remove, hide or clear-alias." },
  );

export const PSDirectivesSchema = z.array(PSDirectiveSchema);
