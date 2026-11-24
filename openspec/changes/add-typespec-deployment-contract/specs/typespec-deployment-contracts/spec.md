## Purpose

Provide optional, source-derived deployment contracts that remain aligned with the existing version-specific API contract without redefining public commands.

## ADDED Requirements

### Requirement: Deployment contract generation is opt-in

The toolkit SHALL generate deployment descriptors only when explicitly enabled. With the feature disabled, existing valid inputs, canonical IR content and generated processor and command outputs MUST remain unchanged.

#### Scenario: Existing generation without the option
- **WHEN** an existing extension is generated without enabling deployment contracts
- **THEN** no deployment-contract section is emitted
- **AND** generated processor and command outputs are unchanged

#### Scenario: Enabled generation discovers no eligible resources
- **WHEN** deployment-contract generation is enabled for a valid source with no eligible ARM create-or-update resources
- **THEN** an explicitly present empty resource collection distinguishes completed discovery from disabled generation

### Requirement: Descriptors use the explicitly selected source version

Each descriptor MUST derive from the same source content and concrete API version used for the corresponding canonical operation. Enabled generation MUST reject an absent version, an unresolved version, `latest` or `all` rather than implicitly selecting a version.

#### Scenario: Older version of a multi-version specification
- **WHEN** a source declares two API versions and contract generation selects the older one
- **THEN** descriptor and operation versions equal that older version
- **AND** properties introduced only in the newer version are absent

#### Scenario: Version alignment cannot be established
- **WHEN** the selected toolchain cannot produce a consistent version-specific deployment projection
- **THEN** generation fails with an actionable diagnostic
- **AND** no alternative-version descriptor is presented as valid

### Requirement: Deployment identity references canonical contracts

A descriptor MUST identify its ARM resource type, create-or-update operation, writable scope, and ordered name and scope input bindings. Request and response contracts MUST remain traceable to canonical operation/type identities. Distinct deployment routes MUST remain distinguishable even when they share a resource model.

#### Scenario: Child resource name binding
- **WHEN** a selected resource route includes parent and child resource-name inputs
- **THEN** the descriptor preserves both inputs in deployment-name order
- **AND** each binding resolves to its canonical input

#### Scenario: Subscription-scoped resource
- **WHEN** a resource is writable at subscription scope rather than resource-group scope
- **THEN** its descriptor records the subscription scope and corresponding bindings
- **AND** it does not invent a resource-group deployment binding

### Requirement: Source semantics are not weakened

Descriptors and their referenced contracts MUST preserve wire names, optionality, nullability, lifecycle visibility, enum extensibility, constraints and sensitivity for the represented inputs. Information not carried by the existing contract MUST be retained as source-derived supplemental metadata, not supplied by a handwritten replacement schema.

#### Scenario: Constrained sensitive input with a renamed wire property
- **WHEN** a represented source property has a JSON wire name distinct from its logical name, string constraints and a sensitivity marker
- **THEN** the deployment contract retains the wire name, constraints and sensitivity
- **AND** it contains no live secret value

#### Scenario: Type cannot be faithfully represented
- **WHEN** a discovered input shape cannot be represented without losing its source semantics
- **THEN** generation reports that limitation explicitly
- **AND** it does not replace the input with an integer, unconstrained object or `any` to claim completeness

### Requirement: Metadata survives canonical pipeline boundaries

Deployment descriptors MUST remain valid through parsing, serialization and enabled transforms. Missing, ambiguous or stale canonical references MUST be diagnosed rather than silently dropped.

#### Scenario: Identity-changing transform
- **WHEN** a transform changes a referenced canonical identity
- **THEN** the descriptor remains correctly bound or processing fails with a reference diagnostic
- **AND** the deployment metadata does not disappear

### Requirement: Generation provenance is reproducible

The contract MUST identify the actual specification content, selected API version and resolved generation-tool versions. It MUST record a resolved source commit when available and MUST NOT fabricate one for local sources.

#### Scenario: Local fixture generation
- **WHEN** a local fixture without remote commit provenance is processed twice with identical inputs and tools
- **THEN** both contracts identify the same source content
- **AND** stable provenance does not depend on the machine's absolute source path
