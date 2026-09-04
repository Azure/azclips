/**
 * Host runtime seam for the compiler's built-in **external** stages.
 *
 * The backbone stays pure — no `child_process`, no `fs`, no `console`. But two
 * of its built-in stages (emit, and the C# generator) are out-of-process tools.
 * The split that keeps the backbone portable:
 *
 *   - The **compiler** owns *which* tool runs, *when* (ordering), and the JSON
 *     protocol it speaks (see `./builtins/protocol.ts`). It references a tool by
 *     a stable logical id, never by a concrete command or path.
 *   - The **host** supplies a `Runtime` that *resolves* a logical tool id to a
 *     real command and *executes* it. Tool resolution (realm layout, `dotnet`
 *     discovery, the hybrid emit toolchain) and process spawning are the host's
 *     job — the parts that legitimately differ between a CLI and a sandboxed web
 *     service.
 *
 * So the runtime is the single impure seam the orchestrator pulls on, injected
 * via `CompileOptions.runtime`. It is byte-level and catalog-blind: it moves a
 * stdin payload in and captures stdout/stderr out; the compiler frames and
 * parses the protocol on top.
 */

/** Logical id of the built-in TypeSpec emit tool (source -> IR). */
export const TSP_EMIT_TOOL = "tsp-emit";

/** Logical id of the built-in C# generator tool (IR -> C# files). */
export const CSHARP_GENERATE_TOOL = "csharp-generate";

/** A request to run one external tool: a logical id + a JSON stdin payload. */
export interface ToolInvocation {
  /** Logical tool id the host resolves to a concrete command. */
  tool: string;
  /** UTF-8 JSON payload delivered to the tool on stdin. */
  input: string;
}

/** The captured result of an external tool run. */
export interface ToolResult {
  /** Full stdout: the tool's JSON (emit) or NDJSON (generate) reply. */
  stdout: string;
  /** Full stderr; surfaced in a diagnostic when the tool fails. */
  stderr: string;
  /** Process exit code, or `null` if the tool was killed by a signal. */
  code: number | null;
}

/** The captured exit info of a *streamed* tool run (stdout was delivered line by line). */
export interface ToolStreamResult {
  /** Full stderr; surfaced in a diagnostic when the tool fails. */
  stderr: string;
  /** Process exit code, or `null` if the tool was killed by a signal. */
  code: number | null;
}

/** Per-invocation services handed to the runtime. */
export interface RuntimeContext {
  /** Cooperative cancellation shared with the enclosing compile run. */
  readonly signal?: AbortSignal;
}

/**
 * Host-provided effect boundary for external stages: given a logical tool id and
 * a stdin payload, resolve and run the tool, returning its captured output.
 *
 * Implementations own tool resolution and process spawning. `spawn` should
 * *resolve* a `ToolResult` even when the tool itself fails (non-zero `code`) so
 * the compiler can turn that into a diagnostic; it should only *reject* on
 * cancellation (an `AbortError`) or an unrecoverable host error.
 */
export interface Runtime {
  /** Run a tool and return its fully-buffered output. For one-shot replies (e.g. emit). */
  spawn(invocation: ToolInvocation, context: RuntimeContext): Promise<ToolResult>;
  /**
   * Run a tool and deliver its stdout **incrementally**: `onLine` is invoked once
   * per line as it arrives and awaited (so a slow consumer applies backpressure),
   * and only the exit info is returned. For line-oriented (NDJSON) tools whose
   * output can be large — the generator streams its files this way so nothing is
   * buffered end to end. Same failure/cancellation contract as `spawn`.
   */
  spawnStreaming(
    invocation: ToolInvocation,
    context: RuntimeContext,
    onLine: (line: string) => void | Promise<void>,
  ): Promise<ToolStreamResult>;
}
