/**
 * Cooperative cancellation helper.
 *
 * We throw a plain `Error` tagged with `name = "AbortError"` rather than
 * relying on `AbortSignal.prototype.throwIfAborted` / `DOMException`, so the
 * behaviour is identical across the Node/@types/node versions the workspace
 * pins. `isAbortError` recognises both our errors and native AbortErrors.
 */

export function throwIfAborted(signal: AbortSignal | undefined): void {
  if (signal?.aborted === true) {
    const error = new Error("The compile operation was aborted.");
    error.name = "AbortError";
    throw error;
  }
}

export function isAbortError(error: unknown): boolean {
  return error instanceof Error && error.name === "AbortError";
}
