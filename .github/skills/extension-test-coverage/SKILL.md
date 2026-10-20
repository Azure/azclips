---
name: extension-test-coverage
description: Add, update, or run the azclips extension command-surface test coverage report and maintain its attribution and publishing workflow.
---

# Extension test coverage report

Use this skill when asked to add, update, or run an azclips test-coverage visibility report that maps automated test signals to each public extension command surface.

Keep the report lightweight. It should provide useful visibility into likely test coverage, not attempt to be a complete static-analysis engine or a replacement for line coverage.

The goal is a Coverlet-style coverage matrix for command surfaces, not line coverage. The report should answer:

- Which extensions exist?
- Which CLI and PowerShell commands does each extension publicly expose through `ExtensionIndex`?
- Which automated test tiers have mapped signals for each command?
- Where are the gaps by extension, product surface, command, and test tier?

## Expected output

Produce both machine-readable and human-readable outputs:

- JSON: `artifacts/TestCoverage/extension-test-coverage.json`
- Markdown: `artifacts/TestCoverage/extension-test-coverage.md`
- Optional GitHub Pages/wiki-ready data file: `artifacts/TestCoverage/data.js`

The Markdown should include:

1. Summary totals by extension.
2. A command matrix grouped by extension.
3. Separate columns for CLI command, PowerShell command, unit tests, scenario tests, script scenario tests, docs presence, docs/help test analysis status, benchmark coverage, and notes.
4. Extension-level benchmark coverage for CLI and PowerShell benchmark classes, even when a benchmark cannot be safely mapped to a specific command.
5. A gaps section sorted by severity:
   - no mapped test signals in applicable tiers,
   - no scenario coverage where scenarios are applicable,
   - CLI-only or PS-only coverage,
   - docs/help missing,
   - benchmark missing.

Prefer conservative attribution. If a test cannot be confidently mapped to a command, include it in an `unmappedTests` section rather than claiming coverage or a confirmed gap.

## Scenario applicability policy

Assess ARM record/playback scenario applicability per command and execution path,
not by extension name alone. Local Config and Extension operations, and verified
MSAL-only Accounts or local profile operations, should focus on unit and benchmark
evidence: without ARM requests, ARM recordings are not an applicable requirement.
Accounts is not categorically ARM-free; login subscription/tenant resolution may
call ARM. Inspect the actual processor and service path before granting an exemption,
and record its reason. Lack of mapped scenarios alone is not evidence of inapplicability.

No ARM traffic does not prohibit non-ARM integration or scenario tests. Preserve
all real mapped scenario and script-scenario evidence, and assess any non-ARM
scenario requirements separately. An ARM-only exemption must not suppress those
tests or waive other applicable requirements.

For a verified inapplicable ARM scenario requirement, report `N/A` with a reason
(for example, "local configuration; no ARM calls"), not `no` or `missing-scenario`.
Exclude that requirement from scenario-gap counts, required-tier status, and any
applicable-tier scoring denominator; do not award coverage points for an exemption.
If applicability has not been established, label it as needing analysis rather
than assuming an exemption. Keep applicability distinct from mapped evidence.

This is the intended reporting policy. The current generator does not yet implement
applicability metadata, `N/A`, or applicability-aware gaps/scoring/status; updating
this skill alone does not change generated reports.

## Current source-based generator

The current implementation is `.github/scripts/generate-extension-test-coverage.py`.
It recognizes C# `RunCli`, `ExecuteCli`, `RunCliAsync`, and `ExecuteCliAsync`
calls, including receiver-qualified calls, literal argument lists/arrays,
and locally resolved expression-bodied or array helpers. Helper declarations
(including `string[]` parameters) are not test invocations. Unresolved calls
retain the actual helper name in the unmapped evidence.

CLI invocations with trailing positional values (such as `profile show prod`)
map to the longest registered command prefix at a token boundary. This applies
to unit tests, scenarios, referenced shell/PowerShell scenario scripts, and
benchmarks. PowerShell command names still require exact matching.
For test tiers, `no` or `Gap` means no mapped signal, not proof of no tests.

Run the attribution regressions before generating or publishing reports:

```powershell
python -B -m unittest discover -s .github\scripts -p test_extension_test_coverage.py
```

The GitHub Actions workflow runs these tests before report generation.

## Repository conventions to use

Use the azclips repository layout:

- Extension source: `src/Extensions/<ExtensionName>/`
- Extension command metadata: `src/Extensions/<ExtensionName>/ExtensionIndex.cs`
- PowerShell cmdlets: `src/PS/<ExtensionName>/Cmdlets/`
- Unit tests: `tests/*Tests/`
- Scenario tests: `tests/Scenario.Tests/`
- Pester docs sync tests: `tests/Cli.DocsSync.Tests/` and `tests/Ps.DocsSync.Tests/`
- Benchmarks: `tests/Benchmark/`
- Benchmark publishing model: `.github/workflows/benchmark.yml`

The authoritative public command list is each extension's `ExtensionIndex`, not test names, docs, or cmdlet files. Read command metadata from the built assemblies when possible. Fall back to source parsing only if reflection is not practical.

## Recommended implementation

Create a small .NET console project rather than a brittle text-only script. Suggested location:

- `tools/ExtensionTestCoverage/ExtensionTestCoverage.csproj`

The tool should:

1. Build or load extension assemblies.
2. Reflect over every `Microsoft.Azure.Azclips.Extensions.<ExtensionName>.ExtensionIndex`.
3. Call `GetAllCommands(ClientType.CLI)` and `GetAllCommands(ClientType.PowerShell)`.
4. Normalize each command into a stable key:
   - extension name,
   - product surface (`CLI` or `PowerShell`),
   - invocation name,
   - processor type full name,
   - parameter names and aliases.
5. Discover tests and classify mapped signals by tier:
   - xUnit unit tests from test assemblies,
   - scenario tests from `tests/Scenario.Tests`,
   - script scenario tests from scenario scripts/runners (follow the referenced scripts and parse the commands they execute),
   - docs/help Markdown presence as a separate non-scoring signal,
   - Pester docs/help tests only when they can be mapped to a command; if this tier is not implemented, report it as `not analyzed` instead of `no`,
   - benchmark tests from `[BenchmarkExtension("<Name>")]`, separated into extension-level coverage and command-level coverage.
6. Attribute tests to commands using a layered strategy:
   - explicit attributes first,
   - scenario host command invocations next,
   - project references and extension-specific test projects next,
   - filename/class/method name heuristics last.
7. Emit the JSON, Markdown, HTML, and `data.js` reports.

Do not require Azure credentials. The report is metadata analysis and should run in CI without recording or playback.

## Strongly preferred attribution model

Add explicit attributes for long-term accuracy instead of relying only on naming heuristics.

Recommended test attribute:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CoversCommandAttribute : Attribute
{
    public CoversCommandAttribute(string extension, string surface, string invocationName)
    {
        Extension = extension;
        Surface = surface;
        InvocationName = invocationName;
    }

    public string Extension { get; }
    public string Surface { get; }
    public string InvocationName { get; }
    public string Tier { get; init; } = "unit";
}
```

Use this on new and high-value tests. Existing tests can be mapped by heuristics initially, then converted incrementally.

Example:

```csharp
[CoversCommand("Resources", "CLI", "group create", Tier = "scenario")]
[CoversCommand("Resources", "PowerShell", "New-AzResourceGroup", Tier = "scenario")]
public class GroupCreateScenarioTests
{
}
```

## JSON schema

Use a stable schema so dashboards can evolve without re-running historical analysis:

When implementing the applicability policy, add per-command applicability and
reason metadata without replacing the existing test evidence arrays. For example,
an additive `tierApplicability.armScenario` object can contain `applicable: false`
and `reason: "local configuration; no ARM calls"`. Distinguish ARM record/playback
from non-ARM scenarios; do not treat this field as a blanket scenario exemption.
Consumers should use verified applicability to derive `N/A`, gaps, and applicable
denominators, preserving existing mapped hits. Absent metadata means applicability
is not analyzed, not `false`. This metadata is proposed, not current generator output.

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-10-19T00:00:00Z",
  "commit": "<sha>",
  "extensions": [
    {
      "name": "Resources",
      "benchmarks": {
        "CLI": [{ "name": "ResourceGroupBenchmarks", "path": "tests/Benchmark/CLI/ResourceGroupBenchmarks.cs", "confidence": "extension-benchmark" }],
        "PowerShell": [{ "name": "ResourceGroupBenchmarks", "path": "tests/Benchmark/PS/ResourceGroupBenchmarks.cs", "confidence": "extension-benchmark" }]
      },
      "commands": [
        {
          "surface": "CLI",
          "invocationName": "group create",
          "processorType": "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupProcessor",
          "unitTests": [],
          "scenarioTests": [],
          "scriptScenarioTests": [],
          "docsPresent": true,
          "docsPath": "src/CLI/Help/Docs/group/create.md",
          "docsTests": [],
          "benchmarks": [],
          "coverageScore": 0,
          "gaps": ["missing-unit", "missing-scenario"]
        }
      ]
    }
  ],
  "unmappedTests": []
}
```

## Coverage scoring

Keep scoring simple and transparent:

- Unit test coverage: 1 point.
- Applicable scenario or script scenario coverage: 2 points.
- Docs/help validation: 1 point.
- Benchmark coverage: informational, 1 point if present.

Also report raw booleans. Never hide raw gaps behind only a percentage.
For the intended applicability-aware model, show `N/A` and its reason alongside
the raw evidence, excluding inapplicable requirements from the denominator rather
than scoring their absence as zero. Benchmarks remain informational: prioritize
them alongside unit tests for local/MSAL-only paths, but do not require them for
`Good` status. Keep any mapped non-ARM scenario evidence visible.

Suggested applicability-aware status (not yet implemented by the current generator):

- `Good`: unit + scenario coverage exists where scenarios are required; unit coverage is sufficient when the scenario requirement is verified `N/A` and no separate non-ARM scenario requirement applies.
- `Partial`: at least one required automated test tier has mapped evidence, but another applicable required tier is missing.
- `Gap`: no mapped automated tests.
- `Unknown`: only heuristic/unconfirmed mapping exists.

## Markdown report format

Use a compact matrix. In the intended applicability-aware report, scenario cells
without evidence should show `N/A` only for verified inapplicable requirements, with
a Notes column explaining why. Use `no` for applicable tiers with no mapped signal,
and `not analyzed` for unassessed applicability. If scenario columns combine ARM
and non-ARM tests, retain mapped hits as `yes` and explain the ARM-only exemption
in Notes rather than hiding them behind `N/A`. Summary scenario gaps count only
applicable missing requirements. The local-command example below illustrates the
intended policy, not current generator output.

```markdown
# Extension test coverage

Generated from `<sha>` on `<date>`.

## Summary

| Extension | Commands | Good | Partial | Gaps | Scenario gaps | CLI bench | PS bench |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| Resources | 14 | 8 | 4 | 2 | 6 | 1 | 1 |

## Resources

Extension-level benchmarks: CLI=1, PowerShell=1.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `group create` | yes | yes | no | yes | not analyzed | yes | Good | ARM scenarios applicable |
| PowerShell | `New-AzResourceGroup` | yes | yes | no | yes | not analyzed | yes | Good | ARM scenarios applicable |

## Config

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `config get` | yes | N/A | N/A | yes | not analyzed | no | Good | Verified local path; no ARM calls or separate non-ARM scenario requirement |
```

## CI/CD workflow

Mirror the benchmark workflow, but keep this lighter:

- Trigger on pushes and PRs touching `src/**`, `tests/**`, `tools/ExtensionTestCoverage/**`, and the workflow.
- Build once with the pinned SDK.
- Run the coverage tool.
- Upload JSON/Markdown artifacts.
- Add Markdown to the GitHub step summary.
- On push to `main`, publish the report to `gh-pages/dev/test-coverage/`, then ensure the Pages/wiki deployment imports that directory into `wiki/.vitepress/dist/dev/test-coverage`, matching the benchmark dashboard model.

If publishing to the GitHub wiki repository directly, use a separate workflow step that checks out `<owner>/<repo>.wiki.git`, copies the generated Markdown, commits only when changed, and pushes. Use least-privilege permissions.

## Suggested workflow file

Recommended path:

- `.github/workflows/extension-test-coverage.yml`

Skeleton:

```yaml
name: Extension Test Coverage

on:
  push:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/**'
      - 'tools/ExtensionTestCoverage/**'
      - '.github/workflows/extension-test-coverage.yml'
  pull_request:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/**'
      - 'tools/ExtensionTestCoverage/**'
      - '.github/workflows/extension-test-coverage.yml'
  workflow_dispatch:

permissions:
  contents: read

jobs:
  report:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - run: dotnet build azclips.slnx --configuration Release
      - run: dotnet run --project tools/ExtensionTestCoverage/ExtensionTestCoverage.csproj --configuration Release -- --repo-root . --out artifacts/TestCoverage --commit ${{ github.sha }}
      - run: cat artifacts/TestCoverage/extension-test-coverage.md >> "$GITHUB_STEP_SUMMARY"
      - uses: actions/upload-artifact@v4
        with:
          name: extension-test-coverage
          path: artifacts/TestCoverage
```

Add a separate publish job once the report is trusted.

## Other visibility options

This command-surface matrix complements, but does not replace:

- Coverlet line/branch coverage for unit-test depth.
- Scenario recording inventory for live/playback coverage.
- Command-surface snapshot tests to detect new public commands without mapped tests.
- PR comments that list newly introduced commands and require at least one mapped test or an explicit waiver.

The fastest path is:

1. Generate the command matrix first.
2. Add explicit `CoversCommandAttribute`.
3. Produce Markdown in CI.
4. Publish to wiki or GitHub Pages.
5. Later add gating thresholds once the baseline is cleaned up.
