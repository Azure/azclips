import { z } from "zod";

export type IRLengthConstraints = {
  minLength?: number | undefined;
  maxLength?: number | undefined;
};

export const IRLengthConstraintsSchema: z.ZodType<IRLengthConstraints> = z
  .object({
    minLength: z.number().int().nonnegative().optional(),
    maxLength: z.number().int().nonnegative().optional(),
  })
  .strict();