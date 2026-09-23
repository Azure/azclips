/** PowerShell-specific metadata shared by command and parameter dialects. */

import { z } from "zod";
import {
  PSCompleterContractSchema,
  PSDefaultValueContractSchema,
} from "./ps-contract.js";

export {
  ConfirmImpacts,
  LifecycleTags,
  type ConfirmImpact,
  type LifecycleTag,
} from "./ps-contract.js";

export type PSCompleterAttribute = z.infer<typeof PSCompleterContractSchema>;
export const PSCompleterAttributeSchema: z.ZodType<PSCompleterAttribute> = PSCompleterContractSchema;

export type PSDefaultValueAttribute = z.infer<typeof PSDefaultValueContractSchema>;
export const PSDefaultValueAttributeSchema: z.ZodType<PSDefaultValueAttribute> = PSDefaultValueContractSchema;
