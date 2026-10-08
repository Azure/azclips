/**
 * Public surface of @azure-client-engine/ir.
 *
 * Re-exports schemas, type aliases, and the small set of helpers needed
 * by emitters, transforms, and generators. Anything not re-exported here
 * is considered package-internal.
 */

export { IR_SCHEMA_VERSION } from "./version.js";

export {
  IRIdSchema,
  serializeId,
  idKey,
  idEquals,
  type IRId,
} from "./id.js";

export {
  PrimitiveKinds,
  FormatKinds,
  AccessKinds,
  UsageKinds,
  VisibilityKinds,
  type PrimitiveKind,
  type FormatKind,
  type IRAccess,
  type IRUsage,
} from "./primitives.js";

export {
  IRLiteralValueSchema,
  type IRLiteralValue,
} from "./literal.js";

export {
  IRTypeRefSchema,
  IRTypeRefPrimitiveSchema,
  IRTypeRefModelSchema,
  IRTypeRefEnumSchema,
  IRTypeRefArraySchema,
  IRTypeRefDictionarySchema,
  IRTypeRefUnionSchema,
  IRTypeRefDiscriminatedUnionSchema,
  IRTypeRefObjectSchema,
  type IRTypeRef,
  type IRTypeRefPrimitive,
  type IRTypeRefModel,
  type IRTypeRefEnum,
  type IRTypeRefArray,
  type IRTypeRefDictionary,
  type IRTypeRefUnion,
  type IRTypeRefDiscriminatedUnion,
} from "./type-ref.js";

export {
  IRPropertyRefSchema,
  type IRPropertyRef,
} from "./property-ref.js";

export {
  IRDiscriminatedUnionSchema,
  IRDiscriminatedUnionVariantSchema,
  type IRDiscriminatedUnion,
  type IRDiscriminatedUnionVariant,
} from "./discriminated-union.js";

export {
  FlattenHintSchema,
  FlattenedMemberSchema,
  FlattenedParameterSchema,
  SerializationHintSchema,
  ClientApiHintSchema,
  DocsHintSchema,
  CommonHintsSchema,
  ObjectPropertyHintsSchema,
  ValuePropertyHintsSchema,
  CollectionPropertyHintsSchema,
  ModelHintsSchema,
  EnumHintsSchema,
  ParameterHintsSchema,
  OperationHintsSchema,
  RequestBodyHintsSchema,
  type FlattenHint,
  type FlattenedMember,
  type FlattenedParameter,
  type SerializationHint,
  type ClientApiHint,
  type DocsHint,
  type CommonHints,
  type ObjectPropertyHints,
  type ValuePropertyHints,
  type CollectionPropertyHints,
  type ModelHints,
  type EnumHints,
  type ParameterHints,
  type OperationHints,
  type RequestBodyHints,
} from "./hints.js";

export {
  IREnumSchema,
  IREnumMemberSchema,
  type IREnum,
  type IREnumMember,
} from "./enum.js";

export {
  IRModelSchema,
  IRPolymorphismSchema,
  IRPropertySchema,
  IRVisibilitySchema,
  type IRModel,
  type IRPolymorphism,
  type IRProperty,
  type IRPropertyPrimitive,
  type IRPropertyObject,
  type IRPropertyCollection,
  type IRPropertyEnum,
  type IRPropertyUnion,
  type IRVisibility,
} from "./model.js";

export {
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
  IROperationSemanticsSchema,
  HttpMethods,
  CollectionFormats,
  type IROperation,
  type IRParameter,
  type IRMultipartValue,
  type IRMultipartHeader,
  type IRMultipartPartBody,
  type IRMultipartPart,
  type IRMultipartBody,
  type IRRequestBody,
  type IRResponse,
  type IRResponseHeader,
  type IRStatusCode,
  type IRApiVersionStamp,
  type IRPagingSegment,
  type IRPagingBinding,
  type IRPagingProperty,
  type IRPageableSemantics,
  type IRLongRunningSemantics,
  type IROperationSemantics,
  type HttpMethod,
  type CollectionFormat,
} from "./operation.js";

export {
  IRServerSchema,
  IRServerParameterSchema,
  IRServerDefinitionSchema,
  type IRServer,
  type IRServerParameter,
  type IRServerDefinition,
} from "./server.js";

export {
  IRSourceSchema,
  type IRSource,
} from "./source.js";

export {
  IRAuthSchema,
  IRAuthOptionSchema,
  IRAuthSchemeSchema,
  IROAuth2FlowSchema,
  IROAuth2ScopeSchema,
  type IRAuth,
  type IRAuthOption,
  type IRAuthScheme,
  type IROAuth2Flow,
  type IROAuth2Scope,
} from "./auth.js";

export {
  IRSchema,
  TransformedIRSchema,
  parseIR,
  safeParseIR,
  parseTransformedIR,
  safeParseTransformedIR,
  emptyIR,
  type IR,
  type TransformedIR,
} from "./ir.js";

export {
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
  CommandTemplates,
  commandKey,
  commandActionKey,
  type IRCommand,
  type IRCommandId,
  type IRCommandParameter,
  type IRCommandParameterSource,
  type IRCommandRequest,
  type IRCommandRequestBody,
  type IRCommandResponse,
  type IRCommandStep,
  type IRCommandSurface,
  type IRCommandPagination,
  type IRCommandTemplate,
  type IRCommandUriSegment,
  type IRCommandValue,
  type IRCommandWireEntry,
  type IRCommandDialect,
  type IRCommandParameterDialect,
} from "./command.js";

export {
  PsModuleDialectSchema,
  PsCommandDialectSchema,
  PsParameterDialectSchema,
  PsParameterSetMembershipSchema,
  ConfirmImpacts,
  PSDeprecatedSchema,
  PSPreviewSchema,
  CliCommandDialectSchema,
  CliParameterDialectSchema,
  type IRClientDialect,
  type PsModuleDialect,
  type PsCommandDialect,
  type PsParameterDialect,
  type PsParameterSetMembership,
  type ConfirmImpact,
  type PSDeprecated,
  type PSPreview,
  type CliCommandDialect,
  type CliParameterDialect,
} from "./dialect.js";

export {
  IRTargetSchema,
  IRTargetDirectoriesSchema,
  type IRTarget,
  type IRTargetDirectories,
} from "./target.js";

export {
  indexIR,
  type IRIndex,
} from "./lookup.js";
