#!/usr/bin/env node
/**
 * `ace` command-line front end — command discovery + dispatch.
 *
 * A thin shell: it maps a command name to its implementation and returns a
 * process exit code. Each command's own logic (flag parsing, execution, help)
 * lives in its own module — today just `ace compile` in ./compile-command.ts.
 * Subcommands like `emit`, `check`, and `watch` slot in alongside it later.
 */

import { realpathSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { runCompile, usage, type CompileCommandOptions } from "./compile-command.js";

/**
 * Run the CLI. Returns a process exit code (0 = success) instead of calling
 * process.exit directly, so it stays testable. `options.cwd` stands in for the
 * process's current directory.
 */
export async function run(argv: string[], options: CompileCommandOptions = {}): Promise<number> {
  const [command, ...rest] = argv;

  if (!command || command === "help" || command === "--help" || command === "-h") {
    process.stdout.write(usage());
    return 0;
  }

  if (command !== "compile") {
    process.stderr.write(`Unknown command: ${command}\n`);
    return 2;
  }

  return runCompile(rest, options);
}

/**
 * True when this module is the process entrypoint (i.e. launched as the `ace`
 * bin), false when imported. Compares realpaths so it still fires when launched
 * through npm's bin shim, which passes a symlinked, non-normalized path in
 * `process.argv[1]` rather than the module's own resolved path.
 */
function isMainModule(): boolean {
  const entry = process.argv[1];
  if (!entry) return false;
  try {
    return realpathSync(entry) === fileURLToPath(import.meta.url);
  } catch {
    return false;
  }
}

// Entrypoint guard: only run when invoked as the bin, not when imported.
if (isMainModule()) {
  run(process.argv.slice(2))
    .then((code) => process.exit(code))
    .catch((err) => {
      process.stderr.write(String(err?.stack ?? err) + "\n");
      process.exit(1);
    });
}
