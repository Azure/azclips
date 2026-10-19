# Extension test coverage report

Use this skill when asked to add, update, or run an azclips test-coverage visibility report that maps automated tests to each public extension command surface.

The goal is a Coverlet-style coverage matrix for command surfaces, not line coverage. The report should answer:

- Which extensions exist?
- Which CLI and PowerShell commands does each extension publicly expose through `ExtensionIndex`?
- Which automated test tiers cover each command?
- Where are the gaps by extension, product surface, command, and test tier?

## Expected output

Produce both machine-readable and human-readable outputs:

- JSON: `artifacts/TestCoverage/extension-test-coverage.json`
- Markdown: `artifacts/TestCoverage/extension-test-coverage.md`
- Optional GitHub Pages/wiki-ready data file: `artifacts/TestCoverage/data.js`

The Markdown should include:

1. Summary totals by extension.
2. A command matrix grouped by extension.
3. Separate columns for CLI command, PowerShell command, unit tests, scenario tests, script scenario tests, docs presence, docs/help tests, benchmark coverage, and notes.
4. Extension-level benchmark coverage for CLI and PowerShell benchmark classes, even when a benchmark cannot be safely mapped to a specific command.
5. A gaps section sorted by severity:
   - no tests at all,
   - no scenario coverage,
   - CLI-only or PS-only coverage,
   - docs/help missing,
   - benchmark missing.

Prefer conservative attribution. If a test cannot be confidently mapped to a command, include it in an `unmappedTests` section rather than claiming coverage.

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
5. Discover tests and classify them by tier:
   - xUnit unit tests from test assemblies,
   - scenario tests from `tests/Scenario.Tests`,
   - script scenario tests from scenario scripts/runners (follow the referenced scripts and parse the commands they execute),
   - docs/help Markdown presence as a separate non-scoring signal,
   - Pester docs/help tests only when they can be mapped to a command (do not count file presence as a test),
   - benchmark tests from `[BenchmarkExtension("<Name>")]`, separated into extension-level coverage and command-level coverage.
6. Attribute tests to commands using a layered strategy:
   - explicit attributes first,
   - scenario host command invocations next,
   - project references and extension-specific test projects next,
   - filename/class/method name heuristics last.
7. Emit the JSON and Markdown reports.

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
- Scenario or script scenario coverage: 2 points.
- Docs/help validation: 1 point.
- Benchmark coverage: informational, 1 point if present.

Also report raw booleans. Never hide raw gaps behind only a percentage.

Suggested status:

- `Good`: unit + scenario coverage exists.
- `Partial`: at least one automated test tier exists.
- `Gap`: no mapped automated tests.
- `Unknown`: only heuristic/unconfirmed mapping exists.

## Markdown report format

Use a compact matrix:

```markdown
# Extension test coverage

Generated from `<sha>` on `<date>`.

## Summary

| Extension | Commands | Good | Partial | Gaps | Scenario gaps | CLI bench | PS bench |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| Resources | 14 | 8 | 4 | 2 | 6 | 1 | 1 |

## Resources

Extension-level benchmarks: CLI=1, PowerShell=1.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `group create` | yes | yes | no | yes | no | yes | Good |
| PowerShell | `New-AzResourceGroup` | yes | yes | no | yes | no | yes | Good |
```

## CI/CD workflow

Mirror the benchmark workflow, but keep this lighter:

- Trigger on pushes and PRs touching `src/**`, `tests/**`, `tools/ExtensionTestCoverage/**`, and the workflow.
- Build once with the pinned SDK.
- Run the coverage tool.
- Upload JSON/Markdown artifacts.
- Add Markdown to the GitHub step summary.
- On push to `main`, publish the report to either:
  - the repository `wiki/` folder if the repo keeps wiki content in-tree, or
  - `gh-pages/dev/test-coverage/` if following the benchmark dashboard model.

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
