/**
 * @azure-client-engine/cli — programmatic entry.
 *
 * Re-exports the command runner so the pipeline can be driven in tests or
 * embedded in other Node programs without spawning the bin. The actual
 * argv/stdout/exit-code wiring lives in `cli.ts`.
 */

export { run } from "./cli.js";
