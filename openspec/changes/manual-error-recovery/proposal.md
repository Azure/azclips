## Why

Users need to decide when to ask AI to diagnose a CLI failure, without losing the
original command and error text. Users who keep recovery off should still learn
that the feature exists.

## What Changes

- Add `az troubleshoot analyze`, `list`, and `show --id`, backed by a local,
  encrypted, per-user failure history (default ten records).
- Add `agent.recovery.trigger=off|manual|auto`, `agent.recovery.style=oneshot|interactive`,
  and `agent.recovery.historySize`, defaulting to off, oneshot, and 10.
- **BREAKING**: remove `agent.enabled`, `agent.sessionStyle`, and `agent.builtin.endpoint`.
  The Agent deletes them from the configuration file when it starts; their values
  are not migrated.
- Setting `agent.recovery.trigger` to off, or deleting it or a parent key, clears the
  history through a keyed configuration-change handler.
- Manual and automatic recovery send the same failed command and error text to the
  same Agent flow. The original error is always shown before any AI output.

## Capabilities

### New Capabilities

- `failure-recovery`: failure capture, protected history, and deliberate diagnosis.

### Modified Capabilities

None.

## Impact

- New recovery code in Core.Abstractions, Core.Authentication, Core.Runtime, CLI, and Agent.
- Config system code is unchanged. The Config commands call a change handler after a
  successful write; `AzclipsServiceProvider` gains `GetKeyedService<T>`.
- New dependency: Tmds.DBus.Protocol (Linux Secret Service without unlock prompts).
