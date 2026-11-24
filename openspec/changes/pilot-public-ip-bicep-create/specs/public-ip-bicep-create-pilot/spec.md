## Purpose

Establish a deliberately bounded Public IP create pilot that uses Bicep deployment execution without redefining the existing command inputs, defaults or resource output, and requires explicit compatibility and regression evidence before activation.

## ADDED Requirements

### Requirement: Exact-version source and compilation gates

The pilot SHALL use the complete Public IP deployment contract derived from the same pinned TypeSpec source and concrete `2025-07-01` API selector as current codegen. Activation MUST remain blocked until resource identity, scope, ordered inputs, writable contract coverage, catalogue compatibility and template compilation have explicit passing evidence. Catalogue/compiler association MUST be verified separately; version presence alone SHALL NOT constitute a pass.

#### Scenario: Real source association is unresolved
- **WHEN** the selected source cannot provide an unambiguous Public IP resource/operation association
- **THEN** the gate SHALL report the unresolved source identity and block activation
- **AND** it SHALL NOT infer an association from a generated class name or handwritten route

#### Scenario: Catalogue is missing, incompatible or unsupported
- **WHEN** the exact resource/version is missing or any required full-contract comparison is incompatible or unsupported
- **THEN** activation SHALL remain blocked with the report's coverage and reasons
- **AND** another API version or a synthetic incomplete contract SHALL NOT be substituted

#### Scenario: Narrow execution case does not conceal contract gaps
- **WHEN** a minimal Public IP example compiles but the complete source/catalogue gate has unresolved differences
- **THEN** the example SHALL NOT authorize pilot activation or be reported as full-contract compatibility

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

### Requirement: Single-resource Incremental deployment and resource output

Eligible pilot execution SHALL deploy exactly one Public IP declaration at `2025-07-01` in Incremental resource-group mode, await terminal deployment success and return the final Public IP using the existing resource retrieval/output contract. Deployment transport API version SHALL be tracked independently of the resource version.

#### Scenario: Successful create
- **WHEN** the opted-in eligible deployment completes successfully
- **THEN** the declaration SHALL contain only the intended Public IP and no created resource group or dependency resource
- **AND** the command SHALL retrieve the target Public IP at `2025-07-01` and return its resource-shaped CLI/PowerShell output
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
