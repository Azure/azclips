## Purpose

Defines how a PowerShell command model is derived from IR operations — grouping, verb/noun
naming, and variant/parameter-set fan-out — so the same IR consistently produces the same
command shape without per-service hand-tuning.

## ADDED Requirements

### Requirement: Commands are grouped using the IR operation's namespace
Since IR has no client/operation-group hierarchy, commands SHALL be grouped for naming and
merge purposes using each IR operation's `id.namespace`.

#### Scenario: Two operations under the same namespace are merge candidates
- **WHEN** two IR operations share the same `id.namespace`
- **THEN** they SHALL be considered candidates for verb/noun collision and multi-operation merge

### Requirement: Verb is inferred from the operation's action word, falling back to HTTP method only when unmapped
The schema SHALL infer a command's verb by looking up the IR operation's leading action word in
a verb-mapping table. A table match SHALL be used regardless of HTTP method. Only when the
action word has no table entry SHALL the schema fall back to an HTTP-method-based default.

#### Scenario: Create action maps to New
- **WHEN** an IR operation's action word is `Create`
- **THEN** the inferred verb SHALL be `New`

#### Scenario: CreateOrUpdate over PUT maps to New, not Set
- **WHEN** an IR operation's action word is `CreateOrUpdate` and its HTTP method is PUT
- **THEN** the inferred verb SHALL be `New`
- **AND NOT** `Set`, matching the real hand-written `NewResourceGroupProcessor`/
  `NewResourceGroupCmdlet` precedent (PUT-based `ResourceGroups.createOrUpdate` -> `New-AzResourceGroup`)

#### Scenario: PATCH-based update maps to Update
- **WHEN** an IR operation's action word is `Update` and its HTTP method is PATCH
- **THEN** the inferred verb SHALL remain `Update`

#### Scenario: Unmapped action word over PUT falls back to Set
- **WHEN** an IR operation's action word has no entry in the verb-mapping table and its HTTP
  method is PUT
- **THEN** the inferred verb SHALL be `Set`

#### Scenario: Unmapped action word over POST falls back to Invoke
- **WHEN** an IR operation's action word has no entry in the verb-mapping table and its HTTP
  method is POST
- **THEN** the inferred verb SHALL be `Invoke`

### Requirement: Noun is the singularized resource name with a configurable subject prefix
The schema SHALL derive a command's noun from the singularized resource/model name, prefixed
with a configurable subject prefix.

#### Scenario: Resource name is singularized and prefixed
- **WHEN** an IR operation targets the `ResourceGroups` resource with subject prefix `Az`
- **THEN** the inferred noun SHALL be `AzResourceGroup`

### Requirement: Operations sharing a verb+noun are merged into one command via parameter sets
When multiple IR operations under the same namespace resolve to the same verb+noun, the schema
SHALL represent them as one command with one parameter set per source operation, rather than as
separate commands.

#### Scenario: Get and List merge into one command
- **WHEN** a `get` operation and a `list` operation under the same namespace both resolve to
  verb `Get` and the same noun
- **THEN** the schema SHALL represent them as a single command with two parameter sets, one
  named for each source operation

### Requirement: Single-operation variant fan-out is limited to base and Expanded
For a single IR operation with a body parameter, the schema SHALL generate at most two
variants: `base` (whole body object) and `Expanded` (flattened body, reusing IR's `FlattenHint`).
It SHALL NOT generate `ViaIdentity` or `ViaJsonString`/`ViaJsonFilePath` variants.

#### Scenario: Body-bearing operation produces base and Expanded variants
- **WHEN** an IR operation has a body parameter with a flattenable model
- **THEN** the schema SHALL produce a `base` variant taking the whole body object
- **AND** SHALL produce an `Expanded` variant with the body's properties flattened into
  individual parameters, per IR's `FlattenHint`

#### Scenario: No identity or JSON-escape-hatch variants are produced
- **WHEN** an IR operation has path parameters or a body parameter
- **THEN** the schema SHALL NOT produce a `ViaIdentity` variant
- **AND** SHALL NOT produce `ViaJsonString` or `ViaJsonFilePath` variants
