# Agent telemetry ownership

The Agent executable and the four resource tool processors use the shared
`ITelemetryLayerScopeFactory` contract, not an Agent-owned `ActivitySource`.
The layer is `Agent`, its schema is `agent-v1`, and its provider and trace file
belong exclusively to the returned scope.

## Standalone Agent host

`AgentTelemetryHost` creates the scope lazily. A valid `TelemetryLayerEnvironment`
launch supplies its immutable invocation metadata and file ID. Malformed launch
data produces a local diagnostic and falls back to cached standalone routing,
without undoing a parent's telemetry opt-out. Routing initialization never
acquires an Azure token or calls ARM.

DI receives borrowed `ITelemetryProvider` and `ITelemetryService` views. Disposing
the container must not dispose or flush the layer provider a second time.
CLI ends its command span before starting `CLI.Activity.AiRecovery` under the
saved command context. Recovery remains in the CLI provider and file, with an
explicit copy of the command's routing metadata. Its W3C `traceparent` and
`tracestate` travel in the launch packet. `Agent.Session` uses that remote parent;
only a standalone session without a recovery parent starts an independent root.
`Agent.Create` and `Agent.Send` are session children, and callbacks use the active
operation or nested callback as their immediate parent.
The explicit-parent and independent-root service APIs return
`TelemetryActivityScope`. Operations retain that wrapper for ambient restoration.
Session retains the handle across layer completion of its `.Activity`, then
disposes the handle in the caller flow to restore ambient correctly.

`AgentRuntimeLifetime` exists before SDK creation. Client/session cleanup is
registered before creation can return or fail. The business outcome is frozen
before cleanup begins. Agent owns producer admission and the ten-second drain
deadline, which starts at shutdown, not when recovery starts. SDK/cache cleanup
and cancellation callbacks share that deadline. Core.Telemetry is not asked to
complete until Agent has drained those producers, or reports an incomplete owner.
Core then owns its separate two-second file-completion budget.
There is no separate host-side five-second session plus five-second client wait.
SDK session cleanup precedes client cleanup; the invocation context stays alive
until that actual cleanup finishes, even if the bounded host wait has ended.

## Operations, callbacks, and tools

`AgentInvocationContext` still owns the single operation slot and live turn CTS.
SDK callbacks bind at execution time, rather than keeping the `Create` context
captured by the SDK. Nested callbacks retain their immediate parent and token.
Every tool, hook, permission callback, and event acquires a real producer lease,
including callbacks that have not created an activity. A lease ends only after
the actual callback and its `finally` finish.

Hook bodies use the context's combined operation, callback, host, and lifetime
cancellation token. Tracked nested work and legitimate SDK teardown callbacks
remain admitted during drain. When the Agent deadline expires, Agent closes
producer admission and passes `allowPublication: false` to Core. Core keeps the
file unpublished, closes new activity admission and eventually releases accepted
writes and its provider. A late callback cannot restart publication. Neither
component forcibly stops another thread's span.

Tools receive an explicit activity-creation delegate. `ToolInvocationLog` retains
its stopwatch, local queue, arguments/results, and idempotent disposal behavior,
but has no global telemetry service locator or source.

## Resource cache

Only the Agent host attaches `ArmCacheRefresher` to its `IAgentProducerLifetime`.
This Agent-tools interface avoids a dependency on the Agent executable. Its task
registry includes outer kicks before tenant lookup and every started shared task,
including competing `GetOrAdd` factories. Closing and task admission share a gate.
A caller still cancels only its own wait; host shutdown cancels shared work.

`ResourceCache.GetOrCreateRefresher` returns the actual installed owner. Live
first-writer-wins registrations are reused; a closing Agent owner is not reused
by a later host. CLI refreshers remain detached and do not acquire an Agent host
lifetime.

## Direct CLI resource tools

Find, tree, show, and SKU processors share `AgentProcessorBase.RunToolCommandAsync`.
They capture the current command's already-resolved routing/correlation metadata
through `ICommandTelemetryContext`. They never read inherited Agent launch IDs.
Their common creator uses `StartIndependentRootActivity(..., activate: false)`,
so the tool timer is an Agent root while business auth/profile work keeps the
original CLI ambient parent.

The helper completes and disposes its scope in an async `finally` before an
exception can reach CLI fallback. Help, environment, and refresh processors do
not gain tool telemetry scopes.

## File claiming and replay

CLI creates an invocation reservation as soon as its command root exists, before
startup or command children can export. The same invocation ID is reused by
recovery and the Agent file. After the Agent exits and the CLI provider closes,
CLI marks the reservation ready and includes the Agent `.log` only if publication
finished. An Agent `.partial` is never uploaded as a complete file.

Reservation mutations and file claiming share a cross-process `.upload.lock`.
The lock file stays on disk; deleting it could create two independently locked
files. A separate `.uploader.lock` spans the actual upload, so stale-claim recovery
cannot steal files from a live, slow uploader. Lock contention is bounded and
diagnosed; it does not change the command's business exit code.

Uploader keeps the reserved file set together on export failure and retries it
after abandoned-claim recovery. Replay uses parent-first ordering across service
and profile groups, with separate SpanId maps for each target and data boundary.
EU and Global never share a map or an ingestion route. An already delivered group
may be exported again on retry; this is not exactly-once delivery or a durable
mapping across uploader runs.

## Validation

Use the explicitly selected offline Agent test classes with
`-p:CopilotSkipCliDownload=true`; do not run the whole assembly without reviewing
its live tests. The SDK fixtures use loopback JSON-RPC peers, never a real Copilot
daemon. HTTP/auth fixtures are in-memory fakes. Layer tests write only under
the test output directory's `TestArtifacts` folder.
