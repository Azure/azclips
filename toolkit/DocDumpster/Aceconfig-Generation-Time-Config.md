# aceconfig.yaml: the Generation-Time Configuration File

> **Status:** Proposed — layout agreed in discussion; partly implemented (see [Phasing](#phasing-non-binding)). _(2026-10-06; revised 2026-10-07: `seed-ir`; 2026-10-12: `seed-ir` order and entry checks)_
>
> **Scope:** how a run of the Azure Client Engine (ACE) is configured: the file a
> user writes for each generated extension, how the `ace` CLI reads it, and what
> each pipeline stage receives from it. The directive format and remote specs are
> out of scope (see [Deferred](#deferred-recorded-not-designed-now)).
>
> This is a decision record of a design discussion. It captures the direction and
> the rationale; it is **not** an implementation plan and does not change code yet.

## Summary

One YAML file, `aceconfig.yaml`, describes everything needed to regenerate one
extension: where the TypeSpec comes from, which entrypoints to compile and at
which API version, where output goes, the facts about the extension that the spec
cannot provide, and per-stage settings. The **CLI deserializes the file; the
CLI and the compiler each take their own keys, and the compiler hands each stage
only its own entry, never interpreted**. A new mandatory first transform,
`seed-ir`, turns the non-spec facts (identity and placement today) into the IR's
top-level `target`. The stage catalog still owns order, and every stage runs
unless the file disables it. The CLI keeps today's flags as overrides for now.

## Context — where we are today

- **Configuration is flags plus an optional JSON `CompileConfig`.** `ace compile`
  takes `--tsp-location`, `--entrypoint` (temporary), `--api-version`,
  `--out-dir`, `--generator`, `--root-namespace`, and `--config <json>`, which is
  cast to `CompileConfig` (`pipeline` toggles, `stageOptions`, `apiVersion`,
  `emitter`, …) without validation (`packages/cli/src/compile-command.ts`).
- **The host maps flags onto stages by hand.** `--root-namespace` is written into
  `stageOptions["create-command"]`, and `--generator` sets a `pipeline` toggle.
  `--root-namespace` was removed on 2026-10-12: facts come only from config.
- **Every stage can read the whole config.** `TransformContext.config` and
  `GeneratorContext.config` expose all of `CompileConfig`. The `csharp` adapter
  forwards it whole to the C# host (`GenerateToolOptions.config`), which never
  reads it.
- **Stage options are already routed by id.** `stageOptions` is keyed by stage id
  and handed untouched to each stage's factory (`stageEntry()` in
  `packages/compiler/src/compile.ts`), and each step checks its own options when
  it runs.
- **One entrypoint, one flat copy.** Staging copies the `--tsp-location` directory
  flat into `<out-dir>/source`. Imports that reach outside that directory break;
  `--entrypoint` only partly works around this.
- **`--out-dir` defaults to the current directory,** and the CLI deletes that
  directory's `Models/`, `Processors/`, `source/` and `ir/`.
- **The target was built inside `create-command`.** Its `buildTarget` turned the
  `rootNamespace`, `projectName` and `directories` options into `IRTarget`, in the
  same stage that projects operations into commands. Until IR 0.14.0, `IRTarget`
  existed only on `TransformedIR`. Moved to `seed-ir` on 2026-10-12.
- **`transformers` depended on `compiler`** for the transform contract
  (`TransformStep`, `TransformContext`), and hosts registered every transform
  (`STAGE_IMPLEMENTATIONS` in `packages/cli/src/compile-command.ts`). Reversed on
  2026-10-07: see
  [Revision — compiler-owned stage implementations](./Compiler-Owned-Stage-Orchestration.md#revision--compiler-owned-stage-implementations).

## Decision

1. **One file per extension, named `aceconfig.yaml`.** It is named after the tool,
   parallel to `tspconfig.yaml`. `compiler-options.yaml` was rejected:
   `CompileOptions` already names host plumbing in the compiler, and TypeSpec's
   `CompilerOptions` is what the emit worker passes to `compile()`.

2. **The file is location-independent.** It may live anywhere; nothing depends on
   a repository layout. Relative paths in the file resolve against the file's own
   folder, and `output-dir` defaults to that folder, never to the current
   directory.

3. **Keys fall into three areas, by consumer:** run inputs (the CLI and
   `tsp-emit`), document facts (`seed-ir`), and `stages` (each stage reads its own
   section). See [The file](#the-file).

4. **One shared `tsp-location`, inside a specs repo.** `tsp-location` is the
   directory to copy. Entrypoints are relative to it and compile from inside the
   copy. See [Source acquisition](#source-acquisition).

5. **Several entrypoints merge into one extension.** Each entrypoint compiles
   separately at its own `api-version`, and the emit stage merges the results into
   one IR. See [Merging entrypoints](#merging-entrypoints).

6. **Everything the spec cannot provide goes to the top level.** These are
   document facts: `service-name`, `root-namespace`, `project-name`, `version`,
   `description` and `directories` today, with more to come. Their values are
   written out explicitly: no derivation, no interpolation. A fact is set only at
   the top level. Facts differ from instructions: how a stage should shape
   spec-derived content (its options, and directives once designed) stays in that
   stage's section.

7. **`seed-ir` resolves the facts into the IR.** It is a new mandatory transform
   that runs first among the transforms. It writes the IR's top-level `target`,
   computing defaults for missing facts. `IRTarget` becomes an optional property
   of `IR`: a field is required on `IR` only if the emitter produces it, and the
   emitter cannot produce `target`. `TransformedIR` keeps requiring it.
   `create-command` stops building the target. Every non-spec fact added later is
   resolved here too. For now the facts extend `IRTarget`; that is a temporary
   choice. See [seed-ir](#seed-ir).

8. **The compiler routes config by stage id and never interprets it.** The
   packages layer as `cli` → `compiler` → `transformers`, and every package
   depends on `ir` (see
   [Revision — compiler-owned stage implementations](./Compiler-Owned-Stage-Orchestration.md#revision--compiler-owned-stage-implementations)).
   Each party takes the keys it knows, and what is left is gen-time config. The
   CLI reads and deserializes the file, takes its own keys (the run inputs and
   `schema-version`), applies flag overrides, and passes the rest to the
   compiler. The compiler takes its own settings (`stages`) and builds one entry
   per stage: `stages.<id>.options` for each stage, and the top-level keys left
   over for `seed-ir`. It creates each stage's context, passes every entry
   untouched and validates nothing beyond routing; an unknown stage id is still
   an error. Neither the CLI nor the compiler knows the facts, and the CLI never
   names a stage. Each transformer owns the knowledge of its entry and resolves
   it at transform time. The compiler maps every stage id to its implementation
   itself, so hosts register nothing.

9. **Stages run unless disabled.** `stages` is a map keyed by catalog stage id.
   The catalog still owns order and `requires`. `"off"` is removed from
   `StageDefault`, so every stage is mandatory or on, and `disabled: true` is the
   only switch. Opt-in behavior becomes an option of a stage that always runs.
   See [Stage enablement](#stage-enablement).

10. **Each stage receives only its own entry.** The compiler picks it out by the
    stage's id and puts it in that stage's context: `TransformContext`, or
    `GeneratorContext` for a generator. No stage sees the whole config, so the
    contexts stop exposing it, and the `csharp` adapter forwards only its own
    entry. An entry is a plain dictionary, resolved lazily by the stage when it
    runs, as options are today. Every stage checks its own entry the same way: a
    key it does not know is a warning, and an invalid value, or a missing key the
    stage requires, is an error that fails the stage. No default stands in for an
    invalid value, and the compiler stops the run after a stage that reports an
    error (2026-10-12). Directives, once designed, live inside a stage's
    `options`.

11. **Casing.** Structural keys are kebab-case, and stage ids are the catalog ids.
    Everything under `options:` is camelCase, so today's option names pass through
    without mapping: `inliningThreshold` and `noInline` for `flatten-model`, and
    `CompileConfig`'s `warnAsError`, `trace`, `nostdlib` and `additionalImports`
    for `tsp-emit`.

12. **No flag sets a document fact; the other flags stay as overrides, for now.**
    Facts live only in the file, so the file alone reproduces an extension's
    identity: `--root-namespace` is removed (2026-10-12). `--generator` is
    removed and `--config` takes an `aceconfig.yaml`. Whether to retire the
    remaining run-input flags is decided later. See [CLI](#cli).

## The file

### Example

```yaml
schema-version: 1

# Run inputs: the CLI copies the spec; tsp-emit compiles each entrypoint.
output-dir: .                            # default: this file's folder
tsp-location: ../azure-rest-api-specs/specification/elasticsan/resource-manager/Microsoft.ElasticSan/ElasticSan
entrypoints:                             # relative to tsp-location
  - path: client.tsp
    api-version: 2024-05-01

# Document facts: what the spec cannot provide; seed-ir writes them into the IR's target.
service-name: ElasticSan
root-namespace: Microsoft.Azure.Azclips.Extensions.ElasticSan
project-name: Microsoft.Azure.Azclips.Extensions.ElasticSan
version: 0.1.0
description: ElasticSan extension for AzClips
directories:
  models: Models
  processors: Processors

# Stages: keyed by stage id; every stage runs unless disabled.
stages:
  tsp-emit:
    options:
      warnAsError: true
  flatten-model:
    options:
      inliningThreshold: 24
  # tweak-ir:
  #   disabled: true
```

### Fields

| Key | Value | Default | Consumed by |
|---|---|---|---|
| `schema-version` | `1` | — | config loader |
| `output-dir` | folder, relative to this file | this file's folder | CLI: output root; staging under `<output-dir>/source` |
| `tsp-location` | folder inside a specs repo, relative to this file | — (required unless `--tsp-location` is given) | CLI: the directory to copy |
| `entrypoints[].path` | `.tsp` file or folder, relative to `tsp-location` | see [Open details](#open-details-proposed-defaults-confirm-when-implementing) | CLI (staging), `tsp-emit` |
| `entrypoints[].api-version` | a version, `latest`, or `all` | see [Open details](#open-details-proposed-defaults-confirm-when-implementing) | `tsp-emit` |
| `service-name` | string | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `root-namespace` | dotted namespace | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `project-name` | string | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `version` | string | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `description` | string | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `directories.models`, `directories.processors` | relative folders in the output | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `stages.<id>.disabled` | boolean | `false` | the compiler's planner |
| `stages.<id>.options` | map, camelCase keys, passed unchanged | `{}` | that stage only |

### Stage enablement

A stage runs unless its section says `disabled: true`.

| What the file says about a stage | Result |
|---|---|
| no `stages:` key at all | runs |
| its id is absent from `stages:` | runs |
| an empty section (`<id>:` with no value) | runs |
| only `options:` | runs, with those options |
| `disabled: false` | runs |
| `disabled: true` | skipped |
| `disabled: true` on a mandatory stage (`tsp-emit`, `seed-ir`) | error: `stage-not-optional` |
| `disabled: true` on a stage an enabled stage `requires` (e.g. `create-command`, required by `csharp`) | error: `missing-required-stage`, unless the dependent stage is disabled too |
| an id the catalog does not declare | error: `unknown-stage` |
| every generate stage disabled | warning: `no-generator`; the run produces IR only |

The errors and the warning are today's planner behavior, unchanged. A stage added
to the catalog later runs in every existing file without edits.

## seed-ir

The catalog becomes:
`tsp-emit` → `seed-ir` → `tweak-ir` → `flatten-model` → `create-command` → `csharp`.

- **Mandatory, and first among the transforms.** Without it there would be no
  target, and the generator would reject the document.
- **Input:** its entry, every top-level key that neither the CLI (the run inputs
  and `schema-version`) nor the compiler (`stages`) takes. No flag overrides a
  fact. Keys arrive as written (kebab-case). It has no `options:` section of its
  own.
- **Output:** the IR's top-level `target`. Missing facts get defaults, which are
  today's `buildTarget` rules moved into this stage:

  | Fact | Default when absent |
  |---|---|
  | `root-namespace` | the first entrypoint's spec namespace (`sources[0]`), else `Generated` |
  | `project-name` | the root namespace |
  | `directories` | `models: Models`, `processors: Processors` |
  | `service-name`, `version`, `description` | unset |

- **Checking the entry (agreed 2026-10-12).** `seed-ir` owns the facts' schema
  and checks its entry when it runs, as every stage does (decision 10):
  - A fact with the wrong type, or an empty value, is an error (`invalid-fact`)
    that fails the stage: no default stands in for it, no target is written,
    and the run stops. Every fact is still checked first, so one run reports
    them all. For example, YAML reads `version: 1.0` as the number 1, so it must
    be quoted.
  - `root-namespace` is a dotted string with no empty segments. A list of
    segments is not accepted.
  - A key it does not know, at the top level or under `directories`, is a
    warning (`unknown-fact`). Its entry is whatever the CLI and the compiler
    leave at the top level, so this catches misspelled top-level keys.
  - A key with no value (YAML `null`) counts as absent, and gets its default.

- **Boundaries.** It writes only top-level IR nodes. It never renames anything and
  never touches ids or spec-derived content (models, operations). `tweak-ir`
  therefore stays the only renaming stage, and it still runs before the stages
  that copy names. `seed-ir` never reads another stage's section or host-only
  inputs such as `tsp-location`.
- **Registration.** It lives in `transformers` and owns the schema of the facts.
  The compiler maps it to its id like every other transform; hosts register
  nothing. `create-command` no longer builds a target: it declares
  `requires: ["seed-ir"]` and fails if the target is missing.

### Where the facts live in the IR (2026-10-07)

- **For now, the facts extend `IRTarget`.** Service name, version and
  description join root namespace, project name and directories. **Temporary:**
  `IRTarget` began as placement, and more non-spec facts are expected. Two
  alternatives stay open for when they arrive:
  - a separate top-level node for identity and package metadata, leaving
    `IRTarget` as placement only;
  - one umbrella node holding every non-spec fact, so `seed-ir` writes exactly one
    node.
- **Required fields come from the emitter.** A field is required on `IR` only if
  the emitter produces it, so `target` is optional on `IR` while `TransformedIR`
  keeps requiring it. An alternative was discussed and not taken: a separate
  post-`seed-ir` document phase in which `target` is required.

### First implementation cut (agreed 2026-10-07)

- **IR side only.** Move `buildTarget`'s defaults out of `create-command` into
  `seed-ir`, and add service name, version and description to `IRTarget`. That
  changes the IR schema: bump `IR_SCHEMA_VERSION`, regenerate `ir.schema.json` and
  `Ir.Generated.cs`, and migrate the `elasticsan.ir.json` fixture.
- **Not in this cut:** having the C# generator write the new fields (such as
  `Version` and `Description`) into the generated `.csproj`.
- **Order: `seed-ir` first (decided 2026-10-12).** It is built ahead of
  [phasing](#phasing-non-binding) steps 1–3. Not taken: aceconfig loading,
  staging and routing first, then `seed-ir`.
- **Routing before the file exists (2026-10-12).** The compiler already routes
  the leftovers as in decision 8. Until phase 3, its own settings are today's
  `CompileConfig` fields (`apiVersion`, `additionalImports`, `warnAsError`,
  `trace`, `nostdlib`, `emitter`, `pipeline` and `stageOptions`) rather than
  `stages`. A JSON `--config` therefore carries facts as flat kebab-case keys
  beside them, and every other key goes to `seed-ir`. `--root-namespace` is
  removed: no flag sets a fact (decision 12). Rejected: an
  interim `stageOptions["seed-ir"]` entry written by the CLI, which made the CLI
  choose the stage.
- **Progress (2026-10-07):** the work started with `IRTarget`, which landed as IR
  0.14.0. `target` is optional on `IR`, `IRTarget` has optional `serviceName`,
  `version` and `description`, the JSON Schema and C# classes are regenerated, and
  the `elasticsan.ir.json` fixture is stamped 0.14.0 (the additions are optional, so
  its content needed no change). `create-command` still resolves the target, and
  nothing writes the new fields yet. Remaining in this cut: `seed-ir` itself,
  taking over `buildTarget`'s defaults.
- **Progress (2026-10-12):** `seed-ir` landed, completing this cut.
  - It is the mandatory first transform: `seedIrStep` in `transformers`, mapped
    in the compiler's `TRANSFORMS`. It writes every fact, the identity facts
    included, and checks its entry as described above.
  - `create-command` no longer builds a target, and the catalog declares its
    `requires: ["seed-ir"]`.
  - `CompileConfig` admits facts as extra top-level keys. The compiler's
    `stageEntry()` hands `seed-ir` every key that is not one of its own
    settings, kept in step with `CompileConfig` by a type check.
  - Captured IR gains `ir/1-seed-ir.json`, and later indices shift by one.
  - `--root-namespace` is removed; the root namespace, like every fact, comes
    only from config.
  - The C# generator's own `rootNamespace` option is removed. It dated from
    phase 1, before the IR had a target, and was by now only a fallback for
    documents without one. The tool request carries no namespace, and the
    generator rejects a document without a target
    (`ace-generator/missing-target`) before its cleanup pass.
  - A transform that reports an error now fails, and the compiler stops the
    run after it, as it already did after emit. An invalid fact therefore
    stops the run at `seed-ir` instead of falling back to a default. One other
    behavior changes with it: `create-command`'s `duplicate-command-identity`
    error now stops the run before generation.
  - The other stages do not warn about unknown keys yet.

## Source acquisition

The CLI stages the spec before the compiler runs, following AzDev's
`New-DevTSPModule` (`Azure/azure-powershell`, `tools/AzDev/src/Typespec`):

1. **Find the specs repo root.** Walk up from `tsp-location` to the first folder
   that contains `specification/`, `.github/` and `.azure-pipelines/` (AzDev's
   `IsSpecRoot`). No `.git` is needed, so zip downloads, worktrees and the
   private `-pr` repo all work. If no root is found, staging fails.
2. **Collect `additionalDirectories`.** For each entrypoint, read the nearest
   `tspconfig.yaml` at or above it (TypeSpec's own lookup rule), under
   `options."@azure-tools/typespec-client-generator-cli".additionalDirectories`.
   The paths are relative to the specs repo root (`specification/...`). Combine
   them across entrypoints.
3. **Copy, keeping the repo's layout.** Copy `tsp-location` and each additional
   directory to `<output-dir>/source/<path relative to the specs repo root>`, as
   AzDev's `TempTypeSpecFiles` does. Imports such as `../Shared/x.tsp` keep
   working, and a future remote sparse checkout produces the same tree.
   - Skip directories that are already inside a copied one.
   - Exclude `node_modules`, `.git` and `tsp-output`.
   - A listed directory that is missing locally is an error.
   - Each copied directory must not overlap `<output-dir>/source`; the existing
     guard applies.
4. **Provide the toolchain.** The `node_modules` link stays at
   `<output-dir>/source/node_modules`, above every copied directory.
5. **Compile from the copy.** Each entrypoint maps to its counterpart inside
   `<output-dir>/source`.

**Test escape hatch.** With `ACE_ALLOW_STANDALONE_SPEC=1`, a `tsp-location`
outside any specs repo is copied flat into `<output-dir>/source`, as today. Any
`additionalDirectories` are then an error, because there is no root to resolve
them against. Inside a specs repo the variable has no effect.

Cleanup and `--capture-source` behave as today.

In azure-rest-api-specs (at `b91dabc3`), 32 `tspconfig.yaml` files declare
`additionalDirectories`, all as `specification/...` paths. Some point inside the
project, which the copy already covers; others point at siblings such as
`Contoso.WidgetManager.Shared`. Foundry keeps one `tspconfig.yaml` per client
folder (`src/sdk-*/`), which is why the lookup is per entrypoint.

## Merging entrypoints

- **One compile per entrypoint.** Each entrypoint becomes its own TypeSpec compile
  at its own `api-version`, producing its own IR. A single version string cannot
  fit several specs, which version independently.
- **The emit stage merges.** The results are combined into one IR before the
  first transform, so the linear sequence still carries exactly one IR.
- **Most of the merge works already.** Types declared in a spec carry that spec's
  source id as `IRId.module`, so two specs cannot collide. Library types
  (Azure.Core, ARM common types) carry the library's package name instead
  (`sourceModule` in `packages/typespec-emitter/src/types.ts`), so they
  deduplicate by id. The IR already holds several `sources`, one server
  definition per source, and `IRCommandId.module`, and `tweak-ir` already
  resolves name clashes across namespaces.
- **Errors:**
  - the same id with two different shapes, for example two specs pinned to
    different ARM common-types versions;
  - the same service (source id) listed twice.

## CLI

| Flag | Effect |
|---|---|
| `--config <path>` | the `aceconfig.yaml` to use; without it, `./aceconfig.yaml` when present |
| `--tsp-location <path>` | overrides `tsp-location` |
| `--entrypoint <path>` | overrides `entrypoints` with a single entry (still temporary) |
| `--api-version <v>` | overrides the API version; allowed only with a single entrypoint |
| `--out-dir <path>` | overrides `output-dir` |
| `--root-namespace` | removed: no flag sets a document fact |
| `--capture-ir`, `--capture-source` | run switches, unchanged |
| `--generator` | removed: with nothing off by default, there is nothing for it to switch on |

- **Precedence:** flag, then file, then default.
- **Path resolution:** relative paths in flags resolve against the current
  directory; relative paths in the file resolve against the file's folder.
  `--entrypoint` stays relative to `tsp-location`.
- **No file:** without `--config` or `./aceconfig.yaml`, flags alone work as
  today.
- **The old JSON format is retired:** `--config` no longer accepts the JSON
  `CompileConfig` shape (`pipeline`, `stageOptions`).

## Why — benefits

- **Reproducible from one reviewable file.** Everything that shapes an
  extension's output is in one file, and regeneration needs no arguments.
- **No hidden repository conventions.** Paths resolve against the file, so the
  same file works from any checkout or working directory, and output no longer
  defaults to the current directory.
- **Orchestration stays compiler-owned, and stage contracts shrink.** A stage
  sees only its own entry. The generator sees the IR plus its own entry, so a
  captured IR explains the output's identity.
- **One place turns non-spec facts into IR.** `create-command` only projects
  commands, and later stages, including future client transforms, read identity
  from the IR.
- **The compiler stays schema-free.** It routes entries by id, so it never
  changes when a stage gains an option or the facts grow.
- **Multi-spec extensions without copying a common root.** Only `tsp-location`
  and the declared `additionalDirectories` are copied.
- **Same model as AzDev.** Specs-repo detection, the mirrored temporary layout and
  the specs-repo requirement match the azure-powershell workflow.

## Costs / tradeoffs (recorded honestly)

- **Stage ids become a committed surface.** Renaming or splitting a stage breaks
  every file that names it. Keep ids stable, or accept the old id after a split.
- **New stages run everywhere.** A stage added to the catalog runs in every
  existing file on its next regeneration.
- **Lazy validation lets typos through, for now.** The compiler never validates.
  Every stage is to warn about keys it does not know (decision 10), but so far
  only `seed-ir` does; the other stages' option readers still ignore unknown
  keys, so a misspelled option is silently ignored.
- **Mixed casing in one file:** kebab-case structure, camelCase options.
- **`IR` gains a field the emitter never writes.** `target` is optional on `IR`,
  so only `commands` now marks that `create-command` ran.
- **Specs must live in a specs repo.** Ad-hoc local specs need the test-only
  escape hatch.
- **Merges can fail on shared types,** with no automatic resolution.
- **Two input paths until the flag review.** Both flags and the file configure a
  run's inputs. Facts come only from the file.

## Conflicts with current code/tests (reconcile when implementing)

Recorded so implementation changes them deliberately. Early tests are listed
here, not used to justify keeping the old model. Paths are relative to the
toolkit root.

- **Source:**
  - `packages/compiler/src/stages.ts:48`: `StageDefault` includes `"off"`.
  - `packages/compiler/src/types.ts`: `CompileInput.entrypoint` is a single path.
    `CompileConfig` carries one `apiVersion`; top-level `additionalImports`,
    `warnAsError`, `trace`, `nostdlib` and `emitter`; the `pipeline` and
    `stageOptions` maps; and the facts as any other key.
  - `packages/compiler/src/generate.ts:30`: `GeneratorContext` exposes the whole
    `CompileConfig`. `TransformContext` (now `packages/transformers/src/transform.ts`)
    carries none; a transform's options still arrive through its factory.
  - `packages/compiler/src/compile.ts:334` (`stageEntry()`): delivers each
    entry through the stage's factory; entries move into the stage's context.
    Its list of the compiler's own settings shrinks to `stages`.
  - `packages/compiler/src/builtins/generate-csharp.ts`: forwards
    `context.config` whole to the C# host.
  - `packages/compiler/src/builtins/emit.ts`: sends one `EmitToolRequest` per run.
  - `packages/cli/src/compile-command.ts`: the JSON `--config` loader,
    `--generator`, `usage()`, and `--out-dir` defaulting to `process.cwd()`.
  - `packages/cli/src/staging.ts`: a flat copy of one directory, with no
    specs-repo detection, `additionalDirectories` or `tsp-output` exclusion.
  - Docs: `README.md` (the `{ "pipeline": … }` example and
    `config.stageOptions`, lines 80, 149, 207) and
    `packages/generator/README.md` (`ace compile` usage with `--generator`, and
    the temporary `--entrypoint` note).
- **Tests (early-stage; reconcile, don't let them shape the design):**
  - `packages/cli/test/cli.test.ts`: compiles `packages/ir/examples/pets.tsp`
    and `packages/typespec-emitter/test/fixtures/multipart-service.tsp`, neither
    inside a specs repo, so they need the escape hatch. It also writes JSON
    `--config` files with `pipeline` toggles and uses `--generator csharp`.
  - `packages/cli/test/staging.test.ts`: builds spec trees in temp folders and
    asserts the flat layout.
  - `packages/cli/test/emit.test.ts`: passes `config.emitter.sourceId` and
    `config.apiVersion` to `compile()`, and turns stages off through
    `config.pipeline` (`EMIT_ONLY`, line 28).
  - `packages/compiler/test/stages.test.ts:99`: a catalog stage with
    `default: "off"`.
  - `packages/compiler/test/runtime.test.ts:97` and
    `packages/compiler/test/pipeline.test.ts:52,113,167`: build `CompileConfig`
    with `pipeline`, `stageOptions`, `apiVersion` and `emitter`.

## Relationship to other records

- [`Compiler-Owned-Stage-Orchestration.md`](./Compiler-Owned-Stage-Orchestration.md):
  its point 3 ("turn-on only") shipped as a two-way toggle (`config.pipeline`).
  This record narrows it to **disable-only** and moves the switch into
  `stages.<id>.disabled`. Catalog ownership of order and `requires` is unchanged.
  Its 2026-10-07 revision records the package layering this design relies on:
  the compiler owns stage implementations, and hosts register nothing.
- [`TransformedIR-schema.md`](./TransformedIR-schema.md): `IRTarget` stays
  "resolved once, at transform time, from generation-time configuration", now by
  `seed-ir` rather than `create-command`. This file is that configuration.
  `target` moves onto `IR` as an optional property, and `TransformedIR` still
  requires it.
- [`Emitter-IR-check.md` TODO-1](./Emitter-IR-check.md#todo-1): a deliberate TCGC
  scope token is a prerequisite if emit-time alterations are ever added.
- `packages/generator/README.md`: its temporary `--entrypoint` note pointed at
  this design.
- **AzDev `New-DevTSPModule`** (`Azure/azure-powershell`,
  `tools/AzDev/src/Typespec`, at `731535de`):
  - **Taken:** specs-repo marker detection, requiring a specs repo, the
    repo-mirrored temporary layout, the `tsp-output` exclusion, discovering the
    config in the current directory, and output falling back to the config's
    folder.
  - **Not taken:** `npm install` of a pinned emitter package on every run (ACE
    links its own toolchain); writing a merged `tspconfig.yaml` and running
    `tsp compile` (ACE calls the compiler API); output relative to a repo root;
    and reading `additionalDirectories` from the emitter's own `tspconfig.yaml`
    section (no spec has an ACE section).

## Deferred (recorded, not designed now)

- **Directive format.** Directives will live in a stage's `options`. Selectors,
  actions and matching rules are not designed yet.
- **Emit-time alterations.** There is no concrete use case, and they would
  largely duplicate `tspconfig.yaml`.
- **Remote specs.** `repo` + `commit` sources using AzDev's flow: clone without
  checkout, sparse-checkout the same directories, then check out the commit. This
  produces the same mirrored tree. Provenance would be recorded with `/`
  separators.
- **Schema polish.** The other stages adopting the unknown-key warning (the
  mechanism is decided in decision 10, and `seed-ir` has it), and a JSON Schema
  for editor completion. The compiler stays validation-free.
- **Flag review.** Whether to retire the remaining run-input flags, and
  `--out-dir` vs. `output-dir` naming.
- **Generator use of the new `IRTarget` fields.** The C# generator writing
  service name, version and description into the generated project (for example
  the `.csproj` `Version` and `Description`) follows `seed-ir`'s first cut.

## Open details (proposed defaults; confirm when implementing)

- **`entrypoints` omitted:** one entrypoint, `tsp-location` itself (today's
  `--entrypoint` default).
- **`api-version` omitted:** `latest` (today's default).
- **Required keys:** `schema-version`, and `tsp-location` unless
  `--tsp-location` is given.
- **`tspconfig.yaml` lookup:** stops at the specs repo root.
- **`options` on a disabled stage:** ignored, or a warning.
- **Emitter options such as `sourceId`:** no place yet. A single `sourceId` would
  collide across entrypoints.
- **YAML parser:** the CLI, which owns deserialization, should use a YAML 1.2
  parser (the `yaml` package, already installed through TypeSpec but not yet a
  CLI dependency), so that `api-version: 2024-05-01` stays a string. js-yaml 3.13
  reads it as a date.

## Phasing (non-binding)

1. **Load the file:** the CLI reads and deserializes it, takes its own keys,
   resolves paths, applies flag overrides and passes the rest to the compiler.
   Map `stages` onto today's `pipeline` and `stageOptions`, with a single
   entrypoint; facts already pass through as top-level keys.
2. **Staging:** specs-repo detection, the mirrored layout, `additionalDirectories`,
   the `tsp-output` exclusion, and the escape hatch.
3. **Compiler:** drop `"off"`, move each stage's entry from its factory into its
   context (`seed-ir`'s included), shrink the compiler's own settings to
   `stages`, remove the whole-config contexts, and retire `--generator`.
4. **`seed-ir`:** done, ahead of steps 1–3. The IR part, `IRTarget` on `IR` with
   the new fields (IR 0.14.0), landed on 2026-10-07; the stage itself, taking
   over `buildTarget`'s defaults from `create-command`, landed on 2026-10-12 (see
   [First implementation cut](#first-implementation-cut-agreed-2026-10-07)).
5. **Multiple entrypoints:** one compile per entrypoint, merged in the emit stage.

The package-layering refactor
([Revision — compiler-owned stage implementations](./Compiler-Owned-Stage-Orchestration.md#revision--compiler-owned-stage-implementations))
landed on 2026-10-07, ahead of these steps, so `seed-ir` needs no host
registration.
