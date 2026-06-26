# PR Plan

This document tracks logging behavior validation and follow-up work for this PR.

## Testing Results

Checklist:

- [x] Respects config level set in user config (command CLI)
- [ ] Respects config level set in user config (help CLI) (fail: doesnt show any logs).
- [x] Respects config level set in user config (command PWSH)
- [ ] Respects config level set in user config (get-help PWSH) (wont fix: PowerShell Get-Help is host help/metadata retrieval, not our cmdlet execution path).
- [x] Env vars override user config (command CLI)
- [ ] Env vars override user config (help CLI) (fail: doesnt show any logs).
- [x] Env vars override user config (command PWSH)
- [ ] Env vars override user config (get-help PWSH) (wont fix: PowerShell Get-Help is host help/metadata retrieval, not our cmdlet execution path).
- [x] `-debug` overrides all (command CLI)
- [ ] `-debug` overrides all (help CLI) (fail: doesnt show any logs).
- [x] `-debug` overrides all (command PWSH)
- [ ] `-debug` overrides all (get-help PWSH) (wont fix: PowerShell Get-Help is host help/metadata retrieval, not our cmdlet execution path).

## Scope Notes

- PowerShell `Get-Help` is out of scope for project logging. It does not execute our cmdlet runtime path.
- CLI `help` remains in scope and appears to bypass the normal execution/session path where logging policy is applied.

## Follow-Up

- Default logging to error.
