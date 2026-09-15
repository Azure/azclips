## 1. Dependency suite

- [x] 1.1 Implement independent package metadata validation and automatic resolution/import scenarios; verify real local-package fixtures.
- [x] 1.2 Implement missing/incompatible package and loaded-version conflict scenarios; verify expected and unrelated error fixtures.
- [x] 1.3 Implement bounded child processes, isolated scratch state, cleanup, and dependency reports; verify timeout, cleanup, and report fixtures.
- [x] 1.4 Implement supported-runtime discovery, missing-framework, and older-PowerShell rejection probes; verify runtime fixtures and native diagnostics.

## 2. Pipeline integration

- [x] 2.1 Wire Dependencies before Install and update phase reports/publication semantics; verify pipeline fixtures including hard/soft failures.
- [x] 2.2 Add dependency fixtures to both CI entry points and document behavior and local usage; verify commands and configuration match the implementation.

## 3. Completion

- [x] 3.1 Run targeted dependency, lifecycle, smoke, and pipeline fixtures and inspect the resulting diff for unintended changes.
