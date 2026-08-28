# Command-level PS SDK types vs. `PSCommandAttribute`/`PSParameterSetAttribute`

All property lists below are empirically verified via direct reflection (`[Type].GetProperties(...)`)
against the installed PowerShell SDK, cross-checked against Microsoft Learn docs where noted.
Ecosystem-usage verdicts below are confirmed at **full-corpus scale**: all 190 modules / 14,485 real
generated cmdlet files / 190 `.psd1` manifests under `azure-powershell/generated/` — not a 2-module
sample.

## `CommandInfo` (base class — reflected via `Get-Command`)
| Property | Type | Coverage |
|---|---|---|
| `Name` | `string` | Covered — decomposed into `verb`+`noun`+`subjectPrefix` |
| `CommandType` | `CommandTypes` enum | N/A — always `Cmdlet` for our surface |
| `Module` | `PSModuleInfo` | Covered indirectly (grouping key for `PSModuleAttribute`) |
| `ModuleName` | `string` | Same as above |
| `Source` | `string` | N/A — assembly/module path, not PS vocabulary |
| `Version` | `Version` | Structurally out of scope — not settable via `CmdletAttribute` at all; assembly version only |
| `Visibility` | `SessionStateEntryVisibility` | Confirmed absent — full-corpus (0/14,485 files) |
| `RemotingCapability` | enum | Confirmed absent from real ecosystem |
| `Parameters` | `Dictionary<string, ParameterMetadata>` | Covered — separate flat `PSParameterAttribute[]` |
| `ParameterSets` | `ReadOnlyCollection<CommandParameterSetInfo>` | Covered — `parameterSets: PSParameterSetAttribute[]` |
| `OutputType` | `ReadOnlyCollection<PSTypeName>` | Covered — `outputTypes: IRId[]` |
| `Definition` | `string` | N/A — auto-generated syntax string |

## `CmdletInfo` (adds to `CommandInfo`)
| Property | Type | Coverage |
|---|---|---|
| `Verb` | `string` | Covered |
| `Noun` | `string` | Covered |
| `DefaultParameterSet` | `string` | Covered — `defaultParameterSetName` |
| `ImplementingType` | `Type` | N/A — .NET type reference |
| `Options` | `ScopedItemOptions` | Structurally out of scope — not on `CmdletAttribute` either; confirmed absent full-corpus |
| `HelpFile` | `string` | Confirmed absent full-corpus (0/14,485 files); module-wide convention (`<ModuleName>-help.xml`), not per-command |
| `PSSnapIn` | `PSSnapInInfo` | N/A — legacy snap-in registration |

## `CommandMetadata` (reflected; constructed from a `CmdletInfo`/type)
| Property | Type | Coverage |
|---|---|---|
| `DefaultParameterSetName` | `string` | Covered |
| `SupportsShouldProcess` | `bool` | Covered |
| `SupportsPaging` | `bool` | Covered |
| `PositionalBinding` | `bool` | Not authorable at all (absent even from the attribute) — generator-default only |
| `SupportsTransactions` | `bool` | Confirmed absent |
| `HelpUri` | `string` | Covered |
| `RemotingCapability` | enum | Confirmed absent |
| `ConfirmImpact` | enum | Covered |

## `CmdletCommonMetadataAttribute` + `CmdletAttribute` (compile-time)
Same 7 fields as `CommandMetadata` (minus `PositionalBinding`) — same verdicts — plus:
| Property | Type | Coverage |
|---|---|---|
| `VerbName` | `string` | Covered |
| `NounName` | `string` | Covered |

## `OutputTypeAttribute`
| Property | Type | Coverage |
|---|---|---|
| `Type` | `PSTypeName[]` | Covered — `outputTypes: IRId[]` |
| `ParameterSetName` | `string[]` | Tracked, not built (per-set output types) |
| `ProviderCmdlet` | `string` | Confirmed absent full-corpus (0/14,485 files; PS Provider-cmdlet concept, irrelevant to ARM cmdlets) |

## `CommandParameterSetInfo` (per-set view, from `$cmd.ParameterSets`)
| Property | Type | Coverage |
|---|---|---|
| `Name` | `string` | Covered — `PSParameterSetAttribute.name` |
| `IsDefault` | `bool` | Covered, inverted representation — `PSCommandAttribute.defaultParameterSetName` string instead of a per-set flag |
| `Parameters` | `ReadOnlyCollection<CommandParameterInfo>` | Covered, inverted — each `PSParameterAttribute.parameterSetName` points back instead of the set embedding parameters |

## `ProcessorInfoAttribute` (azclips's own, real & active — `src/Core/Core.Abstractions/CLIContracts.cs`)
| Property | Type | Coverage |
|---|---|---|
| `Name` | `string` | N/A — processor type name |
| `Summary` | `string` | Covered |
| `Description` | `string` | Covered |
| `Examples` | `string[]` | Covered |
| `Tags` (`HelpTag[]`) | enum array | Covered — `lifecycleTag` (singular) |

# Parameter-level PS SDK types vs. `PSParameterAttribute`

| Type | Property | Coverage |
|---|---|---|
| `ParameterMetadata` | `Name` | Covered |
| | `ParameterType` | N/A — comes from IR's own type directly, not duplicated into the PS schema |
| | `ParameterSets` | Covered — `parameterSets: Record<string, PSParameterSetOverride>` |
| | `IsDynamic` | N/A — dynamic parameters (`IDynamicParameters`) unused by AutoRest-style generation |
| | `Aliases` | Covered — `alias` |
| | `Attributes` | N/A — raw reflection bag |
| | `SwitchParameter` | N/A — type-mapping decision for the generator, not PS attribute vocabulary |
| `ParameterSetMetadata` | `IsMandatory` | Covered |
| | `Position` | Covered |
| | `ValueFromPipeline` | Covered |
| | `ValueFromPipelineByPropertyName` | Covered |
| | `ValueFromRemainingArguments` | Confirmed absent full-corpus (0/14,485 files) |
| | `HelpMessage` | Covered |
| | `HelpMessageBaseName`/`HelpMessageResourceId` | Not covered — not ecosystem-checked (only remaining unchecked item) |
| `CommandParameterInfo` | *(same fields, per-set-first view)* | Same verdicts |
| `ParameterAttribute` | `ExperimentName`/`ExperimentAction` | Confirmed absent full-corpus — 14 initial `ExperimentName=` hits were a false positive (a same-named real API parameter in Chaos/FrontDoor modules, not the PS attribute) |
| | `DontShow` | Confirmed absent from real business parameters full-corpus (86,910 total occurrences, 100% on the 6 standard common parameters: proxy creds/URI/default-creds, SendAsync append/prepend, debugger-wait) |
| `AliasAttribute` | `AliasNames` | Covered — `alias` |
| `SupportsWildcardsAttribute` | *(marker only)* | Confirmed absent full-corpus |
| `ArgumentCompleterAttribute` (PS-native) | `Type`, `ScriptBlock` | Different mechanism — AutoRest's `CompleterInfoAttribute`/our `PSCompleterAttribute` is the real source, translated into this at proxy-export time (confirmed real & active: 81 matches/23 files) |
| `PSDefaultValueAttribute` (PS-native) | `Value`, `Help` | Different mechanism — AutoRest's `DefaultInfoAttribute`/our `PSDefaultValueAttribute` is the real source, translated at export |
| `ValidateSetAttribute` | `ErrorMessage`, `IgnoreCase`, `ValidValues` | Confirmed absent full-corpus |
| `ValidateRangeAttribute` | `MinRange`, `MaxRange` | Confirmed absent full-corpus |

# Module-level: `PSModuleInfo` (reflected via `Get-Module`) vs. `PSModuleAttribute`

| Property | Coverage |
|---|---|
| `Description` | Covered — `description` |
| `ExportedCmdlets`/`ExportedCommands` | Covered — `cmdletsToExport` |
| `Name`, `Version`, `Guid`, `Author`, `CompanyName`, `Copyright`, `PowerShellVersion` | Out of scope (packaging config, not IR-derivable) |
| `RequiredModules`, `RequiredAssemblies`, `FileList`, `ModuleList`, `NestedModules`, `Scripts`, `CompatiblePSEditions`, `ClrVersion`, `DotNetFrameworkVersion`, `ExportedFormatFiles`, `ExportedTypeFiles` | Out of scope — packaging/build config |
| `ExportedFunctions`, `ExportedVariables`, `ExportedAliases`, `ExportedDscResources` | Out of scope — we only generate cmdlets |
| `HelpInfoUri` | Confirmed absent full-corpus (0/190 `.psd1` files) |
| `ExperimentalFeatures` | Confirmed absent full-corpus |
| `PrivateData`/`Tags`/`ProjectUri`/`IconUri`/`LicenseUri`/`ReleaseNotes`/`RepositorySourceLocation` | Out of scope — PowerShell Gallery packaging metadata (`PSData` block) |
| `Prefix` (module-level `DefaultCommandPrefix`) | Confirmed absent full-corpus — design note, not a gap: generated `Az` modules bake the prefix directly into each noun (`AzResourceGroup`) at generation time instead of using `Import-Module -Prefix` |
| `Path`, `ModuleBase`, `ImplementingAssembly`, `Definition`, `ModuleType`, `AccessMode`, `SessionState`, `OnRemove`, `UseAppDomainLevelModuleCache`, `LogPipelineExecutionDetails`, `ProcessorArchitecture`, `PowerShellHostName`, `PowerShellHostVersion` | N/A — pure runtime/session/build plumbing |


