import { z } from "zod";

export type IRConstraints = {
  minLength?: number | undefined;
  maxLength?: number | undefined;
  pattern?: string | undefined;
};

export const IRConstraintsSchema: z.ZodType<IRConstraints> = z
  .object({
    minLength: z.number().int().nonnegative().optional(),
    maxLength: z.number().int().nonnegative().optional(),
    pattern: z.string().refine((value) => value.trim().length > 0).optional(),
  })
  .strict();