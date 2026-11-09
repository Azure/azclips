# Contributing to AzCLIPS

Thank you for your interest in AzCLIPS, which brings together Azure CLI and Azure PowerShell. This project is currently in preview.

Maintainers review proposed contributions for scope and alignment with project goals.

## Before You Start

- Review [README.md](README.md) and [SECURITY.md](SECURITY.md).
- For substantial changes, open or link an issue before starting implementation so maintainers can align on scope.
- Do not include secrets, customer data, internal-only information, or private service details in issues, pull requests, tests, logs, or documentation.

## Issues and Feedback

Use GitHub issues for bugs, questions, and product feedback unless the report contains security-sensitive information. For substantial features or behavior changes, open a proposal issue before implementation.

## Contributor License Agreement

Contributions may require signing the [Microsoft Contributor License Agreement (CLA)](https://cla.microsoft.com/) before they can be merged. A CLA bot or maintainer will provide instructions when a pull request requires CLA confirmation.

## Development Setup

Install the .NET SDK version pinned in [global.json](global.json), then build locally:

```bash
dotnet build azclips.slnx
```

Run the tests relevant to your changes. For example, to run the CLI tests:

```bash
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
- Use the [pull request template](.github/pull_request_template.md) and follow the PR title and release-note requirements below.
- Describe the user impact, implementation approach, and validation performed.
- Link related issues, specs, or design notes when available.
- Add or update tests for new behavior and bug fixes.
- Update documentation when behavior, setup, support, or security guidance changes.
- Prefer Conventional Commit style for commit messages only, when practical, such as `feat(auth): add token cache` or `fix(cli): handle empty command input`. PR titles must use the component-prefix format below instead.

### Release Note Guidance

Release notes are generated from pull request metadata during stable release preparation.

- Customer-facing PR titles must start with `[Component Name]`, for example `[CLI] Add resource group list output`.
- Non-customer-facing PR titles must start with `{Component Name}`. This includes internal, test-only, docs-only, and release-process changes, for example `{Docs} Update contribution guidance`.
- PR titles must not end with trailing punctuation or trailing spaces.
- Use the `History Notes` section in the PR description when one PR needs multiple release-note entries or when the PR title is only a summary.
- Write history notes as `[Component Name] message`; for example, ``[CLI] `az group list`: Add table output``.
- History note lines must not end with trailing punctuation or trailing spaces.
- Mark breaking changes with `BREAKING CHANGE:` so they are visible during release review.

## Reviews and Maintainer Expectations

Maintainers may ask for design changes, additional tests, dependency updates, or security review before merging. Changes that affect public command behavior, authentication, telemetry, AI behavior, packaging, licensing, or release process require extra care and may need broader review.

## Reporting Security Issues

Do not report security vulnerabilities through public GitHub issues. Follow [SECURITY.md](SECURITY.md) instead.
