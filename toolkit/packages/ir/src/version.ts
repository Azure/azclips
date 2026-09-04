/**
 * IR schema version. Bump on any breaking change to the IR shape.
 *
 * Producers stamp this onto the root IR; consumers verify their accepted range.
 * Mismatch should be a hard error at the pipeline/generator boundary.
 */
export const IR_SCHEMA_VERSION = "0.6.0" as const;
