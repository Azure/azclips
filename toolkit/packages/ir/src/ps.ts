/** PowerShell-specific metadata shared by command and parameter dialects. */

import { z } from "zod";

/** PowerShell `ConfirmImpact` level. */
export const ConfirmImpacts = ["None", "Low", "Medium", "High"] as const;
export type ConfirmImpact = (typeof ConfirmImpacts)[number];

/** Lifecycle tag emitted through `ProcessorInfoAttribute`. */
export const LifecycleTags = ["Preview", "Deprecated", "Experimental", "GA"] as const;
export type LifecycleTag = (typeof LifecycleTags)[number];

/** Mirrors AutoRest's `CompleterInfoAttribute`. */
export type PSCompleterAttribute = {
  script?: string | undefined;
  name?: string | undefined;
  description?: string | undefined;
};

export const PSCompleterAttributeSchema: z.ZodType<PSCompleterAttribute> = z
  .object({
    script: z.string().min(1).optional(),
    name: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
  })
  .strict()
  .refine((hint) => hint.script !== undefined || hint.name !== undefined || hint.description !== undefined, {
    message: "PSCompleterAttribute requires at least one of script/name/description.",
  });

/** Mirrors AutoRest's `DefaultInfoAttribute`. */
export type PSDefaultValueAttribute = {
  script: string;
  name?: string | undefined;
  description?: string | undefined;
  setCondition?: string | undefined;
};

export const PSDefaultValueAttributeSchema: z.ZodType<PSDefaultValueAttribute> = z
  .object({
    script: z.string().min(1),
    name: z.string().min(1).optional(),
    description: z.string().min(1).optional(),
    setCondition: z.string().min(1).optional(),
  })
  .strict();
