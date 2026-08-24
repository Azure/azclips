/**
 * Public surface of @azure-client-engine/ps-commands.
 */

export {
  PsParameterSetSchema,
  PsCommandSchema,
  PsModuleSchema,
  type PsParameterSet,
  type PsCommand,
  type PsModule,
} from "./schema.js";

export {
  derivePsCommands,
  deriveModules,
  resolveParameterHintForSet,
  type ResolvedParameterHint,
} from "./derive.js";
