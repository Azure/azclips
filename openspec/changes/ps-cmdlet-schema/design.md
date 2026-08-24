## Context

**Method**: work backward from a certain target, not forward from IR/Emitter. The PowerShell SDK
already defines exactly what a module/command/parameter must look like — that shape isn't up for
debate, so it's the starting point, not something to guess at from precedent or convention. For
each level (module, command, parameter), this design:
1. Lists every real field found by reflecting on an actual, running PowerShell module (not by
   reading attribute source code, and not by inferring from generator conventions) —
   `System.Management.Automation.PSModuleInfo` / `CmdletInfo` / `CommandMetadata` /
   `ParameterMetadata` / `ParameterSetMetadata`, verified live: `Import-Module Az.Accounts`,
   `Get-Command New-AzResourceGroup`, `$cmd.Parameters["Name"].ParameterSets["..."]`, etc.
2. Decides whether each field belongs in `hints.powershell` using a fixed framework (below).
3. Records the resulting schema, and — just as importantly — what was excluded and why.

A fourth level (output-formatting, `Format.ps1xml`) was found later by directly auditing the
real AutoRest.PowerShell generator source and the generated ecosystem, rather than by live
reflection — the same method, applied to a level this design didn't start out looking for (see
the dedicated "Output-formatting level" section).

Only after that is the derivation mechanism (the pipeline that actually produces this schema from
IR) designed, and only then do the implementation tasks reuse whatever's fastest to build it
(`tasks.md`).

**Decision framework**, applied to every field found by reflection:
1. **Real & load-bearing?** Genuinely enforced/used by PowerShell at runtime, not vestigial (the
   same test that found azclips's own `ArgumentInfoAttribute.Required` is never actually read by
   anything — see below).
2. **IR-derivable?** Does a TypeSpec resource definition actually determine this fact, or is it a
   fixed/release-level/generator-policy constant (like a module's `GUID`, or `-Profile`)?
3. **Verified against the real, mature ecosystem** (azure-powershell + AutoRest.PowerShell, not
   azclips's own current hand-written surface) — checking a small, still-growing codebase for
   "do we use this yet" produces a conclusion that goes stale as azclips grows; checking whether
   the *entire* mature Az PowerShell ecosystem (thousands of cmdlets) ever needs a field is a
   durable signal that doesn't need revisiting later.

Include a field only if (1) and (2) pass; (3) is the tiebreaker/evidence for genuinely uncertain
cases, replacing any temptation to reason from "does azclips's own code use this yet" (which
would wrongly exclude something azclips just hasn't gotten around to, and wrongly include
something no real Az module ever needed either).

**Real hand-written azclips precedent** (traced end-to-end: `NewResourceGroupProcessor.cs` +
`NewResourceGroupCmdlet.cs` + `Resources/ExtensionIndex.cs`): every command today is declared up
to FOUR times by hand, with no shared source of truth — a real drift risk once this is
code-generated at scale:
- A **Cmdlet** class: `[Cmdlet(verb, noun, SupportsShouldProcess, ConfirmImpact)]` + `[OutputType]`
  + per-property `[Parameter(Mandatory, ParameterSetName)]` — the one place carrying the REAL,
  live PS SDK truth (confirmed: `System.Management.Automation.ParameterAttribute`/`CmdletAttribute`
  binding actually runs this).
- A **Processor** class: properties decorated with `[ArgumentInfo(Required, Description,
  SerializedName, ...)]`, duplicating the Cmdlet's parameters by hand. Traced its real consumer
  (`ExtensionHelpManifestBuilder`) and found most of its fields (`Required`, `Description`,
  `Aliases`, `Enums`) are never actually read by anything — only `ParameterSetName`,
  `SupportsPipeline`, `SupportsWildcards`, `SerializedName` are. A concrete, demonstrated bug from
  this: `GetResourceGroupProcessor.Name`'s `[ArgumentInfo]` has no `ParameterSetName`, so generated
  help wrongly claims it applies to `"(All)"` sets — the real Cmdlet correctly scopes it to `"Get"`
  only.
- An **`ExtensionIndex`** registration: one `CommandMetadata` per (processor × client-type) — a
  `_PS` entry and a `_CLI` entry, both pointing at the same `ProcessorType`, each hand-typing an
  identical `Description`/`IsRequired`/`AllowedValues` per parameter (differing only in key
  casing). This IS the genuinely live, dual-surface runtime path — confirmed by tracing the real
  call chain: `ExecutionSession.GetProcessor()` → `ArgumentParsingService.BindArgumentsToProcessor()`,
  reading `ExtensionIndex`'s `ParameterMetadata`, for both CLI and PS.

**AutoRest.PowerShell + azure-powershell** (reference only — azclips does not depend on either)
were used two ways: (a) as a real, working example of the exact "reflect on the real target,
derive everything else from it" method this design follows (`ExportProxyCmdlet.cs` reflects on an
already-compiled module's real `CommandInfo`/`ParameterMetadata` via `Get-Command`, and derives
its `VariantGroup`/`Variant` model directly from those real reflected types — it never invents an
upfront schema); (b) as the evidence source for framework criterion 3 (mature-ecosystem usage
checks), not as a source of vocabulary in its own right. Its architecture (proxy-cmdlet layer
calling a generated low-level client directly) is not adopted — it doesn't match azclips's
Processor-based, CLI+PS dual-surfaced architecture.

## Goals / Non-Goals

**Goals:**
- PowerShell surface only (no CLI dual-registration in this change).
- Every included field is justified by the three-part framework above, not by precedent alone.
- One canonical parameter representation drives what would otherwise be duplicated across the
  Cmdlet's `[Parameter]` and the Processor's `[ArgumentInfo]`.
- A customization/override mechanism (directive-style), designed now rather than deferred.
- Reuse IR's existing `FlattenHint` as-is for body-expansion; no new IR field for this.

**Non-Goals** (each excluded by the framework above, not by "azclips doesn't have it yet"):
- CLI-surface command generation/dual-registration — deferred; only the PS shape is in scope.
- The actual Roslyn/C# generator implementation that consumes this schema — separate, later
  effort; `noopGenerator` remains a stub after this change.
- `AsJob` (background-job execution) — framework check (2)/(3) both real, but framework check on
  azclips's own architecture blocks it: `Engine`/`ExecutionSession` runs synchronously
  (`session.Run()`) with no background-execution path today. Modeling it now would be dead
  vocabulary with nothing to generate against; revisit once `Engine` gains that capability.
- Azure API-profile gating (`ProfileAttribute`) — architectural mismatch: solves
  multi-simultaneous-API-version builds, while azclips builds one API version per module.
- `DefaultProfile`/`Break`/`HttpPipelineAppend`/`HttpPipelinePrepend` — tied to the legacy Az
  PowerShell HTTP-pipeline runtime azclips's `Engine` doesn't replicate. Azclips's real
  equivalent, `-Profile`, is operation-independent generator boilerplate (see the Command-level
  section), not per-node vocabulary.
- The breaking-change/deprecation attribute family (`CmdletBreakingChangeAttribute` and
  siblings) — a large, self-contained vocabulary deserving its own design effort, not folded in
  here. The simpler lifecycle-tag concept (Preview/Deprecated/Experimental/GA) this family also
  covers is already in scope (Command-level section).
- `ModelCmdletAttribute`-style helper cmdlets, `ExternalDocsAttribute`, AutoRest-internal
  bookkeeping attributes with no azclips use case even on merit (`ExportAsAttribute`,
  `OriginAttribute`, `ConstantAttribute`, `InternalExportAttribute`,
  `NotSuggestDefaultParameterSetAttribute`),
  `ViaIdentity`/`ViaJsonString`/`ViaJsonFilePath` variant fan-out, a generated `[Category]`
  wire-location attribute (duplicates what IR already encodes) — see prior reasoning per item;
  none passes framework criterion (2) or (3) today.
- Module manifest (`.psd1`)/`PSModuleInfo` release-and-packaging fields — see Module-level section.
- `SupportsTransactions`/`RemotingCapability`/`HelpMessageBaseName`/`HelpMessageResourceId` — see
  Command-level and Parameter-level sections; confirmed absent across the mature ecosystem
  (framework criterion 3), not merely unused by azclips today.
- `ExperimentName`/`ExperimentAction` (`ParameterAttribute`'s real engine-level experimental-
  feature gating) — no azclips equivalent exists (no experimental-feature-flag system today);
  distinct from this schema's own documentation-only `lifecycleTag`, which stays in scope.
- `OutputTypeAttribute.ParameterSetName` (per-parameter-set output types) — a real SDK capability,
  but checking the generated ecosystem (`Advisor`, `Compute`) found zero real uses of it; fails
  framework criterion 3 today, unlike the analogous per-parameter-set `mandatory`/`position`/
  `helpMessage` finding, which does have direct real-target evidence. See Command-level section.
- `ValidateScriptAttribute`/`ValidateDriveAttribute`/`ValidateUserDriveAttribute`/
  `ValidateTrustedDataAttribute`, `ArgumentTransformationAttribute`, `PSTypeNameAttribute` — real
  SDK attributes, found via a full audit of `Attributes.cs`, excluded because none apply to
  REST-API-derived parameters (arbitrary script logic, filesystem-provider/WDAC-security
  specifics, custom transformation logic, or WMI-style loose typing beyond azclips's clean
  TypeSpec-derived types).
- `ValidateRangeAttribute`/`ValidateLengthAttribute`/`ValidatePatternAttribute`/
  `ValidateCountAttribute`/`ValidateNotNullAttribute`/`ValidateNotNullOrEmptyAttribute` — real,
  load-bearing SDK attributes not modeled today; tracked as contingent on IR exposing the
  TypeSpec constraint decorators they'd derive from (see Parameter-level section), not excluded
  on the merits.
- `Format.ps1xml`/`Types.ps1xml` (output-formatting, `FormatTableAttribute`/`DoNotFormatAttribute`/
  ETS type extensions) — see the dedicated "Output-formatting level" section below. Genuinely
  passes the framework (real, cheap, ecosystem-confirmed), but deliberately excluded anyway: it's
  purely cosmetic (a Cmdlet works correctly with zero format view), unlike everything else in
  this change, which is functionally load-bearing. Revisit once a real generator exists.
- `ExportedFunctions`/`ExportedVariables`/`ExportedAliases`/`HelpInfoUri`/`IconUri` — see
  Module-level section; real `PSModuleInfo` fields with no IR-derivable source of truth or no
  azclips precedent (command aliases), not silently dropped.
- `IsDynamic` (dynamic parameters) — see Parameter-level section; real but requires
  runtime-conditional parameter computation azclips's static, TypeSpec-derived parameters don't
  need today.

## Module-level schema

### Real target (reflected live against `Az.Accounts`)
`$module = Get-Module Az.Accounts; $module.GetType().FullName` → `System.Management.Automation.
PSModuleInfo`. Full property list: `Name`, `Version`, `Guid`, `Author`, `CompanyName`, `Copyright`,
`Description`, `PowerShellVersion`, `CompatiblePSEditions`, `RequiredModules`,
`RequiredAssemblies`, `ProjectUri`, `LicenseUri`, `Tags`, `ReleaseNotes`, `Prefix`, `ModuleType`,
`ExportedCmdlets`, `ExportedFunctions`, `ExportedAliases`, `ExportedVariables`, `HelpInfoUri`,
`IconUri`, and several build/runtime-only fields (`ClrVersion`, `DotNetFrameworkVersion`,
`ModuleBase`, `SessionState`, etc. — never plausible IR-derived vocabulary, skipped without
individual analysis).

### Framework verdicts
| Field | (1) Real? | (2) IR-derivable? | (3) Ecosystem check | Verdict |
|---|---|---|---|---|
| `ExportedCmdlets` | Yes, as a `PSModuleInfo` concept | **Yes** — free; it's the flattened list of every derived command's name | Checked a real generated manifest directly (`Az.Advisor.psd1`): it declares `FunctionsToExport`, not `CmdletsToExport` — AutoRest.PowerShell's proxy cmdlets are PowerShell **functions** (script-based), not compiled `[Cmdlet]` classes. azclips's own Cmdlets are real compiled classes (confirmed earlier: `NewResourceGroupCmdlet.cs`'s `[Cmdlet(...)]`), a deliberate, different architecture (see Goals) — so azclips's manifest should use `CmdletsToExport`, not copy the field name, just the "flattened export list" concept | **In scope** — as `CmdletsToExport`, matching azclips's own real architecture |
| `Description` | Yes | **Yes** — same pattern as `summary`/`docs` already captured per-operation/per-model | Confirmed present in the same real manifest (`Description = 'Microsoft Azure PowerShell: Advisor cmdlets'`) | **In scope** |
| `Guid`, `Version`, `Author`, `CompanyName`, `Copyright` | Yes (real fields) | **No** — release/versioning identity, kept stable across releases; nothing in a TypeSpec resource definition determines them | Confirmed present in the same real manifest, all fixed publisher-identity values (`'Microsoft Corporation'`, etc.), not per-module/per-spec derived | Out of scope |
| `RequiredModules`/`RequiredAssemblies`, `PowerShellVersion`, `CompatiblePSEditions` | Yes | **No** — packaging/dependency constraints, not spec-derived | Confirmed present in the same real manifest (`RequiredAssemblies`, `PowerShellVersion = '5.1'`, `CompatiblePSEditions = 'Core', 'Desktop'`); `RequiredModules` itself absent in this sample — dependency wiring happens elsewhere in this architecture | Out of scope |
| `ProjectUri`/`LicenseUri`/`Tags`/`ReleaseNotes` | Yes | **No** — PSGallery marketing/release metadata | Confirmed present under the same manifest's `PrivateData.PSData` block | Out of scope |
| `ExportedFunctions`/`ExportedVariables` | Yes, but N/A | — azclips only ever generates cmdlets, never advanced functions or exported variables | `ExportedVariables`/`VariablesToExport` confirmed absent from the same real manifest; `ExportedFunctions` is real for AutoRest.PowerShell specifically because of its function-based architecture (see `ExportedCmdlets` row above) — not evidence azclips needs it | Out of scope |
| `ExportedAliases` (`AliasesToExport`/`New-Alias`) | Yes — real, distinct from a parameter's `[Alias]` | Yes, trivially (could alias a command's own name), but no azclips precedent uses command aliases — Az's own guidelines actively discourage them for discoverability | Checked directly: `AliasesToExport` is absent from the same real manifest — confirmed absent, not merely "no azclips precedent yet" | Out of scope |
| `HelpInfoUri` (updatable-help catalog, distinct from the per-command `HelpUri` already in scope) | Yes | No — packaging/release concern, not spec-derived | Checked directly: absent from the same real manifest | Out of scope |
| `IconUri` | Yes | No — PSGallery marketing metadata, same bucket as `ProjectUri`/`Tags` | Checked directly: absent from the same real manifest (icon is set at the PSGallery listing level, not the module manifest) | Out of scope |

### Schema decided, implemented
Gave `IRSource` a `rendering` field for the first time (mirroring `IROperation`/`IRModel`/
`IREnum`, which already have one) with a `powershell` namespace holding just `description`.
Separately, added `PsModule` to the `ps-commands` package — `{ sourceId, description,
cmdletsToExport }` — and a `deriveModules(ir: IR): PsModule[]` function that groups the
already-derived `PsCommand[]` by source and flattens each group's `verb`+`noun` names into
`cmdletsToExport`. Mirrors the Command-level split exactly: per-node authoring lives as an IR
hint; cross-cutting grouping is a separate, later function, not a pipeline stage (see "Why
grouping can't happen inside a `TransformStep`" below) — called only from inside a `Generator`'s
own `generate(ir, context)`. Verified end-to-end against the real `ResourceGroup` fixture:
`cmdletsToExport` comes back as exactly `["New-AzResourceGroup", "Get-AzResourceGroup",
"Remove-AzResourceGroup"]`. Wired into `generate-ps-commands`, which now writes
`{ commands, modules }` to `ps-commands.json`.

**Alternative considered**: model the full `.psd1` manifest shape as `hints.powershell` on
`IRSource`. Rejected — most of those fields have no TypeSpec-derivable source of truth; forcing
them into this schema would produce vocabulary with nothing real to populate it. They need their
own authoring surface (e.g. a per-extension packaging config), a distinct, later concern.

## Command-level schema

### Real target (reflected live against `New-AzResourceGroup`)
`$cmd = Get-Command New-AzResourceGroup; $cmd.GetType().FullName` → `System.Management.Automation.
CmdletInfo`. Properties: `CommandType`, `DefaultParameterSet`, `HelpUri`, `ImplementingType`,
`Module`, `ModuleName`, `Name`, `Noun`, `OutputType`, `Parameters`, `ParameterSets`, `Source`,
`Verb`, `Version`. Notably, `$cmd.Noun` returns `"AzResourceGroup"` — the real reflected noun
already includes the module prefix (unlike this schema's deliberate `noun`/`subjectPrefix` split,
kept separate internally since `derivePsCommands` needs to combine them, but worth knowing the
real target doesn't expose them separately).

`CmdletInfo` alone doesn't carry `ConfirmImpact`/`SupportsShouldProcess` — those live on the real
`System.Management.Automation.CommandMetadata` type (`[CommandMetadata]::new($cmd)`): `Name`,
`CommandType`, `DefaultParameterSetName`, `SupportsShouldProcess`, `SupportsPaging`,
`PositionalBinding`, `SupportsTransactions`, `HelpUri`, `RemotingCapability`, `ConfirmImpact`,
`Parameters`.

### Framework verdicts
| Field | (1) Real? | (2) IR-derivable? | (3) Ecosystem check | Verdict |
|---|---|---|---|---|
| `Verb`/`Noun` | Yes | Yes — from the operation's action word + resource name | — | **In scope** |
| `SupportsShouldProcess`/`ConfirmImpact` | Yes | Yes — from HTTP method | — | **In scope** |
| `SupportsPaging`/`HelpUri` | Yes | Yes — trivially derivable | — | **In scope** |
| Alias, hidden, completer, summary/description/examples, lifecycle tag, `httpPath` | Yes (mirrors `ProcessorInfoAttribute`) | Yes | — | **In scope** |
| `PositionalBinding` | Yes | No — same category as `-Profile` (see below) | Checked directly: every AutoRest-generated module sets `PositionalBinding = $false` on every proxy cmdlet, confirmed systematically across 13+ generated azure-powershell modules — a universal generator default, not an occasional per-command choice | **Generator-default note, not a schema field** (see below) |
| `SupportsTransactions` | Rarely used PS-wide | — | Checked directly against real azure-powershell code (hand-written and generated, across Compute/Network/Resources/Storage/Accounts/Authorization/KeyVault/Sql and several generated modules): **completely absent** | Out of scope — durable, not "azclips hasn't gotten there" |
| `RemotingCapability` | Rarely relevant | — | Same check, same result: **completely absent** | Out of scope |
| `DefaultParameterSetName` (`CmdletCommonMetadataAttribute`) | Yes — real command-level field, distinct from the per-operation `parameterSetName` | Yes — real precedent already cited in prose (`GetResourceGroupCmdlet`'s `DefaultParameterSetName = "List"`) but never actually given a schema field | Checked directly: `DefaultParameterSetName=` appears on the generated `[CmdletBinding(...)]` of essentially every proxy cmdlet across the generated ecosystem (101+ matches across 63+ files in an initial, truncated sample spanning `ADDomainServices`/`Advisor`/`Aks`/`AksArc`) — a universal, load-bearing pattern, not a rare one | **In scope — confirmed gap**, found via a full audit of `Attributes.cs` (`CmdletCommonMetadataAttribute`), then verified against the real generated ecosystem |
| `OutputTypeAttribute.ParameterSetName` | Yes — real SDK capability, output types can be scoped per parameter set (`[OutputType(typeof(X), ParameterSetName = new[] {"Get"})]`) | Yes, in principle — same shape as the per-parameter-set finding for parameters | Checked directly: every `[OutputType(...)]` occurrence found across `Advisor`'s and `Compute`'s generated `exports`/`internal` proxy cmdlets is unqualified (no `ParameterSetName=` argument used anywhere) — a real SDK capability the mature ecosystem doesn't actually exercise | **Tracked, not built now** — fails criterion 3; revisit only if a real per-set output-type need surfaces (see Open Questions) |

### Schema decided
`hints.powershell` on `IROperation.rendering`: `verb`, `noun`, `subjectPrefix`, `parameterSetName`,
`confirmImpact`, `alias`, `hidden`, completer reference, `summary`/`description`/`examples`
(mirrors `ProcessorInfoAttribute`), lifecycle tag (Preview/Deprecated/Experimental/GA), `httpPath`
equivalent (path template + api-version), `supportsPaging`, `helpUri`.

**Verb/noun policy**: verb via table lookup seeded from AutoRest's real default table
(`autorest.powershell/powershell/autorest-configuration.md`'s `verb-mapping:` block:
`Create`/`CreateOrUpdate`→`New`, `Update`→`Update`, etc.). **A table match always wins regardless
of HTTP method** — verified against real precedent: azclips's own `NewResourceGroupProcessor`
issues an HTTP PUT for `ResourceGroups.createOrUpdate`, yet the real cmdlet is
`New-AzResourceGroup`, not `Set-AzResourceGroup`. Only when the action word has no table entry at
all does the schema fall back to an HTTP-method default (PUT→`Set`, PATCH→`Update`, POST→`Invoke`).
Noun is the singularized resource/model name plus a configurable subject prefix (kept as its own
field, not baked into `noun`, so it can be recombined later).

**Multi-operation merge + variant fan-out**: two distinct mechanisms, both in `ps-commands`'s
`derivePsCommands()` (not inside `create-ps-commands` — see below):
- **Multi-operation merge**: operations sharing a resolved verb+noun under the same
  `id.namespace` merge into one `PsCommand` with one parameter set per source operation (real
  precedent: `GetResourceGroupCmdlet`'s `"Get"`/`"List"` parameter sets).
- **Single-operation variant fan-out**: limited to `base` (whole body) and `Expanded` (flattened
  body, reusing IR's existing `FlattenHint` — no new PS-specific flatten heuristic). `ViaIdentity`
  and `ViaJsonString`/`ViaJsonFilePath` explicitly excluded — no proxy-cmdlet layer justifies them.

**`PositionalBinding`, resolved**: belongs in the same category as `-Profile` — fixed
generator-level policy applied identically to every generated command, not `hints.powershell`
vocabulary an author would override per-operation. The future C# generator should set
`PositionalBinding = false` unconditionally on every generated `[Cmdlet(...)]`, with no schema
field behind it.

**`-Profile`, resolved**: stays generator-level boilerplate, not schema vocabulary. It's
identical, operation-independent infrastructure injected into every command (today, via
`ExtensionIndex`'s static-constructor loop) — it has no connection to any IR operation/parameter/
model, so folding it into `hints.powershell` would model something the schema has no way to
derive.

**`DefaultParameterSetName`, confirmed gap, implemented**: added a command-level
`defaultParameterSetName` field to `PsCommand`, distinct from the per-operation `parameterSetName`
each merged operation already contributes. Found by auditing the real
`CmdletCommonMetadataAttribute` source directly (not just live reflection) — real precedent
(`GetResourceGroupCmdlet`'s `DefaultParameterSetName = "List"`) was already cited in this design's
prose without ever being backed by an actual schema field. `derivePsCommands()` computes it with a
two-tier heuristic, both checked against real precedent: merging distinct source operations (e.g.
`get`+`list`) prefers whichever has fewer required parameters — real precedent confirmed
end-to-end against the real `ResourceGroup` fixture (`List` over `Get`, no `resourceGroupName`
required); a single operation fanned into `base`+`Expanded` prefers `Expanded` — real precedent
found directly in generated modules (e.g. `DefaultParameterSetName='CreateExpanded'`). No
author-override mechanism was added — no existing per-operation hint field fits, and no real
fixture needed one; revisit only if a real case surfaces where the heuristic picks wrong.

**Per-parameter-set output types, tracked but not built now**: `OutputTypeAttribute.ParameterSetName`
proves output types can vary per parameter set in principle, but checking the real generated
ecosystem (`Advisor`, `Compute`) found zero uses of that qualifier — every generated
`[OutputType(...)]` is unqualified. Unlike `DefaultParameterSetName` (confirmed heavily used) or
the per-parameter-set `mandatory`/`position`/`helpMessage` finding (direct real-target evidence
via `ParameterSetMetadata`), this fails framework criterion 3. Not added to `PsCommand.outputTypes`
now; revisit only if a real need surfaces.

**Command-level directives**: a directive-style (`select: "command"`/`where`/`set`) mechanism,
passed as `create-ps-commands`'s `options`, for bulk/discoverable renames/hides/adjustments —
modeled on AutoRest's `docs/directives.md` grammar. Applied as defaults, before the final
`hints.powershell` write; an existing inline hint always wins over a directive's value for the
same field (directives are bulk defaults, inline hints are node-specific authoring).

## Parameter-level schema

### Real target (reflected live against `New-AzResourceGroup`'s parameters)
`$p = $cmd.Parameters["Tag"]; $p.GetType().FullName` → `System.Management.Automation.
ParameterMetadata`. Full property list: `Aliases`, `Attributes`, `IsDynamic`, `Name`,
`ParameterSets`, `ParameterType`, `SwitchParameter`. **Critically, there is no flat
`IsMandatory`/`Position` here** — those live one level deeper:

`$p.ParameterSets["__AllParameterSets"].GetType().FullName` → `System.Management.Automation.
ParameterSetMetadata`. Full property list: `HelpMessage`, `HelpMessageBaseName`,
`HelpMessageResourceId`, `IsMandatory`, `Position`, `ValueFromPipeline`,
`ValueFromPipelineByPropertyName`, `ValueFromRemainingArguments`. Confirmed live: the same
parameter can have *different* `IsMandatory`/`Position`/`HelpMessage` per parameter set it
belongs to — `ParameterMetadata.ParameterSets` is a dictionary keyed by set name, not a single
flat value.

**Properties vs. Attributes, an important distinction found during this exercise**:
`ParameterMetadata`'s properties are PowerShell's own pre-aggregated summary, computed from the
real C# attributes on the underlying property. Several real, load-bearing facts are **not**
exposed as convenient properties at all — only as raw `Attributes` instances you must filter by
type: `ValidateSetAttribute` (→ `allowedValues`; confirmed via
`$p.Attributes | Where-Object { $_ -is [ValidateSetAttribute] } | Select -Expand ValidValues`),
`ArgumentCompleterAttribute` (→ `completer`), `AliasAttribute` (→ `aliases`, though this one *is*
also surfaced as a `ParameterMetadata.Aliases` property).

### Framework verdicts
| Field | (1) Real? | (2) IR-derivable? | (3) Ecosystem check | Verdict |
|---|---|---|---|---|
| `mandatory`, `position`, `helpMessage`, `valueFromPipeline`, `valueFromPipelineByPropertyName`, `valueFromRemainingArguments` | Yes (`ParameterSetMetadata`) | Yes | — | **In scope**, but see per-parameter-set finding below |
| `aliases` (`AliasAttribute`) | Yes | Yes | — | **In scope** |
| `allowedValues` (`ValidateSetAttribute`) | Yes — found only via raw `Attributes`, not a `ParameterMetadata` property | Yes | — | **In scope** |
| `completer` (`ArgumentCompleterAttribute`) | Yes — same, raw `Attributes` only | Yes | — | **In scope** |
| `supportsWildcards` | Yes | Yes | — | **In scope** |
| `readOnly`/`create`/`update` mutability | Not a native PS SDK concept — mirrors AutoRest's real `InfoAttribute.ReadOnly/Create/Update` | Yes — lets generated Update variants correctly omit create-only/server-computed properties | Confirmed present in a real generated module's `generated/runtime/` folder | **In scope** |
| Default-value expression | Mirrors AutoRest's real `DefaultInfoAttribute` | Yes — e.g. defaulting `-SubscriptionId` from ambient context | Same source | **In scope** |
| `dontShow` | Yes (`ParameterAttribute.DontShow`) | Yes | — | **In scope** — per-parameter, distinct from the operation-level `hidden` |
| `HelpMessageBaseName`/`HelpMessageResourceId` | Real but niche (resource-based/localized help lookup) | — | Checked directly against real azure-powershell code: **completely absent** — the entire ecosystem uses inline `HelpMessage` strings exclusively | Out of scope |
| `ValidateRange`/`ValidateLength`/`ValidatePattern`/`ValidateCount`/`ValidateNotNull(OrEmpty)` (the `Validate*` constraint family) | Yes — real, load-bearing SDK attributes, found via a full audit of `Attributes.cs`, not modeled at all today | **Contingent** — maps directly onto real TypeSpec decorators (`@minValue`/`@maxValue`, `@minLength`/`@maxLength`, `@pattern`, `@minItems`/`@maxItems`, required-ness), becoming genuinely IR-derivable once IR exposes those constraints | Checked directly: zero occurrences of `ValidateRange(`/`ValidateLength(`/`ValidatePattern(` found across `Advisor`'s, `Compute`'s, and `KeyVault`'s generated proxy-cmdlet exports; `ValidateNotNull()` appears only on generator-boilerplate parameters (`DefaultProfile`, `HttpPipelineAppend`/`Prepend`), never on real REST-derived business parameters | **Tracked, not yet in scope** — fails criterion 3 today too, consistent with the IR gap; see note below |
| `ExperimentName`/`ExperimentAction` (`ParameterAttribute`) | Yes — real engine-level feature-flag gating, distinct from the documentation-only `lifecycleTag` this schema already has | No azclips equivalent — no experimental-feature-flag system exists today | Checked directly: zero occurrences across the same generated samples | Out of scope (see Non-Goals) |
| `PSDefaultValueAttribute` | Yes, but non-semantic — purely a documentation aid for tooling, not binding logic | Already covered by the existing `defaultValue` field, which drives real default-application logic | Checked directly: zero occurrences across the same generated samples — confirms this isn't a pattern the real generator invests in either | No new schema field — a future generator can additionally emit this attribute FROM the existing `defaultValue` field as a documentation bonus, though the ecosystem evidence suggests this bonus is low-value |
| `ValidateScript`/`ValidateDrive`/`ValidateUserDrive`/`ValidateTrustedData` | Real SDK attributes | No — arbitrary script logic, filesystem-provider-specific, or WDAC security policy; none apply to REST-API-derived parameters | — | Out of scope |
| `ArgumentTransformationAttribute`, `PSTypeNameAttribute` | Real SDK attributes | No — arbitrary custom transformation logic / WMI-style loose typing beyond the .NET type system; azclips's types are all clean TypeSpec-derived classes | — | Out of scope |
| `IsDynamic` (`ParameterMetadata`, backing `IDynamicParameters`) | Yes, but complex — parameters computed/added at runtime based on other parameter values | No — azclips's parameters are all statically known from the TypeSpec definition; nothing produces runtime-conditional parameter sets today | — | Out of scope |

### Schema decided
`hints.powershell` on `IRParameter.rendering`: `mandatory`, `parameterSetName`, `helpMessage`,
`position`, `valueFromPipeline`/`valueFromPipelineByPropertyName`, `valueFromRemainingArguments`,
`supportsWildcards`, `aliases`, `allowedValues` (`ValidateSet` equivalent), completer reference,
mutability flags (`readOnly`/`create`/`update`), `dontShow`, and an optional default-value
expression (script/name/set-condition).

**Per-parameter-set field variation** (the one real structural gap this exercise found): extend
`hints.powershell` on `IRParameter.rendering` with an optional `parameterSets` map —
`Record<string, { mandatory?, position?, helpMessage? }>` — keyed by parameter-set name, so these
three fields can vary per set, matching the real `ParameterSetMetadata` structure exactly. The
existing flat `mandatory`/`position`/`helpMessage` fields remain the default/fallback used when a
parameter belongs to only one set (today's common case) or a specific set has no override entry.
This is exactly the case multi-operation merge produces most often: a shared parameter name
across several source operations, each with potentially different requirements for it.

**Parameter-level directives**: the same directive mechanism as Command-level
(`select: "parameter"`/`where`/`set`), for bulk parameter renames/hides/alias assignment across
every command sharing a parameter name.

**`Validate*` constraint family, tracked but not yet in scope**: `ValidateRangeAttribute`,
`ValidateLengthAttribute`, `ValidatePatternAttribute`, `ValidateCountAttribute`, and
`ValidateNotNullAttribute`/`ValidateNotNullOrEmptyAttribute` are real, load-bearing SDK attributes
with no equivalent in `hints.powershell` today — found by auditing `Attributes.cs` directly, not
by live reflection (they're compile-time decorations, not runtime `ParameterMetadata` properties).
Unlike `ValidateSet`/`ArgumentCompleter` (already in scope), these map onto real TypeSpec
constraint decorators (`@minValue`/`@maxValue`, `@minLength`/`@maxLength`, `@pattern`,
`@minItems`/`@maxItems`) that IR may not yet expose. Not added to the schema now; revisit once IR
exposes these constraints (tracked separately, outside this change).

## Output-formatting level (`Format.ps1xml`) — out of scope for now

A fourth level exists beyond Module/Command/Parameter: how a **model's instances** display in the
PowerShell console (`Format.ps1xml` table/list views). Found only after this design's own three
levels were already drafted, by directly searching the real ecosystem rather than reasoning from
attribute source code. **Not scoped to a command's response/output types** — checked directly:
real `Az.Astro.format.ps1xml` gives `AstroIdentity` (a pipeline-identity *input* type, never
returned by any cmdlet) and `ErrorDetail` (an error-detail type) their own views too. The real
generator (`ExportFormatPs1xml.cs`) confirms why: it reflects over every exported class in the
generated `Models` namespace, with no request/response distinction at all — the concern is "could
a user ever see an instance of this type," not "is this type a command's output."

### Real target (found directly in `azure-powershell/generated/**/*.Autorest/`)
249 `*.format.ps1xml` files, essentially one per generated module (e.g.
`generated/Astro/Astro.Autorest/Az.Astro.format.ps1xml`), confirmed real and mechanically
generated, not hand-maintained: `autorest-configuration.md` wires
`format-ps1xml: $(current-folder)/$(module-name).format.ps1xml`, and `build-module.ps1` calls a
real `Export-FormatPs1xml` cmdlet during every module build.

**The actual generation rule, read directly from the real generator**
(`ExportFormatPs1xml.cs`/`PsAttributes.cs`), not guessed from output samples: for every model
type, include a property in the table view if it isn't marked `[DoNotFormat]` / project-excluded,
and either (a) it carries an explicit `[FormatTable(Index/Label/Width)]` override, or (b) its
`Origin != Inlined` and its .NET type `IsPsSimple()` (a scalar — string/number/bool/enum/date,
not a nested object or collection). Ordered by explicit `Index` if present, else by `Origin`
(`Inherited`/`Owned`/`Inlined`) then declaration order. This explains the real difference between
`VirtualMachineRunCommand` (shows only `Location`/`Name`/`ResourceGroupName` — its other
properties, `Source`/`Tags`/`InstanceView`, are complex/nested and excluded) and
`VirtualMachineRunCommandInstanceView` (shows all 7 properties — every one happens to be a simple
scalar): it's a deterministic function of each property's own shape, not per-model hand curation.
Applied to azclips's own IR, this would mean iterating every `IRModel` in `ir.models` — not just
ones reachable from a `PsCommand`'s output types — with the same scope caveat validated above.

The real `[FormatTable]`/`suppress-format` overrides are themselves only ever authored in
AutoRest.PowerShell's own `readme.md` `directive:` block (confirmed via real, in-repo examples:
`Dns`/`Storage`/`Purview`'s `README.md`) — a downstream, post-compilation generator-config layer,
never the API spec itself (TypeSpec or hand-written Swagger alike). That's real, additional
evidence for the verdict below, not just a curiosity.

### Framework verdict
| Field | (1) Real? | (2) IR-derivable? | (3) Ecosystem check | Verdict |
|---|---|---|---|---|
| Default table view (simple-scalar, non-inlined properties, in declaration order) | Yes — real generator output, consumed by PowerShell's formatting engine at runtime | Yes, cheaply — a pure function of `IRModel`'s existing property list, each property's type, and the existing `FlattenHint`/origin data | Confirmed ecosystem-wide: 249 generated modules, one file each | Passes the framework, but see decision below |
| Per-property override (`FormatTableAttribute`-equivalent: index/label/width, `DoNotFormat`-equivalent exclude) | Yes — real, bounded per-property authoring surface | Yes, but with no realistic authoring path today (see below) | Present in real generated output; always authored via a downstream directive, never the spec | Out of scope for now |
| `Types.ps1xml` (ETS type extensions) | Yes, but rare | — | Only 4 files exist workspace-wide (`Accounts`/`Sql`/`Functions`, all hand-authored, none auto-generated) — fails the "mature, ecosystem-wide" bar the `Format.ps1xml` evidence clears | Out of scope — not a generator-wide pattern |

### Decision: out of scope for now
This is the one level in this design that genuinely passes the three-part framework **and** is
still being deliberately excluded — not because it's expensive (it isn't; the default view is a
cheap, pure function of data IR already has) but because it's a categorically different kind of
requirement from everything else in this change:

- **Command/Parameter/Module-level schema is functionally load-bearing.** A generated Cmdlet
  cannot exist without *some* value for verb/noun/mandatory/etc. — these are required inputs to
  correct cmdlet behavior.
- **Output-formatting is purely cosmetic.** A generated Cmdlet works completely correctly with
  *zero* format view — PowerShell simply falls back to a default property dump. Nothing breaks, no
  data is wrong, no binding fails. It only affects how polished the console output looks.

Given `generate-ps-commands` is still an explicit debug placeholder — no real C# generator exists
yet to consume *any* of this schema, Module-level included — investing further in the one part of
this change with zero functional necessity is the wrong thing to prioritize right now. Revisit
once a real generator exists and someone actually hits "my cmdlet's default console output looks
bad" in practice; at that point the default-view derivation is cheap to add back (this section's
research stays valid), and the override authoring question (a `model`/`property` directive
selector, mirroring the real `format-table`/`suppress-format` directive) can be scoped then, with
a real consumer to design against instead of a speculative one.

## Pipeline mechanism (cross-cutting, not level-specific)

**Why two stages are required, not chosen for style**: `create-ps-commands` is a genuine
`TransformStep` — `run(ir: IR, context): IR` — that enriches each node's `rendering.powershell` in
place rather than returning a `PsCommand[]`/`PsModule[]`. This was discovered as a hard
implementation blocker: the real `TransformStep` interface
(`toolkit/packages/compiler/src/transform.ts`) requires returning an `IR`, and `IRSchema` is a
`.strict()` Zod schema with no room for an arbitrary aggregate. The `Generator` interface is
similarly constrained (`{ files: Map<string,string> }`, meant for text output). A separate, plain
function (`derivePsCommands`/`deriveModules`, in `ps-commands`, not a pipeline stage) reads the
already-enriched IR afterward and does the cross-node grouping. Checking AutoRest.PowerShell's
own real architecture confirmed this exact split is the working precedent, not an
azclips-specific workaround: its `create-commands-v2.ts` enriches each operation individually,
while the actual grouping into one exported command with multiple parameter sets happens in a
completely separate, later stage (`PsProxyTypes.cs`'s `VariantGroup`/`Variant`, run via C#
reflection over already-generated proxy cmdlet classes) — AutoRest doesn't merge inside its
per-operation enrichment step either.

**Where the derive functions actually get called**: never as a pipeline stage — only from inside
a `Generator`'s own `generate(ir, context)`, since that's the one stage in `compile()` not
required to return `IR`. The real, future Roslyn/C# generator will call `derivePsCommands(ir)`/
`deriveModules(ir)` internally as the first thing it does, exactly mirroring how AutoRest's
`Export-ProxyCmdlet` does its own `GroupBy` reflection downstream of per-operation enrichment.
`generate-ps-commands` is this eventual generator's placeholder — registered under its final
registry id/factory name now, so there's no rename/re-export churn later; today it only calls
`derivePsCommands(ir)`/`deriveModules(ir)` and writes the result as `ps-commands.json`.

**Vocabulary lives as IR hints, not a parallel schema**: per-parameter/per-command PS vocabulary
is modeled directly on `IROperation`/`IRParameter`/`IRSource` — mirroring AutoRest's real
`CommandOperation extends ProgrammaticOperation` (same object, PS facet added), adapted to IR's
hint-mutation mechanism since IR nodes are immutable Zod data, not subtypeable classes. The
separate `ps-commands` package holds only the thin cross-node aggregates (`PsCommand`/
`PsParameterSet`/`PsModule`) that hints structurally cannot express — not a redeclaration of any
per-node field.

**`ExtensionIndex`/Processor generation note**: the illustrative three-artifact story (Cmdlet +
Processor + `ExtensionIndex` entry) assumed each is an isolated, generatable unit — checking the
real file (`src/Extensions/Resources/ExtensionIndex.cs`) shows it isn't: it's a single,
non-`partial` class mixing hand-written `_PS`/`_CLI` `CommandMetadata` fields, `s_psTable`/
`s_cliTable`, and shared lookup methods. A PS-only generator can't safely emit a whole
replacement file without clobbering the hand-written CLI side. Splitting `ExtensionIndex` into a
`partial class` is a real prerequisite change to azclips's existing code, deferred to whichever
later change implements the C# generator. Separately, the Processor's `[ArgumentInfo]` fields are
nominally PS-scoped vocabulary but the Processor itself is shared with CLI — safe today only
because every traced example shows identical `[ArgumentInfo]` values across `_PS`/`_CLI`
registrations; would need a CLI-aware dimension if that ever diverges.

## Open Questions

- Exact directive grammar's JSON/YAML shape and its `TransformStep`-options binding — high-level
  mechanism decided above; concrete grammar is a `tasks.md` implementation detail.
- Whether `hints.powershell` should be a shared abstraction across `Core.Abstractions` +
  extensions + codegen, not just a codegen-internal IR namespace — investigated at length
  (a full draft OpenSpec change unifying `hints.powershell` with `ArgumentInfoAttribute` was
  written and then abandoned once tracing real consumers showed `ArgumentInfoAttribute` is mostly
  unused, and `hints.powershell`'s real target is the Cmdlet's actual `[Parameter]` attribute, not
  an intermediate C# type). The one genuinely live, duplicated-today vocabulary —
  `ParameterMetadata`/`CommandMetadata` in each extension's `ExtensionIndex.cs`, hand-typed twice
  per parameter (`_PS`/`_CLI`) — remains a real, separate, not-yet-scoped problem.

## Appendix: Full Schema Reference

### `hints.powershell` on `IRSource.rendering` (module-level)
| Field | Notes |
|---|---|
| `description` | Module summary/description text; author-settable, not yet wired to a TypeSpec decorator |

### `ps-commands` package: `PsModule` (thin aggregate)
| Field | Notes |
|---|---|
| `sourceId` | `IRSource.id` this module derives from |
| `description` | Copied from `IRSource.rendering.powershell` |
| `cmdletsToExport` | Flattened `verb`+`noun` names from every `PsCommand` grouped under this source |

### `hints.powershell` on `IROperation.rendering` (per-command)
| Field | Notes |
|---|---|
| `verb` | Resolved by `create-ps-commands`; overridable by authors |
| `noun` | Bare, singularized, WITHOUT subject prefix (e.g. `"ResourceGroup"`) |
| `subjectPrefix` | Own field, not baked into `noun` (e.g. `"Az"`) — combined into `PsCommand.noun` at grouping time |
| `parameterSetName` | Source-operation-derived (e.g. `"Get"`, `"List"`) |
| `confirmImpact` | From HTTP method; overridable by authors |
| `alias` | |
| `hidden` | |
| completer reference | Mirrors `CompleterInfoAttribute` |
| `summary` / `description` / `examples` | Mirrors `ProcessorInfoAttribute` |
| lifecycle tag | Preview / Deprecated / Experimental / GA |
| `httpPath` equivalent | Path template + api-version |
| `supportsPaging` | Mirrors `System.Management.Automation.CmdletAttribute.SupportsPaging` |
| `helpUri` | Mirrors `System.Management.Automation.CmdletAttribute.HelpUri` |

### `hints.powershell` on `IRParameter.rendering` (per-parameter)
| Field | Notes |
|---|---|
| `mandatory` | Flat default; overridable per set via `parameterSets` |
| `parameterSetName` | |
| `helpMessage` | Flat default; overridable per set via `parameterSets` |
| `position` | Mirrors `ParameterAttribute.Position`; flat default, overridable per set |
| `valueFromPipeline` / `valueFromPipelineByPropertyName` | |
| `valueFromRemainingArguments` | Mirrors `ParameterAttribute.ValueFromRemainingArguments` |
| `supportsWildcards` | |
| `aliases` | |
| `allowedValues` | `ValidateSet` equivalent — only ever surfaced via raw `Attributes` on the real SDK type, never a `ParameterMetadata` property |
| completer reference | Mirrors `CompleterInfoAttribute`/`ArgumentCompleterAttribute` — same "raw attributes only" caveat |
| `readOnly` / `create` / `update` | Mutability flags; mirrors `InfoAttribute.ReadOnly/Create/Update` |
| `dontShow` | Per-parameter hide (distinct from the operation-level `hidden`); mirrors `ParameterAttribute.DontShow` |
| default-value expression | Mirrors `DefaultInfoAttribute` (script/name/set-condition) |
| `parameterSets` | Optional per-set override map — `{ mandatory?, position?, helpMessage? }` keyed by set name, matching real `ParameterSetMetadata`'s structure |

### `ps-commands` package: `PsParameterSet` (thin aggregate)
| Field | Notes |
|---|---|
| `name` | Source-operation-derived (e.g. `"Get"`, `"List"`) |
| `sourceOperationId` | Reference into IR — `hints.powershell` holds the parameter details |

### `ps-commands` package: `resolveParameterHintForSet(parameter, setName)`
A standalone helper, not a `PsParameterSet` field (kept bare by design — see above). Resolves a
parameter's effective `mandatory`/`position`/`helpMessage` for one parameter set: checks
`parameterSets[setName]` first, falls back to the flat value. A future generator calls this once
per `PsParameterSet` it emits a binding for.

### `ps-commands` package: `PsCommand` (thin aggregate)
| Field | Notes |
|---|---|
| `verb` / `noun` | Copied from the grouped operations' resolved `hints.powershell` |
| `subjectPrefix` | Configurable, e.g. `"Az"` |
| `supportsShouldProcess` / `confirmImpact` | Copied from `hints.powershell` |
| `outputTypes` | By-id refs into IR |
| `parameterSets` | `PsParameterSet[]` — one per merged source operation |
| `defaultParameterSetName` | Mirrors `CmdletCommonMetadataAttribute.DefaultParameterSetName`; computed by `derivePsCommands` — fewest-required-parameters wins across merged operations (e.g. `List` over `Get`), `Expanded` wins over `base` for a single fanned-out operation; no author override yet |
| `sourceOperationIds` | IR operation id(s) this command derives from |
| `alias` / `hidden` / `completer` | Copied from `hints.powershell` |
| `summary` / `description` / `examples` / `lifecycleTag` | Copied from `hints.powershell` |
| `httpPath` / `supportsPaging` / `helpUri` | Copied from `hints.powershell` |
