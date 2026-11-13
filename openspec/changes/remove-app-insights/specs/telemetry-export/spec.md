# Spec Delta

## Purpose

Define product telemetry destinations and benchmark artifact behavior while excluding App Insights from supported export paths.

## ADDED Requirements

### Requirement: Remote telemetry uses Aria only
The system SHALL export Remote product telemetry through Aria/OneCollector and SHALL NOT export product traces to App Insights.

#### Scenario: Live remote export
- **WHEN** an enabled live provider selects Remote
- **THEN** only Aria handles remote export and each activity retains its regional routing

#### Scenario: Recorded remote export
- **WHEN** the uploader replays a group with a recorded EU or ROW region
- **THEN** Aria uses that recorded region without consulting the current profile

### Requirement: Local tracing and telemetry controls are preserved
The system SHALL preserve Local OTLP export, trace buffering/replay, privacy filtering, target defaults, and the existing telemetry opt-out.

#### Scenario: Local target
- **WHEN** an enabled Debug build selects Local
- **THEN** traces use the existing local OTLP endpoint and no remote exporter is registered

#### Scenario: Telemetry disabled
- **WHEN** telemetry.enabled is false
- **THEN** telemetry collection and export remain disabled according to the existing client gates

### Requirement: Benchmark results remain local artifacts
The benchmark runner SHALL retain charts, summaries and sampled command traces without uploading aggregated results to App Insights or failing on a removed upload operation.

#### Scenario: Default benchmark run
- **WHEN** a benchmark completes without --local or --ci
- **THEN** local artifacts are generated without an App Insights upload

#### Scenario: Compatible local and CI flags
- **WHEN** a caller supplies --local, -l or --ci
- **THEN** existing invocation compatibility is retained and --ci selects the existing stable artifact path
