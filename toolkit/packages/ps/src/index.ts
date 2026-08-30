/**
 * Public surface of @azure-client-engine/ps.
 */

export { PS_SCHEMA_VERSION } from "./version.js";

export {
  ConfirmImpacts,
  LifecycleTags,
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  PSCommandAttributeSchema,
  PSParameterSetAttributeSchema,
  PSParameterSetOverrideSchema,
  PSParameterAttributeSchema,
  PSModuleAttributeSchema,
  PSSchemaRootSchema,
  type ConfirmImpact,
  type LifecycleTag,
  type PSCompleterAttribute,
  type PSDefaultValueAttribute,
  type PSCommandAttribute,
  type PSParameterSetAttribute,
  type PSParameterSetOverride,
  type PSParameterAttribute,
  type PSModuleAttribute,
  type PSSchemaRoot,
} from "./attributes.js";

export { resolveVerb } from "./verb-mapping.js";

export {
  PSDirectiveSchema,
  PSDirectiveSelectorSchema,
  PSDirectiveSetSchema,
  PSDirectivesSchema,
  type PSDirective,
  type PSDirectiveSelector,
  type PSDirectiveSet,
} from "./directives.js";

export {
  deriveAttributes,
  type DeriveAttributesOptions,
  type DerivedAttributes,
} from "./derive.js";

