# Tasks

## 1. Unified storage paths

- [x] 1.1 Resolve trace and boundary-cache paths and startup profiles from the Azclips home; verify default-root, override, and persisted-cache regression tests.
- [x] 1.2 Update telemetry storage documentation with the new paths and no-migration policy; verify documented paths match the shared helpers.

## 2. Integration validation

- [x] 2.1 Run the telemetry suite and relevant CLI regression tests, then build the solution; record exact commands and results.
- [x] 2.2 Validate OpenSpec artifacts and check whitespace; verify pre-existing Benchmark edits are discarded as subsequently requested by the user.

## Validation Results

- `dotnet test tests\Core.Telemetry.Tests\Core.Telemetry.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TraceDirectoryProviderTests|FullyQualifiedName~DataBoundaryResolverTests"`: passed, 41 tests.
- `dotnet test tests\Core.Telemetry.Tests\Core.Telemetry.Tests.csproj --no-build --no-restore -v minimal`: passed, 560 tests.
- `dotnet test tests\Cli.Tests\Cli.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~AgentFallbackSkipTelemetryTests|FullyQualifiedName~CliTelemetryProviderTests|FullyQualifiedName~TelemetryUploaderSpawnTests|FullyQualifiedName~ProfileServiceTests" -v minimal`: passed, 74 tests.
- `dotnet build src\CLI\Core.Cli.csproj --no-restore -v quiet`: passed.
- `dotnet build azclips.slnx --no-restore -v quiet`: initially blocked by pre-existing Benchmark access changes; passed with 5 existing warnings after those changes were discarded at the user's request.
- `dotnet .\artifacts\Debug\Core.Cli\az.dll config get --key clients.cli.output.format --output json`: passed with an isolated `AZCLIPS_HOME`, profile, and EU boundary cache; all 11 spans were written under that home's `traces` directory, with the expected root tenant and boundary. Isolated files were removed afterward.
- `npx --yes @fission-ai/openspec validate unify-telemetry-storage-paths --strict`: passed.
- `git diff --check`: passed.
