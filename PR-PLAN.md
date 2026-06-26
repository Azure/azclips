# PR Plan

This document tracks logging behavior validation and follow-up work for this PR.

## Testing Results

Checklist:

- [x] Respects config level set in user config (command CLI)
- [x] Respects config level set in user config (help CLI)
- [x] Respects config level set in user config (command PWSH)
- [ ] Respects config level set in user config (get-help PWSH) (wont fix: PowerShell Get-Help is host help/metadata retrieval, not our cmdlet execution path).
- [x] Env vars override user config (command CLI)
- [x] Env vars override user config (help CLI)
- [x] Env vars override user config (command PWSH)
- [ ] Env vars override user config (get-help PWSH) (wont fix: PowerShell Get-Help is host help/metadata retrieval, not our cmdlet execution path).
- [x] `-debug` overrides all (command CLI)
- [x] `-debug` overrides all (help CLI)
- [x] `-debug` overrides all (command PWSH)
- [ ] `-debug` overrides all (get-help PWSH) (wont fix: PowerShell Get-Help is host help/metadata retrieval, not our cmdlet execution path).

## Scope Notes

- PowerShell `Get-Help` is out of scope for project logging. It does not execute our cmdlet runtime path.
- CLI `help` remains on a separate discovery/session path, but now reuses the same logging policy/streams as normal command execution.

## Follow-Up

- Default logging to error.
