# Azclips Custom Code Blacklist

This blacklist records upstream Azure CLI or Azure PowerShell custom-code behavior that should not be ported into Azclips by default.

When a behavior matches an entry here, record it in the classification output under `blacklist_skips` and do not implement it unless the user explicitly asks to override the blacklist.

## Entries

### `parameter-validation`

Skip code whose only purpose is validating user-supplied parameters. Do not add equivalent validation in Azclips even if the current Azclips command has no matching local validation, because the API/service is responsible for validating those parameters.

Common examples:
- Required parameter checks.
- Allowed-value checks.
- Basic type, null, empty string, and format checks.
- Cross-parameter checks that are already enforced by shared Azclips metadata, binding, or service behavior and do not affect command routing or request construction.

Do not skip:
- Validation that changes Azure resource side effects or selects a different REST operation.
- Validation that protects secrets, prevents destructive actions, or enforces a product rule not represented elsewhere in Azclips.
- Normalization or discovery needed to build the actual request body, resource ID, subscription, resource group, location, credential, identity, or related resource reference.
- Local file, URI, JSON, YAML, certificate, key, template, parameter, or payload parsing that turns user input into request content.
- Local filesystem, working-directory, network, tool, runtime, or environment checks that the Azure API cannot perform.
- Service-side preflight, check, test, validate, name-availability, permission, dependency, or existence operations that are part of the command behavior.
- Resource existence, state, dependency, or permission probes used to choose whether to create, update, link, attach, skip, or fail a later operation.
- Content-integrity checks such as hashes or transfer validation that protect upload/download correctness.
- User confirmation behavior such as PowerShell `ShouldProcess` or CLI destructive-operation confirmation.

Replacement action:
- Do not implement the blacklisted validation by default.
- Do not add metadata, binding rules, wrapper checks, or helper methods solely to reproduce upstream parameter validation.
- If a validation block also performs non-validation work, separate the non-validation behavior and route only that remaining behavior.

## Future Entries

Add new entries here as more no-longer-needed upstream custom-code categories are identified.