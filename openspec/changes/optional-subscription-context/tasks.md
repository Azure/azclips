## 1. Regression coverage

- [x] 1.1 Add failing tests for subscriptionless login modes, profiles, and parameter-set defaults.
- [x] 1.2 Add failing tests for optional binding, explicit scope precedence, and missing subscription at request construction.

## 2. Implementation

- [x] 2.1 Make profile subscriptions nullable and profile default application optional; exclude fresh login from inherited defaults.
- [x] 2.2 Allow subscriptionless login while preserving explicit subscription validation and profile reauthentication.
- [x] 2.3 Guard subscription-scoped URI construction across extensions and generated processors without blocking complete IDs, URLs, or tenant scopes.

## 3. Verification and documentation

- [x] 3.1 Update related login/profile help and generator documentation.
- [x] 3.2 Run affected test projects, generator tests, and the complete solution build; resolve regressions caused by the change.
