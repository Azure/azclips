**Parent Feature:** #32

---

> 🔲 **PLACEHOLDER** — Detailed requirements, examples, and edge cases to be added.

## Description

Cirrus must support profile-based authentication that enables users to create, store, and switch between named login profiles — each tied to a specific identity, tenant, and subscription. This unifies the sign-in experience across CLI and PowerShell, achieves SSO capabilities, and reduces friction for users who operate across multiple Azure environments.

**Traces to:**
- §1.1 Goal: Unified authentication experience with profile-based login
- §3.1 KPI: ≥50% of preview users successfully complete login using profile-based authentication
- §6.12 in planning doc

## User I Can Statements

- I can create a named login profile that remembers my identity, tenant, and default subscription.
- I can switch between profiles without re-authenticating each time (SSO-backed).
- I can list all my saved profiles and see which one is currently active.
- I can share profile configuration across CLI and PowerShell sessions on the same machine.

## Explicitly Deferred

- Cross-tenant profile traversal
- Service principal profiles
- Enterprise policy-driven profile provisioning
- Team/org shared profile templates

## Related Items

- **Parent:** #32 (Unified Auth, Identity, Onboarding)
- **Related:** #42 (Context & Profile Display)
- **Related:** #43 (First-Run Onboarding) — profile creation is part of onboarding