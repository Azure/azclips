# Proposal

## Why

Telemetry currently collapses unavailable tenants, authentication failures, and unsuccessful boundary lookups into the same unknown value. Developers need a single typed policy to give these scenarios different regional defaults without scattering endpoint decisions across callers.

## What Changes

- Represent confirmed boundaries and three unavailable scenarios explicitly.
- Define code-owned fallback destinations: tenant unavailable and authentication unavailable use ROW; boundary unavailable uses EU.
- Preserve confirmed EU/Global routing and cache only confirmed boundaries.
- Carry the resolution outcome through activity tags, buffered traces, and recovery context so live and replay exports agree.
- Keep legacy records without a resolution outcome conservative (EU for unknown boundaries).

## Capabilities

### New Capabilities

- `telemetry-boundary-routing`: Scenario-based regional routing for Aria telemetry.

### Modified Capabilities

None.

## Impact

Core.Abstractions, Core.Telemetry, Engine, CLI startup, PowerShell import telemetry, related tests and telemetry documentation. Azure Monitor destinations and Agent model gateway selection are unchanged. No new product dependencies or user-editable configuration.
