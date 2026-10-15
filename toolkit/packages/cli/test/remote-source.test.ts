/**
 * Remote sources: reading GitHub URLs, and staging what they name.
 *
 * github.com is never contacted: `createGitHubFixture` points git at local bare
 * repos, and the fetch runs as it would against GitHub, as a sparse checkout of one
 * commit. The tests that fetch are skipped where git cannot run.
 */

import { existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";

import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";
import type { CompilerDiagnostic } from "@azure-client-engine/compiler";

import { FETCH_TIMEOUT_VARIABLE, isUrl, parseGitHubUrl, type GitHubLocation } from "../src/remote-source.js";
import { stageProject, type StagedProject, type StagingRequest } from "../src/staging.js";
import { createGitHubFixture, gitAvailable, type GitHubFixture } from "./github-fixture.js";

/** The location a URL names, which must be readable. */
function parse(url: string): GitHubLocation {
  const parsed = parseGitHubUrl(url);
  if ("problem" in parsed) throw new Error(`'${url}' ${parsed.problem}`);
  return parsed.location;
}

/** What is wrong with a URL, which must not be readable. */
function problem(url: string): string {
  const parsed = parseGitHubUrl(url);
  if (!("problem" in parsed)) throw new Error(`'${url}' was read as ${JSON.stringify(parsed.location)}`);
  return parsed.problem;
}

describe("isUrl", () => {
  it("tells a URL from a path", () => {
    expect(isUrl("https://github.com/Azure/azure-rest-api-specs")).toBe(true);
    expect(isUrl("HTTP://example.com")).toBe(true);
    for (const path of ["C:\\specs\\Widget", "C:/specs/Widget", "/specs/Widget", "../Widget", "Widget", "\\\\server\\share"]) {
      expect(isUrl(path), path).toBe(false);
    }
  });
});

describe("parseGitHubUrl", () => {
  it("reads a folder URL", () => {
    expect(parse("https://github.com/Azure/azure-rest-api-specs/tree/main/specification/widget/Widget")).toEqual({
      owner: "Azure",
      repo: "azure-rest-api-specs",
      ref: "main",
      path: "specification/widget/Widget",
      isFile: false,
    });
  });

  it("reads a file URL, and a path ending in .tsp as a file either way", () => {
    expect(parse("https://github.com/o/r/blob/0123abc/spec/tspconfig.yaml")).toMatchObject({
      ref: "0123abc",
      path: "spec/tspconfig.yaml",
      isFile: true,
    });
    expect(parse("https://github.com/o/r/tree/main/spec/client.tsp").isFile).toBe(true);
  });

  it("reads a trailing slash, the repo root, and percent-encoding", () => {
    expect(parse("https://github.com/o/r/tree/main/spec/Widget/").path).toBe("spec/Widget");
    expect(parse("https://github.com/o/r/tree/v1.0")).toMatchObject({ ref: "v1.0", path: "", isFile: false });
    expect(parse("HTTPS://GITHUB.COM/o/r/tree/main/a%20b").path).toBe("a b");
  });

  it("says what is wrong with any other URL", () => {
    expect(problem("https://gitlab.com/o/r/tree/main/spec")).toContain("the only host ACE fetches from");
    expect(problem("http://github.com/o/r/tree/main/spec")).toContain("the only host ACE fetches from");
    expect(problem("https://github.com/o/r/blob/main/spec/main.tsp#L3")).toContain("query or fragment");
    expect(problem("https://github.com/o/r/tree/main/spec?plain=1")).toContain("query or fragment");
    expect(problem("https://github.com/o/r")).toContain("names no ref");
    expect(problem("https://github.com/o/r/tree")).toContain("names no ref");
    expect(problem("https://github.com/o/r/pull/12/files")).toContain("'/pull/'");
    expect(problem("https://github.com/o/r/tree/main/spec/../other")).toContain("'..'");
    expect(problem("https://github.com/o/r/tree/main/spec/%2E%2E/other")).toContain("'..'");
    expect(problem("https://github.com/o/r/tree/main/spec//Widget")).toContain("empty");
    expect(problem("https://github.com/o/r/tree/main/spec%2FWidget")).toContain("encoded '/'");
    expect(problem("https://github.com/o/r/tree/main/spec%ZZ")).toContain("percent-encoding");
    expect(problem("https://github.com/o/r%3Ax/tree/main/spec")).toContain("not a GitHub owner and repository");
    expect(problem("https://github.com/o_x/r/tree/main/spec")).toContain("not a GitHub owner and repository");
    expect(problem("https://github.com/o/r/tree/--upload-pack=x/spec")).toContain("'--upload-pack=x'");
    expect(problem("https://github.com/o/r/blob/main")).toContain("no path");
  });
});

describe.skipIf(!gitAvailable)("stageProject: GitHub sources", () => {
  const SERVICE = "specification/widget/Widget.Management";
  const SHARED = "specification/widget/Widget.Shared";

  let github: GitHubFixture;
  /** Azure/specs at main. */
  let commit: string;
  /** Contoso/common at main. */
  let common: string;
  let temp: string;
  let outRoot: string;

  /** A URL in Azure/specs. */
  const specs = (ref: string, path: string, kind: "tree" | "blob" = "tree"): string =>
    `https://github.com/Azure/specs/${kind}/${ref}/${path}`;

  beforeAll(() => {
    github = createGitHubFixture();
    commit = github.publish("Azure", "specs", {
      [`${SERVICE}/main.tsp`]: "// main\n",
      [`${SERVICE}/sub/extra.tsp`]: "// extra\n",
      [`${SHARED}/shared.tsp`]: "// shared\n",
      "specification/other/other.tsp": "// other\n",
      "README.md": "# specs\n",
    });
    common = github.publish("Contoso", "common", { "types/common.tsp": "// common\n" });
  });

  afterAll(() => github.dispose());

  beforeEach(() => {
    temp = mkdtempSync(join(tmpdir(), "ace-remote-"));
    outRoot = join(temp, "out");
  });

  afterEach(() => {
    rmSync(temp, { recursive: true, force: true });
  });

  async function stage(
    request: Partial<StagingRequest> & Pick<StagingRequest, "tspLocation">,
  ): Promise<{ staged: StagedProject | undefined; diagnostics: CompilerDiagnostic[] }> {
    const diagnostics: CompilerDiagnostic[] = [];
    const staged = await stageProject({ outDir: outRoot, ...request }, (diagnostic) =>
      diagnostics.push(diagnostic),
    );
    return { staged, diagnostics };
  }

  /** Stage what must stage, with no diagnostic at all. */
  async function stageOk(request: Partial<StagingRequest> & Pick<StagingRequest, "tspLocation">): Promise<StagedProject> {
    const { staged, diagnostics } = await stage(request);
    expect(diagnostics).toEqual([]);
    expect(staged).toBeDefined();
    return staged!;
  }

  /** Stage what must fail, returning the errors it reported. */
  async function stageFails(
    request: Partial<StagingRequest> & Pick<StagingRequest, "tspLocation">,
  ): Promise<CompilerDiagnostic[]> {
    const { staged, diagnostics } = await stage(request);
    expect(staged).toBeUndefined();
    return diagnostics.filter((diagnostic) => diagnostic.severity === "error");
  }

  const codes = (diagnostics: readonly CompilerDiagnostic[]): string[] =>
    diagnostics.map((diagnostic) => diagnostic.code.replace("@azure-client-engine/cli/", ""));

  /** The staged counterpart of a repo path. */
  const inCopy = (path: string): string => join(outRoot, "source", ...path.split("/"));

  /** A local file, creating its folders. */
  function write(path: string, contents = "// local\n"): void {
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, contents, "utf8");
  }

  it("stages a folder at its place in the repo, fetching only the folders it needs", async () => {
    const staged = await stageOk({ tspLocation: specs(commit, SERVICE) });

    expect(staged.root).toBe(resolve(outRoot, "source"));
    expect(staged.entrypoint).toBe(inCopy(SERVICE));
    expect(existsSync(inCopy(`${SERVICE}/main.tsp`))).toBe(true);
    expect(existsSync(inCopy(`${SERVICE}/sub/extra.tsp`))).toBe(true);
    expect(existsSync(inCopy(SHARED))).toBe(false);
    expect(existsSync(inCopy("README.md"))).toBe(false);
    expect(staged.fetched).toEqual([{ owner: "Azure", repo: "specs", ref: commit, commit, folders: [SERVICE] }]);
  });

  it("stages a file URL's folder, and compiles the file", async () => {
    const staged = await stageOk({ tspLocation: specs(commit, `${SERVICE}/main.tsp`, "blob") });

    expect(staged.entrypoint).toBe(inCopy(`${SERVICE}/main.tsp`));
    expect(existsSync(inCopy(`${SERVICE}/sub/extra.tsp`))).toBe(true);
  });

  it("resolves the entrypoint against the fetched folder", async () => {
    const staged = await stageOk({ tspLocation: specs(commit, SERVICE), entrypoint: "sub/extra.tsp" });

    expect(staged.entrypoint).toBe(inCopy(`${SERVICE}/sub/extra.tsp`));
  });

  it("warns about a ref that is not a commit, naming the commit it fetched", async () => {
    const { staged, diagnostics } = await stage({ tspLocation: specs("main", SERVICE) });

    expect(staged?.fetched[0]?.commit).toBe(commit);
    expect(codes(diagnostics)).toEqual(["unpinned-ref"]);
    expect(diagnostics[0]!.severity).toBe("warning");
    expect(diagnostics[0]!.message).toContain(`'${specs("main", SERVICE)}' names 'main', not a commit`);
    expect(diagnostics[0]!.message).toContain(`pin it with '${specs(commit, SERVICE)}'`);
  });

  it("fetches additional directories in tsp-location's repo and ref with it, relative or by URL", async () => {
    for (const written of ["../Widget.Shared", specs(commit, SHARED)]) {
      const staged = await stageOk({ tspLocation: specs(commit, SERVICE), additionalDirectories: [written] });

      expect(existsSync(inCopy(`${SHARED}/shared.tsp`)), written).toBe(true);
      expect(staged.fetched, written).toEqual([
        { owner: "Azure", repo: "specs", ref: commit, commit, folders: [SERVICE, SHARED] },
      ]);
    }
  });

  it("matches the repo ignoring case, and the ref as written", async () => {
    const staged = await stageOk({
      tspLocation: specs(commit, SERVICE),
      additionalDirectories: [`https://github.com/azure/SPECS/tree/${commit}/${SHARED}`],
    });

    expect(existsSync(inCopy(`${SHARED}/shared.tsp`))).toBe(true);
    expect(staged.fetched).toHaveLength(1);
  });

  it("copies a folder from another repo, or another ref, by name", async () => {
    const { staged, diagnostics } = await stage({
      tspLocation: specs(commit, SERVICE),
      additionalDirectories: [`https://github.com/Contoso/common/tree/${common}/types`, specs("main", SHARED)],
    });

    // Another ref is another tree, even at the same commit: an external folder.
    expect(codes(diagnostics)).toEqual(["unpinned-ref"]);
    expect(existsSync(join(outRoot, "source", "types", "common.tsp"))).toBe(true);
    expect(existsSync(join(outRoot, "source", "Widget.Shared", "shared.tsp"))).toBe(true);
    expect(existsSync(inCopy(SHARED))).toBe(false);
    expect(staged?.fetched.map(({ owner, repo, ref }) => `${owner}/${repo}@${ref}`)).toEqual([
      `Azure/specs@${commit}`,
      `Contoso/common@${common}`,
      "Azure/specs@main",
    ]);
  });

  it("copies a local folder by name next to a remote tsp-location", async () => {
    const lib = join(temp, "external", "lib");
    write(join(lib, "lib.tsp"));

    await stageOk({ tspLocation: specs(commit, SERVICE), additionalDirectories: [lib] });

    expect(existsSync(join(outRoot, "source", "lib", "lib.tsp"))).toBe(true);
  });

  it("copies a GitHub folder by name next to a local tsp-location", async () => {
    const repo = join(temp, "repo");
    for (const marker of ["specification", ".github", ".azure-pipelines"]) mkdirSync(join(repo, marker), { recursive: true });
    const service = join(repo, "specification", "local", "Local");
    write(join(service, "main.tsp"));

    const staged = await stageOk({
      tspLocation: service,
      additionalDirectories: [`https://github.com/Contoso/common/tree/${common}/types`],
    });

    expect(staged.entrypoint).toBe(inCopy("specification/local/Local"));
    expect(existsSync(join(outRoot, "source", "types", "common.tsp"))).toBe(true);
    expect(staged.fetched).toHaveLength(1);
  });

  it("refuses a relative additional directory above the repo root, and a file URL", async () => {
    const diagnostics = await stageFails({
      tspLocation: specs(commit, SERVICE),
      additionalDirectories: ["../../../../..", specs(commit, `${SHARED}/shared.tsp`, "blob")],
    });

    expect(codes(diagnostics)).toEqual(["invalid-additional-directory", "invalid-additional-directory"]);
    expect(diagnostics[0]!.message).toContain("climbs above the root of Azure/specs");
    expect(diagnostics[1]!.message).toContain("names a file");
  });

  it("names GitHub URLs, not its temporary checkout, in staging's own errors", async () => {
    const missing = await stageFails({ tspLocation: specs(commit, "specification/missing") });
    expect(codes(missing)).toEqual(["invalid-tsp-location"]);
    expect(missing[0]!.message).toBe(`tsp-location '${specs(commit, "specification/missing")}' does not exist.`);

    const entrypoint = await stageFails({ tspLocation: specs(commit, SERVICE), entrypoint: "missing.tsp" });
    expect(codes(entrypoint)).toEqual(["invalid-entrypoint"]);
    expect(entrypoint[0]!.message).toBe(
      `The entrypoint '${specs(commit, `${SERVICE}/missing.tsp`, "blob")}' does not exist ` +
        `(a relative path resolves against '${specs(commit, SERVICE)}').`,
    );

    const directory = await stageFails({ tspLocation: specs(commit, SERVICE), additionalDirectories: ["../Missing"] });
    expect(codes(directory)).toEqual(["invalid-additional-directory"]);
    expect(directory[0]!.message).not.toContain("ace-fetch-");
    expect(directory[0]!.message).toContain(specs(commit, "specification/widget/Missing"));
  });

  it("refuses a URL as the entrypoint", async () => {
    const diagnostics = await stageFails({
      tspLocation: specs(commit, SERVICE),
      entrypoint: specs(commit, `${SERVICE}/main.tsp`, "blob"),
    });

    expect(codes(diagnostics)).toEqual(["invalid-entrypoint"]);
    expect(diagnostics[0]!.message).toContain("is a URL");
  });

  it("reports every fetch that fails, quoting git, before touching <out>/source", async () => {
    const prior = join(outRoot, "source", "prior.tsp");
    write(prior, "// prior\n");

    const diagnostics = await stageFails({
      tspLocation: specs("no-such-branch", SERVICE),
      additionalDirectories: [
        `https://github.com/Contoso/common/tree/${common.slice(0, 8)}/types`,
        "https://github.com/Contoso/missing/tree/main/types",
      ],
    });

    expect(codes(diagnostics)).toEqual(["remote-fetch-failed", "remote-fetch-failed", "remote-fetch-failed"]);
    expect(diagnostics[0]!.message).toContain("couldn't find remote ref no-such-branch");
    expect(diagnostics[0]!.message).toContain("holds '/'");
    expect(diagnostics[1]!.message).toContain("all 40 of its hex digits");
    expect(diagnostics[2]!.message).toContain("Cannot fetch 'main' of Contoso/missing");
    expect(existsSync(prior)).toBe(true);
  });

  it("deletes its temporary checkouts, whether staging succeeds or not", async () => {
    const scratch = join(temp, "scratch");
    mkdirSync(scratch);
    const saved = { TEMP: process.env.TEMP, TMP: process.env.TMP, TMPDIR: process.env.TMPDIR };
    Object.assign(process.env, { TEMP: scratch, TMP: scratch, TMPDIR: scratch });
    try {
      expect(tmpdir()).toBe(scratch);
      await stageOk({ tspLocation: specs(commit, SERVICE) });
      await stageFails({ tspLocation: specs(commit, SERVICE), entrypoint: "missing.tsp" });
      await stageFails({ tspLocation: specs("no-such-branch", SERVICE) });
    } finally {
      for (const [name, value] of Object.entries(saved)) {
        if (value === undefined) delete process.env[name];
        else process.env[name] = value;
      }
    }

    expect(readdirSync(scratch).filter((name) => name.startsWith("ace-fetch-"))).toEqual([]);
  });

  it("stops a git command past the hang guard", async () => {
    const saved = process.env[FETCH_TIMEOUT_VARIABLE];
    process.env[FETCH_TIMEOUT_VARIABLE] = "1";
    try {
      const diagnostics = await stageFails({ tspLocation: specs(commit, SERVICE) });

      expect(codes(diagnostics)).toEqual(["remote-fetch-failed"]);
      expect(diagnostics[0]!.message).toContain("timed out after 1ms");
      expect(diagnostics[0]!.message).toContain(FETCH_TIMEOUT_VARIABLE);
    } finally {
      if (saved === undefined) delete process.env[FETCH_TIMEOUT_VARIABLE];
      else process.env[FETCH_TIMEOUT_VARIABLE] = saved;
    }
  });

  it("says so when git is not on PATH", async () => {
    const saved = process.env.PATH;
    process.env.PATH = join(temp, "no-git");
    try {
      const diagnostics = await stageFails({ tspLocation: specs(commit, SERVICE) });

      expect(codes(diagnostics)).toEqual(["remote-fetch-failed"]);
      expect(diagnostics[0]!.message).toContain("git is not on PATH");
    } finally {
      process.env.PATH = saved;
    }
  });
});
