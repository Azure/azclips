import { describe, expect, it } from "vitest";
import { emptyIR } from "@azure-client-engine/ir";

import { createDotnetGenerator, resolveHostCommand } from "../src/dotnet-generate.js";

const context = { reportDiagnostic: () => {}, config: {} };

/** Stands in for the .NET host: replies with a canned response on stdout. */
const fakeHost = (response: string): Record<string, unknown> => ({
  command: process.execPath,
  args: ["-e", `process.stdin.resume(); process.stdin.on("end", () => process.stdout.write(${JSON.stringify(response)}));`],
});

describe("csharp generator: option handling", () => {
  it("runs a .dll through the dotnet muxer and anything else directly", () => {
    expect(resolveHostCommand("/x/Ace.Generator.Host.dll")).toEqual({
      command: "dotnet",
      args: ["/x/Ace.Generator.Host.dll"],
    });
    expect(resolveHostCommand("/x/ace-generator")).toEqual({ command: "/x/ace-generator", args: [] });
  });

  it("rejects an unknown option", async () => {
    await expect(
      createDotnetGenerator({ hostpath: "x" }).generate(emptyIR(), context),
    ).rejects.toThrow(/unknown option 'hostpath'/);
  });

  it("requires a host to spawn", async () => {
    await expect(createDotnetGenerator({}).generate(emptyIR(), context)).rejects.toThrow(
      /needs 'hostPath'/,
    );
  });

  it("reports the host's diagnostics on the compile run", async () => {
    const diagnostics: { severity: string; code: string; message: string }[] = [];
    const generator = createDotnetGenerator(
      fakeHost(
        JSON.stringify({
          files: { "A.cs": "class A {}" },
          diagnostics: [{ severity: "warning", code: "x/y", message: "careful" }],
          success: true,
        }),
      ),
    );

    const out = await generator.generate(emptyIR(), {
      reportDiagnostic: (d) => diagnostics.push(d as never),
      config: {},
    });

    expect(out.files.get("A.cs")).toBe("class A {}");
    expect(diagnostics).toEqual([
      { severity: "warning", code: "@azure-client-engine/generator/x/y", message: "careful" },
    ]);
  });

  it("fails loudly when the host writes no JSON", async () => {
    await expect(
      createDotnetGenerator(fakeHost("not json")).generate(emptyIR(), context),
    ).rejects.toThrow(/without a JSON response/);
  });
});
