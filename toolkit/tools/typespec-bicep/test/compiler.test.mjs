import assert from "node:assert/strict";
import { copyFileSync, mkdtempSync, openSync, closeSync, rmSync, writeSync } from "node:fs";
import { dirname, join } from "node:path";
import { test } from "node:test";
import { compilerPath, verifyAssociation, verifyCompiler } from "../compiler.mjs";

test("the pinned compiler uses the exact March schema and warning-only cases fail qualification", async () => {
  const report = await verifyAssociation();
  assert.equal(report.status, "verified-selected-resource-association");
  assert.deepEqual(report.positive.diagnostics, []);
  assert.equal(report.positive.resources, 1);
  assert.deepEqual(report.negatives.map(({ exitCode, diagnostics }) => ({
    exitCode, codes: diagnostics.map(({ code }) => code),
  })), [
    { exitCode: 0, codes: ["BCP081"] },
    { exitCode: 0, codes: ["BCP036"] },
    { exitCode: 0, codes: ["BCP037"] },
  ]);
});

test("a same-size modified or missing executable fails before invocation instead of using PATH", async () => {
  const scratch = mkdtempSync(join(dirname(compilerPath), "tamper-test-"));
  try {
    await assert.rejects(verifyCompiler(join(scratch, "missing.exe")), /Pinned Bicep CLI missing/);
    const executable = join(scratch, "modified.exe");
    copyFileSync(compilerPath, executable);
    const handle = openSync(executable, "r+");
    try {
      writeSync(handle, Buffer.from([0]), 0, 1, 0);
    } finally {
      closeSync(handle);
    }
    await assert.rejects(verifyCompiler(executable), /digest mismatch; refusing to execute/);
  } finally {
    rmSync(scratch, { recursive: true });
  }
});
