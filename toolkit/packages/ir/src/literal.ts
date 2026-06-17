/**
 * Literal value carrier. Used for default values, discriminator values,
 * enum members, and parameter defaults.
 *
 * Wire-typed: the shape is what the value looks like on the JSON wire,
 * not what the C# representation will be. Format conversions (e.g. a
 * date-time default carried as ISO-8601 string) are the generator's job.
 */

import { z } from "zod";

export type IRLiteralValue =
  | { kind: "string"; value: string }
  | { kind: "boolean"; value: boolean }
  | { kind: "number"; value: number }
  // bigint literals (for int64 etc) carried as decimal strings to avoid JSON precision loss
  | { kind: "bigint"; value: string }
  | { kind: "null" };

export const IRLiteralValueSchema: z.ZodType<IRLiteralValue> = z.discriminatedUnion(
  "kind",
  [
    z.object({ kind: z.literal("string"), value: z.string() }).strict(),
    z.object({ kind: z.literal("boolean"), value: z.boolean() }).strict(),
    z.object({ kind: z.literal("number"), value: z.number() }).strict(),
    z
      .object({
        kind: z.literal("bigint"),
        value: z.string().regex(/^-?\d+$/, "bigint literal must be a decimal integer string"),
      })
      .strict(),
    z.object({ kind: z.literal("null") }).strict(),
  ],
);
