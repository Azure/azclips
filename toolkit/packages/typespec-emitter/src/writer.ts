/**
 * Serialize IR to YAML or JSON, then write to disk via `emitFile`.
 *
 * The writer is intentionally dumb: caller is expected to have already
 * validated the IR via `parseIR`. This module only concerns itself with
 * the I/O / serialization shape.
 */

import { emitFile, resolvePath } from "@typespec/compiler";
import type { Program } from "@typespec/compiler";
// js-yaml@3 is a CommonJS module whose API hangs off the default export.
// Under nodenext ESM interop a named `{ dump }` import resolves to
// `undefined` at runtime, so we take the default and read `.dump` off it.
import jsYaml from "js-yaml";
import type { IR } from "@azure-client-engine/ir";

const yamlDump = jsYaml.dump;

export type WriteOptions = {
  outputDir: string;
  outputFile: string;
  format: "yaml" | "json";
  pretty: boolean;
};

function serialize(ir: IR, format: "yaml" | "json", pretty: boolean): string {
  if (format === "json") {
    return pretty ? JSON.stringify(ir, null, 2) : JSON.stringify(ir);
  }
  // YAML: indent 2, block style, sort keys = false (preserve declaration order).
  return yamlDump(ir, {
    indent: 2,
    noRefs: true,
    skipInvalid: false,
    lineWidth: 120,
  });
}

export async function writeIR(
  program: Program,
  ir: IR,
  opts: WriteOptions,
): Promise<string> {
  const content = serialize(ir, opts.format, opts.pretty);
  const path = resolvePath(opts.outputDir, opts.outputFile);
  await emitFile(program, { path, content });
  return path;
}
