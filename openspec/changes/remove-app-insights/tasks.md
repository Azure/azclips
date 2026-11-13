# Tasks

## 1. Runtime export

- [x] 1.1 Remove Azure Monitor registration, connection string and Core/PowerShell SDK references; verify Remote has only an Aria exporter and Local remains OTLP with targeted telemetry tests.
- [x] 1.2 Update telemetry and observability documentation; verify no current runtime instructions reference the removed destination.

## 2. Benchmark export

- [x] 2.1 Remove App Insights exporter, direct SDK reference, invocation and upload status handling while preserving flags and local artifacts; verify Benchmark builds and discovery runs with default, --local, -l and --ci inputs. Retain the user-approved PowerShell native indirect dependency.
- [x] 2.2 Update benchmark documentation and diagrams; verify they describe local summaries and traces without upload instructions.

## 3. Integration

- [x] 3.1 Build the solution and run affected telemetry, CLI, cmdlet and Agent tests; verify Aria routing and replay remain intact. Solution build passed; Core.Telemetry 576, CLI telemetry 63, cmdlet telemetry 13 and Agent telemetry 87 tests passed. Agent tests excluded Category=LocalOnly.
- [x] 3.2 Inspect restored package graphs and shipping references for removed SDKs, check remaining mentions, and run git diff --check; distinguish the approved PowerShell native dependency, OpenSpec history and officially generated NOTICE from executable remnants. Core/CLI have no SDK dependencies; Azure Monitor is absent from every checked graph and rebuilt Debug payload. Only the approved PowerShell native App Insights dependency remains. No current code/documentation integration mentions remain; whitespace and strict OpenSpec validation passed.

## Workflow follow-up

- Refresh NOTICE through the existing official CG stable-release workflow.
