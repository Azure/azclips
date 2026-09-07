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

import { run } from "../src/index.js";
import { createFsSink } from "../src/compile-command.js";
import { resolveGeneratorHost } from "../src/dotnet-generate.js";

// The csharp generation e2e runs only when the .NET generator host has been
// built (dotnet build packages/generator/host). Otherwise it is skipped, so the
// pure-Node test run stays green without a .NET toolchain.
let generatorHostBuilt = false;
try {
  resolveGeneratorHost();
  generatorHostBuilt = true;
} catch {
  generatorHostBuilt = false;
}

/** Recursively list every file under `root`. */
function walkFiles(root: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const full = join(root, entry.name);
    if (entry.isDirectory()) out.push(...walkFiles(full));
    else out.push(full);
  }
  return out;
}

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

  it.runIf(generatorHostBuilt)("tolerates a leading UTF-8 BOM in the config file", async () => {
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

describe.runIf(generatorHostBuilt)("ace compile: happy path", () => {
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

describe.runIf(generatorHostBuilt)("ace compile: --out-dir", () => {
  it("removes the staged source and generates with the default (csharp) back end", async () => {
    const outDir = join(dir, "gen-default");
    const { code, err } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--out-dir",
      outDir,
    ]);
    expect(err).not.toMatch(/^error:/m);
    expect(code).toBe(0);
    // The transient staged copy is deleted by default...
    expect(existsSync(join(outDir, "source"))).toBe(false);
    // ...and with no --generator the compiler defaults to csharp, so C# output
    // (including a project file) lands in <out-dir> — generation is the product.
    const files = walkFiles(outDir);
    expect(files.some((f) => f.endsWith(".csproj"))).toBe(true);
  });

  it("keeps <out-dir>/source with --capture-source", async () => {
    const outDir = join(dir, "keep-source");
    const { code } = await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--out-dir",
      outDir,
      "--capture-source",
    ]);
    expect(code).toBe(0);
    expect(existsSync(join(outDir, "source", "pets.tsp"))).toBe(true);
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

  it("removes a generator-owned scope, refusing to escape the output dir", async () => {
    const outDir = join(dir, "sink-remove");
    const sink = createFsSink(outDir);
    await sink({ kind: "generated-file", path: "Models/Stale.cs", contents: "// stale" });
    await sink({ kind: "generated-file", path: "Keep.cs", contents: "// keep" });
    expect(existsSync(join(outDir, "Models", "Stale.cs"))).toBe(true);

    // Wiping the Models scope removes it but leaves siblings alone.
    await sink({ kind: "remove", path: "Models" });
    expect(existsSync(join(outDir, "Models"))).toBe(false);
    expect(existsSync(join(outDir, "Keep.cs"))).toBe(true);

    // A scope at/outside the output root is refused (out-dir untouched). Suppress
    // the expected warnings so they don't pollute the reporter.
    const errSpy = vi.spyOn(process.stderr, "write").mockImplementation(() => true);
    try {
      await sink({ kind: "remove", path: ".." });
      await sink({ kind: "remove", path: "" });
    } finally {
      errSpy.mockRestore();
    }
    expect(existsSync(outDir)).toBe(true);
    expect(existsSync(join(outDir, "Keep.cs"))).toBe(true);
  });
});

describe.runIf(generatorHostBuilt)("ace compile: --capture-ir", () => {
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

    // Scan the ir/ folder for snapshot json (the <index>-<step> filename is an
    // incidental decision — don't hardcode it).
    const irFiles = walkFiles(join(outDir, "ir")).filter((f) => f.endsWith(".json"));
    expect(irFiles.length).toBeGreaterThan(0);
    const ir = JSON.parse(readFileSync(irFiles[0]!, "utf8")) as {
      schemaVersion: string;
      models: { name: string }[];
      operations: { name: string; id: { namespace: string[]; interface?: string } }[];
    };
    expect(typeof ir.schemaVersion).toBe("string");
    expect(ir.models.some((m) => m.name === "Pet")).toBe(true);
    expect(ir.operations.some((o) => o.name === "list")).toBe(true);
    for (const operation of ir.operations) {
      expect(operation.id.namespace).toEqual(["PetStore"]);
      expect(operation.id.interface).toBe("Pets");
    }
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

  it("removes a stale ir/ directory when --capture-ir is absent", async () => {
    const outDir = join(dir, "stale-ir");
    // First run WITH --capture-ir writes <out-dir>/ir.
    await captureRun([
      "compile",
      "--tsp-location",
      PETS_TSP,
      "--out-dir",
      outDir,
      "--capture-ir",
    ]);
    expect(walkFiles(join(outDir, "ir")).some((f) => f.endsWith(".json"))).toBe(true);

    // A second run to the SAME out-dir WITHOUT --capture-ir clears the stale ir/.
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

describe("ace compile: csharp generation", () => {
  it.runIf(generatorHostBuilt)(
    "generates C# files under out-dir with --generator csharp",
    async () => {
      const outDir = join(dir, "gen-csharp");
      const { code, err } = await captureRun([
        "compile",
        "--tsp-location",
        PETS_TSP,
        "--generator",
        "csharp",
        "--root-namespace",
        "Contoso.Pets",
        "--out-dir",
        outDir,
      ]);
      expect(err).not.toMatch(/^error:/m);
      expect(code).toBe(0);
      // Scan the output: generation ran end-to-end and produced files, including a
      // C# project. File naming is the generator's concern (pinned in its own
      // tests), so this wiring test asserts no specific names.
      const files = walkFiles(outDir);
      expect(files.length).toBeGreaterThan(0);
      expect(files.some((f) => f.endsWith(".csproj"))).toBe(true);
    },
  );

  it.runIf(generatorHostBuilt)(
    "wipes stale generated files on regeneration (clean codegen)",
    async () => {
      const outDir = join(dir, "regen-clean");
      await captureRun([
        "compile",
        "--tsp-location",
        PETS_TSP,
        "--generator",
        "csharp",
        "--out-dir",
        outDir,
      ]);

      // Plant a stale file inside a generator-owned scope (Models/).
      const stale = join(outDir, "Models", "GhostModel.cs");
      writeFileSync(stale, "// stale", "utf8");
      expect(existsSync(stale)).toBe(true);

      // Regenerate: the model generator wipes Models/ first, so the ghost is gone
      // while fresh output lands.
      const { code } = await captureRun([
        "compile",
        "--tsp-location",
        PETS_TSP,
        "--generator",
        "csharp",
        "--out-dir",
        outDir,
      ]);
      expect(code).toBe(0);
      expect(existsSync(stale)).toBe(false);
      expect(walkFiles(outDir).some((f) => f.endsWith(".cs"))).toBe(true);
    },
  );
});
