/**
 * Emitter options.
 *
 * The shape is duplicated as a JSON Schema in `lib.ts` so the TypeSpec
 * compiler can validate `tspconfig.yaml` settings before our `$onEmit`
 * runs. Keep both in sync.
 */

export type EmitterOutputFormat = "yaml" | "json";

/**
 * The emitter's library name (`$lib.name`): the prefix of its diagnostic codes, and
 * the key TypeSpec files its options under. It lives here, apart from `lib.ts`, so
 * the `tsp-emit` worker can read it without loading `@typespec/compiler`.
 */
export const EMITTER_NAME = "@azure-client-engine/typespec-emitter";

export interface EmitterOptions {
  /** Output file name (relative to emitterOutputDir). Default `ir.yaml`. */
  outputFile?: string;
  /** Serialization format. Default `yaml`. */
  format?: EmitterOutputFormat;
  /** Pretty-print JSON output. Ignored for YAML. Default `true`. */
  pretty?: boolean;
  /** Emitter name to register with TCGC's `createSdkContext`. */
  emitterName?: string;
  /** Override the IRSource.id (and the IRId.module string for types declared by this spec). */
  sourceId?: string;
  /**
   * API version selector read by TCGC (`createSdkContext` → `context.options`).
   * A specific version string, `"latest"`, or `"all"`. Kebab-cased because TCGC
   * reads the literal `"api-version"` key. The emitter itself does not consume
   * it — declaring it here lets it pass the emitter's option-schema validation.
   */
  "api-version"?: string;
  /** If true, any warning-severity diagnostic causes a non-zero exit code. Default `false`. */
  diagnosticsAsErrors?: boolean;
}

export const EMITTER_OPTION_DEFAULTS = {
  outputFile: "ir.yaml",
  format: "yaml" as EmitterOutputFormat,
  pretty: true,
  emitterName: "@azure-client-engine/typespec-emitter",
  diagnosticsAsErrors: false,
} as const;

/**
 * Resolve user-provided options against defaults.
 *
 * `sourceId` and `api-version` are excluded from the required set: they are
 * optional pass-throughs the emitter never reads from here (`api-version` flows
 * through the raw `context.options` to TCGC's `createSdkContext`).
 */
export function resolveOptions(opts: EmitterOptions | undefined): Required<Omit<EmitterOptions, "sourceId" | "api-version">> & { sourceId?: string } {
  const o = opts ?? {};
  return {
    outputFile: o.outputFile ?? EMITTER_OPTION_DEFAULTS.outputFile,
    format: o.format ?? EMITTER_OPTION_DEFAULTS.format,
    pretty: o.pretty ?? EMITTER_OPTION_DEFAULTS.pretty,
    emitterName: o.emitterName ?? EMITTER_OPTION_DEFAULTS.emitterName,
    diagnosticsAsErrors: o.diagnosticsAsErrors ?? EMITTER_OPTION_DEFAULTS.diagnosticsAsErrors,
    ...(o.sourceId !== undefined ? { sourceId: o.sourceId } : {}),
  };
}
