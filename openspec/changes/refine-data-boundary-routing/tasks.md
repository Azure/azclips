# Tasks

## 1. Resolution and policy

- [x] 1.1 Add typed outcomes and a central routing policy; verify all fallback and confirmed-boundary combinations in unit tests.
- [x] 1.2 Classify tenant, token, and boundary failures and reject invalid cache values; verify resolver tests including cancellation and cache behavior.
- [x] 1.3 Document fallback defaults and diagnostic behavior in the telemetry specification.
- [x] 1.4 Remove obsolete ambient-profile exporter routing and duplicate internal wrappers, update routing comments, and verify pure exporter selection and public compatibility tests.

## 2. Routing propagation

- [x] 2.1 Wire CLI, Engine, and PowerShell import to structured results; verify host and execution-session tests.
- [x] 2.2 Persist outcomes through file export, managed layers, and recovery; verify serialization, inheritance, and context tests.
- [x] 2.3 Partition replay by boundary and outcome and use the same policy as dynamic Aria routing; verify live and replay destination tests and legacy compatibility.

## 3. Integration validation

- [x] 3.1 Run targeted telemetry, engine, CLI, and cmdlet tests and record results.
- [x] 3.2 Build azclips.slnx with the pinned SDK and validate OpenSpec artifacts; record any environment blockers.

## Validation results

- SDK: 10.0.403, selected by global.json's latestMinor roll-forward.
- Telemetry: 518 passed. Command: `dotnet test tests\Core.Telemetry.Tests\Core.Telemetry.Tests.csproj --no-restore -p:CustomAfterMicrosoftCommonTargets=C:\Users\norizhang\.copilot\session-state\da0c4060-e9a1-4cad-88e4-3a37a0f0ba82\files\routing-test-exclusions.targets --verbosity minimal`.
  A temporary target excluded only the pre-existing locally modified TelemetryPropertyFilterProcessorTests.cs, which references missing TelemetryTags.Command and ModuleName. The temporary target was removed after validation; the user's file was not changed.
- Engine: 5 passed. Command: `dotnet test tests\Core.Engine.Tests\Core.Engine.Tests.csproj --no-restore --filter FullyQualifiedName~ExecutionSessionSeamTests --verbosity minimal`.
- CLI: 37 passed. Command: `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliTelemetryProviderTests|FullyQualifiedName~AgentFallbackSkipTelemetryTests|FullyQualifiedName~AgentFallbackOrderingTests" --verbosity minimal`.
- PowerShell import: 10 passed. Command: `dotnet test tests\Cmdlets.Tests\Cmdlets.Tests.csproj --no-restore --filter FullyQualifiedName~ImportTelemetryTests --verbosity minimal`.
- Agent: 37 passed. Command: `dotnet test Agent\tests\Core.Agent.Tests\Core.Agent.Tests.csproj --no-restore --filter "FullyQualifiedName~AgentTelemetryTests|FullyQualifiedName~AgentStartupBoundaryTests|FullyQualifiedName~AgentToolCommandScopeTests|FullyQualifiedName~BuiltinEndpointResolverTests" --verbosity minimal`.
- Solution build: `dotnet build azclips.slnx --no-restore --verbosity minimal` failed with five Benchmark accessibility errors. The pre-existing local Core.Telemetry.csproj modification removes InternalsVisibleTo for Benchmark; it was preserved. Affected product projects compiled during the test/build runs.
- OpenSpec: `npx --yes @fission-ai/openspec validate refine-data-boundary-routing --strict` passed with OPENSPEC_TELEMETRY=0.
- Whitespace: `git diff --check` passed.

## Cleanup validation

- Removed the unused active-profile exporter lookup and the duplicate internal boundary-only wrapper. Public compatibility APIs remain available.
- Rebuilt telemetry cleanup tests: 83 passed, using the same temporary exclusion for the unchanged locally broken TelemetryPropertyFilterProcessorTests.cs.
- Rebuilt Agent compatibility tests: 25 passed with filter `FullyQualifiedName~AgentStartupBoundaryTests|FullyQualifiedName~BuiltinEndpointResolverTests`.
- Routing comments now describe the three fallback scenarios, recorded replay metadata, and independent Agent model routing.
