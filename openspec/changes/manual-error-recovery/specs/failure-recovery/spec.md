## ADDED Requirements

### Requirement: Orthogonal recovery configuration
The system SHALL expose `agent.recovery=off|manual|auto`,
`agent.recovery.style=oneshot|interactive`, and a positive integer
`agent.recovery.historySize`, defaulting to off, oneshot, and 10. Public-key
operations SHALL preserve sibling settings across all existing Config hosts.

#### Scenario: Settings can be applied in any order
- **WHEN** the user sets the three public keys in any order and starts a new process
- **THEN** all three values SHALL remain available with correct types and sources
- **AND** deleting trigger SHALL restore off without deleting style or size

#### Scenario: Removed configuration keys are deleted, not migrated
- **WHEN** the stored configuration contains `agent.enabled` or `agent.sessionStyle`
- **THEN** the system SHALL delete them once, without any message
- **AND** recovery SHALL use the defaults until `agent.recovery` is set
- **AND** a new write using a removed key SHALL fail

#### Scenario: Default off keeps no history state
- **WHEN** `agent.recovery` is not set and no history directory exists
- **THEN** a failed command SHALL create no history files and show no recovery hint

#### Scenario: Manual failure is saved without starting AI
- **WHEN** an eligible original command fails with manual configured
- **THEN** the original error and exit status SHALL be preserved without AI startup
- **AND** after confirmed persistence the CLI SHALL display
  `az troubleshoot --id <actual-id>` and the no-argument terminal-default hint

#### Scenario: Persistence cannot hide or indefinitely delay the failure
- **WHEN** secure storage blocks or fails
- **THEN** human-readable original error output SHALL precede capture
- **AND** capture SHALL have a two-second deadline and SHALL not report an unconfirmed ID
- **AND** existing automatic diagnosis SHALL remain eligible to proceed

#### Scenario: Failed help and version requests do not replace business failures
- **WHEN** a help or version request fails, including invalid global argument values
- **THEN** its original error and exit status SHALL remain unchanged
- **AND** the request SHALL not access history, start a capture worker, or produce
  a saved-failure hint
- **AND** utility classification SHALL reuse the existing global argument rules

### Requirement: Protected bounded history
The system SHALL store complete original inputs in an authenticated encrypted
snapshot isolated by local user and state home. Retention SHALL default to the
latest ten successfully persisted records; reducing the configured limit SHALL
take effect on the next successful write, not during reads.

#### Scenario: Retention preserves original diagnostic content
- **WHEN** twelve failures are saved with the default limit
- **THEN** exactly the latest ten records SHALL remain
- **AND** retained command/error values SHALL round-trip including quotes,
  whitespace, Unicode, and inputs larger than normal command-line limits

#### Scenario: Unavailable secure storage keeps only the latest failure
- **WHEN** the OS-protected key storage is unavailable during a save
- **THEN** the system SHALL write only the latest failure, unencrypted, to a
  current-user-only file, replacing any earlier unencrypted failure
- **AND** the CLI SHALL show a warning that the failure was saved without encryption
- **AND** the next save with available key storage SHALL encrypt that failure into
  the snapshot and delete the unencrypted file
- **AND** reset SHALL delete the unencrypted file

#### Scenario: Corruption is not empty history
- **WHEN** an existing record cannot be authenticated, decrypted, or decoded
- **THEN** the operation SHALL report the specific failure without replacing the
  key, returning an empty history, or falling back to plaintext

### Requirement: Stable readable identifiers
The store SHALL allocate `<PID>-<yyyy>-<MMdd>-<HHmm>-<counter>` IDs using UTC,
the original parent-shell PID, and a durable per-home UInt64 sequence under the
writer lock. The counter SHALL use at least four digits and SHALL not wrap,
reset on minute changes, or be reconstructed from retained records.

#### Scenario: Eviction and reset cannot reuse a prior ID
- **WHEN** records from multiple shells alternate with historySize=1, or off is
  followed by manual in the same minute
- **THEN** newly allocated IDs SHALL differ from all previously issued IDs
- **AND** an evicted exact ID SHALL not silently select a different record

#### Scenario: Concurrent allocation and interrupted writes are safe
- **WHEN** multiple processes write with the same parent PID and frozen timestamp,
  or a writer stops after reserving a sequence
- **THEN** completed records SHALL have unique IDs
- **AND** gaps SHALL be allowed without reusing a reserved ID or corrupting the
  previous snapshot

### Requirement: Coordinated off and reset
Off SHALL disable new capture/analysis and explicitly clear records without
decrypting them or unlocking native key stores. Config writes, reset, and capture
SHALL coordinate through one writer lock and durable generations. The lock and
non-sensitive allocator state SHALL survive reset.

#### Scenario: Stale writer cannot undo off
- **WHEN** a writer reads manual, another process completes off, and the old
  writer subsequently tries to commit, even after manual is re-enabled
- **THEN** the stale writer SHALL be rejected by fresh policy/generation checks
- **AND** no old record SHALL be recreated after successful cleanup

#### Scenario: Capture cannot adopt a generation created after it started
- **WHEN** capture has registered its starting generation and another process
  completes off followed by re-enabling before capture reads its options
- **THEN** that capture SHALL be rejected without a confirmed saved ID
- **AND** a new capture registered in the enabled generation SHALL remain eligible

#### Scenario: Completed off observation is read-only without losing its fence
- **WHEN** off has been durably fenced, cleanup is complete, and no controlled
  history data remains
- **THEN** repeated ordinary lifecycle observations of off SHALL not rewrite
  control state or acquire the writer lock
- **AND** the first off observation SHALL still persist a fence for an in-flight
  generation-zero capture even if the history directory does not yet exist
- **AND** re-enabling SHALL advance generation before removing the completed-off fence

#### Scenario: Cleanup has a partial failure
- **WHEN** off is persisted but a later generation update or deletion fails
- **THEN** the Config operation SHALL return nonzero and state that recovery is
  off but cleanup is incomplete
- **AND** a retry SHALL complete cleanup without deleting the lock or unrelated data

#### Scenario: Old draft data needs an explicit reset
- **WHEN** GUID-v1 history is found
- **THEN** readers and writers SHALL report incompatible draft history and preserve
  its bytes until an explicit off/reset
- **AND** reset SHALL not require decrypting those records

### Requirement: Local selection and detail
`az troubleshoot` SHALL select the current parent shell's latest persisted failure,
or the global latest when none matches. `--id` SHALL require an exact match.
`--list` and `show --id` SHALL be local, read-only, and SHALL not start AI,
authentication, boundary discovery, key creation, or cleanup.

#### Scenario: Two shell windows retain independent defaults
- **WHEN** two shells each save a failure and invoke troubleshoot without an ID
- **THEN** each SHALL select its own latest failure
- **AND** a new shell without a matching record SHALL use the global latest

#### Scenario: List and show have the same fields but different error presentation
- **WHEN** records are listed or one is shown
- **THEN** the fields SHALL be id, command, and error detail
- **AND** list SHALL show a single-line error detail of at most 200 characters
- **AND** show SHALL preserve complete text and line breaks even in a narrow table
- **AND** neither presentation SHALL modify stored or Agent-bound content

#### Scenario: Invalid combinations fail before AI startup
- **WHEN** show lacks an exact ID, style is combined with list/show, or an explicit
  ID does not exist
- **THEN** the request SHALL fail clearly without ignoring the argument or
  analyzing a different record

### Requirement: One shared diagnostic flow
Manual and automatic recovery SHALL pass identical failed-command and error-text
values to the same Agent pipeline. Effective style SHALL come from configured
style or a manual invocation's temporary `--style` override, not from trigger.

#### Scenario: Manual interactive has normal interactive semantics
- **WHEN** manual and automatic diagnosis run with the same effective style
- **THEN** their diagnostic prompt, available tools, approvals, and session policy
  SHALL be equivalent while preserving baseline safety gates
- **AND** a temporary override SHALL not write configuration

### Requirement: Existing completion behavior is preserved
History IDs SHALL enter the existing PowerShell, Bash, and Zsh candidate paths
only for troubleshoot ID value positions, including show and equals syntax.
Matching, interaction, ordinary candidates, cache, and fallback SHALL otherwise
remain unchanged.

#### Scenario: Partial input uses native completion
- **WHEN** the user requests completion for part of a troubleshoot ID
- **THEN** the existing shell matcher SHALL receive valid complete ID candidates
- **AND** the candidate lookup SHALL not unlock keys, write history, or make network calls

#### Scenario: Ordinary command completion does not regress
- **WHEN** existing non-troubleshoot inputs are completed
- **THEN** their candidates, order, labels, replacement ranges, matching, fallback,
  cache behavior, and query counts SHALL match the baseline
- **AND** no additional history or key-store access SHALL occur

### Requirement: Output and telemetry remain trustworthy
The feature SHALL preserve original exit status and output channels, use current
console JSON/help conventions, and exclude raw diagnostic inputs and secrets from
new telemetry and logging events.

#### Scenario: Structured errors remain parseable
- **WHEN** a failure has recovery metadata or a capture warning in JSON mode
- **THEN** stderr SHALL contain one coherent structured error document using the
  existing serializer conventions, not appended text or a second document
- **AND** stdout SHALL not be polluted by recovery hints
