/**
 * Public surface of @azure-client-engine/ps: the transform that derives the PowerShell surface
 * (`IR.ps`) from an IR document. The attribute schema itself lives in @azure-client-engine/ir.
 */

export { resolveVerb } from "./verb-mapping.js";

export {
  deriveAttributes,
  type DeriveAttributesOptions,
} from "./derive.js";

