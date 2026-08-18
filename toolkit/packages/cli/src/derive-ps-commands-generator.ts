/**
 * `derive-ps-commands`: a debug/preview `Generator` — NOT the real Roslyn/C#
 * generator (`noopGenerator` remains the default; see proposal.md's Scope
 * note). Its only job is calling `derivePsCommands(ir)` and serializing the
 * result as JSON, so a real `ace compile` invocation has a concrete way to
 * see `PsCommand[]` output today, without a C# back end to consume it yet.
 *
 * `derivePsCommands` is called here, inside `generate()`, rather than as a
 * separate pipeline stage — see design.md Decision 8: a `Generator` is the
 * one stage in `compile()` not required to return `IR`, since nothing runs
 * after it.
 */

import type { Generator, GeneratorContext, GeneratorOutput } from "@azure-client-engine/compiler";
import type { IR } from "@azure-client-engine/ir";
import { derivePsCommands } from "@azure-client-engine/ps-commands";

/** Factory for `CompilerRegistry.generators`, registered under id `"derive-ps-commands"`. */
export function createDerivePsCommandsGeneratorFactory(_options: Record<string, unknown> = {}): Generator {
  return {
    name: "derive-ps-commands",
    generate(ir: IR, _context: GeneratorContext): GeneratorOutput {
      const commands = derivePsCommands(ir);
      return {
        files: new Map([["ps-commands.json", JSON.stringify(commands, null, 2) + "\n"]]),
      };
    },
  };
}
