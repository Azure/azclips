## Context

Login parameter sets and argument binding currently require a subscription even when authentication or the operation does not. Profile persistence already accepts missing subscriptions. Subscription-scoped atomic processors currently rely on the binder to prevent empty URL segments.

## Goals / Non-Goals

**Goals:** Allow subscriptionless login and profiles on both surfaces; preserve explicit subscription and cross-tenant validation; reject missing scope before sending a subscription-scoped request.

**Non-Goals:** Subscription discovery, automatic selection, authentication changes, or a new profile storage format.

## Decisions

- Represent absent profile subscriptions as nullable strings. Normalize absent login values before resolution.
- Fresh login does not inherit profile defaults. Existing-profile reauthentication retains its subscription unless explicitly overridden.
- Allow parameter sets with zero mandatory parameters when pairwise exclusivity still holds. The browser set is the default, not a fallback for invalid authentication inputs.
- Apply available profile defaults during binding; absence alone is not an error.
- Validate subscription at the point where a subscription-scoped URI is built, using one shared required-value helper. This preserves commands using tenant/management-group scopes, complete resource IDs, and explicit URLs; it also covers nested atomic processors. Update the generator so regeneration preserves the guard.
- Keep existing GUID validation for login/profile persistence. The required-value helper only rejects missing values, preserving existing processor behavior for opaque test IDs.

## Risks / Trade-offs

- [Removing the binder guard exposes malformed requests] -> Cover every subscription-scoped URI builder and test representative extension families plus generated output.
- [Default browser set overlaps another authentication set] -> Retain pairwise exclusivity validation and add resolver tests.
- [Nullable model changes expose consumers] -> Build the complete solution and run affected tests.

## Migration Plan

Existing profiles remain readable, including empty-string subscriptions. No storage migration or dependencies are required. Ship defaulting and point-of-use validation together.
