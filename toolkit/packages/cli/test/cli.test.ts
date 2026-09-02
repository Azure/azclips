/**
 * `ace` CLI tests.
 *
 * Exercises the `run()` contract at the argv/exit-code boundary: flag parsing
 * for `ace compile --tsp-location <path> [--config <path>]`, the `--config`
 * JSON loading rules, and one happy-path compile through the real backbone
 * against the committed `packages/ir/examples/pets.tsp`.
 *
 * stdout/stderr are captured (not forwarded) so assertions can inspect the
 * emitted text without polluting the test reporter.
 */

import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { transformSteps } from "@azure-client-engine/transformer";

import { run } from "../src/index.js";
import { createFsSink } from "../src/cli.js";

const PETS_TSP = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "ir",
  "examples",
  "pets.tsp",
);

/** Run the CLI with stdout/stderr captured instead of printed. */
async function captureRun(argv: string[]): Promise<{ code: number; out: string; err: string }> {
  const out: string[] = [];
  const err: string[] = [];
  const push = (bucket: string[]) =>
    ((chunk: string | Uint8Array): boolean => {
      bucket.push(typeof chunk === "string" ? chunk : Buffer.from(chunk).toString("utf8"));
      return true;
    }) as typeof process.stdout.write;
  const outSpy = vi.spyOn(process.stdout, "write").mockImplementation(push(out));
  const errSpy = vi.spyOn(process.stderr, "write").mockImplementation(push(err));
  try {
    const code = await run(argv);
    return { code, out: out.join(""), err: err.join("") };
  } finally {
    outSpy.mockRestore();
    errSpy.mockRestore();
  }
}

let dir: string;

beforeAll(() => {
  dir = mkdtempSync(join(tmpdir(), "ace-cli-"));
  writeFileSync(join(dir, "good.json"), JSON.stringify({ marker: "hello" }), "utf8");
  writeFileSync(join(dir, "array.json"), "[1, 2, 3]", "utf8");
  writeFileSync(join(dir, "bad.json"), "{ not json", "utf8");
  // Valid JSON prefixed with a UTF-8 BOM (as Windows editors often produce).
  writeFileSync(join(dir, "bom.json"), "\uFEFF" + JSON.stringify({ marker: "bom" }), "utf8");
});

afterAll(() => {
  rmSync(dir, { recursive: true, force: true });
});

describe("ace: dispatch", () => {
  it("prints usage and exits 0 with no args", async () => {
    const { code, out } = await captureRun([]);
    expect(code).toBe(0);
    expect(out).toContain("ace compile --tsp-location <path>");
  });

  it("prints usage for `help`", async () => {
    const { code, out } = await captureRun(["help"]);
    expect(code).toBe(0);
    expect(out).toContain("Usage:");
  });

  it("lists every registered transform step so config authors can discover ids", async () => {
    const { out } = await captureRun(["help"]);
    for (const id of Object.keys(transformSteps)) {
      expect(out).toContain(id);
    }
  });

  it("rejects an unknown command with exit 2", async () => {
    const { code, err } = await captureRun(["frobnicate"]);
    expect(code).toBe(2);
    expect(err).toContain("Unknown command: frobnicate");
  });
});

describe("ace compile: argument parsing", () => {
  it("fails without --tsp-location", async () => {
    const { code, err } = await captureRun(["compile"]);
    expect(code).toBe(2);
    expect(err).toContain("missing required --tsp-location");
  });

  it("rejects an unknown flag", async () => {
    const { code, err } = await captureRun(["compile", "--nope"]);
    expect(code).toBe(2);
    expect(err).toContain("Unknown option '--nope'");
  });
});

describe("ace compile: --config loading", () => {
  it("fails when the config file does not exist", async () => {
    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "missing.json"),
    ]);
    expect(code).toBe(2);
    expect(err).toContain("cannot read --config");
  });

  it("fails when the config is not a JSON object", async () => {
    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "array.json"),
    ]);
    expect(code).toBe(2);
    expect(err).toContain("must be a JSON object");
  });

  it("fails when the config is invalid JSON", async () => {
    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "bad.json"),
    ]);
    expect(code).toBe(2);
    expect(err).toContain("is not valid JSON");
  });

  it("tolerates a leading UTF-8 BOM in the config file", async () => {
    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "bom.json"),
      "--out-dir",
      join(dir, "bom-out"),
    ]);
    expect(code).toBe(0);
  });
});

describe("ace compile: happy path", () => {
  it("compiles pets.tsp and exits 0", async () => {
    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--out-dir",
      join(dir, "hp-plain"),
    ]);
    expect(err).not.toMatch(/^error:/m);
    expect(code).toBe(0);
  });

  it("compiles pets.tsp with a valid --config and exits 0", async () => {
    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "good.json"),
      "--out-dir",
      join(dir, "hp-config"),
    ]);
    expect(code).toBe(0);
  });

  it("compiles pets.tsp with --api-version and exits 0", async () => {
    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--api-version",
      "2025-01-01",
      "--out-dir",
      join(dir, "hp-apiver"),
    ]);
    expect(err).not.toMatch(/^error:/m);
    expect(code).toBe(0);
  });
});

describe("ace compile: --out-dir", () => {
  it("stages the spec under <out-dir>/source and, with the no-op generator, writes no output", async () => {
    const outDir = join(dir, "gen-none");
    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--out-dir",
      outDir,
    ]);
    expect(code).toBe(0);
    // The spec is always staged under <out-dir>/source; the default no-op
    // generator produces no files, so that is the only thing in <out-dir>.
    expect(existsSync(join(outDir, "source", "pets.tsp"))).toBe(true);
    expect(readdirSync(outDir)).toEqual(["source"]);
  });
});

describe("createFsSink", () => {
  it("writes generated files and creates nested parent directories", async () => {
    const outDir = join(dir, "sink");
    const sink = createFsSink(outDir);
    await sink({ kind: "generated-file", path: "models/Pet.cs", contents: "// pet" });
    await sink({ kind: "generated-file", path: "Client.cs", contents: "// client" });
    expect(readFileSync(join(outDir, "models", "Pet.cs"), "utf8")).toBe("// pet");
    expect(readFileSync(join(outDir, "Client.cs"), "utf8")).toBe("// client");
  });
});

describe("ace compile: --capture-ir", () => {
  it("writes each pipeline IR to <out-dir>/ir when set", async () => {
    const outDir = join(dir, "capture");
    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--out-dir",
      outDir,
      "--capture-ir",
    ]);
    expect(code).toBe(0);

    const irPath = join(outDir, "ir", "0-emit.json");
    expect(existsSync(irPath)).toBe(true);
    const ir = JSON.parse(readFileSync(irPath, "utf8")) as {
      schemaVersion: string;
      models: { name: string }[];
      operations: { name: string }[];
    };
    expect(typeof ir.schemaVersion).toBe("string");
    expect(ir.models.some((m) => m.name === "Pet")).toBe(true);
    expect(ir.operations.some((o) => o.name === "list")).toBe(true);
  });

  it("does not create the ir/ directory when --capture-ir is absent", async () => {
    const outDir = join(dir, "no-capture");
    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--out-dir",
      outDir,
    ]);
    expect(code).toBe(0);
    expect(existsSync(join(outDir, "ir"))).toBe(false);
  });
});

describe("ace compile: configured pipeline", () => {
  it("runs a transform step named in config and lands its output on the IR", async () => {
    const outDir = join(dir, "ps-derive");
    writeFileSync(
      join(dir, "pipeline.json"),
      JSON.stringify({ pipeline: [{ use: "ps.derive", options: { modulePrefix: "Az" } }] }),
    );

    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "pipeline.json"),
      "--out-dir",
      outDir,
      "--capture-ir",
    ]);
    expect(code).toBe(0);

    const snapshot = join(outDir, "ir", "1-ps.derive.json");
    expect(existsSync(snapshot)).toBe(true);
    const ir = JSON.parse(readFileSync(snapshot, "utf8")) as {
      ps?: { commands: { verb: string; noun: string }[] };
    };
    expect(ir.ps?.commands.length).toBeGreaterThan(0);
    expect(ir.ps!.commands.every((c) => c.noun.startsWith("Az"))).toBe(true);
  });

  it("fails with a diagnostic when config names an unregistered step", async () => {
    writeFileSync(join(dir, "bad-step.json"), JSON.stringify({ pipeline: [{ use: "ps.drive" }] }));

    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "bad-step.json"),
      "--out-dir",
      join(dir, "bad-step"),
    ]);
    expect(code).toBe(1);
    expect(err).toContain("unknown-transform-step");
  });

  it("runs steps in the declared order, feeding ps.derive's output to ps.directives", async () => {
    const outDir = join(dir, "ps-chain");
    writeFileSync(
      join(dir, "chain.json"),
      JSON.stringify({
        pipeline: [
          { use: "ps.derive" },
          {
            use: "ps.directives",
            options: { directives: [{ where: { subject: "Pet" }, set: { subject: "Animal" } }] },
          },
        ],
      }),
    );

    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "chain.json"),
      "--out-dir",
      outDir,
      "--capture-ir",
    ]);
    expect(code).toBe(0);

    const snapshot = join(outDir, "ir", "2-ps.directives.json");
    expect(existsSync(snapshot)).toBe(true);
    const ir = JSON.parse(readFileSync(snapshot, "utf8")) as {
      ps: { commands: { verb: string; noun: string }[] };
    };
    const nouns = ir.ps.commands.map((c) => c.noun);
    expect(nouns).toContain("Animal");
    // `subject: Pet` is anchored, so the rename must not reach `PetStatus`.
    expect(nouns).toContain("PetStatus");
  });

  it("fails when ps.directives runs without ps.derive ahead of it", async () => {
    writeFileSync(
      join(dir, "no-derive.json"),
      JSON.stringify({ pipeline: [{ use: "ps.directives" }] }),
    );

    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--config",
      join(dir, "no-derive.json"),
      "--out-dir",
      join(dir, "no-derive"),
    ]);
    expect(code).toBe(1);
    expect(err).toContain("ps-surface-missing");
  });
});
