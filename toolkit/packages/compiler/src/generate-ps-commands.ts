/**
 * Placeholder for the eventual real PowerShell generator (Cmdlet + Processor classes).
 * Kept under its final registry id/factory name now to avoid rename churn later. Today
 * it just calls `derivePsCommands(ir)`/`deriveModules(ir)` and writes the result as JSON,
 * so `ace compile` has a concrete way to see `PsCommand[]`/`PsModule[]` output. Called
 * here inside `generate()` rather than as a pipeline stage, since `Generator` is the one
 * `compile()` stage not required to return `IR` (design.md Decision 8).
 */

import type { Generator, GeneratorContext, GeneratorOutput } from "./generate.js";
import type { IR } from "@azure-client-engine/ir";
import { derivePsCommands, deriveModules } from "@azure-client-engine/ps-commands";

/** Factory for `CompilerRegistry.generators`, registered under id `"generate-ps-commands"`. */
export function createGeneratePsCommandsFactory(_options: Record<string, unknown> = {}): Generator {
  return {
    name: "generate-ps-commands",
    generate(ir: IR, _context: GeneratorContext): GeneratorOutput {
      const commands = derivePsCommands(ir);
      const modules = deriveModules(ir, commands);
      return {
        files: new Map([
          ["ps-commands.json", JSON.stringify({ commands, modules }, null, 2) + "\n"],
        ]),
      };
    },
  };
}
