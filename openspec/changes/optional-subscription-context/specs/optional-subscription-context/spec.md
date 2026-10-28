## ADDED Requirements

### Requirement: Authentication does not require subscription
Every supported login method SHALL accept an omitted subscription when its authentication inputs are valid. Login SHALL save the authenticated tenant without subscription discovery or inheritance from an unrelated active profile.

#### Scenario: Tenant-only or home-tenant login
- **WHEN** a caller logs in without subscription, with or without an explicit tenant
- **THEN** login succeeds and saves the token's tenant with no subscription
- **AND** no subscription service calls are made

#### Scenario: Explicit subscription
- **WHEN** a caller supplies a subscription
- **THEN** login preserves subscription validation and owning-tenant resolution

### Requirement: Profiles support absent subscription
Profiles SHALL persist, activate, display, and reauthenticate without a subscription. Existing-profile reauthentication SHALL preserve its subscription unless explicitly overridden.

#### Scenario: Subscriptionless profile
- **WHEN** a subscriptionless profile is saved and activated
- **THEN** it remains usable for authenticated operations that do not need a subscription

### Requirement: Profile defaults are optional
Argument binding SHALL apply available profile defaults without requiring missing defaults. Explicit command arguments SHALL take precedence.

#### Scenario: Missing default
- **WHEN** a processor accepts subscription but the selected profile has none
- **THEN** binding succeeds and operation-specific scope resolution determines whether it is needed

### Requirement: Scope requirements are checked at point of use
Subscription-scoped requests SHALL fail locally with actionable missing-parameter guidance if no subscription can be resolved. Explicit IDs, URLs, and tenant or management-group scopes SHALL NOT require an unrelated profile subscription.

#### Scenario: Subscription-scoped request without scope
- **WHEN** an operation needs a subscription and neither input nor profile nor resource ID provides one
- **THEN** it fails before sending its HTTP request with guidance to supply a subscription or choose a profile containing one

#### Scenario: Complete target scope
- **WHEN** the caller supplies a complete resource ID, URL, or supported tenant/management-group scope
- **THEN** the operation proceeds without a profile subscription
