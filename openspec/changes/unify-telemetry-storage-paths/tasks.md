# Tasks

## 1. Unified storage paths

- [x] 1.1 Resolve trace and boundary-cache paths and startup profiles from the Azclips home; verify default-root, override, and persisted-cache regression tests.
- [x] 1.2 Update telemetry storage documentation with the new paths and no-migration policy; verify documented paths match the shared helpers.

## 2. Integration validation

- [x] 2.1 Run the telemetry suite and relevant CLI regression tests, then build the solution; record exact commands and results.
- [x] 2.2 Validate OpenSpec artifacts and check whitespace; verify pre-existing Benchmark edits are discarded as subsequently requested by the user.

## Validation Results

The results below cover the initial path-only implementation.

- `dotnet test tests\Core.Telemetry.Tests\Core.Telemetry.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TraceDirectoryProviderTests|FullyQualifiedName~DataBoundaryResolverTests"`: passed, 41 tests.
- `dotnet test tests\Core.Telemetry.Tests\Core.Telemetry.Tests.csproj --no-build --no-restore -v minimal`: passed, 560 tests.
- `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~AgentFallbackSkipTelemetryTests|FullyQualifiedName~CliTelemetryProviderTests|FullyQualifiedName~TelemetryUploaderSpawnTests|FullyQualifiedName~ProfileServiceTests" -v minimal`: passed, 74 tests.
- `dotnet build src\CLI\Core.Cli.csproj --no-restore -v quiet`: passed.
- `dotnet build azclips.slnx --no-restore -v quiet`: initially blocked by pre-existing Benchmark access changes; passed with 5 existing warnings after those changes were discarded at the user's request.
- `dotnet .\artifacts\Debug\Core.Cli\az.dll config get --key clients.cli.output.format --output json`: passed with an isolated `AZCLIPS_HOME`, profile, and EU boundary cache; all 11 spans were written under that home's `traces` directory, with the expected root tenant and boundary. Isolated files were removed afterward.
- `npx --yes @fission-ai/openspec validate unify-telemetry-storage-paths --strict`: passed.
- `git diff --check`: passed.

## 3. Persistence integration

- [x] 3.1 Inject persistence into boundary resolution and caching, and use the profile service for startup snapshots; verify mocked I/O, cancellation, failure logging, and custom-root isolation tests.
- [x] 3.2 Use injected trace paths for writers, reservations, and uploader startup; verify custom-root producer/reservation and child-environment tests, and update storage documentation.
- [x] 3.3 Wire CLI, PowerShell, Engine, and Agent callers; verify affected telemetry, CLI, cmdlet, and Agent tests and the solution build.

## Persistence Integration Validation

- `dotnet build azclips.slnx --no-restore -v quiet`: passed; final incremental build reported 0 warnings and 0 errors.
- `dotnet test tests\Core.Telemetry.Tests\Core.Telemetry.Tests.csproj --no-build --no-restore -v minimal`: passed, 569 tests.
- `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~TelemetryUploaderSpawnTests|FullyQualifiedName~AgentFallbackSkipTelemetryTests|FullyQualifiedName~CliTelemetryProviderTests|FullyQualifiedName~EngineSubscriptionTests|FullyQualifiedName~ProfileServiceTests" -v minimal`: passed, 76 tests.
- `dotnet test tests\Cmdlets.Tests\Cmdlets.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ImportTelemetryTests|FullyQualifiedName~PSTelemetryProviderTests|FullyQualifiedName~ConnectAccountCmdletTests" -v minimal`: passed, 37 tests.
- `dotnet test Agent\tests\Core.Agent.Tests\Core.Agent.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~AgentLaunchContextTests|FullyQualifiedName~AgentTelemetryProviderTests|FullyQualifiedName~AgentRuntimeLifecycleTests|FullyQualifiedName~ChatAgentModelTelemetryTests|FullyQualifiedName~AgentStartupBoundaryTests|FullyQualifiedName~AgentDataCleanupTests|FullyQualifiedName~AgentToolCommandScopeTests" -v minimal`: passed, 138 tests.
- Negative control: temporarily removing the tenant from `StandaloneAsync` made `Host_AwaitsInjectedProfileAndCacheBeforePublishingItsScope` fail at the injected-tenant assertion; the mutation was removed and the Agent tests passed again.
- Additional compatibility check: adding `FullyQualifiedName~PersistenceStoreTests` to the CLI selector yielded 102 passes and 2 failures. `FailedReplacementSurfacesErrorAndCleansTemporaryFile` expects `IOException`, but Windows returns `UnauthorizedAccessException`; `ConcurrentAccessNeverReadsPartialWrites` also fails with `UnauthorizedAccessException` in native replacement. Both failures reproduced with only those two tests selected. Their test source and `FilePersistenceStore` are unchanged in this integration.
- `npx --yes @fission-ai/openspec validate unify-telemetry-storage-paths --strict` and `git diff --check`: passed.
