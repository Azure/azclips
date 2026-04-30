# Changelog

All notable changes to this project will be documented in this file.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- **Tab autocomplete (experimental, Bash only).** New standalone executable
  [`src/Autocomplete`](src/Autocomplete) → `azv2-autocomplete`, invoked directly
  by a Bash completion hook on `<Tab>`. Renders an interactive menu of suggested
  next tokens. Uses a dedicated, separately-configured AI agent
  ([`src/Autocomplete/Agent/AutocompleteAgent.cs`](src/Autocomplete/Agent/AutocompleteAgent.cs))
  so completion can run on a smaller, lower-latency model (e.g. `gpt-4.1-mini`)
  without affecting the main command agent. See the
  "Tab Autocomplete (Experimental)" section in [README.md](README.md).
- Spinner rendered on the line below the prompt while the autocomplete agent
  is in flight, written to `/dev/tty` so it remains visible while Bash captures
  stdout for `COMPREPLY`.
- New tests project [`tests/Autocomplete.Tests`](tests/Autocomplete.Tests)
  covering `SuggestionParser` and `CompletionClassifier`.
- Repository-wide error-handling rule added to [AGENTS.md](AGENTS.md):
  errors must be surfaced (logged or translated to a typed result), never
  swallowed silently.

### Changed
- Suppressed `NU1902` (GHSA-88hf-wf7h-7w4m) locally in
  `src/Core/Core.Telemetry/Core.Telemetry.csproj` so the solution can build.
  The Zipkin exporter is a Debug-only dev dependency; upgrading the
  OpenTelemetry stack is owned by another team and tracked separately.
