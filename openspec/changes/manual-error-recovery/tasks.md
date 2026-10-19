## 1. Baseline and existing implementation

- [x] 1.1 Refresh main and related PR heads, preserve pending-PR boundaries, and integrate current main.
- [x] 1.2 Build the merged baseline with the SDK pinned by current main.
- [ ] 1.3 Reuse existing configuration, history, process, output, and completion helpers; keep added functions cohesive.

## 2. Configuration and shared contracts

- [x] 2.1 Implement orthogonal recovery options and public/storage key mapping across configuration operations.
- [x] 2.2 Remove released legacy keys from stored configuration without migration, and remove recovery-only legacy gates, including existing test callers.
- [x] 2.3 Define common diagnostic input/effective-style contracts and versioned history allocation/reset interfaces.
- [x] 2.4 Verify configuration ordering, deletion, defaults/sources, environment binding, and existing Config hosts.

## 3. Protected history and reset

- [x] 3.1 Implement v2 snapshots, readable IDs, durable sequence reservation, retention, and explicit v1 handling.
- [x] 3.2 Implement home/generation protected keys and noninteractive secure-backend failure handling.
- [x] 3.3 Coordinate off/reset and configuration writes with generation fencing and truthful partial failures.
- [x] 3.4 Verify crash points, stale writers, concurrent processes, eviction/reset ID nonreuse, and read-only paths.

## 4. CLI failure capture

- [x] 4.1 Resolve the original parent-shell PID on supported platforms and preserve fallback boundaries.
- [x] 4.2 Bound capture, isolate uncancellable storage work where necessary, and clean up owned child processes.
- [x] 4.3 Preserve original failures, implement the two manual hints, and keep structured output parseable.
- [x] 4.4 Verify exclusions, at-most-once capture, long input, timeouts, cancellation, and unconfirmed commits.

## 5. Shared Agent recovery

- [x] 5.1 Route automatic and manual command/error input through one Agent transport and prompt path.
- [x] 5.2 Resolve style consistently and remove draft-only manual tool/permission restrictions without weakening existing gates.
- [x] 5.3 Preserve outcomes, model/provider settings, trace hierarchy, routing, and diagnostic privacy.
- [x] 5.4 Verify input/prompt/tool/policy equivalence and nonzero infrastructure failure outcomes.

## 6. Troubleshoot and completion

- [x] 6.1 Implement exact/show/list/style grammar and terminal-aware default selection with explicit errors.
- [x] 6.2 Render the three local fields, list summaries, lossless show, and current runtime help.
- [x] 6.3 Integrate new ID candidates into existing shell paths and isolate unrelated draft completion changes.
- [ ] 6.4 Verify local-only behavior and real PowerShell/Bash/Zsh nonregression against the baseline.

## 7. Delivery and independent review

- [x] 7.1 Update directly related configuration, recovery, help, and completion documentation.
- [x] 7.2 Run targeted offline tests and the solution build; record actual platform coverage and unresolved environment limits.
- [x] 7.3 Have the same three original reviewers independently review the implementation, including reuse and function size.
- [ ] 7.4 Resolve substantive review findings and re-run affected validation.
- [x] 7.5 Commit and update the existing PR #934, verify it remains Draft, and report completed work and remaining validation.

## 8. User-requested desktop end-to-end validation

- [ ] 8.1 Provision the selected local Ubuntu desktop VM through an authorized Hyper-V session.
- [ ] 8.2 Verify actual CLI/keyring persistence, no-prompt failure paths, multiple terminals, completion, and an authorized real-model recovery; keep platform-specific results distinct from offline tests.

## Validation status

- On 2026-09-25, the user explicitly requested publishing the current checkpoint.
  Implementation and review repairs through `de480bb1b` were pushed to the existing
  PR #934, and its remote head and Draft status were verified. This checkpoint
  publication does not close task 7.4, approve the pending JSON timing decision,
  or complete the final reviewer follow-up and native E2E gates.
- All three original reviewers completed independent code review of `0134c6d`.
  Confirmed capture fencing, configuration compatibility, output consistency, and
  documentation findings are being repaired under task 7.4; review completion
  does not imply those fixes or native E2E have passed.
- Review-repair tests: 193 capture/store/configuration/concurrency tests passed
  with fake keys, followed by 179 focused configuration/parser/console/real-CLI
  routing tests. These selections overlap and are not additional E2E totals.
- Capture now registers its starting generation before reading options. A durable
  completed-off fence prevents stale writes while making repeated clean off
  observations read-only. First off still fences an absent directory.
- Failed help/version utilities are excluded using existing parser classification.
  Configuration repair guidance, non-recovery flat-key compatibility, shared
  console JSON formatting, and the telemetry style documentation are corrected.
- Late automatic-Agent diagnostics in JSON mode still need the user's output
  timing decision. No streaming-JSON change has been made and task 7.4 remains open.
- Latest review-repair solution build: passed, zero errors and three existing
  Copilot SDK experimental-API warnings. Earlier CLI rebuilds reported the two
  existing Identity test nullability warnings.
- Focused Agent tests: 56 passed, including common tool/permission policy and real
  pipe transport rejected before AI startup.
- Integrated CLI/configuration/recovery/help/completion/parser/console offline tests: 678 passed,
  four existing live tests intentionally skipped, with `Category!=NativeKeyStore`.
- This includes all 25 stream-routing tests, including migration warnings and
  failed-help/version capture exclusions. Config commands retain their existing
  diagnostics without duplicate host warnings.
- Real PowerShell and Git Bash completion/baseline comparisons were rerun and
  passed against `83f27105`. Zsh and
  real macOS/Linux sessions remain unverified.
- Linux now uses Tmds.DBus.Protocol 0.95.1 for direct Secret Service calls over
  local Unix sockets, with no service activation, interactive authorization,
  Unlock, or Prompt calls. Missing/locked services fail closed. macOS now uses its
  dedicated no-UI native adapter; both Unix backends share verified, nonsecret key
  markers in the common store. Task 3.2 implementation is complete, not native E2E.
- Linux fake-service and protected-key-marker tests: 42 passed. The authentication
  project also built in Release with zero warnings/errors. These tests do not
  access any real native key store.
- macOS adapter: 43 mock/native-facade tests passed, including empty-key rejection
  and common marker interface dispatch. No actual macOS Keychain was accessed.
- The user selected a local Ubuntu desktop VM for real E2E. Hyper-V is running
  and host capacity is sufficient, but the current session lacks Hyper-V management
  authorization. No VM has been created and no Linux desktop/keyring E2E has run.
- Native Windows validation was not rerun because an earlier native batch was
  stopped by Defender; no protection bypass is allowed.
- Main remained at `cfccc60d3fe9a5a69449bf7ab0a0f8a81a81223a`; related open PR
  heads remained unchanged at integration validation. PR #934 remains Draft.
