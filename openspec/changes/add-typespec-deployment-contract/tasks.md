## 1. Optional contract schema and reference handling

- [ ] 1.1 Add typed resource descriptors, supplemental property facets and provenance in an optional IR section, following existing schema/version conventions.
- [ ] 1.2 Validate canonical operation, type, property and input bindings, and update directly dependent TypeScript and .NET readers without changing existing documents.
- [ ] 1.3 Add focused parsing and round-trip cases for absent metadata, enabled empty discovery and malformed references; update the generated IR schema artifact.
- [ ] 1.4 Keep descriptor references valid through existing identity-changing transforms and verify metadata survives the .NET reader.

## 2. Version-aligned TypeSpec extraction

- [ ] 2.1 Add the opt-in emitter option through existing option validation and forwarding; reject non-concrete version selectors only when enabled.
- [ ] 2.2 Prove extraction uses the selected effective TypeSpec version with a multi-version fixture whose older version excludes a newer-only property.
- [ ] 2.3 Extract ARM resource identity, create-or-update operation, writable scope and ordered name/scope bindings using official source metadata.
- [ ] 2.4 Capture source-derived constraints and sensitivity alongside canonical wire/type/lifecycle identities without copying a second resource schema.
- [ ] 2.5 Record resolved source/content and toolchain provenance; diagnose unresolved version projections, bindings and lossy input representations explicitly.
- [ ] 2.6 Cover child-resource and subscription-scope bindings, renamed wire properties and a constrained sensitive input with focused fixture cases.

## 3. Build-only catalogue compatibility audit

- [ ] 3.1 Add an explicit offline audit entry point in the toolkit, reusing existing IR infrastructure and pinning official Bicep loader/catalogue dependencies in a build-only component.
- [ ] 3.2 Resolve catalogue resource versions and referenced types; reject unknown selections, missing versions and unreadable or unresolved definitions without alternate-version fallback.
- [ ] 3.3 Normalize resource name/scope/envelope semantics and compare writable structures, requiredness, nullability, constraints, enum openness and discriminator structure by wire path.
- [ ] 3.4 Add cycle-aware traversal and explicit unsupported-shape diagnostics; do not narrow floating-point inputs or replace unknown shapes with `any`.
- [ ] 3.5 Emit deterministic structured results and coverage/provenance, returning non-zero for any incompatible or unsupported selection and marking unchecked compiler association explicitly.
- [ ] 3.6 Add focused compatible-envelope, enum-narrowing, missing-version, recursive-model and mixed-result cases that verify report shape and exit behavior.

## 4. Regression validation and documentation

- [ ] 4.1 Verify feature-disabled canonical IR and generated processor/command artifacts remain unchanged using existing generation fixtures.
- [ ] 4.2 Run targeted IR, emitter, transform/compiler and .NET reader/audit builds and tests; check the generated IR schema for drift and dependency consistency when manifests change.
- [ ] 4.3 Document source-of-truth boundaries, concrete-version requirements, opt-in generation, offline audit invocation and unsupported-case diagnostics in directly related toolkit documentation.
- [ ] 4.4 Update the research reference with the verified implementation evidence and explicitly retain permission, transport, output, existing-resource and live regression gates for future pilots.
- [ ] 4.5 Confirm no public command, production extension configuration, deployment processor or runtime backend selection changed; leave command pilots and template implementations for a separately approved change.
