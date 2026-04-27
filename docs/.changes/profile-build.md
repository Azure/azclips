# Profile Commands and Storage Scaffold

Date: 2026-04-27

## Summary

Added initial profile command wiring and profile storage scaffolding for `azv2`.

## Changes Made

- Added CLI profile routing to Login extension (`profile` -> `Login`).
- Added CLI profile command metadata and handlers:
  - `profile list`
  - `profile active`
  - `profile set --name <profile>`
- Added profile model and service APIs:
  - save/get/list profiles
  - set/get active profile
- Added profile persistence scaffolding:
  - profile files
  - active profile pointer
  - legacy context fallback retained

## Validation

- Solution builds successfully.
- CLI tests pass (existing matrix scaffolds remain skipped by design).
- `azv2 profile list` now routes and executes.

## Known Gap

- CLI output formatting is still raw object printing for profile list.
  Command executes, but display formatting is not yet user-friendly.
