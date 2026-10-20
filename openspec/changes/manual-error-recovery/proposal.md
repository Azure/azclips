## Why

Users need to decide when to ask AI to diagnose a CLI failure without losing the
original diagnostic input. The existing draft implementation must be aligned with
the reviewed trigger/style design while preserving automatic recovery and the
existing shell completion experience.

## What Changes

- Add `az troubleshoot`, exact history-ID selection, local listing/detail, and
  dynamic ID candidates through the existing shell completion implementations.
- Preserve the complete existing failed-command and error-text inputs in bounded,
  protected, per-user history; default to ten records.
- Separate `agent.recovery=off|manual|auto` from
  `agent.recovery.style=oneshot|interactive`, defaulting to off/oneshot.
- **BREAKING**: remove `agent.enabled` and `agent.sessionStyle` from stored configuration
  without migrating their values; recovery stays off until `agent.recovery` is set.
- Make manual and automatic diagnosis use the same Agent input, prompt, effective
  style, tools, and approval policy.
- Make off/reset, concurrent writers, readable ID allocation, and old draft
  history handling explicit and failure-safe.
- Bound background work on the original command's failure path and preserve
  existing error output, exit status, telemetry routing, and completion behavior.

## Capabilities

### New Capabilities

- `failure-recovery`: configurable failure capture, protected history, deliberate
  diagnosis, and compatibility with existing recovery and shell completion.

### Modified Capabilities

None. There are no published OpenSpec capability files for this functionality.

## Impact

- Core configuration metadata/binding and existing Config CLI/PowerShell hosts.
- Recovery contracts, protected key storage, runtime history persistence, and CLI
  local dispatch/formatting/completion.
- Existing Agent recovery transport, session settings, tools, hooks, and telemetry.
- Targeted offline tests and directly related CLI/configuration/help documentation.
- Continue the existing draft PR #934 against current main. Do not introduce an
  additional AI agent, a persistent service, a completion engine, plan adoption
  tracking, or PowerShell recovery commands.
