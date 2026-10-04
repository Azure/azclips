---
name: "integrate-spec"
description: "Create or update the OpenSpec change for selected custom-code features only."
agent: "integrate-custom-code"
tools: [read, search, edit, execute, todo]
argument-hint: "Selected feature IDs, target API, and optional change ID"
---

Run the agent's `Spec Command Behavior`. This command writes OpenSpec artifacts only.

**Input**: selected feature IDs or an equivalent reviewed behavior list, plus the target service module, resource, and action, or enough conversation context to infer them. Optionally a preferred OpenSpec change ID. Any upstream evidence referenced must come from what `/integrate-compare` gathered.

End with the `openspec_review` report and `blocked_on_user_direction` asking the user to review or approve the change, then run `/opsx:apply <change-id>`.
