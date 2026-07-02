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

- [ ] Rename namespace `Microsoft.Azure.Cirrus.Extensions.Config` to AzClips naming in:
    - [ ] `src/Extensions/Config/Commands/ConfigCommandBase.cs`
    - [ ] `src/Extensions/Config/Commands/DeleteConfigCommand.cs`
    - [ ] `src/Extensions/Config/Commands/GetConfigCommand.cs`
    - [ ] `src/Extensions/Config/Commands/InfoConfigCommand.cs`
    - [ ] `src/Extensions/Config/Commands/ListConfigCommand.cs`
    - [ ] `src/Extensions/Config/Commands/SetConfigCommand.cs`
    - [ ] `src/Extensions/Config/ExtensionIndex.cs`
- [ ] Update reflection strings (`FullName`, `AssemblyName`) in `src/Extensions/Config/ExtensionIndex.cs` to match renamed namespaces/assemblies.

### Help tooling

- [ ] Rename namespaces/usings from `Cirrus.Help.Tool` to AzClips naming in:
    - [ ] `tools/Help/Cirrus.Help.Tool/Program.cs`
    - [ ] `tools/Help/Cirrus.Help.Tool/Scaffolder.cs`
    - [ ] `tools/Help/Cirrus.Help.Tool/Validation/*.cs`
    - [ ] `tools/Help/Cirrus.Help.Tool.Tests/*.cs`
- [ ] Update test project reference path/name in `tools/Help/Cirrus.Help.Tool.Tests/Cirrus.Help.Tool.Tests.csproj` if project/folder names are renamed.
- [ ] Update Cirrus command-prefix constants in `tools/Help/Cirrus.Help.Tool/Program.cs`.
- [ ] Update validator/test message text mentioning Cirrus where branding should change.

### Extension management code paths

- [ ] Replace hardcoded extension prefix `Microsoft.Azure.Cirrus.Extensions.` in `src/Extensions/Extension/Add.cs`.
- [ ] Update user-facing error text "Cirrus extensions..." in `src/Extensions/Extension/Add.cs`.
- [ ] Update solution-root probe from `cirrus.slnx` in:
    - [ ] `src/Extensions/Extension/LocalArtifactProbe.cs`
    - [ ] `src/CLI/Help/MarkdownCommandHelpProvider.cs`
- [ ] Review `CirrusErrorException` references in extension management files and decide:
    - [ ] Keep type name for compatibility for now.
    - [ ] Or rename type and migrate all call sites together.

### PowerShell cmdlet naming

- [ ] Decide strategy for `Invoke-CirrusRestMethod`:
    - [ ] Keep as compatibility alias only.
    - [ ] Introduce new AzClips cmdlet name and keep old alias.
    - [ ] Fully rename and update docs/examples.
- [ ] If changing name, update both code and indices:
    - [ ] `src/PS/Resources/Cmdlets/InvokeCirrusRestMethodCmdlet.cs`
    - [ ] `src/Extensions/Resources/ExtensionIndex.cs`
    - [ ] `src/PS/Resources/help/Invoke-CirrusRestMethod.md`
    - [ ] `src/PS/Resources/Resources.psd1`

## Phase 3 - Cleanup and consistency

- [ ] Update remaining scoped comments/prose mentioning Cirrus in:
    - [ ] `src/Extensions/Resources/Orchestrators/NewResourceGroupDeploymentProcessor_Orchestrator.cs`
    - [ ] `src/Extensions/Resources/Orchestrators/NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.cs`
    - [ ] `src/Extensions/Resources/Orchestrators/RestProcessor_Orchestrator.cs`
    - [ ] `src/CLI/Help/RuntimeHelpDispatcher.cs` (comment mention)
- [ ] Verify repository links changed from `https://github.com/Azure/cirrus` in scoped files.
- [ ] Verify temporary test-prefix strings (for example `cirrus-help-tests-`) are intentionally kept or renamed.

## Validation checklist

- [ ] Build solution (`dotnet: build`).
- [ ] Run focused tests for changed areas (help tooling, extension config/management, PS resources/config/compute).
- [ ] Run grep again in scoped paths and confirm no unintended `Cirrus|cirrus` remains.
- [ ] Confirm any intentionally retained names are documented in PR notes.

## PR notes template

- [ ] Summary of what was renamed.
- [ ] Compatibility choices (especially cmdlet/type names).
- [ ] Follow-up items intentionally deferred.
