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
  PSIdentityAttributeSchema,
  PSIdentityPropertyAttributeSchema,
  PSSchema,
  type ConfirmImpact,
  type LifecycleTag,
  type PSCompleterAttribute,
  type PSDefaultValueAttribute,
  type PSCommandAttribute,
  type PSParameterSetAttribute,
  type PSParameterSetOverride,
  type PSParameterAttribute,
  type PSModuleAttribute,
  type PSIdentityAttribute,
  type PSIdentityPropertyAttribute,
  type PS,
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
} from "./derive.js";

