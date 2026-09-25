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
`Agent.Session` is created through `StartIndependentRootActivity`; command
correlation is metadata, not a W3C parent or a hierarchy handoff.
The explicit-parent and independent-root service APIs return
`TelemetryActivityScope`. Operations retain that wrapper for ambient restoration.
Session retains the handle across layer completion of its `.Activity`, then
disposes the handle in the caller flow to restore ambient correctly.

`AgentRuntimeLifetime` exists before SDK creation. Client/session cleanup is
registered before creation can return or fail. The business outcome is frozen
before cleanup begins, and the layer's shared drain/file budgets govern shutdown.
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
remain admitted during drain. The layer fences late creation and owns abandoned
records at its deadline; the context never forcibly stops another thread's span.

Tools receive an explicit activity-creation delegate. `ToolInvocationLog` retains
its stopwatch, local queue, arguments/results, and idempotent disposal behavior,
but has no global telemetry service locator or source.

## Resource cache

Only the Agent host attaches `ArmCacheRefresher` to a layer lifetime. Its task
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

## Validation

Use the explicitly selected offline Agent test classes with
`-p:CopilotSkipCliDownload=true`; do not run the whole assembly without reviewing
its live tests. The SDK fixtures use loopback JSON-RPC peers, never a real Copilot
daemon. HTTP/auth fixtures are in-memory fakes. Layer tests write only under
the test output directory's `TestArtifacts` folder.
