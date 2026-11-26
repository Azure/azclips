## Purpose

Establish a deliberately bounded Public IP create pilot that uses Bicep deployment execution without redefining the existing command inputs, defaults or resource output, and requires explicit compatibility and regression evidence before activation.

## ADDED Requirements

### Requirement: Exact-version source and compilation gates

The local pilot experiment SHALL use the complete Public IP deployment contract derived from Network source `965a33b4c632fb1aaf5f2ea02e6d603e1c81baa6` and the same concrete `2026-03-01` API selector as experimental canonical command generation. This is the explicitly approved replacement for the May experiment after catalogue availability checks; historical `2025-07-01` and `2026-05-01` evidence SHALL remain intact. Normal Network source/API/toolchain configuration SHALL remain unchanged. Activation MUST remain blocked until resource identity, scope, ordered inputs, writable contract coverage, reviewed command-level handling of every catalogue difference and template compilation have explicit passing evidence. Historical evidence SHALL NOT certify the selected version. Command-level proof SHALL NOT be reported as identical API/Bicep schemas. Catalogue/compiler association MUST be verified separately; version presence alone SHALL NOT constitute a pass.

#### Scenario: Create-only schema experiment
- **WHEN** the approved newer-source Public IP schema experiment runs
- **THEN** it SHALL select the actual version-projected create-or-update source models and generate only the Public IP resource root and its type dependencies
- **AND** full Network/Compute C# generation or unrelated C# naming fixes SHALL NOT be prerequisites for this schema step
- **AND** property-source correspondence SHALL NOT be reported as completed command-parameter bindings, runtime customization qualification or compiled deployment-template evidence

#### Scenario: Real source association is unresolved
- **WHEN** the selected source cannot provide an unambiguous Public IP resource/operation association
- **THEN** the gate SHALL report the unresolved source identity and block activation
- **AND** it SHALL NOT infer an association from a generated class name or handwritten route

#### Scenario: Catalogue is missing, incompatible or unsupported
- **WHEN** the exact resource/version is missing or any required full-contract difference lacks reviewed binding or eligibility evidence
- **THEN** activation SHALL remain blocked with the report's coverage and reasons
- **AND** another API version or a synthetic incomplete contract SHALL NOT be substituted

#### Scenario: Narrow execution case does not conceal contract gaps
- **WHEN** a minimal Public IP example compiles but the complete source/catalogue gate has unaccounted differences
- **THEN** the example SHALL NOT authorize pilot activation or be reported as full-contract compatibility

#### Scenario: Faithfully normalized open catalogue strings
- **WHEN** a catalogue union contains one unconstrained non-sensitive string type and only string literal alternatives
- **THEN** the audit SHALL compare it as an open string after checking nullability independently
- **AND** it SHALL still detect closed source enum widening, source constraint loss and source sensitivity differences
- **AND** constrained, sensitive or mixed-type unions SHALL NOT be flattened to obtain compatibility

#### Scenario: Optional writable root identity
- **WHEN** an API body identity field is optional but writable and has no proven value-preserving declaration binding
- **THEN** the audit SHALL retain an explicit unsupported identity diagnostic
- **AND** it SHALL NOT silently discard the input or infer read-only visibility from optionality

#### Scenario: Catalogue integer range is broader than the source
- **WHEN** a source Int32 input is compared with a catalogue integer without explicit bounds
- **THEN** the audit SHALL report the source and effective Bicep Int64 ranges as different
- **AND** compatibility SHALL NOT be obtained by widening API input acceptance

#### Scenario: Reviewed command-level handling of measured differences
- **WHEN** pilot input proof accounts for the root ID and both measured integer-range diagnostics
- **THEN** the historical `2025-07-01` audit SHALL retain the original three differences
- **AND** the experimental `2026-03-01` contract SHALL be audited independently rather than assuming the same differences
- **AND** the proof SHALL separately establish rejection of supplied root ID, exact normalized Int32 timeout transmission and rejection of nested NAT Gateway input
- **AND** compiler, execution-wiring, transport, both-surface playback and live gates SHALL remain independently required

#### Scenario: Explicitly selected deployment metadata
- **WHEN** Public IP is explicitly selected for deployment extraction
- **THEN** canonical Network IR SHALL remain unchanged apart from the optional deployment section
- **AND** metadata SHALL record the selected exact-version root and preserve its complete writable schema/facets with recursion guards
- **AND** unrelated operations SHALL remain outside the selected audit rather than being declared compatible

#### Scenario: Invalid root or unsupported selected dependency
- **WHEN** a requested root is missing/ambiguous or its writable schema/scope cannot be faithfully represented
- **THEN** extraction SHALL fail explicitly and emit no partial selected contract

#### Scenario: Read-only or resource-shaped dependencies
- **WHEN** a selected resource has read-only fields, generic ID references or embedded resource-shaped models
- **THEN** read-only fields SHALL remain available in canonical retrieval/output contracts without becoming writable declaration inputs
- **AND** model traversal SHALL NOT automatically select additional provisioning operations

### Requirement: Upstream schema reuse and source-aware bindings

The pilot SHALL reuse pinned upstream `@azure-tools/typespec-bicep` code to interpret TypeSpec as Bicep resource-type definitions, rather than maintain an independent competing Bicep schema implementation. Build tooling SHALL derive atomic-parameter-to-resource-field bindings through shared selected-version source correspondence, canonical identities and containing property paths. Command normalization, eligibility and execution policy SHALL remain separate responsibilities.

#### Scenario: Reproducible upstream generation
- **WHEN** upstream resource-type generation is integrated
- **THEN** its source/package, compatible TypeSpec/HTTP/ARM libraries, input source revision and concrete API selector SHALL be recorded
- **AND** selected-version Public IP create discovery at `2026-03-01` SHALL be demonstrated before experimental template generation
- **AND** no TypeSpec/Bicep compiler or schema emitter SHALL be installed or invoked at command runtime

#### Scenario: Flattened or renamed atomic input
- **WHEN** an atomic parameter is flattened or renamed relative to its TypeSpec property
- **THEN** the generated bridge SHALL resolve its canonical property and container chain to the Bicep field
- **AND** CLI/PowerShell display names, aliases or coincidentally matching generated names SHALL NOT be the source of correspondence

#### Scenario: Reused property or special resource identity
- **WHEN** one source property is used in multiple resource/property paths or represents scope, resource name or computed identity
- **THEN** each mapping SHALL include the containing operation/resource/version and usage path
- **AND** writable body assignments SHALL remain distinct from deployment scope, required resource name, computed root ID and nested reference IDs

#### Scenario: Missing upstream support or correspondence
- **WHEN** discovery, an upstream correlation seam or an unambiguous source-to-field mapping is unavailable
- **THEN** the build gate SHALL report the specific unsupported case and block advancement
- **AND** it SHALL NOT guess by name, silently substitute a handwritten mapping or copy a competing emitter implementation
- **AND** any necessary focused upstream integration change SHALL require explicit review

#### Scenario: Handwritten prototype is superseded
- **WHEN** generated Public IP bindings replace the prototype's structural mapping
- **THEN** independent expected-value and default-path regression assertions SHALL be retained
- **AND** existing customization, root-ID rejection, Int32 preservation, optional omission and unsupported-input checks SHALL remain effective
- **AND** the eventual adapter/template SHALL consume the generated correspondence rather than a second manually maintained mapping table

#### Scenario: Upstream output differs from API IR or compiler catalogue
- **WHEN** an upstream-generated type differs from canonical API IR or the independently pinned Bicep compiler catalogue
- **THEN** evidence SHALL distinguish source/version selection, intentional API/Bicep normalization, local integration errors and upstream limitations or defects
- **AND** upstream provenance alone SHALL NOT authorize changing existing command behavior or suppressing audit diagnostics
- **AND** generating type definitions or sample code SHALL NOT count as compiling and verifying the command-specific deployment template

#### Scenario: Superseded source-gate implementation is retired
- **WHEN** upstream-derived generation and independent regression evidence replace an existing source-gate responsibility
- **THEN** redundant custom discovery/schema interpretation and unused facet/audit machinery SHALL be removed after updating their consumers
- **AND** canonical command linkage, exact-version/provenance checks, input constraint preservation and eligibility safeguards SHALL remain covered
- **AND** historical audit reports SHALL remain intact without requiring obsolete implementation-specific tests or a second schema interpreter

#### Scenario: Upstream correspondence includes read-only descendants
- **WHEN** upstream types/correspondence include an output-only nested model
- **THEN** supplemental input constraints SHALL follow only canonical writable dependencies from the selected actual PUT
- **AND** unsupported constraints in read-only or unselected models SHALL NOT contaminate the selected input gate
- **AND** inherited, recursive and collection element/value constraints on writable inputs SHALL remain checked

#### Scenario: Retired emitter configuration is requested
- **WHEN** configuration requests the former custom-discovery `deploymentContract` or `deploymentResources` emitter options
- **THEN** compilation SHALL reject those options explicitly
- **AND** the upstream-derived audit producer SHALL remain separate from ordinary canonical generation
- **AND** the existing audit metadata schema/readers and historical reports SHALL remain usable

### Requirement: Explicit opt-in and unchanged normal commands

The Bicep execution path SHALL be disabled by default and selected only through an explicit internal pilot host/fixture configuration. Public CLI/PowerShell names, aliases, parameters and default-path behavior SHALL remain unchanged.

#### Scenario: Normal command invocation
- **WHEN** `network public-ip create` or `New-AzPublicIpAddress` runs without the internal pilot selection
- **THEN** it SHALL retain the existing direct request, defaulting, polling and output behavior

#### Scenario: No public backend selector
- **WHEN** public command metadata is compared before and after the pilot implementation
- **THEN** no new CLI flag or PowerShell parameter for backend selection SHALL appear

### Requirement: Bounded input eligibility and preserved normalization

The initial pilot SHALL accept only one standalone Standard/Regional, Static, IPv4 Public IP in an existing resource group, with the existing resource identity/location inputs, optional ordinary resource tags and existing idle-timeout input. Existing argument normalization, defaults and omission behavior MUST be retained. Inputs outside that eligibility SHALL fail before deployment submission when the pilot is explicitly selected.

#### Scenario: Omitted defaults
- **WHEN** an eligible command omits SKU, allocation method, IP version and idle timeout
- **THEN** execution SHALL retain the current Standard, Static, IPv4 and four-minute idle-timeout defaults
- **AND** no additional resource setting SHALL be supplied solely because a template has a default

#### Scenario: Explicit tags and timeout
- **WHEN** an eligible command supplies ordinary tags and an idle timeout accepted by the current contract
- **THEN** the deployment SHALL retain their existing normalized values and wire semantics

#### Scenario: Advanced or different create shape
- **WHEN** an explicitly selected pilot supplies zones, DNS, IP tags, DDoS/prefix/extended-location inputs or a different SKU/tier/allocation/IP version
- **THEN** it SHALL reject that unsupported pilot invocation before submitting the deployment
- **AND** it SHALL NOT ignore the inputs or silently fall back to direct creation

#### Scenario: Root ID is not a pilot input
- **WHEN** normalized pilot input contains any non-null root body `id`, including an empty, whitespace, matching or conflicting string
- **THEN** input binding SHALL fail explicitly before preflight or deployment HTTP requests without removing or changing the value
- **AND** canonical API IR, the ordinary direct processor and nested reference/output IDs SHALL remain unchanged
- **AND** root `id` SHALL NOT become a Bicep parameter or declaration assignment

#### Scenario: Existing command surface and normalization
- **WHEN** current Public IP create command metadata and normalized request bodies are checked
- **THEN** neither CLI nor PowerShell metadata SHALL bind a root ID or raw request body
- **AND** existing customization SHALL remain the source of defaults and normalization, including omission of unset root `id`

#### Scenario: Source integer range is preserved
- **WHEN** the normalized Public IP timeout is bound to deployment parameters
- **THEN** it SHALL remain the same exact JSON integer as the direct request, including the source Int32 upper boundary
- **AND** the binding SHALL reject integers outside Int32 instead of accepting Bicep's wider range
- **AND** existing timeout defaulting SHALL run before binding without independent template defaults or new public input types

#### Scenario: Nested timeout is excluded rather than widened
- **WHEN** a normalized request contains a NAT Gateway object, including its timeout
- **THEN** the initial pilot SHALL reject the whole unsupported input explicitly before HTTP
- **AND** the complete source schema and its integer-range diagnostic SHALL remain preserved

### Requirement: Single-resource Incremental deployment and resource output

Eligible experimental pilot execution SHALL deploy exactly one Public IP declaration at `2026-03-01` in Incremental resource-group mode, await terminal deployment success and return the final Public IP using the existing resource retrieval/output contract. Deployment transport API version SHALL be tracked independently of the resource version.

#### Scenario: Successful create
- **WHEN** the opted-in eligible deployment completes successfully
- **THEN** the declaration SHALL contain only the intended Public IP and no created resource group or dependency resource
- **AND** the command SHALL retrieve the target Public IP at `2026-03-01` and return its resource-shaped CLI/PowerShell output
- **AND** it SHALL NOT return a deployment object as the create result

#### Scenario: Compiled template is unavailable
- **WHEN** the verified compiled pilot asset is missing or invalid
- **THEN** the command SHALL report an explicit error before deployment submission
- **AND** it SHALL NOT install or compile Bicep at runtime

### Requirement: Explicit failure and existing-resource protection

The create-only pilot SHALL reject a detected existing target before deployment submission, preserve actionable deployment/polling/retrieval failures and never turn a failed deployment into a successful resource result. It MUST NOT use Complete mode, silently retry through the direct backend or automatically delete unrelated resources.

#### Scenario: Target already exists
- **WHEN** the pilot's preflight finds the intended target already exists
- **THEN** it SHALL report that update/reuse is outside the create-only pilot and submit no deployment

#### Scenario: Deployment fails or polling is cancelled
- **WHEN** deployment execution fails or its wait is cancelled
- **THEN** the command SHALL report that failure/cancellation
- **AND** it SHALL NOT report success through a final GET or direct-backend retry

#### Scenario: Final retrieval fails
- **WHEN** deployment succeeds but the target resource cannot be retrieved
- **THEN** the command SHALL surface the retrieval failure rather than fabricate resource output

### Requirement: Both-surface regression and live activation evidence

Activation evidence SHALL cover default-path stability, normalized declaration/parameters, deployment transport/polling/failures and equivalent resource results through both CLI and PowerShell. Live evidence MUST separately address deployment permissions, existing-resource behavior and bounded test-owned cleanup; offline recordings SHALL NOT be presented as proof of Azure runtime parity.

#### Scenario: Cross-surface playback
- **WHEN** the same eligible pilot recording is replayed through CLI and PowerShell
- **THEN** request bodies as well as method/URI SHALL be checked
- **AND** equivalent resource results and existing public parameter/default behavior SHALL be asserted

#### Scenario: Live execution has not been authorized
- **WHEN** no subscription/resource-group/location/cost/cleanup authorization has been provided for live validation
- **THEN** no live resources SHALL be created
- **AND** permission, provisioning and live regression gates SHALL remain explicitly unverified

#### Scenario: Cleanup after an authorized live run
- **WHEN** an authorized bounded live scenario finishes or fails
- **THEN** cleanup SHALL target only resources explicitly owned by that test run
- **AND** pre-existing or unrelated resources SHALL NOT be removed
