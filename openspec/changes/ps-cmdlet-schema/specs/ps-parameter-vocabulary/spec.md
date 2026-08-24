## Purpose

Defines the PowerShell-relevant attribute vocabulary the schema captures for each generated
command's parameters, scoped to what azclips's existing runtime types already support, so
generated parameters integrate with existing help/telemetry tooling unchanged. See the
`ps-command-vocabulary` capability for the command (cmdlet)-level vocabulary.

## ADDED Requirements

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
unbound arguments — mirroring `System.Management.Automation.ParameterAttribute.Position`/
`.ValueFromRemainingArguments` — rather than diverging from the real PS SDK shape where a direct
equivalent exists. Confirmed by live reflection (`(Get-Command New-AzResourceGroup).
Parameters["Name"].ParameterSets["..."]`) that these are genuine `ParameterSetMetadata` fields.

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

### Requirement: A parameter's mandatory-ness, position, and help message may vary per parameter set
The schema SHALL support overriding a parameter's `mandatory`, `position`, and `helpMessage`
independently per parameter set, since the real PS SDK models these as properties of
`ParameterSetMetadata` (per-set), not as flat properties of `ParameterMetadata` (per-parameter).
When no per-set override exists for a given set, the parameter's flat default value SHALL apply.

#### Scenario: A shared parameter is mandatory in one set but not another
- **WHEN** a parameter belongs to two parameter sets from a multi-operation merge
- **AND** a per-set override marks it mandatory in one set only
- **THEN** the schema SHALL reflect that parameter as mandatory only in that set
- **AND** SHALL apply the parameter's flat default `mandatory` value to every other set with no
  override
