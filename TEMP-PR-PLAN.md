> TODO BEFORE MERGE: Delete this temporary planning file.

# TEMP PR Plan: Extension Resolution and Redundancy

## Scope

- Define deterministic extension conflict resolution across install locations.
- Remove redundancy in extension metadata and management flow.
- Verify assembly-loading config paths are respected, configurable, and observable.
- Reduce manual maintenance for built-in command mapping where feasible.

## Current Status

- Completed: unified verb source-of-truth on this branch (removed `GetVerbs()` contract usage; consumers now resolve verbs from `[ExtensionVerb]`).
- Completed: build + targeted test validation for the verb-source refactor.
- Completed: same-identity selection engine is in place and wired into dispatch/help discovery paths (pin-aware + version-aware behavior verified locally).
- Completed: structured debug diagnostics for same-identity selection decisions (selected/ignored candidates + reason) are emitted in dispatch/help/discovery call sites.
- Completed: cross-identity collision policy is enforced (selector-before-collision; cross-identity duplicate leaf ownership hard-fails in startup and add flows).
- Completed: debug selection logs now use a human-readable summary format in `--debug` output.
- Remaining: add path-level candidate provenance in diagnostics, decide whether machine-parsable payloads still matter, and finalize built-ins strategy/docs decisions.

## Locked Decisions

### 1) Verb source of truth

- Locked: `[ExtensionVerb]` is canonical; `GetVerbs()` contract is removed.

### 2) Precedence within same extension identity

When the same extension identity exists in both locations, resolve in this order:

1. Pin first: if a pinned version exists, prefer pinned candidate.
2. Otherwise choose the higher semantic version.
3. If package id and version are equal, treat candidates as equivalent and choose one deterministically.
4. Deterministic choice should not affect behavior; diagnostics should still record all equivalent candidates observed.

### 3) Duplicate-command collision policy

1. Group candidates by extension identity (package id) first.
2. Same identity across multiple locations is treated as candidate selection, not a collision.
3. Pick one winner within same identity using the precedence rules.
4. Detect collisions only across different extension identities.
5. Across different identities, duplicate command ownership remains a hard-fail.

Diagnostics requirements:

- For same-identity duplicates: log ignored candidates and selection reason.
- For cross-identity duplicates: throw collision error with all conflicting owners.

## Implementation Plan (Locked Decisions)

### Phase A: Selection engine

- [x] Add a pure selector component for candidate resolution by identity.
- [x] Implement deterministic reason codes: `Pinned`, `HigherVersion`, `EquivalentVersion`, `SingleCandidate`.
- [x] Include selected candidate and ignored candidates in structured diagnostics.

### Phase B: Collision handling

- [x] Apply selector before collision detection.
- [x] Run collision detection only across different extension identities.
- [x] Keep hard-fail behavior for cross-identity duplicate command ownership.

### Phase C: Wiring and path behavior

- [x] Enforce configured `assemblyLoading.builtinInstallationDir` and `assemblyLoading.versionedInstallationDir` in discovery/loading.
- [x] Ensure tie-break behavior is stable regardless of directory enumeration order.

### Phase D: Verification and packaging

- [ ] Add table-driven tests for precedence cases P1-P10.
- [x] Add explicit tests for same-identity dedup vs cross-identity hard-fail.
- [ ] Build: `dotnet build azclips.slnx` and run targeted tests.
- [ ] Manually validate default and overridden directory behavior plus conflict paths.
- [ ] Prepare PR summary with behavior changes, test evidence, and deferred items.

### Phase E: Diagnostics readability

- [x] Replace raw one-line selection JSON debug output with a human-readable summary format.
- [ ] Keep machine-parsable payloads available (optional compact JSON toggle or secondary debug line).
- [ ] Add one focused test/assertion for debug output shape stability.

### Precedence Acceptance Tests (Table-Driven)

Legend:

- `Implements`: whether behavior is implemented in code.
- `Validation`: `Automated`, `Manual`, `Automated+Manual`, or `Pending`.

| Case | Implements | Validation       | Candidate in builtin dir | Candidate in versioned dir | Pin state                | Expected winner | Expected reason                                     |
| ---- | ---------- | ---------------- | ------------------------ | -------------------------- | ------------------------ | --------------- | --------------------------------------------------- |
| P1   | [x]        | Automated        | 1.2.0                    | 1.1.9                      | none                     | builtin         | Higher semantic version                             |
| P2   | [x]        | Automated        | 1.1.9                    | 1.2.0                      | none                     | versioned       | Higher semantic version                             |
| P3   | [x]        | Automated        | 1.2.0                    | 1.2.0                      | none                     | either          | Equivalent package id + version; deterministic pick |
| P4   | [x]        | Automated+Manual | 1.2.0                    | 1.3.0                      | pinned=1.2.0 (builtin)   | builtin         | Pin wins over higher unpinned candidate             |
| P5   | [x]        | Automated+Manual | 1.2.0                    | 1.3.0                      | pinned=1.3.0 (versioned) | versioned       | Pin wins and selects matching candidate             |
| P6   | [x]        | Automated        | 1.4.0                    | 1.3.0                      | pinned=1.3.0 (versioned) | versioned       | Pin wins over higher unpinned candidate             |
| P7   | [x]        | Automated        | 1.2.0                    | absent                     | none                     | builtin         | Only builtin candidate exists                       |
| P8   | [x]        | Automated        | absent                   | 1.2.0                      | none                     | versioned       | Only versioned candidate exists                     |
| P9   | [x]        | Automated        | 1.2.0                    | 1.2.0                      | pinned=not-present       | either          | Pin unresolved -> equivalent-version selection      |
| P10  | [ ]        | Pending          | 1.2.0                    | 1.3.0                      | none + custom dirs       | versioned       | Override paths respected, then version precedence   |

Diagnostics assertions for all precedence cases:

- Include selected candidate and ignored candidates in structured debug payloads.
- Include one deterministic reason code: `Pinned`, `HigherVersion`, `EquivalentVersion`, `SingleCandidate`.
- Include persisted preference context (`pinned`, requested version) alongside selection.
- Path-level candidate provenance in diagnostics is pending source-location wiring.

## Scenario Matrix

| Scenario                                                   | Expected behavior                                                   | Test added | Notes                                                                         |
| ---------------------------------------------------------- | ------------------------------------------------------------------- | ---------- | ----------------------------------------------------------------------------- |
| Same extension in builtin and versioned dirs               | Pin first, then higher version; equal version -> deterministic pick | [x]        | Automated + local validation                                                  |
| Same command from same extension identity in both dirs     | Select one winner via precedence; no collision error                | [x]        | Automated + local validation                                                  |
| Same command from different extension identities           | Hard-fail collision error                                           | [x]        | Automated + local validation (`extension add --path` rejected duplicate leaf) |
| Custom `assemblyLoading.builtinInstallationDir` override   | TBD                                                                 | [ ]        | Verify effective path used                                                    |
| Custom `assemblyLoading.versionedInstallationDir` override | TBD                                                                 | [ ]        | Verify effective path used                                                    |
| `ExtensionVerb` duplicates `GetVerbs()` data               | Unified on `[ExtensionVerb]`                                        | [x]        | `GetVerbs()` contract removed                                                 |
| Built-ins `login` and `config` command map maintenance     | TBD                                                                 | [ ]        | Assess manual-edit requirement                                                |

## Pending Decisions (Not Locked)

- Should built-ins continue explicit command-map maintenance or shift to discovery/generation?
- Should canonical naming be `verb`, `leaf`, or `command group` across code/docs?
- Are user-facing docs/help updates required for any policy changes?

## PR Link

- Active PR for this branch: https://github.com/Azure/azclips/pull/370
