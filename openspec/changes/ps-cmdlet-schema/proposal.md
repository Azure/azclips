## Why

Azclips issue #452 ("[Cirrus Codegen] Design PS attribute schema") asks for a schema that lets
the Cirrus TypeSpec-to-C# codegen pipeline emit PowerShell cmdlets. Today the pipeline goes
TypeSpec -> IR (`toolkit/packages/typespec-emitter`, complete) -> a pluggable `TransformStep`
pipeline + `Generator` (`toolkit/packages/compiler`, complete but empty: `identityPipeline`,
`noopGenerator`). Nothing yet turns an `IROperation`/`IRModel`/`IREnum` into a PowerShell
cmdlet's shape.

Azclips already has a working, hand-written convention for this (Resources/Compute/Login
extensions): every command is THREE artifacts — a thin `PSCmdlet` shell, a business-logic
`IProcessor` subclass, and a `CommandMetadata` registration in the extension's `ExtensionIndex`.
Parameters are declared up to FOUR times today by hand, with no shared source of truth — a real
drift risk once this is code-generated at scale: the Cmdlet's `[Parameter]`, the Processor's
`[ArgumentInfo]`, and the `ExtensionIndex`'s `CommandMetadata.Parameters` dictionary (restated
once per PS/CLI variant — e.g. `s_newResourceGroup_PS` and `s_newResourceGroup_CLI` both
re-declare `Required`/`Description`/`Type` for the same `NewResourceGroupProcessor`, differing
only in key casing: `"Name"` vs `"name"`).

AutoRest.PowerShell (reference only — azclips does not depend on it or use it) already solved an
equivalent problem for Az PowerShell: verb/noun inference, parameter-set/variant fan-out, body
flattening, and a directive-based customization mechanism (`docs/directives.md`). Its concrete
policies are reusable as design precedent even though its architecture (single class calling a
generated low-level client directly, proxy-cmdlet layer) does not match azclips's Processor-based
architecture and is explicitly not adopted.

## What Changes

- Add a `powershell` hints sub-namespace to `toolkit/packages/ir/src/hints.ts` that carries both
  authoring-time PS overrides AND the `create-ps-commands` step's resolved output (verb, noun,
  parameter-set name, `ConfirmImpact`, alias, hide, completer reference, mutability flags,
  default-value expression, help/lifecycle tag) — mirroring AutoRest.PowerShell's own
  `CommandOperation extends ProgrammaticOperation` pattern (PS facet bolted onto the same
  operation object), adapted to IR's hint-mutation mechanism since IR nodes are immutable Zod
  data, not classes. Most of what would otherwise be duplicated per-parameter vocabulary lives
  here now, generating both the Cmdlet's `[Parameter]` and the Processor's `[ArgumentInfo]` from
  one enriched IR node instead of a separate parallel schema.
- Add a new, thin Zod-validated aggregate package (`toolkit/packages/ps-commands`, npm name
  `@azure-client-engine/ps-commands`) that holds only the cross-operation grouping shape
  (`PsCommand`/`PsParameterSet`, referencing IR operation ids) plus a `derivePsCommands(ir): PsCommand[]`
  function — not a redeclaration of every field, since those now live as `hints.powershell`.
  This mirrors AutoRest.PowerShell's own two-stage split: per-operation enrichment
  (`create-commands-v2.ts`) is separate from the later grouping stage (`PsProxyTypes.cs`'s
  `VariantGroup`/`Variant`), which also doesn't happen inside the same enrichment step.
- Add a `create-ps-commands` `TransformStep` (`toolkit/packages/compiler`, genuinely `IR -> IR`,
  matching the real `TransformStep` interface) that consumes `IROperation[]`/`IRModel[]`/
  `IREnum[]` and enriches each operation's `hints.powershell` in place: re-deriving
  client/operation-group grouping from `id.namespace` (IR flattened that hierarchy away),
  verb/noun inference (seeded from AutoRest's real default verb-mapping table — action-word table
  match always wins over an HTTP-method-based fallback), and `SupportsShouldProcess`/
  `ConfirmImpact` policy from HTTP method. Grouping operations sharing a resolved verb+noun into
  one command (e.g. Get+List) and single-operation variant fan-out (base + Expanded body
  flattening) both happen afterward, in `ps-commands`'s separate `derivePsCommands()` function —
  not inside this transform step, since a `TransformStep` can only return an `IR`, not an
  arbitrary `PsCommand[]`.
- Design a directive-style (`select`/`where`/`set`) customization/override mechanism passed as
  `TransformStep` options, layered on top of inline `hints.powershell` overrides — modeled on
  AutoRest's `docs/directives.md`.
- Scope: PowerShell surface only — no CLI dual-registration in this change. The actual Roslyn/C#
  generator implementation is a separate, later effort; `noopGenerator` stays a stub after this
  change.

## Capabilities

### New Capabilities

- `ps-command-vocabulary`: the PS-relevant attribute vocabulary, modeled as `hints.powershell`
  fields on IR operations/parameters (Cmdlet/Parameter/OutputType, `ArgumentInfoAttribute`,
  `ProcessorInfoAttribute`, `CommandMetadata`-equivalent fields, plus create/update/read-only
  mutability flags and default-value expressions confirmed as real gaps by cross-checking
  AutoRest's actual generated runtime attributes) scoped to what azclips actually needs. Excludes
  AutoRest features on a case-by-case merit basis — architectural mismatch (API profiles, legacy
  HTTP-pipeline parameters), redundancy with IR (wire-location categories), or large enough to
  warrant a separate follow-on change (the breaking-change/deprecation attribute family) — not a
  blanket "azclips doesn't have it yet."
- `ps-command-derivation`: the `create-ps-commands` `TransformStep` (verb/noun policy, namespace
  grouping, ShouldProcess/ConfirmImpact policy — writing resolved values into `hints.powershell`)
  plus `ps-commands`'s separate `derivePsCommands()` function (multi-operation parameter-set merge,
  variant fan-out, flatten-hint reuse) that groups the enriched IR afterward.
- `ps-command-customization`: the directive-style override mechanism for renaming/hiding/
  adjusting generated commands and parameters.

### Modified Capabilities

_(none — no existing PS-schema specs to modify)_

## Impact

- `toolkit/packages/ir/src/hints.ts`: new `powershell` namespace on `CommonHints` — now the home
  for most per-parameter/per-command vocabulary, not just authoring overrides.
- `toolkit/packages/compiler`: new `TransformStep` implementation, registered via
  `CompilerRegistry`; no change to the backbone itself (`identityPipeline`/`noopGenerator`
  mechanics already support this).
- New package `toolkit/packages/ps-commands` (`@azure-client-engine/ps-commands`) holding the
  thin `PsCommand`/`PsParameterSet` grouping types and the `derivePsCommands()` function — not a
  redeclaration of every field, since those live as `hints.powershell` on IR.
- A tracked `ResourceGroup` IR fixture (hand-authored TypeSpec mimicking `Microsoft.Resources`
  ResourceGroups, chosen so its generated command model can be diffed against the real
  hand-written `New`/`GetResourceGroupCmdlet` + Processors) added under a test-fixtures
  directory, used by `create-ps-commands` unit tests.
- No impact yet on `src/PS`, `src/Extensions/*`, `src/Core` — those stay hand-written until a
  later change implements the actual C# generator consuming this schema.
