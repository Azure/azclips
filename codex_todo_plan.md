# Cirrus Signing Parity Plan (vs Azure PowerShell)

## Goal
Create functional parity where appropriate between Cirrus release-signing implementation and Azure PowerShell signing/release discipline, while preserving Cirrus scope (release artifacts: `.nupkg`, Windows zip payloads, Linux/macOS archives).

## Scope Assumptions
- This plan focuses on signing, verification, provenance, and publish-gating behavior.
- It does not require copying Azure PowerShell module structure or packaging model.
- Existing Cirrus stage fanout/fanin architecture stays unless a parity item explicitly changes it.

## Parity Work Items

| ID | Aspect | What Azure PowerShell Achieves | Current Cirrus State | Plan For Cirrus | High-Level Outcome | Priority | Status |
|---|---|---|---|---|---|---|---|
| P01 | ESRP verify strategy | ESRP sign + ESRP verify in same signing pass for key artifact types | Cirrus currently uses ESRP sign + local verify (faster) | Decide policy per artifact type: keep local-only, re-enable ESRP verify, or hybrid | Clear trust model and predictable runtime | P0 | [x] Done |
| P02 | Timestamp policy | Explicit RFC3161 timestamp policy in signing params | Timestamp currently removed due earlier ESRP incompatibility | Reintroduce timestamp with validated parameter set and fallback toggle | Long-term signature validity with controlled risk | P0 | [x] Done |
| P03 | Strong-name policy | Explicit strong-name passes for selected assemblies | No explicit strong-name pass in Cirrus signing template | Define whether Cirrus binaries require strong-name at signing stage; add pass if required | Assembly identity/compliance parity for managed binaries | P0 | [x] Done |
| P04 | First-party vs third-party separation | Separate handling for Microsoft vs third-party binaries | Cirrus signs target patterns without dedicated third-party mode | Classify signed binary inventory into first/third-party; split operations as needed | Better policy correctness and easier audits | P1 | [ ] Pending |
| P05 | Append-sign behavior for third-party | Uses append signing (`/as`) where signature preservation is required | No dedicated append-sign handling in Cirrus | Introduce optional append-sign pass for approved third-party list | Preserve upstream signature chains while adding Microsoft trust | P1 | [ ] Pending |
| P06 | Pattern governance | Narrow, intentional pattern sets with exclusions | Cirrus has narrowed patterns, but governance is implicit | Add explicit allowlist + exclusion rationale table in docs and pipeline comments | Reduced accidental signing scope drift | P1 | [ ] Pending |
| P07 | Pre-sign inventory contract | Deterministic inventory per pass | Cirrus prints candidate files/size in logs | Promote to formal contract (counts, hash snapshot, threshold checks) | Faster incident triage and drift detection | P1 | [ ] Pending |
| P08 | Post-sign verification depth | Multi-layer verification in signing + release orchestration | Cirrus verifies signed outputs; validate stages also check artifacts | Add explicit verification matrix by file type (NuGet, PE, archive) with pass/fail criteria | Uniform quality gates and less ambiguity | P0 | [x] Done |
| P09 | Provenance handoff model | Sign pipeline build ID treated as canonical release input | Cirrus publish can consume signing output, but provenance policy is not formalized | Formalize `source_pipeline_name/source_build_id` handoff rules and trace fields | Reproducible release provenance and auditability | P0 | [x] Done |
| P10 | Manual release gate | Manual approval before sensitive publish/push actions | Cirrus has preflight and validation, no manual gate in signing flow | Add optional manual validation gate before final publish stage | Human control point for high-impact releases | P1 | [ ] Pending |
| P11 | Policy-file hygiene | Cleanly configured ESRP policy file inputs | Cirrus now clears batch/CI policy paths | Keep explicit empty defaults and add guard checks to prevent path injection regressions | Stable ESRP invocation behavior | P0 | [x] Done |
| P12 | Runtime and timeout tuning | Tuned ESRP concurrency/retry/session based on artifact class | Cirrus has static values (`MaxConcurrency`, `SessionTimeout`, retries) | Parameterize tuning by payload class and environment | Lower tail latency and fewer stuck runs | P1 | [ ] Pending |
| P13 | Failure taxonomy and playbooks | Clear operational runbooks for known ESRP failures | Cirrus has tribal knowledge from recent debugging | Codify failure signatures, probable root cause, immediate actions, escalation paths | Faster MTTR for signing incidents | P0 | [ ] Pending |
| P14 | Validation environment resilience | Validation steps avoid source-tree coupling | Cirrus fixed global.json coupling issues recently | Add regression checks that fail CI if checkout-none jobs depend on repo SDK pins | Prevent repeat of SDK-resolution failures | P0 | [x] Done |
| P15 | Artifact contract formalization | Stable artifact naming and schema for downstream consumers | Cirrus uses stable artifact names and manifest generation | Version and document artifact schemas, include compatibility notes for publish consumers | Safer pipeline evolution with fewer breakages | P1 | [ ] Pending |
| P16 | Security boundary documentation | Clear documentation of service connections, KV certs, and release-only access | Cirrus enforces release project preflight and ESRP variable group usage | Add explicit security-boundary section with least-privilege and rotation expectations | Stronger governance and onboarding clarity | P0 | [x] Done |
| P17 | Cross-platform release semantics | Signed vs collected artifacts clearly distinguished | Cirrus collects Linux/macOS and signs Windows/NuGet | Keep model explicit and document rationale/limitations in release notes | Correct expectation setting for consumers | P1 | [ ] Pending |
| P18 | Change-control and rollback | Safe rollout strategy for signing behavior changes | Ad-hoc rollout during incident response | Define canary path, staged rollout, and one-command rollback per major change | Reduced production risk during signing changes | P0 | [ ] Pending |

## Delivery Phases

| Phase | Focus | Included IDs | Exit Criteria |
|---|---|---|---|
| Phase 1 | Trust and correctness baseline | P01, P02, P03, P08, P09, P11, P14, P16 | Signed outputs are policy-compliant, verification matrix is enforced, provenance is explicit |
| Phase 2 | Operational hardening | P04, P05, P06, P07, P12, P13 | Signing runs are stable under load, failure triage is deterministic, inventory is auditable |
| Phase 3 | Release governance and lifecycle | P10, P15, P17, P18 | Publish gates and rollback are formalized, consumers have stable artifact contracts |

## Suggested Prioritization Rule

| Priority | Meaning | Typical SLA |
|---|---|---|
| P0 | Required for reliability/compliance/safety | Immediate to next sprint |
| P1 | Important hardening and maintainability | Next 1-2 sprints |

## Definition of Done (Plan-Level)

| Area | Done When |
|---|---|
| Policy clarity | Every signing action has an explicit policy decision (why enabled/disabled) |
| Verification | Each artifact type has documented and automated verification gates |
| Provenance | Every release artifact is traceable to a specific signed build input and manifest |
| Operability | On-call runbook can map log signature -> fix path in under 10 minutes |
| Governance | Manual gate and rollback path are available for production release flows |
