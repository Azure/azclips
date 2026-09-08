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
  idEquals,
  type IRId,
} from "./id.js";

export {
  PrimitiveKinds,
  FormatKinds,
  AccessKinds,
  UsageKinds,
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
  IRTypeRefObjectSchema,
  type IRTypeRef,
  type IRTypeRefPrimitive,
  type IRTypeRefModel,
  type IRTypeRefEnum,
  type IRTypeRefArray,
  type IRTypeRefDictionary,
  type IRTypeRefUnion,
} from "./type-ref.js";

export {
  FlattenHintSchema,
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
  IRRequestBodySchema,
  IRResponseSchema,
  IRResponseHeaderSchema,
  IRStatusCodeSchema,
  IRApiVersionStampSchema,
  IRPageableSemanticsSchema,
  IRLongRunningSemanticsSchema,
  IROperationSemanticsSchema,
  HttpMethods,
  CollectionFormats,
  type IROperation,
  type IRParameter,
  type IRRequestBody,
  type IRResponse,
  type IRResponseHeader,
  type IRStatusCode,
  type IRApiVersionStamp,
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
  parseIR,
  safeParseIR,
  emptyIR,
  type IR,
} from "./ir.js";
