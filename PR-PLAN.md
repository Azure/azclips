# PR Plan

This document tracks logging behavior validation and follow-up work for this PR.

## Testing Results

Checklist:

- [x] Respects config level set in user config (command CLI)
- [ ] Respects config level set in user config (help CLI) (fail: doesnt show any logs).
- [x] Respects config level set in user config (command PWSH)
- [ ] Respects config level set in user config (get-help PWSH) (fail: doesnt show any logs, not sure if Get-Help is distinct).
- [x] Env vars override user config (command CLI)
- [ ] Env vars override user config (help CLI) (fail: doesnt show any logs).
- [x] Env vars override user config (command PWSH)
- [ ] Env vars override user config (get-help PWSH) (fail: doesnt show any logs, not sure if Get-Help is distinct).
- [x] `-debug` overrides all (command CLI)
- [ ] `-debug` overrides all (help CLI) (fail: doesnt show any logs).
- [x] `-debug` overrides all (command PWSH)
- [ ] `-debug` overrides all (get-help PWSH) (fail: doesnt show any logs, not sure if Get-Help is distinct).

## Follow-Up

- Default logging to error.
