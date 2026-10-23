## Context

Recovery adds a local failure history and a manual entry point to the existing
automatic Agent flow. The configuration system stays as in main; recovery keys are
ordinary leaf keys under `agent.recovery`.

## Goals / Non-Goals

**Goals:**

- Independent trigger and style, defaulting to off and oneshot.
- Keep the complete failed command and error text for diagnosis.
- Show the original error first in every mode.
- No extra work on the command path while recovery is off.

**Non-Goals:**

- No new model, SDK, background service, completion engine, or PowerShell recovery commands.
- No dynamic history-ID completion.

## Decisions

### Configuration

`agent.recovery.trigger`, `agent.recovery.style`, and `agent.recovery.historySize`
are plain keys declared in `AgentOptions.RecoveryOptions`. Allowed values and the
minimum size are enforced by the existing schema attributes.

The retired keys `agent.enabled`, `agent.sessionStyle`, and `agent.builtin.endpoint`
are removed by `RetiredAgentKeys` when the Agent starts. It lists the stored
configuration and deletes any of the three that exist. Failures are ignored and
time-boxed to one second. Ordinary commands never delete configuration.

### Clearing history on off

The Config `set` and `delete` commands call `NotifyChangeHandlersAsync` after the
write succeeds:

1. The key is canonicalized the same way as `ConfigurationService.CanonicalizeKnownKey`.
2. On delete, every schema key under the deleted prefix (`GetAllKeys()`) is also checked.
3. Each key is looked up with `AzclipsServiceProvider.GetKeyedService<IConfigurationChangeHandler>(key)`.

`RecoveryConfigurationChangeHandler` is registered for `agent.recovery.trigger` in
`Core.Runtime`. A value of off, or a delete (the default is off), clears the history.
If clearing fails, the command still succeeds and prints a warning that tells the
user to run the same off command again.

### History store

- Files: `history.bin` (encrypted records), `control.json` (next sequence),
  `history.key` (Windows), and `history.lock`.
- Save: take the lock, reserve the next sequence, write, and keep the latest
  `historySize` records. The CLI reads `historySize` once and passes it in.
- Clear: take the lock and delete records, key, and the plaintext fallback file.
  The sequence counter survives, so IDs are never reused.
- IDs: `<PID>-<yyyy>-<MMdd>-<HHmm>-<counter>`, UTC, parent-shell PID, counter of at least four digits.
- The record format has a schema version that starts at 1.

### Key storage

- Windows: DPAPI. macOS: Keychain with no-UI queries. Linux: Secret Service over
  Tmds.DBus.Protocol, with no activation, Unlock, or Prompt calls.
- On Linux, an empty search on a locked collection is treated as unavailable, not
  as a missing key.
- When key storage is unavailable, only the latest failure is saved, unencrypted,
  in a current-user-only file, and the CLI warns.

### Failure path

- Off is decided from configuration alone; no history file is opened.
- Capture has a two-second budget. Storage work that cannot be cancelled runs in
  a short-lived worker process over anonymous pipes.
- The original error is printed before capture and before any AI output.
- While recovery is off, a failure shows two hints that explain how to enable it,
  at most 15 times (counter in `agent/state.json`).

### Shared Agent flow

Manual and automatic recovery send the same `FailedCommand` and `ErrorText` through
the same anonymous pipe to the same Agent prompt, tools, and approvals. The failure
text is never placed in arguments or environment variables. Style comes from
configuration or a manual `--style` override.

## Validation

- Linux key storage was checked in Docker (Ubuntu noble, gnome-keyring, Xvfb, gcr).
  With a locked keyring, MSAL `WithLinuxKeyring` launched gcr-prompter and blocked
  read and write until a 30-second kill. The Tmds-only path returned in about
  145 ms (read) and 140 ms (write: unavailable), with no prompter.
- Automated tests assert that the original error precedes the AI plan and that the
  failure reaches the Agent only through the pipe, using a fake Agent process.

## Risks / Trade-offs

- Native key storage can be missing, locked, or slow: fall back to the latest
  failure in plaintext and warn; damaged existing keys remain errors.
- A crash between reserving a sequence and writing records can leave an ID gap,
  but never reuses an ID.
- Parent-shell association can be affected by wrapper processes and PID reuse.
