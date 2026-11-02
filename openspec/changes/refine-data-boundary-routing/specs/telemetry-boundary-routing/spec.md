## Purpose

Route Aria telemetry consistently using confirmed tenant boundaries or explicit scenario-based regional defaults.

## ADDED Requirements

### Requirement: Scenario-based fallback destinations

The system SHALL distinguish tenant unavailable, authentication unavailable, and boundary unavailable. Defaults SHALL be ROW, ROW, and EU respectively. Confirmed EU and Global boundaries SHALL route to EU and ROW.

#### Scenario: Tenant unavailable
- **WHEN** no usable tenant is available
- **THEN** telemetry routes to ROW

#### Scenario: Authentication unavailable
- **WHEN** a tenant is known but no valid matching token can be obtained
- **THEN** telemetry routes to ROW

#### Scenario: Boundary unavailable
- **WHEN** a matching token is obtained but the boundary request fails, times out, cannot be parsed, is empty, or returns an unexpected value
- **THEN** telemetry routes to EU

#### Scenario: Confirmed boundary
- **WHEN** a confirmed EU or Global boundary is obtained from cache or ARM
- **THEN** telemetry routes to EU or ROW respectively

### Requirement: Valid boundary caching

The system SHALL cache only confirmed EU/Global values. Invalid cached values SHALL be treated as misses. Fallback destinations SHALL NOT be stored as confirmed tenant boundaries.

#### Scenario: Unexpected cached value
- **WHEN** the cache contains an unrecognized boundary
- **THEN** the system attempts resolution instead of trusting that value

### Requirement: Consistent live and buffered routing

The system SHALL retain the resolution scenario through trace persistence and recovery context. Buffered groups SHALL NOT mix scenarios with potentially different destinations. Legacy unknown records without a scenario SHALL continue routing EU.

#### Scenario: Buffered unavailable authentication
- **WHEN** authentication-unavailable telemetry is buffered and later uploaded
- **THEN** it routes to ROW without consulting the current active profile

#### Scenario: Legacy unknown record
- **WHEN** a buffered record has no confirmed boundary and no resolution scenario
- **THEN** it routes to EU

### Requirement: Caller cancellation

The system SHALL propagate caller cancellation instead of treating it as a routing failure.

#### Scenario: Caller cancels lookup
- **WHEN** the caller cancels token acquisition or the boundary request
- **THEN** resolution is canceled and no fallback boundary is cached
