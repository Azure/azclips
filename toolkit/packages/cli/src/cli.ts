#!/usr/bin/env node
/**
 * `cirrus` command-line front end.
 *
 * This is a THIN shell over @cirrus/compiler: it parses argv, invokes the
 * backbone, then maps the structured result onto stdout/stderr and a process
 * exit code. All pipeline logic lives in the backbone so the future web app
 * can reuse it unchanged.
 *
 * Skeleton: only `cirrus compile <spec>` is sketched. Subcommands like
 * `emit`, `check`, and `watch` slot in alongside it later.
 */

import { compile, type CompilerDiagnostic } from "@cirrus/compiler";

/** Render a diagnostic for the terminal. */
function formatDiagnostic(d: CompilerDiagnostic): string {
  const loc = d.target?.file
    ? `${d.target.file}:${d.target.line ?? 0}:${d.target.column ?? 0} `
    : "";
  return `${d.severity}: ${loc}${d.code}: ${d.message}`;
}

/**
 * Run the CLI. Returns a process exit code (0 = success) instead of calling
 * process.exit directly, so it stays testable.
 */
export async function run(argv: string[]): Promise<number> {
  const [command, ...rest] = argv;

  if (!command || command === "help" || command === "--help" || command === "-h") {
    process.stdout.write(
      [
        "cirrus — TypeSpec -> C# extension compiler",
        "",
        "Usage:",
        "  cirrus compile <spec.tsp> [--out <dir>] [--source-id <id>]",
        "",
      ].join("\n"),
    );
    return 0;
  }

  if (command !== "compile") {
    process.stderr.write(`Unknown command: ${command}\n`);
    return 2;
  }

  const entrypoint = rest.find((a) => !a.startsWith("-"));
  if (!entrypoint) {
    process.stderr.write("compile: missing <spec.tsp> argument\n");
    return 2;
  }

  const result = await compile({ entrypoint });

  for (const d of result.diagnostics) {
    const sink = d.severity === "error" ? process.stderr : process.stdout;
    sink.write(formatDiagnostic(d) + "\n");
  }

  // A real implementation writes result.files to the output dir here.
  return result.success ? 0 : 1;
}

// Entrypoint guard: only run when invoked as the bin, not when imported.
if (process.argv[1] && import.meta.url === new URL(`file://${process.argv[1]}`).href) {
  run(process.argv.slice(2))
    .then((code) => process.exit(code))
    .catch((err) => {
      process.stderr.write(String(err?.stack ?? err) + "\n");
      process.exit(1);
    });
}
