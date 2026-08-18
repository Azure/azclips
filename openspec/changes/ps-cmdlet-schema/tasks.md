Tasks are organized into milestones, each a working checkpoint rather than a component. Milestone
0 is a deliberately thin vertical slice: minimal `hints.powershell` + minimal `ps-commands`
aggregate + minimal derivation, wired end-to-end and proven against a real fixture, before any
richness is added. See `design.md`'s Decisions 1/2/8 for why derivation is split into two stages
(the `create-ps-commands` `TransformStep` enriches `hints.powershell`; the separate
`derivePsCommands()` function groups the result) and the Appendix for the full schema reference.

## Milestone 0: Fast path — prove `IR -> PsCommand[]` works end-to-end

Goal: one simple IR operation produces one correct `PsCommand`, run through the real pipeline
mechanism plus `derivePsCommands()`. No directives yet; inline-hint overrides work "for free" once
`hints.powershell` exists, since `create-ps-commands` must respect last-writer-wins semantics.

- [x] 0.1 Add a minimal `powershell` sub-namespace to `toolkit/packages/ir/src/hints.ts`'s
      `OperationHints` (not `CommonHints` — these fields are operation-specific and would
      otherwise leak onto `ModelHints`/`EnumHints`/`ParameterHints`, which alias `CommonHints`
      directly): `verb`, `noun`, `parameterSetName`, `confirmImpact` only. This is a hard
      prerequisite for `create-ps-commands` to write into — not deferrable authoring-hook polish.
      Add a doc comment describing the new namespace (`hints.ts` has no prior mention of
      "powershell" today — confirmed by inspection, not an existing comment needing an update).
- [x] 0.2 Add unit tests confirming the new namespace round-trips through IR's existing Zod
      validation without affecting non-PS consumers.
- [x] 0.3 Create `toolkit/packages/ps-commands` (npm name `@azure-client-engine/ps-commands`),
      matching the existing `@azure-client-engine/<name>` workspace conventions (`ir`, `compiler`,
      `typespec-emitter`) — `package.json`, `tsconfig.json`, `src/index.ts`. Add this package to
      the root `toolkit/tsconfig.json` solution's `references` array — npm workspaces alone
      won't include it in `tsc -b` builds.
- [x] 0.4 Add `@azure-client-engine/ir` and `zod` as dependencies.
- [x] 0.5 Define the thin `PsParameterSet` aggregate (Zod schema): `name` (source-operation-derived,
      e.g. `"Get"`/`"List"`), `sourceOperationId` (reference into IR — parameter details live on
      the referenced `IRParameter`'s `hints.powershell`, not redeclared here).
- [x] 0.6 Define the thin `PsCommand` aggregate: `verb`, `noun`, `subjectPrefix`,
      `supportsShouldProcess`, `confirmImpact` (copied from the grouped operations' resolved
      `hints.powershell`), `outputTypes` (by-id refs), `parameterSets` (`PsParameterSet[]`),
      `sourceOperationIds`.
- [x] 0.7 Write unit tests asserting the schema rejects a command with no parameter sets and
      rejects a parameter set with no source operation id (Zod validation smoke tests).
- [x] 0.8 Scaffold the `create-ps-commands` `TransformStep` in `toolkit/packages/compiler`
      (implements the real `TransformStep` interface — `run(ir: IR, context): IR` — from
      `transform.ts`; it enriches `hints.powershell` in place, it does not return a `PsCommand[]`),
      registered via `CompilerRegistry` under id `create-ps-commands`.
- [x] 0.9 Implement namespace-based grouping: recover client/operation-group candidates from
      each `IROperation.id.namespace`.
- [x] 0.10 Implement the verb-mapping table (seeded from AutoRest's default
      `autorest-configuration.md` table): action-word table lookup always wins (`Create`/
      `CreateOrUpdate`->`New`, `Update`->`Update`, etc.); only fall back to an HTTP-method-based
      default (PUT->`Set`, PATCH->`Update`, POST->`Invoke`) when the action word has no table
      entry at all. Write the resolved verb into `hints.powershell.verb`, respecting any
      pre-existing authored value (last-writer-wins — an author's inline override is never
      clobbered). Verify against the real `ResourceGroups.createOrUpdate` (PUT) precedent, which
      must resolve to `New`, not `Set`.
- [x] 0.11 Implement noun inference: singularize the resource/model name, write into
      `hints.powershell.noun` (bare, no prefix); write the configurable subject prefix into its
      own `hints.powershell.subjectPrefix` field (not baked into `noun` — kept separate so
      `PsCommand.subjectPrefix` in `ps-commands` can be populated too; combined into the final,
      generation-ready `PsCommand.noun` by `derivePsCommands`, task 0.13).
- [x] 0.12 Implement `SupportsShouldProcess`/`ConfirmImpact` policy from HTTP method (PUT/POST/
      PATCH/DELETE => true; GET => false); write into `hints.powershell.confirmImpact`.
- [x] 0.13 Implement `derivePsCommands(ir: IR): PsCommand[]` in `ps-commands` — a plain function, not
      a pipeline stage — that reads the now-enriched IR and groups operations under the same
      `id.namespace` sharing a resolved verb+noun into one `PsCommand`, with one `PsParameterSet` per
      source operation.
- [x] 0.14 Author a minimal `ResourceGroup` TypeSpec fixture (`resourcegroup.tsp`) mimicking the
      real `Microsoft.Resources` ResourceGroups shape (`get`, `createOrUpdate` (PUT), `delete`,
      subscription-scoped `list`), chosen specifically because a real hand-written comparison
      exists (`src/PS/Resources/Cmdlets/{New,Get}ResourceGroupCmdlet.cs` +
      `src/Extensions/Resources/Processors/*ResourceGroupProcessor.cs`). Committed under
      `toolkit/packages/compiler/test/fixtures/resource-group/`. Deliberately NOT built on
      `@azure-tools/typespec-azure-resource-manager`'s resource-modeling decorators, so the
      resulting IR shape (operation names, HTTP methods, namespace) stays fully predictable.
      No static `resourcegroup.ir.json` is committed — compiled dynamically at test time instead
      (via `compile()`), matching this codebase's own established e2e-test precedent
      (`typespec-emitter/test/emitter.e2e.test.ts`'s and `compiler/test/compile.e2e.test.ts`'s
      own stated rationale: a static IR snapshot would be robust against nothing an emitter
      change wouldn't immediately invalidate anyway, and risks machine-specific absolute paths).
- [x] 0.15 Wire `create-ps-commands` into a non-identity `TransformPipeline`, compile the
      `ResourceGroup` fixture through the real `compile()` backbone with it, then call
      `derivePsCommands()` on the result and confirm: `ResourceGroups.createOrUpdate` produces one
      `PsCommand` with verb `New`, noun `AzResourceGroup`, `SupportsShouldProcess = true`, and
      `ConfirmImpact = "Medium"`; `ResourceGroups.get` + `.list` merge into one `Get`/`AzResourceGroup`
      command with two parameter sets; `ResourceGroups.delete` produces `Remove`/`AzResourceGroup`
      — proof the full `IR -> (enriched IR) -> PsCommand[]` path executes via the compiler
      backbone's existing pipeline mechanism plus the separate grouping function.
- [x] 0.16 Wire `create-ps-commands` into the real `ace compile` CLI (moved up from Milestone 3,
      since M0 already proves the underlying mechanism works): `toolkit/packages/cli` registers
      `create-ps-commands` in a default `CompilerRegistry`. A config file with `{"pipeline":
      [{"use": "create-ps-commands"}]}` now opts a real `ace compile` invocation into
      `hints.powershell` enrichment; absent that key, behavior is byte-for-byte unchanged
      (identity pipeline).
- [x] 0.17 Register a `derive-ps-commands` `Generator` (`toolkit/packages/cli`) — a debug/preview
      generator, NOT the real Roslyn/C# generator, whose `generate()` calls `derivePsCommands(ir)`
      internally and serializes the result as `ps-commands.json`. Gives a real `ace compile`
      invocation a concrete way to see `PsCommand[]` output today (no C# back end exists yet to
      consume it) via `{"generator": {"use": "derive-ps-commands"}}`; the default remains
      `noopGenerator`, unaffected. Confirms `derivePsCommands` is only ever called from inside a
      `Generator`'s own `generate()` — never a separate pipeline stage — see design.md Decision 8.

## Milestone 1: Full vocabulary & derivation richness (ps-command-vocabulary, ps-command-derivation)

Goal: fill in everything Milestone 0 deferred so the schema/derivation match real precedent
exactly, not just the simple case.

- [ ] 1.1 Add a `powershell` sub-namespace to `toolkit/packages/ir/src/hints.ts`'s
      `ParameterHints` (this is its first field — `ParameterHints` was still a plain `CommonHints`
      alias after Milestone 0) with: `mandatory`, `parameterSetName`, `helpMessage`,
      `valueFromPipeline`/`valueFromPipelineByPropertyName`, `supportsWildcards`, `aliases`,
      `allowedValues` (ValidateSet), completer reference, mutability flags (`readOnly`/`create`/
      `update`, mirroring AutoRest's real `InfoAttribute.ReadOnly/Create/Update` — lets generated
      Update variants correctly omit create-only/server-computed properties), and an optional
      default-value expression (script/name/set-condition, mirroring `DefaultInfoAttribute`).
- [ ] 1.2 Extend `hints.powershell` on `IROperation.rendering` with: `alias`, `hidden`, completer
      reference, summary/description/examples (mirrors `ProcessorInfoAttribute`), a lifecycle tag
      (Preview/Deprecated/Experimental/GA), and an `HttpPath`-equivalent (path template +
      api-version — trivially derivable from the operation itself).
- [ ] 1.3 Implement single-operation variant fan-out limited to `base` and `Expanded` (reusing
      IR's existing `FlattenHint`) inside `derivePsCommands()`; explicitly do not implement
      `ViaIdentity`/`ViaJsonString`.
- [ ] 1.4 Write unit tests using the `ResourceGroup` fixture as a real-data regression case,
      diffed against the real hand-written cmdlets: assert `ResourceGroups.get` +
      `ResourceGroups.list` merge into one command with `Get`/`List` parameter sets (matching
      `GetResourceGroupCmdlet`'s real `DefaultParameterSetName = "List"` + two parameter sets),
      assert `ResourceGroups.createOrUpdate` (PUT) resolves to verb `New` — not `Set` — matching
      `NewResourceGroupCmdlet`, and assert the Expanded variant flattens `ResourceGroupProperties`.

## Milestone 2: Directive-style customization (ps-command-customization)

Goal: bulk/pattern-based overrides layered on top of the inline-hint overrides that already work
as of Milestone 0 (an author setting `hints.powershell.verb` directly is already honored by
`create-ps-commands`'s last-writer-wins write — no additional wiring needed for that case).

- [ ] 2.1 Design and document the concrete `select`/`where`/`set` directive grammar (JSON/YAML
      shape) accepted as the `create-ps-commands` step's `options`, modeled on AutoRest's
      `docs/directives.md` (selectors: `command`, `parameter`; filters: verb, subject,
      subject-prefix, parameter-name).
- [ ] 2.2 Implement directive matching and application inside the `create-ps-commands` step, applied
      as defaults before the final `hints.powershell` write (per design.md Decision 7's
      precedence rule: an existing inline hint value wins over a directive's).
- [ ] 2.3 Write unit tests covering: a directive renaming a parameter alias across matching
      commands, a directive hiding a command, and a conflicting directive+inline-hint case
      resolving in the inline hint's favor.

## Milestone 3: Integration & documentation

- [ ] 3.1 Update `toolkit`'s own README/status docs (whichever tracked file currently describes
      pipeline completeness) to reflect that a PS-command-enrichment `TransformStep` plus a
      `ps-commands` grouping package now exist (still no C# `Generator` — that remains a
      separate, later change).
