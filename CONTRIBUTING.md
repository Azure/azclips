# Contributing to Azure CLI and PowerShell

Thank you for your interest in Azure CLI and PowerShell. This project is currently in preview and is being prepared for broader open source collaboration. Contribution expectations may evolve as the repository moves from private preview to public release.

During private preview, maintainers may limit pull request acceptance to scoped fixes, documentation updates, and changes aligned with preview goals.

## Before You Start

- Review [README.md](README.md) and [SECURITY.md](SECURITY.md).
- For substantial changes, open or link an issue before starting implementation so maintainers can align on scope.
- Do not include secrets, customer data, internal-only information, or private service details in issues, pull requests, tests, logs, or documentation.

## Issues and Feedback

Use GitHub issues for bugs, questions, and product feedback unless the report contains security-sensitive information. For substantial features or behavior changes, open a proposal issue before implementation.

## Contributor License Agreement

Contributions may require signing the Microsoft Contributor License Agreement (CLA) before they can be merged. A CLA bot or maintainer will provide instructions when a pull request requires CLA confirmation.

## Development Setup

Install the .NET SDK version pinned in [global.json](global.json), then build and test locally:

```bash
dotnet build azclips.slnx
dotnet test tests/Cli.Tests/Cli.Tests.csproj
```

Use the repository conventions in [AGENTS.md](AGENTS.md) for coding style, project layout, tests, and module boundaries.

### Configuration Schema Validation

A configuration value key must not also be a parent namespace of another value key.
For example, `something.configkey` and `something.configkey.child` conflict; use
`something.configkey.value` and `something.configkey.child` instead. Namespace-only
parents and sibling keys are valid. Comparisons are case-insensitive and respect
dot-separated segment boundaries.

[ConfigurationSchemaLintTests](tests/Cli.Tests/Configuration/ConfigurationSchemaLintTests.cs)
checks the complete generated schema in the existing CI unit-test suite. Failures
identify both conflicting keys. This is a test-only check, not runtime validation;
it does not inspect saved configuration or unknown keys written with `--force`.
Run it locally before submitting new configuration keys:

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter FullyQualifiedName~ConfigurationSchemaLintTests
```

## Pull Requests

- Keep pull requests focused and small enough to review confidently.
- Describe the user impact, implementation approach, and validation performed.
- Link related issues, specs, or design notes when available.
- Add or update tests for new behavior and bug fixes.
- Update documentation when behavior, setup, support, or security guidance changes.
- Prefer Conventional Commit style in commit messages when practical, such as `feat(auth): add token cache` or `fix(cli): handle empty command input`.

### Release Note Guidance

Private preview releases ship weekly, and release notes are generated from pull request details during release preparation.

- Start customer-facing PR titles with `[Component Name]`, for example `[CLI] Add resource group list output`.
- Start non-customer-facing PR titles with `{Component Name}`, for example `{DevOps} Update release pipeline validation`.
- Use the `History Notes` section in the PR description when one PR needs multiple release-note entries or when the PR title is only a summary.
- Write history notes as `[Component Name] message`; for example, ``[CLI] `az group list`: Add table output``.
- Mark breaking changes with `BREAKING CHANGE:` so they are visible during release review.

## Reviews and Maintainer Expectations

Maintainers may ask for design changes, additional tests, dependency updates, or security review before merging. Changes that affect public command behavior, authentication, telemetry, AI behavior, packaging, licensing, or release process require extra care and may need broader review.

## Reporting Security Issues

Do not report security vulnerabilities through public GitHub issues. Follow [SECURITY.md](SECURITY.md) instead.
