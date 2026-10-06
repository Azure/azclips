// Emit `ir.schema.json` from the Zod IR schema.
//
// The Zod schema in `src/` is the single source of truth for the IR. This
// script derives a JSON Schema from it so non-TypeScript consumers (notably the
// C# generator) can codegen their own types instead of hand-duplicating the
// definition. The emitted `ir.schema.json` is a BUILD ARTIFACT, not source — it
// is not committed; point consumers at a generated copy.
//
// Usage:
//   node scripts/emit-json-schema.mjs [--output-dir <dir>] [--check]
//     --output-dir <dir>  Directory to write (or, with --check, read)
//                         `ir.schema.json`. Relative paths resolve against the
//                         current working directory. Defaults to this package's
//                         `dist/` (a gitignored artifact location).
//     --check             Verify the file already at <output-dir> matches the
//                         current Zod schema; exit non-zero if stale/missing.
//                         Writes nothing. Intended for CI.
//
// Cross-field refinements (`.superRefine`, e.g. pageable "exactly one style",
// LRO `custom` requires `customPollingUrlPath`) cannot be expressed in JSON
// Schema and are intentionally NOT carried across — authoritative validation
// stays in the Zod emitter; the JSON Schema is a structural contract only.

import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import { zodToJsonSchema } from "zod-to-json-schema";

import {
  IR_SCHEMA_VERSION,
  // The generator only ever reads a *transformed* document — commands and a
  // resolved target, not bare operations — so that is the root, and the C# root
  // type is named `TransformedIR` to say so. `IR`'s own types come along as
  // shared definitions.
  TransformedIRSchema,
  // PowerShell metadata definitions consumed by command dialect generation.
  PSDeprecatedSchema,
  PSPreviewSchema,
  // Named nodes registered as shared `$defs` so generated C# gets stable,
  // meaningful type names (IRId, IRModel, ...) instead of anonymous ones.
  // Registering the structurally-recursive types (IRId, IRTypeRef, IRServer)
  // also means their cycles resolve to clean named `$ref`s.
  IRIdSchema,
  IRLiteralValueSchema,
  IRTypeRefSchema,
  // IRTypeRef variant schemas — registered so a narrowed type-ref field (e.g.
  // IRProperty.type) resolves to a clean named `$ref` (and, in C#, to a derived
  // type of IRTypeRef) instead of an inline duplicate object. Note: the
  // model|dict `IRTypeRefObject` union is deliberately NOT registered — its
  // members already derive from IRTypeRef, so a consumer maps that field to the
  // IRTypeRef base rather than a second, conflicting hierarchy.
  IRTypeRefPrimitiveSchema,
  IRTypeRefModelSchema,
  IRTypeRefEnumSchema,
  IRTypeRefArraySchema,
  IRTypeRefDictionarySchema,
  IRTypeRefUnionSchema,
  IRTypeRefDiscriminatedUnionSchema,
  // A property reached through its containers — shared by pagination, and named
  // so generated C# gets one reference type rather than an inline object per use.
  IRPropertyRefSchema,
  IRDiscriminatedUnionSchema,
  IRDiscriminatedUnionVariantSchema,
  IREnumSchema,
  IREnumMemberSchema,
  IRModelSchema,
  IRPropertySchema,
  IRVisibilitySchema,
  IRPolymorphismSchema,
  IROperationSchema,
  IRParameterSchema,
  IRMultipartValueSchema,
  IRMultipartHeaderSchema,
  IRMultipartPartBodySchema,
  IRMultipartPartSchema,
  IRMultipartBodySchema,
  IRRequestBodySchema,
  IRResponseSchema,
  IRResponseHeaderSchema,
  IRStatusCodeSchema,
  IRApiVersionStampSchema,
  IRPagingSegmentSchema,
  IRPagingBindingSchema,
  IRPagingPropertySchema,
  IRPageableSemanticsSchema,
  IRLongRunningSemanticsSchema,
  IRServerSchema,
  IRServerDefinitionSchema,
  IRServerParameterSchema,
  IROperationSemanticsSchema,
  IRSourceSchema,
  IRAuthSchema,
  IRAuthSchemeSchema,
  IROAuth2FlowSchema,
  IROAuth2ScopeSchema,
  // Reusable hint building blocks — registered so they become shared C# types
  // rather than being duplicated inline at every use site.
  FlattenHintSchema,
  FlattenedMemberSchema,
  FlattenedParameterSchema,
  SerializationHintSchema,
  ClientApiHintSchema,
  DocsHintSchema,
  ObjectPropertyHintsSchema,
  ValuePropertyHintsSchema,
  CollectionPropertyHintsSchema,
  ModelHintsSchema,
  EnumHintsSchema,
  ParameterHintsSchema,
  OperationHintsSchema,
  RequestBodyHintsSchema,
  // Commands — the transformer↔generator contract. Registered so the generated C#
  // gets named types rather than anonymous inline objects.
  IRCommandSchema,
  IRCommandIdSchema,
  IRCommandParameterSchema,
  IRCommandParameterSourceSchema,
  IRCommandRequestSchema,
  IRCommandRequestBodySchema,
  IRCommandResponseSchema,
  IRCommandStepSchema,
  IRCommandSurfaceSchema,
  IRCommandPaginationSchema,
  IRCommandUriSegmentSchema,
  IRCommandValueSchema,
  IRCommandWireEntrySchema,
  IRCommandDialectSchema,
  IRCommandParameterDialectSchema,
  // Per-client dialects.
  PsCommandDialectSchema,
  PsParameterDialectSchema,
  PsParameterSetMembershipSchema,
  CliCommandDialectSchema,
  CliParameterDialectSchema,
  // Resolved placement for the generated project.
  IRTargetSchema,
  IRTargetDirectoriesSchema,
} from "../dist/src/index.js";

const here = dirname(fileURLToPath(import.meta.url));
const packageRoot = join(here, "..");

// Parse args. `strict: false` keeps the script resilient to any extra tokens npm
// forwards; only `--output-dir` and `--check` are meaningful.
const { values } = parseArgs({
  options: {
    "output-dir": { type: "string" },
    check: { type: "boolean" },
  },
  strict: false,
  allowPositionals: true,
});

const outputDir =
  values["output-dir"] !== undefined
    ? resolve(process.cwd(), String(values["output-dir"]))
    : join(packageRoot, "dist"); // default: gitignored artifact location
const outFile = join(outputDir, "ir.schema.json");

/** Named definitions → stable `#/definitions/<name>` refs (clean C# class names). */
const definitions = {
  IRId: IRIdSchema,
  IRLiteralValue: IRLiteralValueSchema,
  IRTypeRef: IRTypeRefSchema,
  // IRTypeRef variants — registered so narrowed type-ref fields (IRProperty.type,
  // ...) resolve to `$ref`s into the single IRTypeRef hierarchy instead of
  // inline duplicates.
  IRTypeRefPrimitive: IRTypeRefPrimitiveSchema,
  IRTypeRefModel: IRTypeRefModelSchema,
  IRTypeRefEnum: IRTypeRefEnumSchema,
  IRTypeRefArray: IRTypeRefArraySchema,
  IRTypeRefDictionary: IRTypeRefDictionarySchema,
  IRTypeRefUnion: IRTypeRefUnionSchema,
  IRTypeRefDiscriminatedUnion: IRTypeRefDiscriminatedUnionSchema,
  IRPropertyRef: IRPropertyRefSchema,
  IRDiscriminatedUnion: IRDiscriminatedUnionSchema,
  IRDiscriminatedUnionVariant: IRDiscriminatedUnionVariantSchema,
  IREnum: IREnumSchema,
  IREnumMember: IREnumMemberSchema,
  IRModel: IRModelSchema,
  IRProperty: IRPropertySchema,
  IRVisibility: IRVisibilitySchema,
  IRPolymorphism: IRPolymorphismSchema,
  IROperation: IROperationSchema,
  IRParameter: IRParameterSchema,
  IRMultipartValue: IRMultipartValueSchema,
  IRMultipartHeader: IRMultipartHeaderSchema,
  IRMultipartPartBody: IRMultipartPartBodySchema,
  IRMultipartPart: IRMultipartPartSchema,
  IRMultipartBody: IRMultipartBodySchema,
  IRRequestBody: IRRequestBodySchema,
  IRResponse: IRResponseSchema,
  IRResponseHeader: IRResponseHeaderSchema,
  IRStatusCode: IRStatusCodeSchema,
  IRApiVersionStamp: IRApiVersionStampSchema,
  IRPagingSegment: IRPagingSegmentSchema,
  IRPagingBinding: IRPagingBindingSchema,
  IRPagingProperty: IRPagingPropertySchema,
  IRPageableSemantics: IRPageableSemanticsSchema,
  IRLongRunningSemantics: IRLongRunningSemanticsSchema,
  IRServer: IRServerSchema,
  IRServerDefinition: IRServerDefinitionSchema,
  IRServerParameter: IRServerParameterSchema,
  IROperationSemantics: IROperationSemanticsSchema,
  IRSource: IRSourceSchema,
  IRAuth: IRAuthSchema,
  IRAuthScheme: IRAuthSchemeSchema,
  IROAuth2Flow: IROAuth2FlowSchema,
  IROAuth2Scope: IROAuth2ScopeSchema,
  // Hint building blocks (shared across nodes).
  FlattenHint: FlattenHintSchema,
  FlattenedMember: FlattenedMemberSchema,
  FlattenedParameter: FlattenedParameterSchema,
  SerializationHint: SerializationHintSchema,
  ClientApiHint: ClientApiHintSchema,
  DocsHint: DocsHintSchema,
  ObjectPropertyHints: ObjectPropertyHintsSchema,
  ValuePropertyHints: ValuePropertyHintsSchema,
  CollectionPropertyHints: CollectionPropertyHintsSchema,
  ModelHints: ModelHintsSchema,
  EnumHints: EnumHintsSchema,
  ParameterHints: ParameterHintsSchema,
  OperationHints: OperationHintsSchema,
  RequestBodyHints: RequestBodyHintsSchema,
  // Commands.
  IRCommand: IRCommandSchema,
  IRCommandId: IRCommandIdSchema,
  IRCommandParameter: IRCommandParameterSchema,
  IRCommandParameterSource: IRCommandParameterSourceSchema,
  IRCommandRequest: IRCommandRequestSchema,
  IRCommandRequestBody: IRCommandRequestBodySchema,
  IRCommandResponse: IRCommandResponseSchema,
  IRCommandStep: IRCommandStepSchema,
  IRCommandSurface: IRCommandSurfaceSchema,
  IRCommandPagination: IRCommandPaginationSchema,
  IRCommandUriSegment: IRCommandUriSegmentSchema,
  IRCommandValue: IRCommandValueSchema,
  IRCommandWireEntry: IRCommandWireEntrySchema,
  IRCommandDialect: IRCommandDialectSchema,
  IRCommandParameterDialect: IRCommandParameterDialectSchema,
  // Dialects. Each client's type is structurally distinct, which matters:
  // zodToJsonSchema collapses identical shapes into one definition.
  PsCommandDialect: PsCommandDialectSchema,
  PsParameterDialect: PsParameterDialectSchema,
  PsParameterSetMembership: PsParameterSetMembershipSchema,
  CliCommandDialect: CliCommandDialectSchema,
  CliParameterDialect: CliParameterDialectSchema,
  // Placement.
  IRTarget: IRTargetSchema,
  IRTargetDirectories: IRTargetDirectoriesSchema,
  // PowerShell metadata shared by dialects.
  PSDeprecated: PSDeprecatedSchema,
  PSPreview: PSPreviewSchema,
};

const jsonSchema = zodToJsonSchema(TransformedIRSchema, {
  target: "jsonSchema7",
  // `root` emits absolute `#/definitions/<Name>` refs and never throws or drops
  // recursive types to `any` (which `none` does here, defeated by the
  // superRefine wrapper on IROperationSemantics). Registering every node +
  // reusable hint/oauth type as a `definition` means the only remaining
  // "deep-pointer" refs are trivial shared primitive leaves (e.g. a property's
  // `name`/`optional`), which resolve to inline primitives and are consumed
  // fine by C# codegen.
  $refStrategy: "root",
  definitions,
});

// Stamp identity + version so consumers can gate on the IR schema version.
jsonSchema.$id = `https://azure-client-engine.dev/ir/${IR_SCHEMA_VERSION}/ir.schema.json`;
jsonSchema.title = "Azure Client Engine transformed IR";
jsonSchema.description = `Transformed Intermediate Representation for the Azure Client Engine compiler (schemaVersion ${IR_SCHEMA_VERSION}) — the IR plus the commands and resolved target a generator consumes. Generated from the Zod source of truth in @azure-client-engine/ir; do not edit by hand.`;

const serialized = JSON.stringify(jsonSchema, null, 2) + "\n";

if (values.check === true) {
  const current = existsSync(outFile) ? readFileSync(outFile, "utf8") : "";
  if (current !== serialized) {
    process.stderr.write(
      `ir.schema.json at '${outFile}' is missing or out of date with the Zod IR schema.\n` +
        "Regenerate it, e.g. `npm run schema -w @azure-client-engine/ir -- --output-dir <dir>`.\n",
    );
    process.exit(1);
  }
  process.stdout.write(`ir.schema.json at '${outFile}' is up to date.\n`);
} else {
  mkdirSync(outputDir, { recursive: true });
  writeFileSync(outFile, serialized, "utf8");
  process.stdout.write(`Wrote ${outFile} (schemaVersion ${IR_SCHEMA_VERSION}).\n`);
}
