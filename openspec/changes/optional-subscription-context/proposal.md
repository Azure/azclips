## Why

Authentication establishes an identity and tenant, not a mandatory Azure subscription. Requiring a subscription during login or default binding prevents tenant-scoped operations and commands whose resource IDs already supply their scope.

## What Changes

- Allow every supported login method and saved profile to omit subscription.
- Do not discover or inherit an unrelated active subscription during fresh login.
- Apply profile defaults opportunistically, without treating absence as an error.
- Require a subscription only when constructing a subscription-scoped operation, after explicit IDs and scopes have been resolved.
- Preserve validation and tenant resolution for explicitly selected subscriptions.

## Capabilities

### New Capabilities

- `optional-subscription-context`: Subscriptionless authentication and profiles, optional defaults, and point-of-use scope requirements across CLI and PowerShell.

### Modified Capabilities

None; the repository has no main specs yet.

## Impact

Accounts login and parameter-set resolution, profile models, argument binding, subscription-scoped extension requests and their generator, related tests and user help. No new dependencies or profile storage migration.
