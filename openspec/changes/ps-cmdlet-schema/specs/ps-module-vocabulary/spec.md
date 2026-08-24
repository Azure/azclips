## Purpose

Defines the module-level PowerShell vocabulary the schema captures for a generated module,
scoped strictly to what's derivable from IR — not the full `PSModuleInfo`/`.psd1` manifest shape,
most of which is release/packaging metadata with no TypeSpec-derivable source of truth.

## ADDED Requirements

### Requirement: Module-level vocabulary is limited to IR-derivable facts
The schema SHALL capture, per source (one TypeSpec entrypoint), an optional module description
and the list of commands it exports. The schema SHALL NOT model `.psd1`/`PSModuleInfo` fields
that have no TypeSpec-derivable source of truth (module GUID, version, author, required
modules/assemblies, minimum PowerShell version, compatible PS editions, or PSGallery metadata).

#### Scenario: A module's exported commands are derived, not authored
- **WHEN** one or more commands are derived from operations under the same IR source
- **THEN** the schema SHALL produce a module-level entry whose exported-command list contains
  every one of those commands' `verb`+`noun` names
- **AND** the schema SHALL NOT require an author to separately declare this list

#### Scenario: A module description is author-settable
- **WHEN** an author sets a description on an IR source's `hints.powershell`
- **THEN** the schema SHALL carry that description on the derived module entry
- **AND** the schema SHALL leave the description unset if no author-provided or otherwise
  derived value exists

#### Scenario: Release/packaging metadata is out of scope
- **WHEN** a generator or downstream tool needs a module GUID, version, author, required
  modules, minimum PowerShell version, or PSGallery metadata
- **THEN** the schema SHALL NOT be the source of that data
- **AND** that data SHALL come from a separate, non-IR-derived authoring surface
