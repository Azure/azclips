import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { safeParseIR, safeParseTransformedIR } from "@azure-client-engine/ir";

function fail(code, message) {
  process.stderr.write(`deployment-audit: ${message}\n`);
  process.stdout.write(JSON.stringify({
    schemaVersion: "1.0.0", status: "unsupported", exitCode: 1,
    diagnostics: [{ code, path: "$", message }],
  }) + "\n");
  process.exitCode = 1;
}

async function main() {
  const args = process.argv.slice(2);
  const index = args.indexOf("--ir");
  if (index < 0 || args.lastIndexOf("--ir") !== index || !args[index + 1] || args[index + 1].startsWith("--")) {
    fail("ir-path-required", "Specify exactly one --ir <canonical IR file>.");
    return;
  }
  const input = await readFile(args[index + 1], "utf8");
  const value = JSON.parse(input);
  const result = typeof value === "object" && value !== null && "commands" in value
    ? safeParseTransformedIR(value) : safeParseIR(value);
  if (!result.success) {
    fail("ir-invalid", JSON.stringify(result.error.issues));
    return;
  }
  args.splice(index, 2);
  const dll = resolve(dirname(fileURLToPath(import.meta.url)), "bin", "Debug", "net10.0", "Ace.DeploymentAudit.dll");
  const child = spawnSync("dotnet", [dll, ...args], { input, encoding: "utf8", maxBuffer: 32 * 1024 * 1024 });
  if (child.error) { fail("audit-tool-failed", child.error.message); return; }
  if (child.status === null) { fail("audit-tool-interrupted", `Audit process interrupted (${child.signal}).`); return; }
  process.stdout.write(child.stdout);
  process.stderr.write(child.stderr);
  process.exitCode = child.status;
}

await main().catch((error) => {
  if (error instanceof SyntaxError || ["ENOENT", "EACCES", "EISDIR"].includes(error.code)) {
    fail("ir-input-unreadable", error.message);
  } else {
    throw error;
  }
});
