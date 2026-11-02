# Design

## Context

See proposal.md for motivation. Live Aria exports read activity tags; buffered uploads partition records by boundary. Public boundary-only APIs also serve Agent model routing, which must remain independent.

## Goals / Non-Goals

**Goals:** Preserve three fallback scenarios end-to-end and centralize regional defaults in an immutable, strongly typed code-owned policy.

**Non-Goals:** User-configurable destinations, cache expiration, Azure Monitor routing, and Agent model endpoint changes.

## Decisions

- Add a typed resolution outcome alongside the actual boundary. Do not relabel a fallback ROW destination as a confirmed Global boundary.
- Keep boundary-only API wrappers for existing consumers; new telemetry callers use structured resolution.
- Treat absent or invalid cached values as misses and persist only confirmed EU/Global values.
- Persist an outcome string with activity and managed-layer records and partition uploads by both boundary and outcome. This avoids widening the export-sink contract: each partition's entries carry its outcome.
- Legacy or unrecognized outcomes remain conservative when boundary is unknown. Confirmed EU/Global takes precedence over fallback metadata.
- Use existing diagnostic conventions to report failure classifications without tokens, response bodies, or private URLs. Caller cancellation propagates rather than becoming a fallback.

## Risks / Trade-offs

- Unknown tenants and unavailable authentication now route to ROW by explicit request; they do not prove a non-EU tenant. Boundary query failures still route EU.
- Cached confirmed values bypass authentication, preserving existing behavior.
- Old records cannot recover previously discarded failure reasons; retain EU for legacy unknown records.
- Policy changes affect subsequent uploads of persisted outcomes. The outcome is the fact; destination is derived centrally.

## Migration Plan

Ship additive metadata and compatibility wrappers together. Roll back policy defaults to EU if needed without reclassifying cached boundaries. No cache migration is needed.
