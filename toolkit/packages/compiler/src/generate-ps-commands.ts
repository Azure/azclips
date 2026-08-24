/**
 * `generate-ps-commands`: the placeholder for the eventual real PowerShell
 * artifact generator (Cmdlet + Processor classes; `ExtensionIndex` PS-only
 * registration is a separate, unresolved prerequisite — see design.md's
 * Open Questions). Kept under its final registry id/factory name now so
 * there's no rename/re-export churn once it grows real C# output.
 *
 * Today it only calls `derivePsCommands(ir)`/`deriveModules(ir)`/
 * `deriveFormatViews(ir)` and serializes the result as JSON (`noopGenerator`
 * remains the default `Generator`), so a real `ace compile` invocation has a
 * concrete way to see `PsCommand[]`/`PsModule[]`/`PsFormatView[]` output,
 * without a C# back end to consume it yet.
 *
 * All three derive functions are called here, inside `generate()`, rather
 * than as a separate pipeline stage — see design.md Decision 8: a
 * `Generator` is the one stage in `compile()` not required to return `IR`,
 * since nothing runs after it.
 */

import type { Generator, GeneratorContext, GeneratorOutput } from "./generate.js";
import type { IR } from "@azure-client-engine/ir";
import { derivePsCommands, deriveModules, deriveFormatViews } from "@azure-client-engine/ps-commands";

/** Factory for `CompilerRegistry.generators`, registered under id `"generate-ps-commands"`. */
export function createGeneratePsCommandsFactory(_options: Record<string, unknown> = {}): Generator {
  return {
    name: "generate-ps-commands",
    generate(ir: IR, _context: GeneratorContext): GeneratorOutput {
      const commands = derivePsCommands(ir);
      const modules = deriveModules(ir);
      const formatViews = deriveFormatViews(ir);
      return {
        files: new Map([
          ["ps-commands.json", JSON.stringify({ commands, modules, formatViews }, null, 2) + "\n"],
        ]),
      };
    },
  };
}
