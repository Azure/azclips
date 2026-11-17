# Core.Agent.Tests

Unit and offline integration tests for the Agent layer. By default they run with no credentials,
no network, and no Copilot daemon. `AgentToolsLiveTests` are the exception: they need an installed
package and are skipped by default.

## Shared test infrastructure

Reuse the shared helpers before you write a new fake. If a shared helper lacks a member your test
needs, add that member to the shared helper instead of writing a private copy.

`Infrastructure/` holds the Agent-specific helpers:

| File | Contents |
| ---- | -------- |
| `OfflineCopilotServer.cs` | Loopback JSON-RPC server that the real Copilot SDK connects to in place of the daemon. It supports scripted turns, tool calls, injected events (`EmitAsync`), and capture of the `session.create` options. The test answers `session.model.getCurrent` through the `modelQuery` constructor argument. A request for an unknown method gets JSON-RPC error -32601 and is recorded as a fault; `OfflineChatAgent.RunAsync` checks recorded faults automatically. |
| `OfflineChatAgent.cs` | `OfflineChatAgent.RunAsync` connects a real `ChatAgent` to an `OfflineCopilotServer`, runs the test body, then disposes the agent. |
| `AgentOfflineServices.cs` | `AgentOfflineHttp` (`HttpMessageHandler` that rejects unconfigured requests) and `SilentRenderer` (`IAgentRenderer` that discards output). |
| `AgentTelemetryTestSupport.cs` | `AgentLayerTestHarness` (creates `Agent` layer telemetry scopes; `RunAsync` runs the test body and then waits for their release), `AgentTraceCapture`, and `AgentTestConfiguration`. |
| `TroubleshootTestSupport.cs` | `TroubleshootTestSupport` helpers, the `FakeHistory` failure-history store, `FakeConfiguration`, and the troubleshoot test collection definition. |

`tests/Common` holds the helpers shared with other test projects:

- `Stubs/StubAuthService` and `Stubs/StubProfileService`. For per-test behavior, write a small
  subclass that overrides a virtual member. To fail a test that must not authenticate, override
  `AcquireTokenAsync` to throw.
- `Utilities/SignalingFakeTimeProvider`, a `FakeTimeProvider` that reports each timer it arms.

`SnapshotTesting/` holds the snapshot comparison helpers.

## Run a ChatAgent against the fake daemon

```csharp
await using var server = new OfflineCopilotServer([[usageEvent, idleEvent]]);
await OfflineChatAgent.RunAsync(server, async agent =>
{
    await agent.Agent.SendAsync("prompt", CancellationToken.None);
}, initializeModel: false);
```

## Rules

- Drive timeouts and budgets with `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`
  where the production code accepts a `TimeProvider`. Use `SignalingFakeTimeProvider` to wait until
  the code under test has armed its timer before you advance the clock. Do not assert on real elapsed
  time, except for a coarse upper bound that guards timeliness, such as "returns within 2 seconds".
- Create `Agent` layer scopes inside `AgentLayerTestHarness.RunAsync`: `CreateScope` for the first
  scope and `Own` for any further scope. After the body, the harness waits until each scope releases
  the layer telemetry source, so the next test can create its own scope.
  If a test needs its own
  `TelemetryLayerScopeFactory`, release each scope with `AgentLayerTestHarness.ReleaseAsync`.
