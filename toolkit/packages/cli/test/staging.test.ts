/**
 * `stageLocalProject` staging + bundled-provisioning tests (design Part II, P2.4).
 *
 * Proves the host's acquire + provision step against a fake specs repo (a folder
 * holding the three marker folders): `tsp-location` and the additional directories
 * are copied into `<out>/source` at their places in the repo (excluding
 * `node_modules`, `.git` and `tsp-output`), a `node_modules` junction to ace's
 * hoisted install is dropped at the staged root (so the compiler + libraries +
 * emitter all resolve), and the staged entrypoint points inside the copy. Every
 * problem is reported before anything is touched, and the user's tree never is.
 */

import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join, relative, resolve } from "node:path";

import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { CompilerDiagnostic } from "@azure-client-engine/compiler";

import {
  STANDALONE_SPEC_VARIABLE,
  findSpecsRepoRoot,
  removeDir,
  stageLocalProject,
  type StagedProject,
  type StagingRequest,
} from "../src/staging.js";
import { resolveToolchain } from "../src/toolchain.js";

let temp: string;
/** The fake specs repo. */
let repo: string;
/** The spec staged unless a test says otherwise. */
let service: string;
/** A sibling of the service in the repo. */
let shared: string;
/** A folder outside the repo, holding `lib/lib.tsp`. */
let external: string;
let outRoot: string;

/** Write a file, creating its folders. */
function write(path: string, contents = "// fake\n"): void {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, contents, "utf8");
}

beforeEach(() => {
  temp = mkdtempSync(join(tmpdir(), "ace-stage-"));
  repo = join(temp, "repo");
  for (const marker of ["specification", ".github", ".azure-pipelines"]) {
    mkdirSync(join(repo, marker), { recursive: true });
  }

  service = join(repo, "specification", "widget", "Widget.Management");
  write(join(service, "main.tsp"));
  write(join(service, "sub", "extra.tsp"));
  // What staging must NOT copy, at the top and further down.
  write(join(service, "node_modules", "junk.txt"), "stale");
  write(join(service, ".git", "config"), "[core]");
  write(join(service, "sub", "tsp-output", "old.json"), "{}");

  shared = join(repo, "specification", "widget", "Widget.Shared");
  write(join(shared, "shared.tsp"));
  write(join(shared, "tsp-output", "old.json"), "{}");

  external = join(temp, "external");
  write(join(external, "lib", "lib.tsp"));

  outRoot = join(temp, "out");
});

afterEach(() => {
  // rmSync unlinks the node_modules junction rather than recursing into ace's
  // real install (verified Node behavior on Windows junctions).
  rmSync(temp, { recursive: true, force: true });
});

/** Stage `service` unless the request says otherwise, collecting what is reported. */
async function stage(
  request: Partial<StagingRequest>,
): Promise<{ staged: StagedProject | undefined; diagnostics: CompilerDiagnostic[] }> {
  const diagnostics: CompilerDiagnostic[] = [];
  const staged = await stageLocalProject(
    { tspLocation: service, outDir: outRoot, ...request },
    (diagnostic) => diagnostics.push(diagnostic),
  );
  return { staged, diagnostics };
}

/** Stage what must stage cleanly. */
async function stageOk(request: Partial<StagingRequest> = {}): Promise<StagedProject> {
  const { staged, diagnostics } = await stage(request);
  expect(diagnostics).toEqual([]);
  expect(staged).toBeDefined();
  return staged!;
}

/** Stage what must fail, returning the errors it reported. */
async function stageFails(request: Partial<StagingRequest>): Promise<CompilerDiagnostic[]> {
  const { staged, diagnostics } = await stage(request);
  expect(staged).toBeUndefined();
  expect(diagnostics.every((diagnostic) => diagnostic.severity === "error")).toBe(true);
  return diagnostics;
}

/** The codes of `diagnostics`, without the CLI's prefix. */
const codes = (diagnostics: readonly CompilerDiagnostic[]): string[] =>
  diagnostics.map((diagnostic) => diagnostic.code.replace("@azure-client-engine/cli/", ""));

/** The counterpart of `path`, in the repo, in the staged copy, which stands for the repo root. */
const inCopy = (staged: StagedProject, path: string): string => join(staged.root, relative(repo, path));

/** A prior run's staged copy, which staging wipes only once every check has passed. */
function writePrior(): string {
  const prior = join(outRoot, "source", "prior.tsp");
  write(prior, "// prior\n");
  return prior;
}

describe("stageLocalProject: the specs repo's layout", () => {
  it("copies tsp-location to its place in the repo and points the entrypoint inside it", async () => {
    const staged = await stageOk({ tspLocation: join(service, "main.tsp") });

    expect(staged.root).toBe(resolve(outRoot, "source"));
    expect(staged.entrypoint).toBe(inCopy(staged, join(service, "main.tsp")));
    expect(existsSync(staged.entrypoint)).toBe(true);
    expect(existsSync(inCopy(staged, join(service, "sub", "extra.tsp")))).toBe(true);
    // Only what was asked for: the rest of the repo stays behind.
    expect(existsSync(inCopy(staged, shared))).toBe(false);
  });

  it("stages a folder tsp-location as the folder's counterpart", async () => {
    const staged = await stageOk();

    expect(staged.entrypoint).toBe(inCopy(staged, service));
    expect(existsSync(join(staged.entrypoint, "main.tsp"))).toBe(true);
  });

  it("excludes node_modules, .git and tsp-output at any depth, even a listed folder by those names", async () => {
    write(join(external, "tsp-output", "old.json"), "{}");
    const staged = await stageOk({ additionalDirectories: [shared, join(external, "tsp-output")] });

    expect(existsSync(inCopy(staged, join(service, "node_modules")))).toBe(false);
    expect(existsSync(inCopy(staged, join(service, ".git")))).toBe(false);
    expect(existsSync(inCopy(staged, join(service, "sub", "tsp-output")))).toBe(false);
    expect(existsSync(inCopy(staged, join(shared, "tsp-output")))).toBe(false);
    expect(existsSync(join(staged.root, "tsp-output"))).toBe(false);
    expect(existsSync(inCopy(staged, join(shared, "shared.tsp")))).toBe(true);
  });

  it("links ace's hoisted toolchain at the staged root, where the deeper layout finds it", async () => {
    const staged = await stageOk({ tspLocation: join(service, "main.tsp") });

    // The junction resolves to ace's install, so @typespec/compiler is reachable
    // from the staged tree even though the spec brought no node_modules.
    expect(existsSync(join(staged.root, "node_modules", "@typespec", "compiler"))).toBe(true);

    // ...which is what lets resolveToolchain find a toolchain beside the staged spec,
    // several folders down, rather than falling back to ace's own.
    const tc = resolveToolchain(staged.entrypoint);
    expect(tc.source).toBe("project-local");
    expect(existsSync(tc.compilerPath)).toBe(true);
    expect(existsSync(tc.emitterPath)).toBe(true);
  });

  it("leaves the user's tree untouched", async () => {
    await stageOk();

    // The source keeps its own (stale) node_modules; staging copied, not moved.
    expect(existsSync(join(service, "node_modules", "junk.txt"))).toBe(true);
  });

  it("finds the repo by its three marker folders, with no .git needed", () => {
    expect(findSpecsRepoRoot(join(service, "sub"))).toBe(repo);
    expect(findSpecsRepoRoot(external)).toBeUndefined();

    rmSync(join(repo, ".azure-pipelines"), { recursive: true });
    expect(findSpecsRepoRoot(service)).toBeUndefined();
  });
});

describe("stageLocalProject: entrypoint", () => {
  it("stages the whole tsp-location and points at the entrypoint inside it", async () => {
    const staged = await stageOk({ entrypoint: join("sub", "extra.tsp") });

    expect(staged.entrypoint).toBe(inCopy(staged, join(service, "sub", "extra.tsp")));
    expect(existsSync(staged.entrypoint)).toBe(true);
    // The copy is rooted at tsp-location, not at the entrypoint's folder.
    expect(existsSync(inCopy(staged, join(service, "main.tsp")))).toBe(true);
  });

  it("accepts an absolute entrypoint, including a folder", async () => {
    const staged = await stageOk({ entrypoint: join(service, "sub") });

    expect(staged.entrypoint).toBe(inCopy(staged, join(service, "sub")));
  });

  it("resolves a relative entrypoint against a file tsp-location's folder", async () => {
    const staged = await stageOk({
      tspLocation: join(service, "main.tsp"),
      entrypoint: join("sub", "extra.tsp"),
    });

    expect(staged.entrypoint).toBe(inCopy(staged, join(service, "sub", "extra.tsp")));
  });

  it("rejects an invalid entrypoint before touching <out>/source", async () => {
    const prior = writePrior();

    for (const entrypoint of [
      "../Widget.Shared/shared.tsp", // outside tsp-location, relative...
      outRoot, // ...or absolute
      "missing.tsp",
      "node_modules/junk.txt", // inside a folder staging does not copy
    ]) {
      expect(codes(await stageFails({ entrypoint })), entrypoint).toEqual(["invalid-entrypoint"]);
    }
    expect(existsSync(prior)).toBe(true);
  });
});

describe("stageLocalProject: additional directories", () => {
  it("copies a folder in the repo to its place there, given relative or absolute", async () => {
    for (const written of ["../Widget.Shared", shared]) {
      const staged = await stageOk({ additionalDirectories: [written] });

      expect(existsSync(inCopy(staged, join(shared, "shared.tsp"))), written).toBe(true);
    }
  });

  it("copies a folder outside the repo to the staged root, by name", async () => {
    const staged = await stageOk({ additionalDirectories: [join(external, "lib")] });

    expect(existsSync(join(staged.root, "lib", "lib.tsp"))).toBe(true);
  });

  it("copies a folder once when another one holds it, even tsp-location", async () => {
    // `..` holds both the service and its sibling.
    const staged = await stageOk({ additionalDirectories: ["..", "../Widget.Shared"] });

    expect(staged.entrypoint).toBe(inCopy(staged, service));
    expect(existsSync(join(staged.entrypoint, "main.tsp"))).toBe(true);
    expect(existsSync(inCopy(staged, join(shared, "shared.tsp")))).toBe(true);
  });

  it("maps the entrypoint through the folder that carries it, even one holding the whole repo", async () => {
    // Holding the output folder too, it is refused like any copied folder would be.
    expect(codes(await stageFails({ additionalDirectories: [temp] }))).toEqual(["staging-overlap"]);

    // Outside the repo, it is copied by name, and tsp-location as part of it.
    const outDir = mkdtempSync(join(tmpdir(), "ace-stage-out-"));
    try {
      const staged = await stageOk({ outDir, additionalDirectories: [temp] });

      expect(staged.entrypoint).toBe(join(staged.root, basename(temp), relative(temp, service)));
      expect(existsSync(join(staged.entrypoint, "main.tsp"))).toBe(true);
    } finally {
      rmSync(outDir, { recursive: true, force: true });
    }
  });

  it("reports every invalid additional directory before touching <out>/source", async () => {
    const prior = writePrior();
    const written = [
      "../Widget.Missing", // does not exist
      "main.tsp", // not a folder
      "sub", // under tsp-location, which is copied whole
      ".", // tsp-location itself
    ];

    const diagnostics = await stageFails({ additionalDirectories: written });

    expect(codes(diagnostics)).toEqual(written.map(() => "invalid-additional-directory"));
    for (const [index, entry] of written.entries()) {
      expect(diagnostics[index]!.message).toContain(`'${entry}'`);
    }
    expect(existsSync(prior)).toBe(true);
  });

  it("refuses folders that would land in one place", async () => {
    write(join(temp, "a", "lib", "a.tsp"));
    write(join(external, "specification", "x.tsp"));

    const diagnostics = await stageFails({
      additionalDirectories: [
        join(temp, "a", "lib"), // two external folders with one name
        join(external, "lib"),
        join(external, "specification"), // where the repo's own layout goes
      ],
    });

    expect(codes(diagnostics)).toEqual(["staging-collision", "staging-collision"]);
    expect(existsSync(join(outRoot, "source"))).toBe(false);
  });
});

describe("stageLocalProject: outside a specs repo", () => {
  it("refuses a tsp-location outside a specs repo, naming the escape hatch", async () => {
    const diagnostics = await stageFails({ tspLocation: join(external, "lib") });

    expect(codes(diagnostics)).toEqual(["not-in-specs-repo"]);
    expect(diagnostics[0]!.message).toContain(STANDALONE_SPEC_VARIABLE);
    expect(existsSync(join(outRoot, "source"))).toBe(false);
  });

  it("copies it flat with the escape hatch", async () => {
    const staged = await stageOk({
      tspLocation: join(external, "lib", "lib.tsp"),
      allowStandaloneSpec: true,
    });

    expect(staged.entrypoint).toBe(join(staged.root, "lib.tsp"));
    expect(existsSync(staged.entrypoint)).toBe(true);
    expect(existsSync(join(staged.root, "node_modules", "@typespec", "compiler"))).toBe(true);
  });

  it("refuses additional directories even then: there is no repo to place them in", async () => {
    const diagnostics = await stageFails({
      tspLocation: join(external, "lib"),
      additionalDirectories: [shared],
      allowStandaloneSpec: true,
    });

    expect(codes(diagnostics)).toEqual(["invalid-additional-directory"]);
  });

  it("keeps the repo's layout inside a specs repo, escape hatch or not", async () => {
    const staged = await stageOk({ allowStandaloneSpec: true });

    expect(staged.entrypoint).toBe(inCopy(staged, service));
  });
});

describe("stageLocalProject: output folder", () => {
  it("refuses an output folder whose staging root overlaps a copied folder, deleting nothing", async () => {
    // <out>/source inside the copied tree: the copy would recurse into itself.
    const userFile = join(service, "source", "user.tsp");
    write(userFile, "// user\n");
    expect(codes(await stageFails({ outDir: service }))).toEqual(["staging-overlap"]);
    expect(existsSync(userFile)).toBe(true);

    // A folder to copy inside <out>/source: the staging wipe would delete it.
    const nested = join(outRoot, "source", "lib");
    write(join(nested, "lib.tsp"));
    expect(codes(await stageFails({ additionalDirectories: [nested] }))).toEqual(["staging-overlap"]);
    expect(existsSync(join(nested, "lib.tsp"))).toBe(true);
  });
});

describe("removeDir", () => {
  it("removes the staged tree and unlinks the junction without touching ace's install", async () => {
    const staged = await stageOk();
    expect(existsSync(join(staged.root, "node_modules", "@typespec", "compiler"))).toBe(true);

    const { removed, error } = await removeDir(staged.root);

    expect(error).toBeUndefined();
    expect(removed).toBe(true);
    expect(existsSync(staged.root)).toBe(false);

    // The junction was unlinked, not recursed into: ace's real toolchain (the
    // junction's target) is untouched and still resolvable.
    const tc = resolveToolchain(join(service, "main.tsp"));
    expect(existsSync(tc.compilerPath)).toBe(true);
  });
});
