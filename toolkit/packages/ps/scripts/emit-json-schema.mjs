// Emit `ps.schema.json` from the Zod PS attribute schema.
//
// Mirrors @azure-client-engine/ir's scripts/emit-json-schema.mjs: the Zod schema
// in `src/` is the single source of truth, and this derives a JSON Schema for
// non-TypeScript consumers (a future PS/C# generator stage). The emitted
// `ps.schema.json` is a BUILD ARTIFACT, not source — it is not committed.
//
// Usage:
//   node scripts/emit-json-schema.mjs [--output-dir <dir>] [--check]
//     --output-dir <dir>  Directory to write (or, with --check, read)
//                         `ps.schema.json`. Defaults to this package's `dist/`.
//     --check             Verify the file already at <output-dir> matches the
//                         current Zod schema; exit non-zero if stale/missing.
//                         Writes nothing. Intended for CI.

import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import { zodToJsonSchema } from "zod-to-json-schema";

import { IRIdSchema, IR_SCHEMA_VERSION } from "@azure-client-engine/ir";

import {
  PS_SCHEMA_VERSION,
  PSSchema,
  PSCommandAttributeSchema,
  PSParameterSetAttributeSchema,
  PSParameterAttributeSchema,
  PSModuleAttributeSchema,
  PSCompleterAttributeSchema,
  PSDefaultValueAttributeSchema,
  PSParameterSetOverrideSchema,
} from "../dist/src/index.js";

const here = dirname(fileURLToPath(import.meta.url));
const packageRoot = join(here, "..");

const { values } = parseArgs({
  options: {
    "output-dir": { type: "string" },
    check: { type: "boolean" },
  },
  strict: false,
  allowPositionals: true,
});

const outputDir =
  values["output-dir"] !== undefined
    ? resolve(process.cwd(), String(values["output-dir"]))
    : join(packageRoot, "dist");
const outFile = join(outputDir, "ps.schema.json");

/** Named definitions → stable `#/definitions/<name>` refs. `IRId` is registered so
 * the (self-recursive, via `typeArgs`) by-id references resolve to a clean shared
 * ref instead of an inline duplicate with a fragile internal-path `$ref`. */
const definitions = {
  IRId: IRIdSchema,
  PSCommandAttribute: PSCommandAttributeSchema,
  PSParameterSetAttribute: PSParameterSetAttributeSchema,
  PSParameterAttribute: PSParameterAttributeSchema,
  PSModuleAttribute: PSModuleAttributeSchema,
  PSCompleterAttribute: PSCompleterAttributeSchema,
  PSDefaultValueAttribute: PSDefaultValueAttributeSchema,
  PSParameterSetOverride: PSParameterSetOverrideSchema,
};

const jsonSchema = zodToJsonSchema(PSSchema, {
  target: "jsonSchema7",
  $refStrategy: "root",
  definitions,
});

// `IRId` is defined by @azure-client-engine/ir and only mirrored here so this schema
// stays self-contained. Type-generating consumers must bind it to their existing IR
// type rather than emit a duplicate, so mark ownership as structured data they can
// key off deterministically (unknown `x-` keywords are ignored by validators).
jsonSchema.definitions.IRId["x-defined-by"] = {
  package: "@azure-client-engine/ir",
  schema: `https://azure-client-engine.dev/ir/${IR_SCHEMA_VERSION}/ir.schema.json`,
  pointer: "#/definitions/IRId",
};

jsonSchema.$id = `https://azure-client-engine.dev/ps/${PS_SCHEMA_VERSION}/ps.schema.json`;
jsonSchema.title = "Azure Client Engine PS Attribute Schema";
jsonSchema.description = `PowerShell command/parameter/module attribute schema for the Azure Client Engine (schemaVersion ${PS_SCHEMA_VERSION}). Generated from the Zod source of truth in @azure-client-engine/ps; do not edit by hand.`;

const serialized = JSON.stringify(jsonSchema, null, 2) + "\n";

if (values.check === true) {
  const current = existsSync(outFile) ? readFileSync(outFile, "utf8") : "";
  if (current !== serialized) {
    process.stderr.write(
      `ps.schema.json at '${outFile}' is missing or out of date with the Zod PS schema.\n` +
        "Regenerate it, e.g. `npm run schema -w @azure-client-engine/ps -- --output-dir <dir>`.\n",
    );
    process.exit(1);
  }
  process.stdout.write(`ps.schema.json at '${outFile}' is up to date.\n`);
} else {
  mkdirSync(outputDir, { recursive: true });
  writeFileSync(outFile, serialized, "utf8");
  process.stdout.write(`Wrote ${outFile} (schemaVersion ${PS_SCHEMA_VERSION}).\n`);
}
