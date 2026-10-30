## ADDED Requirements

### Requirement: Recovery configuration
The system SHALL expose `agent.recovery.trigger=off|manual|auto`,
`agent.recovery.style=oneshot|interactive`, and a positive integer
`agent.recovery.historySize`, defaulting to off, oneshot, and 10.

#### Scenario: Retired keys are removed when the Agent starts
- **WHEN** the configuration file contains `agent.enabled`, `agent.sessionStyle`, or `agent.builtin.endpoint`
- **AND** the Agent starts
- **THEN** those keys SHALL be deleted without migrating their values
- **AND** ordinary commands SHALL NOT delete them

#### Scenario: Off does no history work
- **WHEN** recovery is off and a command fails
- **THEN** no history file, lock, or key storage SHALL be opened

### Requirement: Clearing history through configuration
Setting `agent.recovery.trigger` to off, deleting it, or deleting a parent key SHALL clear the history.

#### Scenario: Off clears history regardless of key case
- **WHEN** the user runs `az config set --key Agent.Recovery.Trigger --value off`
- **THEN** the history SHALL be cleared

#### Scenario: Deleting a parent key clears history
- **WHEN** the user runs `az config delete --key agent.recovery --force`
- **THEN** the history SHALL be cleared

#### Scenario: Unrelated keys keep history
- **WHEN** the user sets a key that is not under `agent.recovery.trigger`
- **THEN** the history SHALL be kept

#### Scenario: Clearing fails
- **WHEN** the configuration is written but clearing fails
- **THEN** the command SHALL succeed and print a warning telling the user to run the same off command again

### Requirement: Original error first
The original error SHALL be shown before capture output and before any AI output.

#### Scenario: Automatic recovery
- **WHEN** trigger is auto and an eligible command fails
- **THEN** stderr SHALL contain the original error before the AI plan

#### Scenario: Manual recovery
- **WHEN** trigger is manual and a command fails
- **THEN** the original error and exit code SHALL be kept
- **AND** after a confirmed save the CLI SHALL show `az troubleshoot recover --id <id>` and the no-ID hint

#### Scenario: Recovery is off
- **WHEN** trigger is off and a command fails
- **THEN** the CLI SHALL show two hints that explain how to set manual or auto, at most 15 times

### Requirement: Protected bounded history
The system SHALL keep the complete failed command and error text, encrypted per user,
and keep the latest `historySize` records.

#### Scenario: Key storage is unavailable
- **WHEN** OS key storage is unavailable, including a locked Linux keyring
- **THEN** only the latest failure SHALL be saved unencrypted in a current-user-only file
- **AND** the CLI SHALL warn

#### Scenario: Damaged history is treated as empty
- **WHEN** the history file cannot be parsed
- **THEN** it SHALL be treated as empty history and replaced on the next save

#### Scenario: A missing key is replaced
- **WHEN** OS key storage is available and the key is confirmed absent
- **THEN** a new key SHALL be created on the next save
- **AND** earlier encrypted records SHALL be skipped when read and deleted on the next save
- **AND** a locked or unavailable key storage SHALL NOT cause a new key

#### Scenario: IDs are random
- **WHEN** a record is saved
- **THEN** its ID SHALL be the parent-shell PID and 8 random lowercase hexadecimal digits
- **AND** it SHALL differ from the IDs of all stored records

### Requirement: Local selection and detail
`az troubleshoot recover` SHALL select the latest failure from the current parent shell,
or the global latest. `--id` SHALL require an exact match. `list` and `show` SHALL be
local and SHALL NOT start AI.

### Requirement: One shared diagnostic flow
Manual and automatic recovery SHALL send the same failed command and error text to the
same Agent flow, only through the input pipe.

#### Scenario: Failure text stays out of arguments and environment
- **WHEN** the Agent process is started for recovery
- **THEN** the failed command and error text SHALL appear in the pipe request
- **AND** SHALL NOT appear in the process arguments or environment
