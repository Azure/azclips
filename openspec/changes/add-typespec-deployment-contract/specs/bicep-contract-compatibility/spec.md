## Purpose

Let developers perform reproducible offline comparisons between selected deployment contracts and a pinned Bicep resource-type catalogue without treating schema compatibility as runtime migration approval.

## ADDED Requirements

### Requirement: Audit selection and catalogue identity are explicit

The checker SHALL audit explicitly selected resource descriptors against an explicitly identified, pinned local catalogue. Unknown selections, missing contracts, missing resource versions, unreadable definitions or unresolved catalogue references MUST cause an actionable failure. The audit MUST require no Azure authentication or live service requests.

#### Scenario: Known selected resource and catalogue
- **WHEN** a valid descriptor and its matching API-version entry exist in the selected local catalogue
- **THEN** the checker audits that descriptor without contacting Azure
- **AND** records the catalogue identity and content digest

#### Scenario: Catalogue has only another API version
- **WHEN** the selected catalogue lacks the descriptor's API version
- **THEN** the audit fails with the missing resource/version identity
- **AND** it does not retry using a different API version

### Requirement: Comparison respects resource declaration semantics

The checker MUST compare resource identity, writable scopes and writable input contracts by wire path and structural meaning. Name/scope bindings and standardized resource declaration fields MUST be normalized rather than mistaken for missing PUT-body properties.

#### Scenario: Resource declaration envelope
- **WHEN** Bicep requires a resource name while the API supplies that name through a path input and exposes a read-only resource ID
- **THEN** the checker compares their corresponding declaration semantics
- **AND** it does not require a writable Bicep root ID

### Requirement: Input differences and unsupported comparisons are explicit

The checker MUST evaluate writable property membership, types, requiredness, nullability, constraints, enum extensibility and discriminator structure. A lossy conversion, unhandled input shape or incomplete traversal MUST NOT result in a compatible verdict.

#### Scenario: Catalogue narrows an extensible enum
- **WHEN** the source accepts unknown string enum values but the catalogue accepts only listed literals
- **THEN** the selected result is incompatible
- **AND** the report identifies the affected wire path and enum difference

#### Scenario: Unsupported numeric representation
- **WHEN** a floating-point source input cannot be faithfully compared with the selected catalogue representation
- **THEN** the selected result is incompatible or unsupported, as appropriate to the proven difference
- **AND** the checker does not convert the input to an integer to produce a pass

#### Scenario: Recursive input model
- **WHEN** a writable input references a recursive model
- **THEN** comparison terminates without dropping reachable input paths
- **AND** any unsupported recursive shape is reported explicitly

### Requirement: Reports distinguish catalogue compatibility from execution parity

The checker SHALL emit a deterministic machine-readable report containing selected resource/operation identity, API version, source/tool/catalogue provenance, comparison coverage, a compatible/incompatible/unsupported result and per-path diagnostic reasons. Any selected result other than compatible MUST produce a non-zero exit.

The report MUST state that catalogue compatibility does not establish template correctness or unchanged command behavior. Compiler/catalogue association MUST be marked unverified when it has not actually been checked.

#### Scenario: Successful catalogue comparison
- **WHEN** every selected input contract is completely and faithfully compatible with the catalogue
- **THEN** the audit succeeds and records its checked coverage
- **AND** it does not claim live Azure, template, permission or command-output parity

#### Scenario: Mixed selected results
- **WHEN** one selected descriptor is compatible and another is incompatible or unsupported
- **THEN** the report includes both results
- **AND** the overall audit exits non-zero

### Requirement: Audit does not change product execution

Generating contracts or running the checker MUST NOT change public CLI/PowerShell surfaces, generated processor behavior, customization defaults, deployment transport, runtime dependencies or backend selection.

#### Scenario: Compatible audit for an existing command
- **WHEN** an existing command's underlying resource receives a compatible catalogue result
- **THEN** the command continues using its current backend and existing output contract
- **AND** no Bicep implementation is activated automatically
