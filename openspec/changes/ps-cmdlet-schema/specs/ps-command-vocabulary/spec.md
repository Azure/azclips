## Purpose

Defines the PowerShell-relevant attribute vocabulary the schema captures for each generated
command (cmdlet-level and parameter-level), scoped to what azclips's existing runtime types
already support, so generated commands integrate with existing help/telemetry tooling unchanged.

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

### Requirement: One parameter declaration drives both the cmdlet and processor surfaces
The schema SHALL capture each parameter exactly once, with enough information to generate both
a PowerShell `[Parameter]` declaration and a processor argument declaration, so a command's
parameters are never independently redeclared for the two surfaces.

#### Scenario: A single parameter declaration yields two generated declarations
- **WHEN** the schema defines a parameter for a command (mandatory, parameter-set name, help
  message, pipeline-binding, wildcard support, aliases, wire name, allowed values, and type)
- **THEN** a generator SHALL be able to produce both the cmdlet's parameter declaration and the
  processor's argument declaration from that single schema entry, with no independently
  authored duplicate

### Requirement: Parameter mutability is captured to support correct Update-variant generation
The schema SHALL capture, per parameter, whether it is settable on create, settable on update,
and/or read-only, so a generated Update command can correctly omit create-only or
server-computed properties.

#### Scenario: Read-only property is excluded from generated Update parameters
- **WHEN** a parameter is marked read-only
- **THEN** the schema SHALL exclude it from the parameter set generated for an Update command

### Requirement: Parameters may declare a default-value expression
The schema SHALL support an optional default-value expression (script, name, and set-condition)
for a parameter, so common patterns like defaulting `-SubscriptionId` from the current context
can be generated rather than hand-written per command.

#### Scenario: Parameter with a default-value expression
- **WHEN** a parameter declares a default-value expression
- **THEN** the generated cmdlet parameter SHALL apply that expression's value when the caller
  does not explicitly bind the parameter

### Requirement: Vocabulary matches the real PowerShell SDK attribute shape where an equivalent exists
The schema SHALL capture, per parameter, its pipeline position and whether it consumes remaining
unbound arguments, and, per command, whether it supports result paging and an online help link —
mirroring `System.Management.Automation.ParameterAttribute.Position`/`.ValueFromRemainingArguments`
and `CmdletAttribute.SupportsPaging`/`.HelpUri` respectively, rather than diverging from the real
PS SDK shape where a direct equivalent exists.

#### Scenario: Parameter declares a fixed positional slot
- **WHEN** a parameter declares a position
- **THEN** the generated cmdlet parameter SHALL bind positionally at that index without requiring
  the parameter name

### Requirement: A parameter may be hidden independently of its command
The schema SHALL support hiding an individual parameter from tab-completion and generated help,
mirroring `ParameterAttribute.DontShow`, distinct from hiding an entire command via the
command-level `hidden` field.

#### Scenario: A hidden parameter is excluded from generated help but still bindable
- **WHEN** a parameter is marked `dontShow`
- **THEN** the schema SHALL exclude it from generated tab-completion/help
- **AND** the schema SHALL NOT exclude the command itself, nor any other parameter, as a result

### Requirement: Vocabulary excludes concepts on a case-by-case merit basis, not blanket absence
The schema SHALL NOT model breaking-change metadata, API-profile metadata, or external-docs
links, each excluded for its own documented reason (architectural mismatch, redundancy with IR,
or scope large enough to warrant a separate change) rather than because azclips lacks an
equivalent today.

#### Scenario: Schema omits unsupported metadata
- **WHEN** the schema is authored
- **THEN** it SHALL NOT include fields for breaking-change deprecation, API profiles, or
  external documentation links
