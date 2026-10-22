---
name: extension-test-coverage
description: Add, update, or run the azclips extension command-surface test coverage report and maintain its attribution and publishing workflow.
---

# Extension test coverage report

Use this skill to add, update, or run the command-surface test coverage report.
Keep it lightweight, automatic and advisory: it is not a complete static-analysis
engine, a line-coverage tool, proof tests passed, or a PR quality gate.

The implementation is `.github/scripts/generate-extension-test-coverage.py` with
bounded C# syntax/call-path analysis in `.github/scripts/extension_coverage_analysis.py`;
regressions are `.github/scripts/test_extension_test_coverage.py`, and CI is
`.github/workflows/extension-test-coverage.yml`.

## Hands-off discovery

The registered `ExtensionIndex` metadata is the authoritative inventory. Discover
extensions and commands from source on every run, independently of existing tests.
Do not introduce a manually maintained policy, exemption list, test-to-command
registry, review anchors or required coverage attributes. Contributors adding,
renaming or removing commands/tests must not have to update reporting metadata.
Report JSON/data.js and audit ledgers are disposable generated outputs, never
configuration or checked-in live-extension baselines. There is no per-extension
onboarding metadata step. Keep two Python implementation files: the generator
and its bounded source-analysis module, including the source reader. Regressions
live separately. Use only Python's standard library; add no parser packages,
compiled helpers, embedded C# code or SDK/runtime requirements.

Automatically map supported invocation forms, retain unmatched signals in
`unmappedTests`, and infer shared processor behavior using the exact full processor
identity. A new command using an existing processor can inherit inferred behavior,
but never another command's CLI/PowerShell execution or binding.
New processors with no discovered tests still appear in the inventory and the
potential-gap view. Removed commands disappear naturally; their old invocations
can become unmapped evidence rather than a stale-metadata validation error.

The source parser is intentionally bounded. If a new registration or test syntax
is not supported, improve generic discovery with a representative regression,
not a per-command exception. Do not claim to discover arbitrary C# execution paths.

## Outputs and focused views

Generate all four files:

- `extension-test-coverage.json`: complete command inventory, evidence and summaries.
- `data.js`: the identical full JSON payload assigned to `window.EXTENSION_TEST_COVERAGE`.
- `extension-test-coverage.md`: focused human-readable potential gaps.
- `index.html`: the same focused view for GitHub Pages.

Markdown/HTML summary tables include every extension and command, including fully
Good extensions, with total and Good/Partial/Gap/Unknown counts. Detailed command
rows show only incomplete processor or own-surface execution signals, including
Partial and unresolved-processor Unknown commands. Good command rows stay hidden.
Every summary extension name links to a stable, collision-free section anchor in
both formats, with the emoji outside the link. Fully Good extensions have a short
"No potential gaps identified in mapped dimensions" target, not a command table.
Displayed absences are **automatic, unreviewed
potential gaps**, not confirmed missing tests. State the potential-gap detail count
out of the full command total; do not imply the summary excludes Good commands.
Keep a short About this report area legend for registration, shared processor
logic, helpers, binding, own-surface execution, scenarios and benchmarks.
Define statuses using the implemented evidence rules. Include one concise caveat:
source discovery is not test results or comprehensive quality, unmapped is not
confirmed absent, and full evidence/limitations are in JSON/data.js. Explain the
human evidence checklist below briefly; keep legacy scores and parser plumbing
in this skill and machine output, not the header. Scenario requirements are not
scored and benchmarks are informational. Do not flood the human view with unmapped records.

Show registration, processor behavior, processor helpers, binding and surface
execution separately, alongside scenario/script tiers and benchmarks.
Human Evidence cells contain only bullet-point links to deduplicated declared test
classes, falling back honestly to file stems when the class cannot be determined
(including scripts). Link the class declaration where known, otherwise the file.
Use safe table-cell bullets in Markdown and lists in HTML. Repeated methods,
dimensions and shared paths from the same class contribute one link. Keep all
method links, kinds, inference reasons, call paths and assertion status unchanged
in JSON/data.js; dimension columns and the short legend supply human context.
Summarize all extension statuses; prioritize detailed commands without processor
behavior before those missing only surface execution.
The separate Highest priority mapped-signal gaps table further filters these
incomplete commands to **Unit tests: Not found** and **ARM scenarios: Unknown**
together. Use unit presence, including registration/helpers/setup and legitimate
unit-tier shared evidence, not the three-area checklist score. Exclude established
ARM N/A and Applicable paths from this priority table. Generic scenario evidence
does not establish ARM applicability. Apply the filter before the 50-row limit;
retain the existing per-extension detail rows, full summary and JSON inventory.
An empty priority result does not mean the other detail tables have no gaps.

Immediately below Gap summary, add a separate Benchmark gap summary grouped by
extension, showing total commands and counts with/without mapped benchmarks.
Include every command surface, even Good commands, but only extensions with at
least one unmapped benchmark command. Count command workloads, not fixture/setup
or extension-level benchmark presence. Show an explicit empty state when none
remain. Keep this informational summary separate from status counts and the
highest-priority filter; benchmarks never determine Good or processor/execution
detail inclusion. Apply both summary/filter changes consistently in Markdown
and HTML without changing evidence, scoring or the machine inventory.
If no potential gaps are found, say so without claiming exhaustive coverage.
In the Gap summary's Extension column use red/ yellow/ green circles: red if any
command is Gap, yellow if any Partial/Unknown and no Gap, green only if every
command is Good. Compute this against the full extension inventory, not just the
visible subset. Green means mapped dimensions, not quality, passing tests or
scenario sufficiency. Show fully Good extensions in the summary, not command
rows. Keep the indicator legend concise in Markdown and HTML.

### Human test-evidence checklist

Use compact stacked plain-language summaries in the priority table and detailed
Test evidence column instead of technical gap codes/repeated interpretation prose.
Quantify unit evidence using three equally weighted binary areas: processor
behavior, this command's own-surface argument binding and own-surface execution.
Only unit-tier evidence counts. Exact shared-processor inference may contribute
processor behavior, never another surface's binding/execution; direct engine
execution contributes logic but not CLI/PowerShell dispatch. Binding counts a
recognized binding call or actual own-surface execution, as in the existing model.
Registration, setup, helpers, scenarios and benchmarks do not score unit areas.

Separate test presence from this checklist. Display `Unit tests: Found` whenever
any attributed unit evidence exists, including registration, setup and helpers,
or legitimate unit-tier shared processor evidence. Otherwise use `Unit tests: Not found`.
Use a bulleted summary, nesting the three areas under `Unit test evidence:`,
for example:

- Unit tests: Found
- Unit test evidence:
    - Processor: Not found
    - Param Binding: Not found
    - Command execution: Not found
- Benchmarks: Not found
- ARM scenarios: N/A

Keep Markdown table cells intact using line breaks and indented table-safe
bullets; use semantic nested lists in HTML. Apply the same layout in the priority
and per-extension tables.
Each nested area shows `Found` for mapped unit-tier evidence or `Not found` when
none is mapped. Use the same labels for both states, including `Param Binding`.
This is **not code/line/branch coverage**, passing
outcomes or a new definition of overall Good. Keep the machine checklist status
unchanged: OK at 2/3 or 3/3 (at least 50%), Partial at 1/3, Not mapped at 0/3.
Human tables show the individual areas instead of the aggregate checklist label.
Show benchmark presence separately as `Found` or `Not found`, without fractions,
informational and never gating status. `ARM scenarios` describes ARM request
record/playback applicability: `N/A` for an established non-ARM path, `Applicable`
for a discovered ARM SDK/request operation, otherwise `Unknown`.
Do not add an N/A legend entry to the human report. Independently, when scenario
evidence exists, add `Scenario tests: Found (N files)` using distinct source files,
including when ARM applicability is N/A or Unknown. Do not label generic scenario
evidence as verified ARM recordings or invent a scenario denominator.
Not found means no mapped evidence, not confirmed absence of tests.

## Automatic evidence model

Tier/project and evidence depth are independent. A Cmdlets.Tests test can inspect
only metadata, while a scenario can exercise real command dispatch.

Production positives start at attributed Fact/Theory or Benchmark methods and
follow supported source calls, not file-wide lexical matches. Recognized evidence:

- `RunCli`, `ExecuteCli`, `RunCliAsync` and `ExecuteCliAsync`, including receiver
  qualification, literal argument lists/arrays, expression-bodied argument helpers
  and local array helpers. Preserve actual helper names in unresolved evidence;
  skip declarations, including `string[]` parameters. Helper names alone do not
  prove execution: resolve their bodies to a CLI entry point or dispatcher/session.
- CLI trailing positional values map to the longest registered command prefix at
  a token boundary across unit, scenario, script and benchmark attribution.
  PowerShell names retain exact matching.
- Processor construction and `CreateProcessor<T>` are `processor-setup`, not
  executed behavior. Arbitrary `typeof` references are not behavior.
- Direct instance `ProcessAsync` through typed locals, fixture fields, constructors,
  test factories, parameterized/generic helpers, static imports and tuple returns
  supplies processor behavior, not surface dispatch. Literal InlineData cases,
  boolean conditions, argument names/defaults and collection spreads are supported.
  Literal switch-expression arms are selected independently; generic type arity
  and project-local declarations remain distinct. Unknown/mock object initializers
  do not gain processor execution solely from a declared variable type.
  Resolve namespaces/aliases conservatively; ambiguous identities/overloads remain
  diagnostic evidence. Trace test-subclass overrides rather than assuming they
  execute the registered base processor.
- Follow exception-assertion callbacks, not arbitrary stored/passed delegates.
  Ignore unused helpers, skipped tests, comments/strings, known dead branches and
  statements after returns. Unresolved branch bodies/data providers are not credited.
- Direct `RunPowerShell`/`RunPowerShellCommand` calls are `engine-execution`, not
  cmdlet execution: they bypass PowerShell binding and cmdlet ProcessRecord.
- `AddCommand` alone is `surface-reference`, not proof of Invoke.
- Scripts reached by a discovered playback test supply `script-candidate` evidence,
  not independently verified processor/surface execution: shell control flow is
  not analyzed. Unreferenced/manual smoke scripts do not supply automated evidence.
- Bounded Fact/Theory methods supply method/line provenance for recognized calls.
  Assertion-bearing methods with literal `GetCommandMetadata(name, ClientType)`
  supply registration evidence. The supported parameterized form is
  `GetCommandMetadata(commandName, clientType)` with literal
  `InlineData(ClientType, commandName)` cases.
- Metadata and `BindArgumentsToProcessor` within the same assertion-bearing method
  supply binding-only evidence, not cmdlet ProcessRecord.
- Static processor helper calls in assertion-bearing methods supply helper
  evidence when a namespace import resolves the processor identity unambiguously.
  Helper proof is separate from processor execution.
- Runspace helper paths, including generic cmdlet helpers using CmdletAttribute,
  are recognized when an opened runspace registers that literal command (or
  imports a module), the pipeline is attached to it, and Invoke is reached.
  Public cmdlet names do not automatically prove every internal parameter-set
  route. Keep unmatched public/internal names in discovery diagnostics.
- Benchmark evidence requires a reachable Benchmark workload call, not a tag,
  similar name, setup login, registration or auth verification. Lifecycle setup
  can establish fixture wiring but supplies no command workload evidence.
- Registration parsing excludes C# comments and rejects duplicate identities.
  Source-defined generic dictionary table factories support literal parameters,
  guards and interpolated group names without extension-specific command lists.

These are source signals, not an assertion that a test executed or passed.
The bounded source reader is neither a compiler nor a complete C# parser.
Unsupported expressions/declarations are opaque rather than lexical evidence;
per-file diagnostics and `analysis.unsupportedSyntax` retain counts/examples.
Preprocessor conditions use file-defined symbols, not project build symbols.
Bounded helper traversal is not proof
that all branches/parameters are covered. `assertionStatus` means a recognized
assertion call was found on the source path, not that it validates the command
adequately. Neither a discovered test nor a mapped call establishes a passing run.

### Shared processor behavior

Recognized non-benchmark CLI, PowerShell or direct engine execution can infer
processor behavior for commands with the **same full processor identity**.
Never join by short type, filename, extension name or command similarity.
Setup, metadata, helper-only, binding-only and benchmark signals do not trigger
this inference, and inferred hits are not recursively propagated.

Keep inferred hits in additive `sharedProcessorTests`, separate from the raw
unit/scenario/script arrays. Label the dimension `inferred-shared`, retaining
`confidence: "inferred-shared-processor"`, origin command, tier, file, method/line
where available and an explicit uncertainty reason. Direct evidence takes
precedence over duplicate inference from the same source file/method/tier.

Share no binding, surface execution, scenario recordings or raw tier-score points.
Processor proof counts once, not once per repeated test or surface. Inferred
behavior does not independently verify another surface or conditional branches.

Representative regression cases:

- CLI `profile list` and internal PS `Get-AzProfile_List` use ProfileListProcessor.
  ProfileCliTests supplies processor behavior to both, CLI execution only to CLI.
  Display `Get-AzProfile (internal route: Get-AzProfile_List)` honestly.
- `Get-AzConfig` inherits processor behavior from CLI ConfigCliTests, but not real
  PowerShell invocation. Metadata/output-property checks do not close execution gaps.
- Survey metadata and CreateStartInfo assertions are automatically discovered as
  registration/helper evidence, not processor execution, dispatch or browser launch.
- Access-token argument-binding tests are not actual runspace execution; the NIC
  runspace test is recognized separately.

## Scenario applicability and uncertainty

The human `ARM scenarios` field concerns **ARM request record/playback only**,
not every network request or integration test. Infer `armScenario` independently
of `nonArmScenario`; NuGet/MSAL traffic does not itself require ARM recordings.
Follow processor initializers, constructors, Init/ProcessAsync and supported
source calls. Same-arity overloads are conservatively analyzed together rather
than choosing one arbitrarily. Recognize local/data operations and bounded
framework contracts for configuration storage, extension registry Load/Save and
engine metadata discovery (not arbitrary command execution).
Known NuGet and MSAL protocol APIs are non-ARM boundaries. These describe APIs,
not command exemptions; source-defined implementations still need analysis.
Output, telemetry and event infrastructure are outside command-request scope.
As with all bounded inference, runtime dependency replacements are not proven.

Set `tierApplicability.armScenario` to `not-applicable` only when the supported
path closes without unknown ARM-capable operations. A reached ARM SDK call or
recognized HTTP request targeting a literal/interpolated management endpoint
establishes `applicable`, not executed/passing tests. An endpoint string alone
does not establish a request. Arbitrary HTTP, opaque service/delegate calls,
reflection loading/invocation, unsupported control flow/conversions and unresolved
virtual dispatch retain `unassessed`, displayed as `Unknown`.
Preserve the separate conservative non-ARM assessment; it may remain unassessed
when ARM is N/A. JSON retains `scope: "arm-record-playback"`, separate
`armAssessment`/`nonArmAssessment`, source methods, reasons and diagnostics.
The top-level reason/evidence/diagnostics describe the ARM assessment.

Local Config/Extension/profile operations and MSAL-only operations may therefore
show ARM N/A automatically when their paths close. Accounts is not categorically
ARM-free: login subscription or tenant resolution can call ARM. Extension
installation can call NuGet and still be non-ARM, but unresolved metadata-loading
or other dependencies can keep a particular path Unknown. Never force exemptions
from extension names, filenames, missing tests or absent ARM literals.
This is bounded source inference, not semantic compilation or proof of absence
under every runtime override. No ARM traffic does not prohibit non-ARM integration
tests; preserve actual scenario/script-scenario evidence in all cases. N/A adds
no points, does not affect unit status/Good and imposes no scenario gates or
denominator changes. ARM-specific unknown counts do not include uncertainty that
exists only in the separate non-ARM assessment.

Status describes **discovered processor/execution signals**, independently of
scenario applicability:

- `Good`: processor behavior and this command's own surface execution are mapped.
- `Partial`: some non-benchmark evidence exists, but those dimensions are incomplete.
  Registration/helper/setup signals alone cannot make Good.
- `Gap`: registered processor identity exists, but no non-benchmark evidence is mapped.
- `Unknown`: registered processor identity is unresolved.

Good is not complete scenario coverage or proof tests passed. All no-mapped findings
are unreviewed. Use `no-mapped-processor-tests`, `no-mapped-cli-execution-tests` and
`no-mapped-ps-execution-tests`, not a blanket `missing-unit` claim.

## Compatibility and scoring

Use schemaVersion 2, retaining raw unit/scenario/script/benchmark arrays, full
inventory and per-extension summaries. Version 2 removes documentation-presence
and docs-test tracking: no source help-file probing, documentation score/gap
bookkeeping, human columns or `docsPresent`, `docsPath`, `docsTests` and
`docsTestsAnalyzed` JSON fields (including the old top-level flag).
Product documentation and its actual tests are unaffected.
Retain `analysisMode:
"automatic-discovery-only"` and `advisory: true`; keep `testsExecuted: false`.
Dimensions include registration, processorBehavior, processorHelper, binding,
surfaceExecution, engineExecution, processorSetup and surfaceReference.
Hit provenance includes kind, method, reason, line, sourceUrl and sharedFrom.
Additive `unitEvidence` separates the `found` presence flag from the three `areas`,
`mappedAreas`, `totalAreas` and checklist `status`. Preserve all raw test provenance.

Compatibility fields for historical reviews remain empty: executionCoverage
reviewReason/testingTarget are empty strings, reviewEvidence is empty, and summary
reviewedExecutionGaps is zero. There is no maintained review/exemption source.
`gapAssessments` identifies unreviewed signals or informational benchmarks.
`no-mapped-benchmark` is informational absence of attribution, not proof that no
benchmark exists. Hit provenance also includes `callPath` and `assertionStatus`.
The additive `analysis` object supplies source/tool digests, parsed-file/root
counts, an attributed-root ledger, diagnostic counts/details and limitations.
Detailed analysis scope stays in JSON/data.js; not every root is command-related.

Legacy `coverageScore` is a raw signal total: unit-tier behavior/helper 1, scenario
or script presence 2, benchmark 1. Setup/reference-only hits do not earn unit
behavior points. It is not a percentage or a confidence/coverage-depth score.
`requiredScore` records the single discovered processor-behavior point;
`applicableScoreMaximum` remains null: a source exemption is not a quantified
scenario-completeness denominator.
Shared inference adds no raw score points. Never present an unknown denominator
as zero or imply an exemption was covered.

## Running and evaluating

Use source metadata only: no Azure credentials, product build or product/test
execution. The report requires Python only: no third-party packages, .NET SDK,
compiler subprocess, NuGet restore, temporary binaries or generated syntax files.
Tokenization, balanced declaration/statement parsing and expression parsing live
with the call-path analysis in `extension_coverage_analysis.py`; output formatting
and report orchestration stay in `generate-extension-test-coverage.py`.
Parser metadata records the bounded Python grammar version. Each parse owns its
state; tests also verify operation with an empty executable PATH.
Run regressions before generation; `-S` excludes installed Python site packages:

```powershell
python -S -B -m unittest discover -s .github\scripts -p test_extension_test_coverage.py
python -S -B .github\scripts\generate-extension-test-coverage.py --repo-root . --out artifacts\TestCoverage
```

For investigations/previews, use a session-artifact output directory instead of
committing generated reports. Compare command identities and raw evidence before
and after changes. Assert representative source methods and exact output rows,
not only totals. JSON/data.js must agree; Markdown/HTML must have the same visible
commands; summary tables must cover the entire inventory with exact status counts
and indicators, while Good details remain hidden. An empty discovered inventory is
an explicit generation error, not a successful blank report.

The regressions cover CLI helper forms/declarations/unresolved evidence, positional
prefix resolution across tiers, exact PowerShell matching, source-method evidence,
processor-identity ambiguity, shared behavior without surface/recording credit,
representative Profile/Config/survey/binding/runspace forms, independent scenario uncertainty,
focused views/empty states and all output formats. Lifecycle fixtures add, rename
and remove extensions without any report metadata changes.
Examples are frozen temporary fixtures, not a registry of live commands or test
methods. Live-inventory checks impose no fixed names/counts, so routine command
and test renames/removals do not require reporting-baseline edits.
Only reporting-syntax changes need reporting regressions; adding a normal extension
does not require a hand-maintained baseline entry.

For team evaluations, preserve a disposable before snapshot, compare every command
identity/dimension and evidence addition/removal, and source-review representative
positive, negative and unsupported paths from both previously Good and potential-gap
rows. Evaluate mapping precision (credited paths really match the dimension) and
recall (known source paths are found) against frozen synthetic cases plus a bounded
source audit. Quantify files/roots/commands examined and record unresolved patterns
in a session/CI artifact ledger. Do not call a source scan exhaustive manual
behavioral verification or convert mapped counts into a blanket quality percentage.

## Advisory CI and publication

The workflow watches `src/**`, `tests/**`, the generator, analyzer, Python regressions, skill
and workflow. It runs regressions before generation, uploads all four artifacts,
and adds Markdown to the step summary. No minimum score, missing-test count,
manual waiver or coverage status gates builds, PR execution or merging.

Reporting and publishing jobs/steps use `continue-on-error`; failures remain
visible as warnings/logs and job-summary diagnostics rather than failing the
optional workflow. A failed test skips generation; failed/incomplete reporting
does not publish a fresh report. Publication requires successful regression,
generation and artifact-upload outcomes, and is main-push-only.
Do not soften the local Python test/generator exit codes: local failures still
identify actual tooling problems. CI handles their advisory status explicitly.

On push to main, valid output is published to `gh-pages/dev/test-coverage/`.
The existing build-wiki workflow imports that directory into
`wiki/.vitepress/dist/dev/test-coverage`, matching the benchmark model.
Local generation does not publish or change branch-protection settings.
Keep this workflow out of required merge checks; administrators control repository
rules, and the workflow cannot promise against future external rule changes.

Remaining limitations: bounded source attribution can miss dynamic/member/class
data, unresolved overloads, reflection, property-returned factories, complex
conditions/loops, cross-assembly type conflicts and registration forms beyond the
supported declarations/table factories. Diagnostics disclose detected uncertainty,
but cannot enumerate every possible unsupported idiom.
Known duplicate processor identities across projects are Unknown and never shared.
Unsupported statement loops/switch statements and exception-handler selection
remain diagnostic rather than crediting every lexical branch.
Shell control flow and
all internal PowerShell route selection are not proven. Scenario requirements
are not scored; unresolved dependencies remain Unknown rather than automatically exempted.
Neither discovery nor shared inference proves passing
tests, exhaustive execution, line/branch coverage or absence of other tests.
