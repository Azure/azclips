/**
 * Where the `csharp-generate` tool is: the C# generator host dll.
 *
 * The compiler owns *that* the generator is an external subprocess speaking the
 * `csharp-generate` protocol, and drives it through `runtime.spawnStreaming`. The
 * host runtime runs the dll found here with `dotnet` (see `runtime.ts`).
 */

import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

/** Built assembly name of the generator host executable. */
const HOST_DLL = "Ace.Generator.Host.dll";

/**
 * Folder, relative to the CLI package root, where the build script stages the
 * published generator host (its dll + dependency dlls + runtime config). The CLI
 * resolves the host here at runtime and is deliberately build-config-agnostic:
 * choosing Debug/Release and gathering dependency dlls are the build script's
 * concern, not the CLI's.
 */
const HOST_DIR = "generator-host";

/**
 * Resolve the generator host dll to spawn via `dotnet`.
 *
 * The CLI looks it up at a fixed location relative to its own package —
 * `<cli>/generator-host/` — which the build script populates by publishing the
 * host + its dependencies there. This resolves identically in local dev (relative
 * to the CLI source tree) and when installed from npm (relative to the installed
 * package), and keeps runtime code free of any build-config (Debug/Release)
 * knowledge. `ACE_GENERATOR_HOST_DLL` overrides for bespoke setups.
 */
export function resolveGeneratorHost(): string {
  const override = process.env.ACE_GENERATOR_HOST_DLL;
  if (override !== undefined && override.length > 0) {
    if (!existsSync(override)) {
      throw new Error(`ACE_GENERATOR_HOST_DLL points at a missing file: ${override}`);
    }
    return override;
  }

  const dll = join(cliPackageRoot(), HOST_DIR, HOST_DLL);
  if (!existsSync(dll)) {
    throw new Error(
      `Generator host not found at '${dll}'. Stage it by running the toolkit build ` +
        "script (./build.ps1), which publishes the C# generator host next to the CLI, " +
        "or set ACE_GENERATOR_HOST_DLL to a built Ace.Generator.Host.dll.",
    );
  }
  return dll;
}

/** The CLI package root: the nearest ancestor of this module that has a package.json. */
function cliPackageRoot(): string {
  let dir = dirname(fileURLToPath(import.meta.url));
  for (;;) {
    if (existsSync(join(dir, "package.json"))) return dir;
    const parent = dirname(dir);
    if (parent === dir) {
      throw new Error(`Cannot locate the CLI package root (no package.json above '${dir}').`);
    }
    dir = parent;
  }
}