/**
 * Public surface of @azure-client-engine/transformer: IR -> IR transforms, one per client
 * surface. Each derives its own property on the IR document (`IR.ps`, and later `IR.cli`); the
 * attribute schemas themselves live in @azure-client-engine/ir.
 */

export { resolveVerb } from "./ps/verb-mapping.js";

export {
  deriveAttributes,
  type DeriveAttributesOptions,
} from "./ps/derive.js";

