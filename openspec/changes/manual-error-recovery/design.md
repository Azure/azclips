## Context

Draft PR #934 already has encrypted history, a local troubleshoot entry, anonymous
pipe transport, and ID completion. This change reuses those seams and the existing
automatic Agent flow rather than introducing a second diagnostic implementation.
The approved plan was independently reviewed and its findings closed by three
reviewers. Implementation starts from main `cfccc60d`, including SDK 10.0.401,
current JSON escaping, terminal-width tables, and runtime help.

## Goals / Non-Goals

**Goals:**

- Make trigger and interaction style independent, with off/oneshot defaults.
- Preserve the original failed command and error text without truncating storage
  or Agent input.
- Provide safe, local history selection and unchanged ordinary completion.
- Keep persistence, configuration, process transport, and rendering in small,
  cohesive components, using existing interfaces and helpers where possible.

**Non-Goals:**

- No new model, SDK, AI agent, completion engine, background service, plan adoption
  ledger, automatic plan application, or PowerShell recovery entry.
- No merging other pending PRs wholesale, no Ready transition, and no live-model
  or Azure-resource operations in offline validation.

## Decisions

### Configuration is mapped once

Public `agent.recovery` maps to internal `agent.recovery.trigger`. The style and
historySize leaves retain their names. Reflection-based schema metadata carries
the public/storage mapping through reads, writes, deletion, defaults, source
reporting, environment merging, and typed binding. CLI and existing PowerShell
Config commands share this path. Replacing the public key or silently replacing
a scalar with an object would violate the approved interface.

The released keys `agent.enabled` and `agent.sessionStyle` are removed from the
stored configuration on first read, under the configuration mutation lock, and
are not migrated; new writes to them are rejected. No setting means off/oneshot/10.
An off policy with no history directory touches no files. Other Agent
provider/model/trace settings remain unchanged.

### The store allocates stable IDs

Use `<PID>-<yyyy>-<MMdd>-<HHmm>-<counter>`, with UTC time, the original CLI's parent
shell PID, and a per-home monotonically increasing UInt64 counter. Counters start
at zero, have at least four displayed digits, and never reset at minute boundaries.
Allocate under the existing cross-process writer lock.

A minimal versioned control file retains the next sequence, generation,
pending-clear state, and a non-sensitive completed-off fence independently of
record retention. Reserve the counter
atomically before replacing the encrypted snapshot: a failed write can leave a
gap, but cannot reuse an ID. Do not pretend two files form one atomic transaction.
Off/reset keeps the lock and non-sensitive allocator state.

The v2 envelope has variable-length IDs and authenticated version/key-generation
metadata. Existing GUID-v1 files remain untouched and explicitly incompatible
until the user resets through off. Missing or corrupt state is not empty history.

### Off is a coordinated transition

Use one writer lock and a consistent history-lock then configuration-lock order.
Persist off, advance the generation and pending-clear state, then clear controlled
record/temporary/key-marker files without decrypting or opening native key stores.
Report partial failures accurately, retain off, and retry idempotently. Never
delete the lock file or recursively delete the history directory.

Writers register their starting generation through `BeginCaptureAsync` before
reading capture options, and recheck fresh effective policy under the lock before
allocation/commit. Read-only generation inspection is not capture registration.
A capture spanning a generation change is rejected rather than adopting a newer
generation. Re-enabling after off completes cleanup and advances generation before
the completed-off fence is removed or new writes are allowed. A reset uses a new
protected key namespace so unreadable old secrets cannot prevent future valid writes.

The first observed off must persist its fence even if the history directory does
not exist: a generation-zero capture may already be in flight. Once that fence is
durable and no controlled data or pending cleanup remains, repeated off observation
does not take the writer lock, advance generation, or rewrite state. A new enabled
capture must reopen the generation under the same lock, including when its effective
policy comes from an environment override.

Manual file/environment changes are observed at the next ordinary CLI lifecycle.
List, show, help, and Tab never perform cleanup. Pending cleanup is an explicit
local-view error, not a success-shaped empty history.

### Original failures remain responsive

Human-readable original errors precede capture. The full capture has a two-second
budget, including native key access and file IO. JSON can wait within that budget
to include recovery metadata in one parseable error document. A failure or
unconfirmed commit never prints a purported saved ID or prevents existing auto
diagnosis.

Native backends must be noninteractive. Isolate proven-noninteractive but
uncancellable operations in a transient local CLI worker using anonymous pipes,
not in a detached Task.WhenAny operation that can continue writing. Terminate only
the worker created for that request on cancellation/timeout. No Agent install,
network startup, plaintext temporary file, or recursive capture is involved.

Linux uses the maintained Tmds.DBus.Protocol client rather than duplicating D-Bus
transport/native-memory management or using high-level helpers that may unlock.
Only local Unix-socket session buses are accepted. Secret Service requests disable
service activation and interactive authorization, never call Unlock/Prompt, and
reject any returned prompt. Creating an item never replaces an existing key.
Closing the connection closes its Secret Service sessions; reads have a bounded
operation lifetime, and capture retains its encompassing worker deadline.
Unix key markers are nonsecret and written only after a valid native key read or
verified write, during write-enabled requests. Read-only views never create them.

macOS uses a dedicated, injectable Security/CoreFoundation adapter, including the
query no-UI policy and a scoped process-wide Keychain interaction guard. The guard
restores its prior state; capture retains the worker isolation/deadline. The guard
is not a per-key setting and can affect other Keychain clients in the same process,
so it must not overlap unrelated interactive authentication. Actual macOS desktop
behavior remains a separate validation gate from mock/native-facade tests.

### Trigger does not define Agent semantics

Manual selection extracts the saved FailedCommand and ErrorText. Automatic
recovery supplies that same pair immediately. Both use the same transport,
diagnostic prompt, effective style, tool availability, approval hooks, and session
state. Remove only draft-added manual-specific restrictions; preserve baseline
plan gates and approvals. Keep provider/model/trace and current telemetry routing.

### Local presentation and completion reuse existing machinery

Default selection uses the newest successfully persisted sequence for the current
parent PID, falling back to the newest global sequence. Exact IDs never fall back.
Linux/macOS use getppid; Windows uses its native parent-process query. Extra
wrappers, PID reuse, and shared directories across PID namespaces are documented
limits of lightweight association.

List sorts by numeric PID ascending, then UTC time and counter descending.
List/show expose id, command, and error detail; only list normalizes/truncates error
detail to 200 characters. Show uses lossless output even with table format and
narrow terminals, without changing other commands' table behavior.

PowerShell, Bash, and Zsh obtain dynamic IDs only in troubleshoot ID positions.
Keep the original matching, candidate order/labels, replacement ranges, fallback,
cache, and enable/disable behavior elsewhere. Do not add server-side prefix
filtering that discards candidates the shell could otherwise match.

## Risks / Trade-offs

- Native secure storage can be missing, locked, or slow: keep only the latest failure
  unencrypted in a current-user-only file and warn, following the Identity token-cache
  precedent; missing, invalid, or damaged existing keys remain errors. Prove
  noninteractive access and bounded execution separately.
- Process crashes can occur between allocator and snapshot writes: preserve the
  previous snapshot and tolerate reserved counter gaps.
- Off/config writes cannot be atomic with multiple file deletions: retain an
  explicit generation/pending state and truthful partial-failure diagnostics.
- Main and pending PRs share Program, AgentModeEntry, and ChatAgent: use narrow
  helpers and recheck their heads before integration.
- Existing completion has platform-specific behavior: compare real shell results
  against the pre-feature baseline; mocks alone are insufficient.
- New diagnostics could leak through ActivityEvent messages: use ConsoleSink or
  structured error output; telemetry receives only approved non-sensitive codes.

## Migration Plan

1. Integrate latest main without discarding its output/help/SDK changes.
2. Implement public configuration mapping and removal of released old keys, including
   tests and existing test-harness callers.
3. Introduce v2 persistence and explicit off/reset for old draft data.
4. Wire failure capture, shared Agent semantics, local presentation, and completion.
5. Run targeted offline tests, build, and available platform/shell checks.
6. Have the same three reviewers independently review the implementation and fix
   substantive findings before updating the existing draft PR.

Old binaries are not expected to read v2. Downgrades must not silently delete or
rewrite v2 data; disabling/resetting is an explicit user operation.

## Open Questions

No unresolved product decisions. Real native backend behavior, cross-platform
tests, and shell nonregression are implementation gates, not assumed successes.
