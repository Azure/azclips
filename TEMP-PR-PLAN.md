# TEMP PR Plan - Cirrus to AzClips (Scoped Areas)

## Scope

- Focus only on help, config, extension management, and help tooling.
- Exclude generated outputs (`obj`, `bin`, `artifacts`, `devops/out`, `.git`).
- Track compatibility-sensitive renames separately from string/docs updates.

## Phase 1 - Safe text and metadata updates

### CLI Help docs

- [x] Update Cirrus branding text in `src/CLI/Help/Docs/config/index.md`.
- [x] Update extension package examples in `src/CLI/Help/Docs/extension/remove.md`.
- [x] Update extension package examples in `src/CLI/Help/Docs/extension/show.md`.

### PowerShell help markdown

- [x] Update external help file names from `Microsoft.Azure.Cirrus.*` to target branding in `src/PS/Config/help/*.md`.
- [x] Update external help file names from `Microsoft.Azure.Cirrus.*` to target branding in `src/PS/Resources/help/*.md`.
- [x] Update external help file names from `Microsoft.Azure.Cirrus.*` to target branding in `src/PS/Compute/help/*.md`.
- [x] Review and update all prose/examples that mention Cirrus in `src/PS/Resources/help/Invoke-CirrusRestMethod.md`.

### Project and package metadata

- [x] Update extension config package metadata in `src/Extensions/Config/Extensions.Config.csproj`:
    - [x] `AssemblyName`
    - [x] `RootNamespace`
    - [x] `ExtName`
    - [x] `Description`
    - [x] `RepositoryUrl`
    - [x] `PackageId`
- [x] Update PS Config metadata in `src/PS/Config/Cmdlets.Config.csproj` and `src/PS/Config/Config.psd1`.
- [x] Update PS Resources metadata in `src/PS/Resources/Cmdlets.Resources.csproj` and `src/PS/Resources/Resources.psd1`.
- [x] Update PS Compute metadata in `src/PS/Compute/Cmdlets.Compute.csproj` and `src/PS/Compute/Compute.psd1`.

## Phase 2 - Namespace and identifier renames (code-level)

### Extensions Config

- [x] Rename namespace `Microsoft.Azure.Cirrus.Extensions.Config` to AzClips naming in:
    - [x] `src/Extensions/Config/Commands/ConfigCommandBase.cs`
    - [x] `src/Extensions/Config/Commands/DeleteConfigCommand.cs`
    - [x] `src/Extensions/Config/Commands/GetConfigCommand.cs`
    - [x] `src/Extensions/Config/Commands/InfoConfigCommand.cs`
    - [x] `src/Extensions/Config/Commands/ListConfigCommand.cs`
    - [x] `src/Extensions/Config/Commands/SetConfigCommand.cs`
    - [x] `src/Extensions/Config/ExtensionIndex.cs`
- [x] Update reflection strings (`FullName`, `AssemblyName`) in `src/Extensions/Config/ExtensionIndex.cs` to match renamed namespaces/assemblies.

### Help tooling

- [x] Rename namespaces/usings from `Cirrus.Help.Tool` to AzClips naming in:
    - [x] `tools/Help/Azclips.Help.Tool/Program.cs`
    - [x] `tools/Help/Azclips.Help.Tool/Scaffolder.cs`
    - [x] `tools/Help/Azclips.Help.Tool/Validation/*.cs`
    - [x] `tools/Help/Azclips.Help.Tool.Tests/*.cs`
- [x] Update test project reference path/name in `tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj` after project/folder rename.
- [x] Update Cirrus command-prefix constants in `tools/Help/Azclips.Help.Tool/Program.cs`.
- [x] Update validator/test message text mentioning Cirrus where branding should change.

### Extension management code paths

- [x] Replace hardcoded extension prefix `Microsoft.Azure.Cirrus.Extensions.` in `src/Extensions/Extension/Add.cs`.
- [x] Update user-facing error text "Cirrus extensions..." in `src/Extensions/Extension/Add.cs`.
- [x] Update solution-root probe from `cirrus.slnx` in:
    - [x] `src/Extensions/Extension/LocalArtifactProbe.cs`
    - [x] `src/CLI/Help/MarkdownCommandHelpProvider.cs`
- [x] Review `CirrusErrorException` references in extension management files and decide:
    - [ ] Keep type name for compatibility for now.
    - [x] Or rename type and migrate all call sites together.

### PowerShell cmdlet naming

- [ ] Decide strategy for `Invoke-CirrusRestMethod`:
    - [ ] Keep as compatibility alias only.
    - [ ] Introduce new AzClips cmdlet name and keep old alias.
    - [x] Fully rename and update docs/examples.
- [x] Decision note: hard cutoff for `Invoke-CirrusRestMethod` naming (no compatibility alias retained).
- [x] If changing name, update both code and indices:
    - [x] `src/PS/Resources/Cmdlets/InvokeCirrusRestMethodCmdlet.cs`
    - [x] `src/Extensions/Resources/ExtensionIndex.cs`
    - [x] `src/PS/Resources/help/Invoke-CirrusRestMethod.md`
    - [x] `src/PS/Resources/Resources.psd1`

## Phase 3 - Cleanup and consistency

- [x] Update remaining scoped comments/prose mentioning Cirrus in:
    - [x] `src/Extensions/Resources/Orchestrators/NewResourceGroupDeploymentProcessor_Orchestrator.cs`
    - [x] `src/Extensions/Resources/Orchestrators/NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.cs`
    - [x] `src/Extensions/Resources/Orchestrators/RestProcessor_Orchestrator.cs`
    - [x] `src/CLI/Help/RuntimeHelpDispatcher.cs` (comment mention)
- [x] Verify repository links changed from `https://github.com/Azure/cirrus` in scoped files.
- [x] Verify temporary test-prefix strings (for example `cirrus-help-tests-`) are intentionally kept or renamed.

## Validation checklist

- [x] Build solution (`dotnet: build`).
- [x] Run focused tests for changed areas (help tooling, extension config/management, PS resources/config/compute).
- [x] Run grep again in scoped paths and confirm no unintended `Cirrus|cirrus` remains.
- [x] Confirm any intentionally retained names are documented in PR notes.

### Intentionally retained names

- PowerShell cmdlet implementation namespaces/usings under `src/PS/**/Cmdlets` remain `Microsoft.Azure.Cirrus.Cmdlets.*` for compatibility with existing binary/module surfaces in this PR.

## PR notes template

- [ ] Summary of what was renamed.
- [ ] Compatibility choices (especially cmdlet/type names).
- [ ] Follow-up items intentionally deferred.
