---
name: "integrate-compare"
description: "Compare Azure CLI and Azure PowerShell custom code for an Azclips command and stop with a feature decision table."
agent: "integrate-custom-code"
tools: [read, search, web]
argument-hint: "Service module and operation group, optional actions such as get or create, and optional upstream source hints"
---

Run the agent's `Compare Command Behavior`. This command is read-only: do not create or edit files.

**Input**: the Swagger/TSP service module and operation group, and optionally one or more actions, such as `get` or `create`, to compare only those commands; without actions, the whole operation group is compared. Optional hints may include known Azclips command names, Azure CLI commands, Azure PowerShell cmdlets, repo-relative upstream paths, or features to exclude.

Resolve upstream sources from the agent's `Upstream Repository Map`, and classify with `azclips-custom-code-classifier`.

End with the `feature_selection` report and `blocked_on_user_direction` asking which feature IDs to implement.
