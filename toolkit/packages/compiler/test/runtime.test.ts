/**
 * Runtime-backed built-in stage tests.
 *
 * Prove the compiler-owned catalog: with a host `Runtime` injected (and no
 * explicit `emit`/`generator`), `compile()` drives its built-in `tsp-emit` and
 * `csharp` stages through `runtime.spawn`, framing the protocol itself. A fake
 * in-memory runtime stands in for the host so these stay process-free.
 */

import { describe, expect, it } from "vitest";

import {
  compile,
  createRegistry,
  CREATE_COMMAND_STAGE,
  type EmitProducer,
  type EmitToolRequest,
  type EmitToolResponse,
  type GenerateToolFrame,
  type GenerateToolRequest,
  type OutputDelegate,
  type Runtime,
} from "../src/index.js";

import { emptyIR, type IR, type TransformedIR } from "@azure-client-engine/ir";

/**
 * The catalog's optional transform stages have no built-in implementation, so
 * these tests turn them off and exercise only the two runtime-backed built-ins.
 */
const BUILTINS_ONLY = { pipeline: { "flatten-model": false } } as const;

/**
 * `create-command` cannot be turned off the way `flatten-model` can — the generate
 * stage `requires` it — and it has no built-in, because the real projection lives in
 * `@azure-client-engine/transformers`, which depends on this package. A stub stands
 * in so these tests exercise the runtime-backed built-ins rather than the projection.
 *
 * It must produce a valid transformed document: the csharp generator validates its
 * input before spawning anything.
 */
const COMMAND_STUB = createRegistry({
  transforms: {
    [CREATE_COMMAND_STAGE]: () => ({
      name: CREATE_COMMAND_STAGE,
      run: (ir: IR): TransformedIR => ({
        ...ir,
        commands: [],
        target: {
          rootNamespace: ["Stub"],
          projectName: "Stub",
          directories: { models: "Models", processors: "Processors" },
        },
      }),
    }),
  },
});

/** A minimal schema-shaped IR with a single identifiable source. */
function stubIR(id: string): IR {
  return {
    ...emptyIR(),
    sources: [
      {
        id,
        entrypoint: "stub.tsp",
        apiVersion: { value: "2024-01-01", location: "query", parameterName: "api-version" },
        server: { module: id, namespace: [id], name: "server" },
        rootNamespace: [id],
      },
    ],
    servers: [{
      id: { module: id, namespace: [id], name: "server" },
      endpoint: { kind: "literal", url: "https://stub.example/" },
    }],
  };
}

/** A fake host runtime that answers the two built-in tools from handlers. */
function fakeRuntime(handlers: {
  emit?: (req: EmitToolRequest) => EmitToolResponse;
  generate?: (req: GenerateToolRequest) => GenerateToolFrame[];
}): Runtime {
  return {
    // Emit is a single buffered JSON reply.
    async spawn({ tool, input }) {
      if (tool === "tsp-emit") {
        const response = handlers.emit?.(JSON.parse(input) as EmitToolRequest) ?? {
          diagnostics: [],
        };
        return { stdout: JSON.stringify(response), stderr: "", code: 0 };
      }
      return { stdout: "", stderr: `unexpected buffered tool ${tool}`, code: 1 };
    },
    // The csharp generator streams its NDJSON frames line by line.
    async spawnStreaming({ tool, input }, _context, onLine) {
      if (tool === "csharp-generate") {
        // With no generate handler, still emit a terminal result frame so the
        // built-in csharp generator (now the default) sees a clean, empty run.
        const frames =
          handlers.generate?.(JSON.parse(input) as GenerateToolRequest) ??
          ([{ kind: "result", success: true, diagnostics: [] }] as GenerateToolFrame[]);
        for (const frame of frames) await onLine(JSON.stringify(frame));
        return { stderr: "", code: 0 };
      }
      return { stderr: `unexpected streaming tool ${tool}`, code: 1 };
    },
  };
}

describe("built-in emit (runtime tsp-emit tool)", () => {
  it("runs the tsp-emit tool and returns its IR", async () => {
    const runtime = fakeRuntime({ emit: () => ({ ir: stubIR("FromTool"), diagnostics: [] }) });

    const result = await compile({ entrypoint: "x.tsp", config: BUILTINS_ONLY }, { runtime, registry: COMMAND_STUB });

    expect(result.success).toBe(true);
    expect(result.ir?.sources[0]?.id).toBe("FromTool");
  });

  it("forwards entrypoint + config to the tsp-emit request", async () => {
    let seen: EmitToolRequest | undefined;
    const runtime = fakeRuntime({
      emit: (req) => {
        seen = req;
        return { ir: stubIR("S"), diagnostics: [] };
      },
    });

    await compile(
      { entrypoint: "e.tsp", config: { apiVersion: "2025-09-01", emitter: { sourceId: "X" } } },
      { runtime, registry: COMMAND_STUB },
    );

    expect(seen?.entrypoint).toBe("e.tsp");
    expect(seen?.apiVersion).toBe("2025-09-01");
    expect(seen?.emitterOptions).toEqual({ sourceId: "X" });
  });

  it("surfaces tsp-emit diagnostics", async () => {
    const runtime = fakeRuntime({
      emit: () => ({
        ir: stubIR("S"),
        diagnostics: [
          { severity: "warning", code: "tsp/missing-api-version", message: "no api version" },
        ],
      }),
    });

    const result = await compile({ entrypoint: "x.tsp", config: BUILTINS_ONLY }, { runtime, registry: COMMAND_STUB });

    expect(result.success).toBe(true);
    expect(result.diagnostics.some((d) => d.code === "tsp/missing-api-version")).toBe(true);
  });

  it("reports emit-tool-failed when the tool returns no parseable reply", async () => {
    const runtime: Runtime = {
      async spawn() {
        return { stdout: "not json", stderr: "boom", code: 1 };
      },
      async spawnStreaming() {
        return { stderr: "", code: 0 };
      },
    };

    const result = await compile({ entrypoint: "x.tsp", config: BUILTINS_ONLY }, { runtime, registry: COMMAND_STUB });

    expect(result.success).toBe(false);
    expect(
      result.diagnostics.some((d) => d.code === "@azure-client-engine/compiler/emit-tool-failed"),
    ).toBe(true);
  });
});

describe("built-in csharp generator (runtime csharp-generate tool)", () => {
  const emitOk = (): EmitToolResponse => ({ ir: stubIR("S"), diagnostics: [] });

  it("defaults to the csharp generator when config.generator is absent", async () => {
    const runtime = fakeRuntime({
      emit: emitOk,
      generate: () => [
        { kind: "file", path: "out/Default.cs", contents: "// default" },
        { kind: "result", success: true, diagnostics: [] },
      ],
    });

    // No config.generator: the pipeline still ends in the built-in csharp back end.
    const result = await compile({ entrypoint: "x.tsp", config: BUILTINS_ONLY }, { runtime, registry: COMMAND_STUB });

    expect(result.success).toBe(true);
    expect(result.files.get("out/Default.cs")).toBe("// default");
  });

  it("streams generated files from the tool into the result", async () => {
    const runtime = fakeRuntime({
      emit: emitOk,
      generate: () => [
        { kind: "file", path: "out/one.cs", contents: "// widget" },
        { kind: "file", path: "out/two.cs", contents: "// gadget" },
        { kind: "result", success: true, diagnostics: [] },
      ],
    });

    const result = await compile(
      { entrypoint: "x.tsp", config: BUILTINS_ONLY },
      { runtime, registry: COMMAND_STUB },
    );

    expect(result.success).toBe(true);
    expect(result.files.get("out/one.cs")).toBe("// widget");
    expect(result.files.get("out/two.cs")).toBe("// gadget");
  });

  it("forwards remove artifacts before generated files, in stream order", async () => {
    const runtime = fakeRuntime({
      emit: emitOk,
      generate: () => [
        { kind: "remove", path: "Models" },
        { kind: "remove", path: "Processors" },
        { kind: "file", path: "out/one.cs", contents: "// one" },
        { kind: "result", success: true, diagnostics: [] },
      ],
    });

    const seen: string[] = [];
    const output: OutputDelegate = (a) => {
      if (a.kind === "remove") seen.push(`remove:${a.path}`);
      else if (a.kind === "generated-file") seen.push(`file:${a.path}`);
    };

    const result = await compile(
      { entrypoint: "x.tsp", config: BUILTINS_ONLY },
      { runtime, registry: COMMAND_STUB, output },
    );

    expect(result.success).toBe(true);
    // Removes clear scopes before any file is written.
    expect(seen).toEqual(["remove:Models", "remove:Processors", "file:out/one.cs"]);
  });

  it("forwards the rootNamespace option and the IR to the generate request", async () => {
    let seen: GenerateToolRequest | undefined;
    const runtime = fakeRuntime({
      emit: emitOk,
      generate: (req) => {
        seen = req;
        return [{ kind: "result", success: true, diagnostics: [] }];
      },
    });

    await compile(
      {
        entrypoint: "x.tsp",
        config: { ...BUILTINS_ONLY, stageOptions: { csharp: { rootNamespace: "Contoso.Clients" } } },
      },
      { runtime, registry: COMMAND_STUB },
    );

    expect(seen?.options.rootNamespace).toBe("Contoso.Clients");
    expect(seen?.ir.sources[0]?.id).toBe("S");
  });

  it("surfaces result-frame diagnostics and fails on an error", async () => {
    const runtime = fakeRuntime({
      emit: emitOk,
      generate: () => [
        {
          kind: "result",
          success: false,
          diagnostics: [
            { severity: "error", code: "ace-generator/boom", message: "generation failed" },
          ],
        },
      ],
    });

    const result = await compile(
      { entrypoint: "x.tsp", config: BUILTINS_ONLY },
      { runtime, registry: COMMAND_STUB },
    );

    expect(result.success).toBe(false);
    expect(result.diagnostics.some((d) => d.code === "ace-generator/boom")).toBe(true);
  });

  it("reports generate-tool-failed when no terminal result frame arrives", async () => {
    const runtime = fakeRuntime({
      emit: emitOk,
      generate: () => [{ kind: "file", path: "out/one.cs", contents: "// widget" }],
    });

    const result = await compile(
      { entrypoint: "x.tsp", config: BUILTINS_ONLY },
      { runtime, registry: COMMAND_STUB },
    );

    expect(result.success).toBe(false);
    expect(
      result.diagnostics.some(
        (d) => d.code === "@azure-client-engine/compiler/generate-tool-failed",
      ),
    ).toBe(true);
  });

  it("fails with generator-requires-runtime when csharp is selected but no runtime is given", async () => {
    // Inject emit (escape hatch) so we reach generator resolution with no runtime.
    const emit: EmitProducer = { name: "stub", produce: () => ({ ir: stubIR("S") }) };

    const result = await compile(
      { entrypoint: "x.tsp", config: BUILTINS_ONLY },
      { emit, registry: COMMAND_STUB },
    );

    expect(result.success).toBe(false);
    expect(
      result.diagnostics.some(
        (d) => d.code === "@azure-client-engine/compiler/generator-requires-runtime",
      ),
    ).toBe(true);
  });
});
