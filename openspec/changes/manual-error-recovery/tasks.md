## 1. Configuration

- [x] 1.1 Recovery keys as plain leaf keys in `AgentOptions.RecoveryOptions`; config system unchanged.
- [x] 1.2 Retired keys removed at Agent start (`RetiredAgentKeys`).
- [x] 1.3 Keyed `IConfigurationChangeHandler`, `AzclipsServiceProvider.GetKeyedService<T>`, and Config set/delete notification with key canonicalization and parent-key lookup.

## 2. History and key storage

- [x] 2.1 Encrypted store: `history.bin`, `control.json`, `history.key`, `history.lock`; schema version 1.
- [x] 2.2 Save trims to the `historySize` passed by the CLI; clear keeps the sequence counter.
- [x] 2.3 Windows DPAPI, macOS no-UI Keychain, Linux Secret Service over Tmds.DBus.Protocol.
- [x] 2.4 Linux locked keyring is unavailable, not missing; plaintext latest-only fallback with a warning.

## 3. CLI and Agent

- [x] 3.1 Original error first; capture within two seconds; off decided from configuration only.
- [x] 3.2 Off hints (at most 15) and manual hints.
- [x] 3.3 `az troubleshoot analyze`, `list`, `show --id`.
- [x] 3.4 Manual and automatic recovery share one pipe-based Agent flow.

## 4. Validation

- [x] 4.1 Cli.Tests, Core.Agent.Tests, Core.Telemetry.Tests pass.
- [x] 4.2 Tests: original error before AI plan; failure text only in the pipe (fake Agent).
- [x] 4.3 Linux keyring behavior checked in Docker.
- [x] 4.4 Windows package: agent tool help, troubleshoot list, hint command, off and parent-key delete clear history.
