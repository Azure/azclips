# CI Plan

Having a quick and reliable Continuous Integration (CI) system is essential for maintaining code quality and preventing regressions. This document outlines the CI plan for our project.

## Criteria for CI

- The code builds.
- All tests pass.
- E2E tests pass.

## Notes

- Refer to [README.md](../../README.md) for detailed build and test instructions.
- E2E tests are to simply run the commands and check for successful completion.
    - For CLI flavor, run `az --help` and ensure it exits with code 0.
    - For PowerShell flavor, import the Accounts module and run `Get-Command -Module Accounts` to ensure commands are available.