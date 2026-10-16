/**
 * The `tsp-emit` worker's contract with its host.
 *
 * The compiler owns the `tsp-emit` protocol: an `EmitToolRequest` in, an
 * `EmitToolResponse` out. Like every out-of-process tool, the worker implements its
 * end by hand, so this package depends on the IR alone; the host, which sees both
 * sides, type-checks that they fit. The host adds what it resolved (which
 * `@typespec/compiler` and which emitter to load) and passes the compiler's request
 * through untouched.
 *
 * Plain JSON both ways, no live TypeSpec objects: that is what lets the worker load a
 * *different* `@typespec/compiler` without any of its types leaking into the host.
 */

/** Sent by the host to the worker. */
export interface TspEmitWorkerRequest {
  /** Resolved `@typespec/compiler` entry to dynamic-import. */
  compilerPath: string;
  /** Resolved emitter entry to register with that compiler. */
  emitterPath: string;
  /** The compiler's `tsp-emit` request. */
  request: TspEmitRequest;
}

/** The compiler's `tsp-emit` request, as the worker reads it. */
export interface TspEmitRequest {
  /** Staged entrypoint (project dir or `.tsp` file) to compile. */
  entrypoint: string;
  /** Options forwarded verbatim to the emitter. */
  emitterOptions?: Record<string, unknown>;
  /**
   * API version to compile at (a specific version, `"latest"`, or `"all"`).
   * Delivered to the emitter as its `api-version` option, which TCGC reads to
   * project the `@versioned` program before the IR is built.
   */
  apiVersion?: string;
  /** Extra imports layered on top of the entrypoint's own imports. */
  additionalImports?: string[];
  /** Promote warning-severity TypeSpec diagnostics to errors. */
  warnAsError?: boolean;
  /** TypeSpec trace areas to enable. */
  trace?: string[];
  /** Skip loading the TypeSpec standard library. */
  nostdlib?: boolean;
}

/** Sent back by the worker: the compiler's `tsp-emit` reply. */
export interface TspEmitReply {
  /** Parsed IR the emitter produced, or undefined if none was emitted. */
  ir?: unknown;
  /** Diagnostics from the compile and emit. */
  diagnostics: TspEmitDiagnostic[];
}

/** A diagnostic in the compiler's shape. */
export interface TspEmitDiagnostic {
  severity: "error" | "warning";
  code: string;
  message: string;
  /** Where it was reported; line and column are 1-based. */
  target?: { file: string; line: number; column: number };
}