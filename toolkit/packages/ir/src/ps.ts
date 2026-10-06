/** PowerShell-specific metadata shared by command and parameter dialects. */

import { z } from "zod";

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
