## Context

The current Storage account create path is generated and supports only a limited set of request fields. It can create a basic account with common generated properties, but it cannot send the selected shared create properties for encryption and identity, Azure Files authentication, network/routing topology, or security and policy controls.

The integration workflow forbids editing generated Storage models or atomic processors. Handwritten behavior must therefore live in the Storage customization layer, with command routing and selected PowerShell surface binding adjusted only as needed.

## Goals / Non-Goals

**Goals:**
- Build the selected shared Storage account create request fields through handwritten Storage customization.
- Keep CLI and PowerShell shared behavior aligned for the selected request-shaping fields.
- Preserve existing generated create behavior for already-supported fields.
- Preserve normal processor lifecycle behavior, including authentication, request sending, long-running operation handling, and output writing.

**Non-Goals:**
- CLI-only name-availability warning/continue behavior.
- PowerShell-only name-availability fail-fast behavior.
- CLI-only defaults, aliases, local context, or help metadata not required for shared request fields.
- PowerShell-only `-AsJob`, parameter-set, pipeline, or output wrapper behavior.
- Pure client-side validation and invalid-combination checks already handled by metadata, binding, serialization, or service errors.
- Editing generated files under `src/Extensions/Storage/Models` or `src/Extensions/Storage/Processors`.
- Creating live-test scripts or xUnit tests as part of the OpenSpec apply work.

## Decisions

### 1. Implement request shaping in Storage customization

Use a handwritten Storage create customization that reuses the existing generated create processor lifecycle where possible and overrides request construction for the selected fields. The customization should keep the same ARM create operation and output behavior while expanding the request body.

**Rationale**: The selected behavior is shared request construction, not a new standalone operation. Reusing the current processor lifecycle keeps authentication, request dispatch, LRO handling, and output behavior consistent.

**Alternative considered**: Modify generated models or atomic processors. Rejected because generated Storage files are not an allowed edit surface in this workflow.

### 2. Keep additional request types customization-owned

Define any additional request DTOs, helper builders, or JSON-body construction under `src/Extensions/Storage/Customization`. Do not add fields to generated model files.

**Rationale**: The current generated create model does not include the selected advanced Storage account properties, and generated code may be refreshed. Customization-owned request construction avoids model churn and preserves the generated/codegen boundary.

### 3. Expose only selected shared surface fields

Add or adjust CLI metadata and PowerShell wrapper parameters only for fields required by the selected shared behavior. Do not add dropped surface-only behavior or validations as part of this change.

**Rationale**: The selected features are shared Storage account create semantics. Surface-only UX differences and pure validation are explicitly out of scope.

### 4. Prefer service-compatible request shaping over duplicated validation

Normalize values that are part of the request contract, such as TLS version spelling and default SAS expiration action, but do not port broad client-side invalid-combination checks.

**Rationale**: Request normalization changes the payload that reaches the service; pure validation is blacklisted for this workflow unless the desired service call cannot otherwise be expressed.

## Risks / Trade-offs

- **[Risk] Advanced Storage account create payloads are broad** -> Keep implementation grouped by the selected four request areas and avoid unrelated Storage account fields.
- **[Risk] CLI and PowerShell parameter names may differ** -> Route both surfaces into the same internal properties or builder inputs, with surface-specific aliases handled only at metadata/wrapper boundaries.
- **[Risk] Generated models may later gain these fields** -> Keep customization isolated so a future codegen refresh can replace or simplify the custom body builder without changing command behavior.

## Open Questions

_(none)_