# aceconfig.yaml: the Generation-Time Configuration File

> **Status:** Proposed — layout agreed in discussion; partly implemented (see [Phasing](#phasing-non-binding)). _(2026-10-06; revised 2026-10-07: `seed-ir`; 2026-10-12: `seed-ir` order and stage config checks; 2026-10-14: source acquisition; 2026-10-15: fewer facts, positional config file)_
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
only its own stage config, never interpreted**. A new mandatory first transform,
`seed-ir`, turns the non-spec facts (identity and placement today) into the IR's
top-level `target`. The stage catalog still owns order, and every stage runs
unless the file disables it. The CLI keeps today's flags as overrides for now.

## Context — where we are today

- **Configuration is flags plus an optional JSON `CompileConfig`.** `ace compile`
  takes `--tsp-location`, `--entrypoint` (temporary), `--api-version`,
  `--out-dir`, `--generator`, `--root-namespace`, and `--config <json>`, which is
  cast to `CompileConfig` (`pipeline` toggles, `stageOptions`, `apiVersion`,
  `emitter`, …) without validation (`packages/cli/src/compile-command.ts`).
  Replaced by `aceconfig.yaml` on 2026-10-13.
- **The host maps flags onto stages by hand.** `--root-namespace` is written into
  `stageOptions["create-command"]`, and `--generator` sets a `pipeline` toggle.
  Both flags were removed on 2026-10-12: facts come only from config, and with
  every stage on unless disabled, there is nothing for `--generator` to switch on.
- **Every stage can read the whole config.** `TransformContext.config` and
  `GeneratorContext.config` expose all of `CompileConfig`. The `csharp` adapter
  forwards it whole to the C# host (`GenerateToolOptions.config`), which never
  reads it. Since 2026-10-12 no context exposes it, and the `csharp` adapter
  forwards only its own options. Since 2026-10-14 the request carries them as
  `config`, with no `options` wrapper, and the C# generator's context exposes
  them as `GenerationContext.Config`.
- **Stage options are already routed by id.** `stageOptions` is keyed by stage id
  and handed untouched to each stage's factory (`configOf()` in
  `packages/compiler/src/compile.ts`), and each step checks its own options when
  it runs. Since 2026-10-12 they are `stages.<id>.options`. Since 2026-10-14 each
  stage reads its stage config from its context (`config`), and the step factories
  are gone.
- **One entrypoint, one flat copy.** Staging copies the `--tsp-location` directory
  flat into `<out-dir>/source`. Imports that reach outside that directory break;
  `--entrypoint` only partly works around this. Since 2026-10-14 staging keeps the
  specs repo's layout and copies `additional-directories` too; one entrypoint
  remains until phase 5.
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
   directory to copy, and the entrypoints, relative to it, are what to compile.
   Folders the compile needs outside it are listed in `additional-directories`,
   also relative to it; ACE reads no `tspconfig.yaml` (2026-10-14). See
   [Source acquisition](#source-acquisition).

5. **Several entrypoints merge into one extension.** Each entrypoint compiles
   separately at its own `api-version`, and the emit stage merges the results into
   one IR. See [Merging entrypoints](#merging-entrypoints).

6. **Everything the spec cannot provide goes to the top level.** These are
   document facts: `service-name`, `root-namespace`, `project-name` and
   `description` today, with more to come. Their values are
   written out explicitly: no derivation, no interpolation. A fact is set only at
   the top level. Facts differ from instructions: how a stage should shape
   spec-derived content (its options, and directives once designed) stays in that
   stage's section. `version` and `directories` were facts until 2026-10-15 (see
   [Facts removed](#facts-removed-decided-2026-10-15)).

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
   compiler. The compiler takes its own settings (`stages`) and builds each
   stage's config: `stages.<id>.options` for each stage, and the top-level keys
   left over for `seed-ir`. It creates each stage's context, passes every stage
   config untouched and validates nothing beyond routing; an unknown stage id is
   still an error. Neither the CLI nor the compiler knows the facts, and the CLI
   never names a stage. Each transformer owns the knowledge of its stage config
   and resolves it at transform time. The compiler maps every stage id to its
   implementation itself, so hosts register nothing.

9. **Stages run unless disabled.** `stages` is a map keyed by catalog stage id.
   The catalog still owns order and `requires`. `"off"` is removed from
   `StageDefault`, so every stage is mandatory or on, and `disabled: true` is the
   only switch. Opt-in behavior becomes an option of a stage that always runs.
   See [Stage enablement](#stage-enablement).

10. **Each stage receives only its own stage config.** The compiler picks it out by
    the stage's id and puts it in that stage's context, as `config`:
    `TransformContext`, `GeneratorContext` for a generator, or `EmitContext` for
    the emit stage (the field name and the emit context decided 2026-10-14). No
    stage sees the whole config, so the contexts stop exposing it, and the
    `csharp` adapter forwards only its own stage config. A stage config is a plain
    dictionary, resolved lazily by the stage when it runs, as options are today.
    Every stage checks its own config the same way: a key it does not know is a
    warning, and an invalid value, or a missing key the stage requires, is an
    error that fails the stage. No default stands in for an invalid value, and
    the compiler stops the run after a stage that reports an error (2026-10-12).
    Directives, once designed, live inside a stage's `options`.

11. **Casing.** Structural keys are kebab-case, and stage ids are the catalog ids.
    Everything under `options:` is camelCase, so today's option names pass through
    without mapping: `inliningThreshold` and `noInline` for `flatten-model`, and
    `warnAsError`, `trace`, `nostdlib`, `additionalImports` and `emitter` (the
    emitter's own options) for `tsp-emit`.

12. **No flag sets a document fact; the other flags stay as overrides, for now.**
    Facts live only in the file, so the file alone reproduces an extension's
    identity: `--root-namespace` is removed (2026-10-12). `--generator` is
    removed. The file is named by the command's one positional argument,
    `ace compile [<config>]`, a file or a folder holding `aceconfig.yaml`;
    `--config` is removed (2026-10-15). Whether to retire the
    remaining run-input flags is decided later. See [CLI](#cli).

## The file

### Example

```yaml
schema-version: 1

# Run inputs: the CLI copies the spec; tsp-emit compiles each entrypoint.
output-dir: .                            # default: this file's folder
tsp-location: ../azure-rest-api-specs/specification/elasticsan/resource-manager/Microsoft.ElasticSan/ElasticSan
# additional-directories:                # folders the compile needs, not under tsp-location
#   - ../Shared                          # relative to tsp-location, or absolute
entrypoints:                             # one, for now
  - path: client.tsp                     # relative to tsp-location; tsp-location itself when absent
    api-version: 2024-05-01

# Document facts: what the spec cannot provide; seed-ir writes them into the IR's target.
service-name: ElasticSan
root-namespace: Microsoft.Azure.Azclips.Extensions.ElasticSan
project-name: Microsoft.Azure.Azclips.Extensions.ElasticSan
description: ElasticSan extension for AzClips

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
| `additional-directories[]` | folder not under `tsp-location`, relative to `tsp-location` or absolute | none | CLI: copied at its repo-relative path, or by name directly under `<output-dir>/source` when outside the specs repo |
| `entrypoints[].path` | `.tsp` file or folder, relative to `tsp-location` | `tsp-location` itself, with a single entrypoint (see [Open details](#open-details-proposed-defaults-confirm-when-implementing)) | CLI (staging), `tsp-emit` |
| `entrypoints[].api-version` | a version, `latest`, or `all` | see [Open details](#open-details-proposed-defaults-confirm-when-implementing) | `tsp-emit` |
| `service-name` | string | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `root-namespace` | dotted namespace | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `project-name` | string | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
| `description` | string | see [seed-ir](#seed-ir) | `seed-ir` → IR `target` |
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
- **Input:** its stage config, every top-level key that neither the CLI (the run
  inputs and `schema-version`) nor the compiler (`stages`) takes. No flag
  overrides a fact. Keys arrive as written (kebab-case). It has no `options:`
  section of its own.
- **Output:** the IR's top-level `target`. Missing facts get defaults, which are
  today's `buildTarget` rules moved into this stage:

  | Fact | Default when absent |
  |---|---|
  | `root-namespace` | the first entrypoint's spec namespace (`sources[0]`), else `Generated` |
  | `project-name` | the root namespace |
  | `service-name`, `description` | unset |

- **Checking its config (agreed 2026-10-12).** `seed-ir` owns the facts' schema
  and checks its stage config when it runs, as every stage does (decision 10):
  - A fact with the wrong type, or an empty value, is an error (`invalid-fact`)
    that fails the stage: no default stands in for it, no target is written,
    and the run stops. Every fact is still checked first, so one run reports
    them all. For example, YAML reads an unquoted `1.0` as the number 1, so a
    fact with that value must be quoted.
  - `root-namespace` is a dotted string with no empty segments. A list of
    segments is not accepted.
  - A key it does not know is a warning (`unknown-fact`). Its stage config is
    whatever the CLI and the compiler leave at the top level, so this catches
    misspelled top-level keys, and the removed `version` and `directories`.
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
  description join root namespace, project name and directories; IR 0.15.0
  removed version and directories again (2026-10-15). **Temporary:**
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
  choose the stage. Superseded later the same day: the compiler takes `stages`
  instead of those fields (see [Loading the file](#loading-the-file-agreed-2026-10-12)).
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
    included, and checks its stage config as described above.
  - `create-command` no longer builds a target, and the catalog declares its
    `requires: ["seed-ir"]`.
  - `CompileConfig` admits facts as extra top-level keys. The compiler's
    `documentFacts()` hands `seed-ir` every key that is not one of its own
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
  - Every transform warns about unknown keys in its own stage config through one
    shared function, `warnUnknownKeys` (`packages/transformers/src/stage-config.ts`):
    `unknown-fact` for `seed-ir`, `unknown-option` for the others. A stage that
    takes no options, such as `create-command`, warns about any key it is given.
  - Still open against decision 10: `flatten-model` falls back to its defaults
    for an invalid `inliningThreshold` or `noInline` instead of failing. (The
    `csharp` generator's check landed with the compiler half of loading the
    file.) Done on 2026-10-14.

### Facts removed (decided 2026-10-15)

- **`version` is not a fact.** An extension's version is decided at release,
  not at generation. The stable-release pipeline owns it: it reads the
  `<Version>` of each extension's `.csproj`, keeps a new extension's declared
  version, and patch-bumps it on later releases
  (`devops/scripts/publishing/Update-ChangedExtensionVersions.ps1`, which fails
  for an extension without an `x.y.z` `<Version>`). So:
  - `seed-ir` no longer takes `version`, and `IRTarget.version` is gone;
  - the generated `.csproj` has no `<Version>` (it hardcoded `0.1.0`), and
    finalizing an extension adds one before its first release.
- **`directories` is not configurable.** Where generated files land is the C#
  generator's convention: `Models` and `Processors`. `seed-ir` no longer takes
  `directories`, `IRTarget.directories` is gone, and the generator's `Placement`
  fixes both folders. This also ends a mismatch: the model generator's cleanup
  always wiped `Models`, even when another models folder was configured.
- **IR 0.15.0.** Both fields leave `IRTarget`. `IRTarget` is strict, so IR
  captured at 0.14.0 that carries them no longer parses.
- **A `version` or `directories` key** in an existing file is warned about as an
  unknown fact and ignored.

## Source acquisition

The CLI stages the spec before the compiler runs, following AzDev's
`New-DevTSPModule` (`Azure/azure-powershell`, `tools/AzDev/src/Typespec`).
Revised 2026-10-14: ACE reads no `tspconfig.yaml`, and the folders to copy come
from aceconfig (see [Decisions](#source-acquisition-decisions-2026-10-14)).

1. **Find the specs repo root.** Walk up from `tsp-location` to the first folder
   that contains `specification/`, `.github/` and `.azure-pipelines/` (AzDev's
   `IsSpecRoot`). No `.git` is needed, so zip downloads, worktrees and the
   private `-pr` repo all work. If no root is found, staging fails.
2. **Collect the folders to copy:** `tsp-location` and each
   `additional-directories` entry: a folder the compile needs that is not under
   `tsp-location`. A relative entry resolves against `tsp-location`, the
   effective one after a `--tsp-location` override, as entrypoints do; an
   absolute entry is used as is. An entry under `tsp-location` is an error,
   since it contradicts that definition (decided 2026-10-14).
3. **Copy, keeping the repo's layout.** `<output-dir>/source` stands for the
   specs repo root.
   - `tsp-location`, and each listed folder inside the specs repo, goes to
     `<output-dir>/source/<path relative to the specs repo root>`, as AzDev's
     `TempTypeSpecFiles` does. Their positions relative to each other are kept,
     so imports such as `../Shared/x.tsp` keep working, and a future remote
     sparse checkout produces the same tree.
   - A listed folder outside the specs repo, an external reference, goes
     directly under `<output-dir>/source`, by folder name (decided 2026-10-14).
     Imports may not resolve to it there; external references are not
     expected, and the rule is kept for now.
   - Two folders landing at the same place is an error: for example, two
     external folders with one name, or an external folder named
     `specification`. Copied together, one's files would silently overwrite
     the other's, which the compile cannot detect.
   - A listed folder inside another listed folder, or one that contains
     `tsp-location`, is copied once, as part of the outer folder.
   - Exclude `node_modules`, `.git` and `tsp-output` at any depth; a listed
     folder by those names is skipped too.
   - A listed folder that does not exist is an error (confirmed 2026-10-14), and
     so is one that is a file: the key lists directories.
   - Each copied folder must not overlap `<output-dir>/source`; the existing
     guard applies.
   - Everything is checked before `<output-dir>/source` is wiped.
4. **Provide the toolchain.** The `node_modules` link stays at
   `<output-dir>/source/node_modules`, above every copied directory.
5. **Compile from the copy.** Each entrypoint maps to its counterpart inside
   `<output-dir>/source`, through the copied folder that carries it:
   `tsp-location`'s folder, or a listed folder that holds it. Staging treats the
   entrypoint as a path: it must be under `tsp-location`, exist, and be copied.
   What it means to TypeSpec (a folder compiles its `main.tsp`) is the emit
   side's concern; the CLI and the compiler do not check it (decided
   2026-10-14, see [Open details](#open-details-proposed-defaults-confirm-when-implementing)).

**Test escape hatch.** With `ACE_ALLOW_STANDALONE_SPEC=1`, a `tsp-location`
outside any specs repo is copied flat into `<output-dir>/source`, as staging did
before. Any `additional-directories` are then an error, because there is no root
to mirror them against. Inside a specs repo the variable has no effect.

Cleanup and `--capture-source` behave as before.

**Implemented 2026-10-14** (`packages/cli/src/staging.ts`):

- Every problem is reported, before anything is touched, as an
  `@azure-client-engine/cli/...` error: `invalid-tsp-location`,
  `invalid-entrypoint`, `not-in-specs-repo`, `invalid-additional-directory`,
  `staging-collision` or `staging-overlap`. Any of them stops the run with exit
  code 1.
- Three checks added while implementing were dropped (decided 2026-10-14). Each
  guarded a problem that the entrypoint mapping or the compile already reports:
  - An entry that contains the whole specs repo. It is outside the repo, so it
    is copied by name, with `tsp-location` inside it, and the entrypoint maps
    through it. When it also holds the output folder, the overlap guard refuses
    it.
  - An entry named `node_modules`, `.git` or `tsp-output`. The copy skips it like
    any folder by those names; if the compile needed it, the compile reports
    `import-not-found`.
  - A folder landing under `<output-dir>/source/node_modules`, which only a folder
    inside the repo's own `node_modules` can do. The toolchain link is then
    skipped, and the compile cannot resolve the TypeSpec libraries.
- Checked against azure-rest-api-specs (at `b91dabc3`):
  - ElasticSan, staged in the mirrored layout, gives the same five IR snapshots
    as a flat copy, apart from `sources[].entrypoint`, and byte-identical C#.
  - `Contoso.WidgetManager` compiles with
    `additional-directories: [../Contoso.WidgetManager.Shared]`, and fails with
    `import-not-found` without it.

### Source acquisition decisions (2026-10-14)

- **ACE reads no `tspconfig.yaml`.**
  - ACE's emit worker calls TypeSpec's `compile()` with explicit options, and
    `compile()` loads no tspconfig; only the `tsp` CLI and the language server do.
    The one exception is inside the compiler: for a folder entrypoint, it reads
    that folder's tspconfig `entrypoint` field (default `main.tsp`), which no
    spec sets.
  - The 651 tspconfigs in azure-rest-api-specs (at `b91dabc3`) hold `options`
    for other tools (650: mostly other emitters, plus tsp-client's
    `additionalDirectories` in 32), `linter` (649), `emit` (632), `parameters`
    (428), `output-dir` (144), and top-level `imports` (12, all in
    `ai-foundry/data-plane/Foundry`). Only `imports` changes what is compiled,
    and aceconfig's equivalent is `stages.tsp-emit.options.additionalImports`.
  - Cost: an aceconfig restates what a spec's tspconfig may carry, which is
    additional directories (32 specs) and imports (12, Foundry only).
- **`additional-directories` belongs to aceconfig, as a run input the CLI
  takes.** The host copies these folders, so they are its concern, not the
  compiler's or an emitter's. tsp-client and AzDev keep them under
  emitter-specific `options` sections, which ACE does not read. aceconfig is
  rooted at `tsp-location`, and these folders are literally additional to it: by
  definition, folders the compile needs that are not under `tsp-location`. So a
  relative path resolves against `tsp-location`, absolute paths are allowed, and
  an entry under `tsp-location` is an error. The rule must cover remote sources
  too (deferred): there, a relative path resolves against `tsp-location` in the
  same repo and commit.
- **Where a listed folder lands.** Inside the specs repo, whether given relative
  or absolute, it lands at its repo-relative path, so its position relative to
  `tsp-location` is kept. Outside the repo, it lands directly under
  `<output-dir>/source` by folder name. That may not suit external references,
  whose imports would not resolve there, but none are expected; the rule is kept
  for now.
- **Mirror relative to the specs repo root, for now.** The direction is to not
  depend on any specific folder structure. Whether that is achievable is not
  known yet: every spec comes from azure-rest-api-specs, and there may be hidden
  facts that depend on its layout. The alternative, mirroring relative to the
  common ancestor of `tsp-location` and the additional directories, needs no root
  detection, no specs-repo requirement and no escape hatch, and keeps the copied
  folders' relative positions, which is all imports need. It is the candidate to
  revisit.
- **A missing listed folder stays an error.** Because the list lives in aceconfig,
  a stale entry is the aceconfig's own mistake to fix.

In azure-rest-api-specs (at `b91dabc3`), the 32 tspconfigs that declare
`additionalDirectories` all do so under tsp-client's section,
`@azure-tools/typespec-client-generator-cli`; 2 repeat them under the PowerShell
emitter's, the section AzDev reads. They list 169 `specification/...` paths with
no `{...}` variables, 156 of them outside the tspconfig's folder, at siblings such
as `Contoso.WidgetManager.Shared`. 3 entries in Foundry's `sdk-agents` point at
folders that were never in git. Today's flat copy fails on such specs:
`Contoso.WidgetManager` stops at
`import-not-found: "../Contoso.WidgetManager.Shared"`.

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
| `<config>` (positional) | the `aceconfig.yaml` to use, or a folder holding one; without it, `./aceconfig.yaml` when present. Replaced `--config` on 2026-10-15 |
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
- **No file:** without a config argument or `./aceconfig.yaml`, flags alone work
  as today, except that `--out-dir` is required (2026-10-12). The CLI wipes
  generated folders in the output directory, so it never defaults to the
  current one.
- **The config argument (decided 2026-10-15):** `ace compile ../../aceconfig.yaml`
  needs no flag name. A relative path resolves against the current directory. A
  folder means the `aceconfig.yaml` in it, so `ace compile ../ext` works too.
  More than one is an error (exit code 2). `--config` is removed, so there is one
  way to name the file.
- **The old JSON format is retired:** the config file no longer accepts the JSON
  `CompileConfig` shape (`pipeline`, `stageOptions`).

### Loading the file (agreed 2026-10-12)

Each party takes the keys it knows (decision 8).

- **The CLI** finds the file (the config argument, else `./aceconfig.yaml`),
  parses it with the `yaml` package, and takes its own keys:
  - `schema-version`, required and `1`. A file without it, such as an old JSON
    `CompileConfig`, is rejected.
  - `tsp-location` and `output-dir`, resolved against the file's folder.
  - `entrypoints`: one entry until multiple entrypoints land (phase 5); more is
    an error. Its `path` defaults to `tsp-location` (2026-10-15). A key it does
    not know inside an entrypoint is a warning.

  Flags override these keys only. Every other key goes to the compiler
  untouched, and an invalid value stops the run before compiling.
- **The compiler** receives the entrypoint and its API version as compile input,
  and the rest as `CompileConfig`: `stages` plus the facts. `stages` is its only
  setting, and it checks the shape: a `disabled` that is not a boolean, or
  `options` that are not a map, is an error; a key it does not know in a stage's
  section is a warning; an unknown stage id is an error, as today. `options` on a
  disabled stage are ignored.
- **Each stage** gets only its own `options`, the two external stages included.
  `tsp-emit` reads `warnAsError`, `trace`, `nostdlib`, `additionalImports` and
  `emitter` from them, and the `csharp` generator no longer sees the whole config.
  Both check their keys with the shared `warnUnknownKeys`.
- **Order:** the compiler half goes first, while the JSON `--config` carries the
  same `stages` shape; the YAML loader follows.
- **Progress (2026-10-12):** the compiler half landed.
  - `CompileConfig` is `stages` plus the facts; `pipeline`, `stageOptions` and the
    top-level emit settings are gone. `CompileInput` carries the API version.
  - `readStages()` (`packages/compiler/src/stages.ts`) checks the shape, and the
    planner is disable-only: `StageDefault` is `mandatory` or `on`.
  - `tsp-emit` and `csharp` read and check their own options. `tsp-emit` fails on
    an invalid value before starting its tool.
  - The CLI passes the JSON `--config` on untouched and `--api-version` as compile
    input; `--generator` is removed. Remaining: the YAML loader.
- **YAML loader plan (agreed 2026-10-12; implemented 2026-10-13 as described):**
  - **Where:** a new `packages/cli/src/aceconfig.ts` finds, parses and checks
    the file; `compile-command.ts` combines flags, file and defaults. `yaml`
    becomes a CLI dependency.
  - **Finding the file:** `--config <path>`, relative to the current directory;
    else `./aceconfig.yaml` when it exists; else no file. Since 2026-10-15 the
    path is the positional argument, and may name a folder.
  - **Parsing:** YAML 1.2, so a JSON file also parses. A syntax error or a
    duplicate key stops the run and names `file:line:col`. The top level must
    be a map.
  - **The CLI's keys:**
    - `schema-version`: required, `1`.
    - `tsp-location` and `output-dir`: strings, resolved against the file's
      folder; `output-dir` defaults to that folder.
    - `additional-directories`: a list of non-empty strings, kept as written;
      staging resolves them against the effective `tsp-location` (2026-10-14).
    - `entrypoints`: exactly one item `{ path?, api-version? }`, `path` relative
      to `tsp-location`, and `tsp-location` itself when absent (2026-10-15). An
      empty list is an error, and two or more are refused until phase 5. A key it
      does not know inside the item is a warning.
  - **Checks:** every problem is reported, and any error stops the run before
    staging (exit code 2).
  - **Flags:** `--tsp-location` and `--out-dir` (relative to the current
    directory), `--entrypoint` and `--api-version` override the file; a flag
    beats the file, which beats the default. With no file, `--tsp-location` and
    `--out-dir` are required.
  - **Hand-off:** staging receives absolute paths, and the additional
    directories as written; `compile()` receives the entrypoint, its API
    version, and every other key untouched.
  - **Reporting:** the loader's problems print like compiler diagnostics, with
    `@azure-client-engine/cli/...` codes (`invalid-setting`, `missing-setting`,
    `unknown-setting`) and, for a syntax error, the file location.
  - **Shared checks:** the compiler re-exports `warnUnknownKeys`,
    `describeValue` and `isMap`, so the CLI's checks read like every stage's.
  - **Testing:** `runCompile` takes an optional current directory, so finding
    `./aceconfig.yaml` is testable without changing the test process's
    directory.

## Why — benefits

- **Reproducible from one reviewable file.** Everything that shapes an
  extension's output is in one file, and regeneration needs no arguments.
- **No hidden repository conventions.** Paths resolve against the file, so the
  same file works from any checkout or working directory, and output no longer
  defaults to the current directory.
- **Orchestration stays compiler-owned, and stage contracts shrink.** A stage
  sees only its own stage config. The generator sees the IR plus its own stage
  config, so a captured IR explains the output's identity.
- **One place turns non-spec facts into IR.** `create-command` only projects
  commands, and later stages, including future client transforms, read identity
  from the IR.
- **The compiler stays schema-free.** It routes stage configs by id, so it never
  changes when a stage gains an option or the facts grow.
- **Multi-spec extensions without copying a common root.** Only `tsp-location`
  and the listed `additional-directories` are copied.
- **Same model as AzDev.** Specs-repo detection, the mirrored temporary layout and
  the specs-repo requirement match the azure-powershell workflow.

## Costs / tradeoffs (recorded honestly)

- **Stage ids become a committed surface.** Renaming or splitting a stage breaks
  every file that names it. Keep ids stable, or accept the old id after a split.
- **New stages run everywhere.** A stage added to the catalog runs in every
  existing file on its next regeneration.
- **Lazy validation lets typos through, for now.** The compiler never validates
  facts or options. Every stage warns about keys it does not know (decision 10),
  so a misspelled key is only a warning, raised when its stage runs.
- **Mixed casing in one file:** kebab-case structure, camelCase options.
- **`IR` gains a field the emitter never writes.** `target` is optional on `IR`,
  so only `commands` now marks that `create-command` ran.
- **Specs must live in a specs repo, for now.** Ad-hoc local specs need the
  test-only escape hatch. Mirroring relative to the common ancestor would lift
  this (see [Source acquisition](#source-acquisition-decisions-2026-10-14)).
- **aceconfig restates spec-owned lists.** Additional directories, and for
  Foundry the imports, are copied by hand from the spec's `tspconfig.yaml` and can
  drift from it.
- **Merges can fail on shared types,** with no automatic resolution.
- **Two input paths until the flag review.** Both flags and the file configure a
  run's inputs. Facts come only from the file.

## Conflicts with current code/tests (reconcile when implementing)

Recorded so implementation changes them deliberately. Early tests are listed
here, not used to justify keeping the old model. Paths are relative to the
toolkit root.

- **Source:**
  - `packages/compiler/src/types.ts`: `CompileInput` carries a single
    entrypoint and its API version.
  - `packages/compiler/src/builtins/emit.ts`: sends one `EmitToolRequest` per run.

## Relationship to other records

- [`Compiler-Owned-Stage-Orchestration.md`](./Compiler-Owned-Stage-Orchestration.md):
  its point 3 ("turn-on only") shipped as a two-way toggle (`config.pipeline`).
  This record narrows it to **disable-only** and moves the switch into
  `stages.<id>.disabled` (implemented 2026-10-12). Catalog ownership of order and
  `requires` is unchanged.
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
    and reading `additionalDirectories` from a `tspconfig.yaml` at all (ACE reads
    no tspconfig, and aceconfig lists the folders; 2026-10-14).

## Deferred (recorded, not designed now)

- **Directive format.** Directives will live in a stage's `options`. Selectors,
  actions and matching rules are not designed yet.
- **Emit-time alterations.** There is no concrete use case, and they would
  largely duplicate `tspconfig.yaml`.
- **Remote specs.** `repo` + `commit` sources using AzDev's flow: clone without
  checkout, sparse-checkout the same directories, then check out the commit. This
  produces the same mirrored tree. Relative `additional-directories` resolve
  against `tsp-location` in the same repo and commit, which gives the paths to
  sparse-check-out. Provenance would be recorded with `/` separators.
- **Schema polish.** A JSON Schema for editor completion. The compiler stays
  validation-free. (`flatten-model` failing on invalid option values, decision 10,
  was done on 2026-10-14.)
- **Flag review.** Whether to retire the remaining run-input flags, and
  `--out-dir` vs. `output-dir` naming.
- **Generator use of the new `IRTarget` fields.** The C# generator writing
  service name and description into the generated project (for example the
  `.csproj` `Description`) follows `seed-ir`'s first cut. The version is not
  among them: it is decided at release (see
  [Facts removed](#facts-removed-decided-2026-10-15)).

## Open details (proposed defaults; confirm when implementing)

- **`entrypoints` omitted:** one entrypoint, `tsp-location` itself (today's
  `--entrypoint` default). Implemented 2026-10-13.
- **A single entrypoint without `path` (decided 2026-10-15):** `tsp-location`
  itself, as when `entrypoints` is omitted, so the file can set the API version
  without naming a path. The CLI only resolves the default. A folder without a
  main file fails in the `tsp-emit` worker with `invalid-tsp-source`, like any
  folder entrypoint (see the folder entrypoint item below). Once several
  entrypoints are supported (phase 5), each needs a `path`.
- **`api-version` omitted:** `latest` (today's default). Implemented 2026-10-13.
- **Required keys:** `schema-version`, and `tsp-location` unless
  `--tsp-location` is given. Implemented 2026-10-13.
- **`tspconfig.yaml` lookup:** none; ACE reads no tspconfig (decided 2026-10-14).
- **Folder entrypoint (decided 2026-10-14):** keep TypeSpec's rule. A folder
  compiles `main.tsp`, and a folder holding only `client.tsp` needs it named in
  `entrypoints`. Evidence from azure-rest-api-specs (at `b91dabc3`):
  - 374 folders hold both files. The repo's validation rule `client-tsp-import`
    requires `main.tsp` to `import "./client.tsp"`, and 372 comply, so compiling
    `main.tsp` loads `client.tsp` too. ElasticSan is one: compiled from
    `main.tsp` and from `client.tsp`, its five IR snapshots are identical. The 2
    exceptions are in Foundry (`Foundry` and `src/sdk-extensions-openai`).
  - 45 folders hold only `client.tsp`, 20 of them with their own tspconfig.
    Compiling such a folder fails, loudly, for want of `main.tsp`.
  - tsp-client, which also calls `compile()`, prefers `client.tsp` when it
    exists (`findEntrypoint("client.tsp") ?? findEntrypoint("main.tsp")`); the
    repo's CI compiles `main.tsp`, and `client.tsp` separately when it exists.
  - TypeSpec's other ways for a folder to name its main file are unused: no
    `package.json` under `specification/` sets `tspMain` (0 of 33), and no
    tspconfig sets `entrypoint`.
  - **Who checks it (decided 2026-10-14):** the emit side. The CLI and the
    compiler do not need to know what an entrypoint means to the emitter, so
    staging does not look for `main.tsp`. TypeSpec's `compile()` used to fail
    such a folder with a bare `file-not-found: File
    <output-dir>/source/.../main.tsp not found.`
  - **Implemented 2026-10-14** in the `tsp-emit` worker
    (`packages/typespec-emitter/src/tsp-emit-worker.ts`), the only code that sees
    the failure: the emitter itself never runs when the main file is missing. The
    worker moved there from the CLI the same day (see
    [the layering record](./Compiler-Owned-Stage-Orchestration.md#revision--the-tsp-emit-tool-lives-in-the-emitter-package)).
    - When TypeSpec loads nothing from the entrypoint, its location-less load
      errors (`file-not-found`, `file-load`, `invalid-main`) become one
      `@azure-client-engine/typespec-emitter/invalid-tsp-source` error.
    - The error says whether the entrypoint does not exist, is a folder without
      a main file, or is a file TypeSpec cannot load, and quotes TypeSpec. For a
      folder it adds: "TypeSpec compiles a folder's main.tsp; to compile another
      file, such as client.tsp, name that file as the entrypoint."
    - The worker relies on TypeSpec's own resolution rather than repeating it.
      It also checks the program's loaded files, so a broken install that reports
      the same codes is left as it is.
    - The path it names is still the staged copy's, as in every TypeSpec
      diagnostic.
- **Absolute `additional-directories` for a remote source:** what an absolute
  path means there is decided with remote support.
- **A flag for `additional-directories`:** none for now; the flag review decides.
- **`options` on a disabled stage:** ignored (decided 2026-10-12).
- **Emitter options such as `sourceId`:** `stages.tsp-emit.options.emitter`
  (decided 2026-10-12). A single `sourceId` would still collide across
  entrypoints once there are several (phase 5).
- **YAML parser (decided 2026-10-12):** the CLI, which owns deserialization, uses
  the `yaml` package (YAML 1.2), already installed through TypeSpec and added as a
  CLI dependency, so that `api-version: 2024-05-01` stays a string. js-yaml 3.13
  reads it as a date.

## Phasing (non-binding)

1. **Load the file:** the CLI reads and deserializes it, takes its own keys,
   resolves paths, applies flag overrides and passes the rest to the compiler
   untouched; by then the compiler takes `stages` itself (see step 3). Done on
   2026-10-13 (see [Loading the file](#loading-the-file-agreed-2026-10-12)).
2. **Staging:** specs-repo detection, the mirrored layout, `additional-directories`
   from aceconfig, the `tsp-output` exclusion, and the escape hatch. Design
   settled on 2026-10-14 and done the same day (see
   [Source acquisition](#source-acquisition)).
3. **Compiler:** drop `"off"`, move each stage config from its factory into its
   stage's context (`seed-ir`'s included), shrink the compiler's own settings to
   `stages`, remove the whole-config contexts, and retire `--generator`. Done on
   2026-10-12, ahead of step 1, except moving stage configs into contexts (see
   [Loading the file](#loading-the-file-agreed-2026-10-12)), which was done on
   2026-10-14:
   - Every stage reads its stage config from its context as `config`: the four
     transforms, `csharp`, and `tsp-emit` through `EmitContext`. Nothing is
     planned for the emitter's own config yet, but the field leaves room for it.
   - The four transform factories held nothing but their config, so they are
     gone. Each step is a plain object, and `TRANSFORMS` maps a stage id straight
     to its step. `createBuiltinEmit` and `createCsharpGenerator` remain, because
     they bind the host runtime; they no longer take a config.
   - `flatten-model` now fails on an invalid option value. An absent
     `inliningThreshold` (or `null`) is 100, and an absent `noInline` is empty.
4. **`seed-ir`:** done, ahead of steps 1–3. The IR part, `IRTarget` on `IR` with
   the new fields (IR 0.14.0), landed on 2026-10-07; the stage itself, taking
   over `buildTarget`'s defaults from `create-command`, landed on 2026-10-12 (see
   [First implementation cut](#first-implementation-cut-agreed-2026-10-07)).
5. **Multiple entrypoints:** one compile per entrypoint, merged in the emit stage.

The package-layering refactor
([Revision — compiler-owned stage implementations](./Compiler-Owned-Stage-Orchestration.md#revision--compiler-owned-stage-implementations))
landed on 2026-10-07, ahead of these steps, so `seed-ir` needs no host
registration.
