import { existsSync } from "node:fs";
import { createRequire } from "node:module";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import { buildRoot, verifySource } from "./prepare.mjs";

verifySource();
const entry = join(buildRoot, "dist", "src", "index.js");
if (!existsSync(entry)) throw new Error("Pinned upstream build missing; run npm run setup.");
const require = createRequire(import.meta.url);
export const compiler = await import(pathToFileURL(require.resolve("@typespec/compiler")).href);
export const bicepTypes = await import(pathToFileURL(require.resolve("@azure/bicep-types")).href);
const upstreamRequire = createRequire(join(buildRoot, "package.json"));
export const emitter = await import(pathToFileURL(upstreamRequire.resolve("@azure-tools/typespec-bicep")).href);

// This validates version labels; callers still must supply a selected-version program.
export function getProviderDefinitionsChecked(context, apiVersion) {
  if (typeof apiVersion !== "string" || !apiVersion.trim() ||
      apiVersion !== apiVersion.trim() || ["latest", "all", "default"].includes(apiVersion.toLowerCase())) {
    throw new Error("An explicit concrete API version is required.");
  }
  const definitions = emitter.getProviderDefinitions(context);
  if (definitions.length === 0) throw new Error("Upstream discovered no ARM resource definitions.");
  for (const definition of definitions) {
    if (definition.apiVersion !== apiVersion) {
      throw new Error(`Upstream selected '${definition.apiVersion}', not requested '${apiVersion}'; supply a verified selected-version program.`);
    }
  }
  return definitions;
}
