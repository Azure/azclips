Tasks are organized by the level each piece of schema targets — module, command, parameter —
following design.md's target-first method: for each level, the real PS SDK shape was found first
(via live reflection), then a fixed framework decided what's in scope, then the schema/derivation
was implemented. Pipeline mechanism (shared across all levels) and real-fixture verification are
their own sections since they aren't level-specific. See design.md for the full target analysis,
framework verdicts, and Appendix schema reference behind every task below. A fourth level
(output-formatting, `Format.ps1xml`) was investigated and designed but is deliberately out of
scope for now — see design.md's "Output-formatting level" section for why.

## Pipeline infrastructure

Goal: the mechanism every level's schema flows through — scaffolding only, no vocabulary yet.

- [x] Create `toolkit/packages/ps-commands` (npm name `@azure-client-engine/ps-commands`),
      matching the existing `@azure-client-engine/<name>` workspace conventions — `package.json`,
      `tsconfig.json`, `src/index.ts`. Added to the root `toolkit/tsconfig.json` solution's
      `references` array. Depends on `@azure-client-engine/ir` and `zod`.
- [x] Scaffold the `create-ps-commands` `TransformStep` in `toolkit/packages/compiler`
      (implements the real `TransformStep` interface — `run(ir: IR, context): IR` — from
      `transform.ts`; enriches `hints.powershell` in place, never returns an aggregate),
      registered via `CompilerRegistry` under id `create-ps-commands`.
- [x] Wire `create-ps-commands` into the real `ace compile` CLI: `toolkit/packages/cli` registers
      it in a default `CompilerRegistry`. A config file with `{"pipeline":
      [{"use": "create-ps-commands"}]}` opts a real `ace compile` invocation into
      `hints.powershell` enrichment; absent that key, behavior is byte-for-byte unchanged
      (identity pipeline).
- [x] Register a `generate-ps-commands` `Generator` (`toolkit/packages/compiler`) under its
      eventual real-generator registry id/factory name, so there's no rename/re-export churn once
      it grows real C# output. Today its `generate()` only calls `derivePsCommands(ir)` internally
      and serializes the result as `ps-commands.json`, opted into via
      `{"generator": {"use": "generate-ps-commands"}}`; the default remains `noopGenerator`.
      Confirms `derivePsCommands` is only ever called from inside a `Generator`'s own
      `generate()` — never a separate pipeline stage (design.md's pipeline-mechanism section).
- [x] Design and document the concrete `select`/`where`/`set` directive grammar (JSON/YAML shape)
      accepted as the `create-ps-commands` step's `options`, modeled on AutoRest's
      `docs/directives.md` (selectors: `command`, `parameter`; filters: verb, subject,
      subject-prefix, parameter-name).
- [x] Implement directive matching and application inside the `create-ps-commands` step, applied
      as defaults before the final `hints.powershell` write — an existing inline hint value wins
      over a directive's.

## Module-level schema

Goal: cover the one real gap found by reflecting directly on a real PS module
(`System.Management.Automation.PSModuleInfo`, verified live against `Az.Accounts`) — nothing in
this schema modeled module-level facts before this. Scoped narrowly to what's genuinely
IR-derivable; see design.md's Module-level section for the field-by-field framework verdicts and
why release/packaging `.psd1` fields (GUID, version, required modules, PSGallery metadata) are
excluded.

- [x] Add a `rendering` field to `IRSource` (`toolkit/packages/ir/src/source.ts`) for the first
      time, with a `powershell` namespace holding just `description` (module summary text,
      author-settable, last-writer-wins like every other hint). Added Zod schema (`SourceHints`/
      `PowershellSourceHint` in `hints.ts`) + unit tests confirming it round-trips without
      affecting existing `IRSource` validation (`source.test.ts`, new).
- [x] Add `PsModule` to `toolkit/packages/ps-commands` (`{ sourceId, description,
      cmdletsToExport }`) plus `deriveModules(ir: IR): PsModule[]`, grouping the already-derived
      `PsCommand[]` by source and flattening each group's `verb`+`noun` into `cmdletsToExport`.
      Called only from inside a `Generator`'s own `generate()` — never a pipeline stage.
      Determines each command's source by looking up its first `sourceOperationId`'s real
      `IROperation.source` — one lookup per command, not a full re-grouping of `ir.operations`.
- [x] Write unit tests using the real `ResourceGroup` fixture: compiling it end-to-end produces
      one `PsModule` whose `cmdletsToExport` contains exactly
      `["New-AzResourceGroup", "Get-AzResourceGroup", "Remove-AzResourceGroup"]` (bare noun,
      matching `PsCommand.noun`'s existing prefix-combining convention) — verified in
      `cli/test/create-ps-commands.e2e.test.ts`; synthetic multi-source and no-description-hint
      cases covered in `ps-commands/test/derive.test.ts`.
- [x] Wire `deriveModules` into the `generate-ps-commands` placeholder generator alongside
      `derivePsCommands`, writing both to `ps-commands.json` as `{ commands: [...], modules:
      [...] }`, so a real `ace compile` invocation can preview module-level output the same way
      it already can for commands.

## Command-level schema

Goal: match the real target found by reflecting on `System.Management.Automation.CmdletInfo`/
`CommandMetadata` (verified live against `Get-Command New-AzResourceGroup`) — verb/noun,
`ShouldProcess`/`ConfirmImpact`, help metadata, multi-operation merge, and variant fan-out. See
design.md's Command-level section for the framework verdicts (including why `PositionalBinding`
is a generator-default note, not a schema field, and why `SupportsTransactions`/
`RemotingCapability` are excluded — both checked directly against real azure-powershell usage).

- [x] Add a minimal `powershell` sub-namespace to `toolkit/packages/ir/src/hints.ts`'s
      `OperationHints` (not `CommonHints` — these fields are operation-specific and would
      otherwise leak onto `ModelHints`/`EnumHints`/`ParameterHints`): `verb`, `noun`,
      `parameterSetName`, `confirmImpact`. Add unit tests confirming round-trip through IR's
      existing Zod validation without affecting non-PS consumers.
- [x] Implement namespace-based grouping: recover client/operation-group candidates from each
      `IROperation.id.namespace` (IR flattened the original client/operation-group hierarchy
      away).
- [x] Implement the verb-mapping table (seeded from AutoRest's real default
      `autorest-configuration.md` table): action-word table lookup always wins (`Create`/
      `CreateOrUpdate`→`New`, `Update`→`Update`, etc.); only fall back to an HTTP-method-based
      default (PUT→`Set`, PATCH→`Update`, POST→`Invoke`) when the action word has no table entry
      at all. Write the resolved verb into `hints.powershell.verb`, respecting any pre-existing
      authored value. Verified against the real `ResourceGroups.createOrUpdate` (PUT) precedent,
      which resolves to `New`, not `Set`.
- [x] Implement noun inference: singularize the resource/model name into `hints.powershell.noun`
      (bare, no prefix); write the configurable subject prefix into its own
      `hints.powershell.subjectPrefix` field (not baked into `noun` — combined into the final
      `PsCommand.noun` by `derivePsCommands`).
- [x] Implement `SupportsShouldProcess`/`ConfirmImpact` policy from HTTP method (PUT/POST/PATCH/
      DELETE ⇒ true; GET ⇒ false); write into `hints.powershell.confirmImpact`.
- [x] Implement `derivePsCommands(ir: IR): PsCommand[]` in `ps-commands` — a plain function, not a
      pipeline stage — that reads the enriched IR and groups operations under the same
      `id.namespace` sharing a resolved verb+noun into one `PsCommand`, with one `PsParameterSet`
      per source operation.
- [x] Extend `hints.powershell` on `IROperation.rendering` with: `alias`, `hidden`, completer
      reference, `summary`/`description`/`examples` (mirrors `ProcessorInfoAttribute`), a
      lifecycle tag (Preview/Deprecated/Experimental/GA), and an `httpPath` equivalent (path
      template + api-version).
- [x] Implement single-operation variant fan-out limited to `base` and `Expanded` (reusing IR's
      existing `FlattenHint`) inside `derivePsCommands()`; explicitly do not implement
      `ViaIdentity`/`ViaJsonString`.
- [x] Reviewer-feedback follow-up: cross-check the operation-level vocabulary against the real
      `System.Management.Automation.CmdletAttribute` (not just azclips's own
      `ProcessorInfoAttribute`). Added `supportsPaging`, `helpUri`
      (`CmdletAttribute.SupportsPaging`/`.HelpUri`).
- [x] Bug fix: `derivePsCommands()` never copied the operation-level richness fields (`alias`,
      `hidden`, `completer`, `summary`, `description`, `examples`, `lifecycleTag`, `httpPath`,
      `supportsPaging`, `helpUri`) onto the derived `PsCommand`. Added the missing fields to
      `PsCommand`/`PsCommandSchema` and `derivePsCommands()`, so a future generator reading
      `PsCommand[]` doesn't have to separately re-inspect the source IR.
- [x] Write a unit test covering a directive hiding a command by verb+subject, and a conflicting
      directive+inline-hint case resolving in the inline hint's favor (`ConfirmImpact`).
- [x] Verify end-to-end against the real `resourcegroup.tsp` fixture (not synthetic operations):
      a command-hide directive matches the real `delete`-derived `Remove-AzResourceGroup` command
      by its actually-inferred verb/subject.
- [x] Add `defaultParameterSetName` to `PsCommand` (`toolkit/packages/ps-commands`), mirroring
      `CmdletCommonMetadataAttribute.DefaultParameterSetName` — confirmed gap found via a full
      audit of the real `Attributes.cs` source, not just live reflection. `derivePsCommands()`
      computes it with a two-tier heuristic, both checked against real precedent: merged distinct
      operations (e.g. `get`+`list`) prefer the one with fewer required parameters (`List` over
      `Get`, matching `GetResourceGroupCmdlet.DefaultParameterSetName = "List"`); a single
      operation fanned into `base`+`Expanded` prefers `Expanded` (matching real generated modules'
      `DefaultParameterSetName='CreateExpanded'`). No author-override mechanism yet — not added,
      since there's no existing per-operation hint field for it and none of the real fixtures
      needed one; revisit if a real case surfaces where the heuristic picks wrong. Added unit
      tests in `derive.test.ts`/`schema.test.ts` and e2e coverage using the real `ResourceGroup`
      fixture (`cli/test/create-ps-commands.e2e.test.ts`).

## Parameter-level schema

Goal: match the real target found by reflecting on `System.Management.Automation.
ParameterMetadata`/`ParameterSetMetadata` (verified live against
`(Get-Command New-AzResourceGroup).Parameters`) — mandatory/help/pipeline-binding/wildcards/
aliases/allowed-values/mutability/defaults, and (found only after reflecting on real
`ParameterSetMetadata`) per-parameter-set variation. See design.md's Parameter-level section for
the framework verdicts, including the "properties vs. raw attributes" distinction that surfaced
`allowedValues`/`completer`.

- [x] Add a `powershell` sub-namespace to `toolkit/packages/ir/src/hints.ts`'s `ParameterHints`
      (its first field — `ParameterHints` was still a plain `CommonHints` alias) with:
      `mandatory`, `parameterSetName`, `helpMessage`, `valueFromPipeline`/
      `valueFromPipelineByPropertyName`, `supportsWildcards`, `aliases`, `allowedValues`
      (`ValidateSet`), completer reference, mutability flags (`readOnly`/`create`/`update`,
      mirroring AutoRest's real `InfoAttribute.ReadOnly/Create/Update`), and an optional
      default-value expression (mirroring `DefaultInfoAttribute`).
- [x] Reviewer-feedback follow-up: cross-check the parameter-level vocabulary directly against the
      real `System.Management.Automation.ParameterAttribute` (not just azclips's own
      `ArgumentInfoAttribute`, which only partially covers the real PS SDK shape). Added the
      confirmed gaps: `position` (`ParameterAttribute.Position`), `dontShow`
      (`ParameterAttribute.DontShow` — per-parameter, distinct from the operation-level `hidden`),
      `valueFromRemainingArguments` (`ParameterAttribute.ValueFromRemainingArguments`).
- [x] Write a unit test covering a directive renaming a parameter's alias across every matching
      command, and a unit test covering a non-matching directive not hiding/renaming anything it
      shouldn't.
- [x] Verify end-to-end against the real `resourcegroup.tsp` fixture: an author-inline parameter
      hint (`mandatory`/`helpMessage`/`position`) set on the real `resourceGroupName` path
      parameter survives `create-ps-commands` unclobbered (no parameter-level inference exists to
      conflict with it); a parameter-alias directive applies across every real operation sharing
      the `resourceGroupName` parameter.
- [x] Add an optional `parameterSets` map to `PowershellParameterHint`
      (`toolkit/packages/ir/src/hints.ts`) — `Record<string, { mandatory?, position?,
      helpMessage? }>` keyed by parameter-set name. The existing flat `mandatory`/`position`/
      `helpMessage` fields remain the default/fallback. Added Zod schema + unit tests confirming
      both the flat-only and per-set-override shapes round-trip.
- [x] Added `resolveParameterHintForSet(parameter, setName)` to `toolkit/packages/ps-commands`
      (exported from `index.ts`), resolving a parameter's effective `mandatory`/`position`/
      `helpMessage` by checking `parameterSets[setName]` first, falling back to the flat value.
      Kept as a standalone helper rather than a `PsParameterSet` field, since `PsParameterSet`
      stays bare by design (schema.ts) — parameter-level detail lives on the referenced
      operation's own parameters, not the grouping shape. A future generator calls this per
      `PsParameterSet` it emits a binding for.
- [x] Added a unit test (synthetic fixture, mirroring the real `SubscriptionId`-across-`Get`/
      `List`-sets case) proving a parameter's effective `mandatory` can differ across two merged
      parameter sets on the same command, plus fallback and no-hint-at-all cases.

## Real-fixture verification & integration

Goal: prove the whole pipeline (all three levels + mechanism) against a real, hand-authored
TypeSpec fixture with a genuine hand-written comparison, and document the result — not
level-specific, so kept separate from the schema sections above.

- [x] Author a minimal `ResourceGroup` TypeSpec fixture (`resourcegroup.tsp`) mimicking the real
      `Microsoft.Resources` ResourceGroups shape (`get`, `createOrUpdate` (PUT), `delete`,
      subscription-scoped `list`), chosen specifically because a real hand-written comparison
      exists (`src/PS/Resources/Cmdlets/{New,Get}ResourceGroupCmdlet.cs` +
      `src/Extensions/Resources/Processors/*ResourceGroupProcessor.cs`). Committed under
      `toolkit/packages/compiler/test/fixtures/resource-group/`. Deliberately not built on
      `@azure-tools/typespec-azure-resource-manager`'s resource-modeling decorators, so the
      resulting IR shape stays fully predictable. No static IR snapshot is committed — compiled
      dynamically at test time instead, matching this codebase's own established e2e-test
      precedent.
- [x] Wire `create-ps-commands` into a non-identity `TransformPipeline`, compile the
      `ResourceGroup` fixture through the real `compile()` backbone with it, then call
      `derivePsCommands()` on the result and confirm: `ResourceGroups.createOrUpdate` produces one
      `PsCommand` with verb `New`, noun `AzResourceGroup`, `SupportsShouldProcess = true`, and
      `ConfirmImpact = "Medium"`; `ResourceGroups.get` + `.list` merge into one `Get`/
      `AzResourceGroup` command with two parameter sets (matching `GetResourceGroupCmdlet`'s real
      `DefaultParameterSetName = "List"`); `ResourceGroups.delete` produces `Remove`/
      `AzResourceGroup`; the Expanded variant flattens `ResourceGroupProperties` (via a test-only
      flatten-decision step, since no real flatten-decision `TransformStep` exists yet) — proof
      the full `IR -> (enriched IR) -> PsCommand[]` path executes via the compiler backbone's
      existing pipeline mechanism plus the separate grouping function.
- [x] Update `toolkit`'s own README/status docs to reflect that a PS-command-enrichment
      `TransformStep` plus a `ps-commands` grouping package now exist (still no C# `Generator` —
      that remains a separate, later change). No such status doc existed yet (confirmed by
      inspection — `toolkit/README.md` only covered dependency management); added a "Pipeline
      status" section to it.
