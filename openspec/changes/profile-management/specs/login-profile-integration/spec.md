## ADDED Requirements

### Requirement: Login creates a profile
The `login` command SHALL create or overwrite a profile upon successful authentication. If `--out-profile <name>` is provided, the profile SHALL be saved with that name. If neither `--out-profile` nor `--profile` is provided, the profile SHALL be saved as "default". Upon success, the `login` command SHALL output the resulting `Profile`.

#### Scenario: Login with --out-profile
- **WHEN** user runs `az login --tenant t1 --subscription s1 --out-profile prod`
- **THEN** a profile named "prod" SHALL be created with tenant "t1", subscription "s1", cloud "AzureCloud", and auth method Interactive
- **AND** the UserPrincipalName SHALL be populated from the authenticated account (surfaced via the auth result)
- **AND** "prod" SHALL be set as the active profile
- **AND** the resulting "prod" `Profile` SHALL be returned as output

#### Scenario: Login without --out-profile creates default
- **WHEN** user runs `az login --tenant t1 --subscription s1`
- **THEN** a profile named "default" SHALL be created with the login result
- **AND** any existing "default" profile SHALL be overwritten
- **AND** "default" SHALL be set as the active profile

#### Scenario: Login with device code flow
- **WHEN** user runs `az login --tenant t1 --subscription s1 --use-device-code --out-profile ci`
- **THEN** the profile SHALL be created with auth method DeviceCode

#### Scenario: Login overwrites existing named profile
- **WHEN** user runs `az login --out-profile prod` and a profile named "prod" already exists
- **THEN** the existing "prod" profile SHALL be overwritten with the new login result

### Requirement: Re-authenticate an existing profile with --profile
The `login` command SHALL accept `--profile <name>` to re-authenticate an existing profile (e.g., when MFA or a refreshed token is required). The command SHALL source tenant, subscription, and cloud from the named profile rather than from `--tenant`/`--subscription`, trigger the authentication flow, overwrite the profile with the refreshed result, set it active, and output it. If the named profile does not exist, the command SHALL fail with a clear error.

#### Scenario: Re-authenticate an existing profile
- **WHEN** user runs `az login --profile prod` and a profile named "prod" exists
- **THEN** the authentication flow SHALL run using "prod"'s tenant, subscription, and cloud
- **AND** the "prod" profile SHALL be overwritten with the refreshed login result
- **AND** "prod" SHALL be set as the active profile
- **AND** the "prod" `Profile` SHALL be returned as output

#### Scenario: Re-authenticate a non-existent profile
- **WHEN** user runs `az login --profile nonexistent` and no such profile exists
- **THEN** the system SHALL fail with an error indicating the profile does not exist

### Requirement: --profile and --out-profile are mutually exclusive
The `login` command SHALL reject invocations that specify both `--profile` and `--out-profile`, failing fast with a clear error before any authentication is attempted. `--profile` re-authenticates an existing profile; `--out-profile` creates a new one — combining them is contradictory.

#### Scenario: Both --profile and --out-profile provided
- **WHEN** user runs `az login --profile prod --out-profile staging`
- **THEN** the system SHALL fail fast with an error stating that `--profile` and `--out-profile` cannot be used together
- **AND** no authentication flow SHALL be triggered

### Requirement: Logout signs out the active profile
The `logout` command SHALL sign out the active profile. When an active profile exists, the command SHALL remove that profile's associated account from the shared MSAL token cache (identified by the profile's `UserPrincipalName`) and SHALL clear the active-profile pointer. The profile definition itself SHALL be retained. When no active profile exists, the command SHALL make no changes.

#### Scenario: Logout with an active profile
- **WHEN** user runs `az logout` and an active profile with a known `UserPrincipalName` exists
- **THEN** the account matching that `UserPrincipalName` SHALL be removed from the MSAL token cache
- **AND** the active-profile pointer SHALL be cleared
- **AND** the active profile's definition SHALL remain in the profile store

#### Scenario: Logout with an active profile that has no UserPrincipalName
- **WHEN** user runs `az logout` and the active profile has no recorded `UserPrincipalName`
- **THEN** no account SHALL be removed from the MSAL token cache
- **AND** the active-profile pointer SHALL still be cleared

#### Scenario: Logout with no active profile
- **WHEN** user runs `az logout` and no active profile is set
- **THEN** the system SHALL make no changes
- **AND** the MSAL token cache SHALL NOT be modified
- **AND** the active-profile pointer SHALL remain unset

### Requirement: "default" is a reserved profile name
The name `"default"` is reserved for bare login. The `--out-profile` flag SHALL reject `"default"` as a value — bare login is the only path that creates/overwrites the default profile.

#### Scenario: --out-profile rejects "default"
- **WHEN** user runs `az login --out-profile default`
- **THEN** the system SHALL display an error indicating "default" is reserved for bare login

### Requirement: No legacy migration required
The product is in preview and breaking changes are expected. The system SHALL NOT migrate `context.json` to profiles. Users SHALL re-login to create new profiles.

#### Scenario: context.json is ignored
- **WHEN** the `ProfileService` is first accessed and `context.json` exists but `profiles.json` does not
- **THEN** no migration SHALL occur
- **AND** the system SHALL behave as if no profiles exist
