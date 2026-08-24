## Purpose

Defines the PowerShell-relevant attribute vocabulary the schema captures for each generated
command at the cmdlet (command) level, scoped to what azclips's existing runtime types already
support, so generated commands integrate with existing help/telemetry tooling unchanged. See the
`ps-parameter-vocabulary` capability for the parameter-level vocabulary.

## ADDED Requirements

### Requirement: Command-level vocabulary reflects HTTP-method-driven mutation semantics
The schema SHALL capture, per command: verb, noun, `SupportsShouldProcess`, `ConfirmImpact`,
zero or more output types (empty for operations with no response body, e.g. `delete`), and zero
or more aliases.

#### Scenario: Mutating command requires ShouldProcess
- **WHEN** a command's underlying HTTP method is PUT, POST, PATCH, or DELETE
- **THEN** the schema SHALL mark the command `SupportsShouldProcess = true` with a `ConfirmImpact`
  level

#### Scenario: Read-only command does not require ShouldProcess
- **WHEN** a command's underlying HTTP method is GET
- **THEN** the schema SHALL NOT mark the command `SupportsShouldProcess`

### Requirement: Command-level help metadata is captured for downstream reflection tooling
The schema SHALL capture a summary, description, examples, and lifecycle tags (Preview,
Deprecated, Experimental, GA) per command, sufficient to drive PlatyPS-based help generation
(`New-MarkdownCommandHelp`) without a separate help-authoring step.

#### Scenario: Generated command carries help metadata
- **WHEN** a command is derived from an IR operation with documentation
- **THEN** the schema SHALL carry that documentation as the command's summary/description
- **AND** the schema SHALL default the lifecycle tag to GA unless an override hint specifies
  otherwise

### Requirement: Vocabulary matches the real PowerShell SDK attribute shape where an equivalent exists
The schema SHALL capture, per command, whether it supports result paging and an online help
link — mirroring `System.Management.Automation.CmdletAttribute.SupportsPaging`/`.HelpUri` —
rather than diverging from the real PS SDK shape where a direct equivalent exists. See the
`ps-parameter-vocabulary` capability for the equivalent parameter-level requirement (`Position`/
`ValueFromRemainingArguments`).

#### Scenario: Command supports server-side paging
- **WHEN** a command's underlying operation is pageable
- **THEN** the schema SHALL mark the command `SupportsPaging = true`

### Requirement: Vocabulary excludes concepts on a case-by-case merit basis, not blanket absence
The schema SHALL NOT model breaking-change metadata, API-profile metadata, or external-docs
links, each excluded for its own documented reason (architectural mismatch, redundancy with IR,
or scope large enough to warrant a separate change) rather than because azclips lacks an
equivalent today.

#### Scenario: Schema omits unsupported metadata
- **WHEN** the schema is authored
- **THEN** it SHALL NOT include fields for breaking-change deprecation, API profiles, or
  external documentation links
