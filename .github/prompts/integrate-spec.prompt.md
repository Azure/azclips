---
name: "integrate-spec"
description: "Create or update the OpenSpec change for selected custom-code features only."
agent: "integrate-custom-code"
tools: [read, search, edit, execute, todo]
argument-hint: "Selected feature IDs, target API, and optional change ID"
---

Create or update the Azclips OpenSpec contract for selected upstream custom-code features. This command writes OpenSpec artifacts only.

**Input**: The argument must include selected feature IDs or an equivalent reviewed behavior list. It should also include the target service module, resource, and action, or enough conversation context to infer them. Optional input may include a preferred OpenSpec change ID. If upstream evidence is referenced, it must come from remote `main` evidence gathered by `/integrate-compare`.

**Steps**

1. Confirm there is an explicit reviewed feature selection. If not, stop and ask the user to run `/integrate-compare` or provide feature IDs.
2. Use a flat change/spec ID based on the target, such as `compute-virtual-machine-create`, unless the user provides one.
3. Use `openspec-propose` to create or update:
   - `openspec/changes/<change-id>/proposal.md`
   - `openspec/changes/<change-id>/design.md`
   - `openspec/changes/<change-id>/tasks.md`
   - `openspec/changes/<change-id>/specs/<change-id>/spec.md`
4. Include only selected features as OpenSpec requirements, scenarios, or implementation tasks. Record dropped or blacklisted features only as non-goals, rationale, or `blacklist_skips`.
5. Write implementation tasks so `/opsx:apply <change-id>` changes product code only. Do not include live-test script creation or editing as an apply task; live-test scripts are generated later with `/integrate-live-test`.
6. Keep the spec as the source of truth for desired Azclips behavior. Do not describe upstream origin, history, or selection mechanics in the spec.
7. Run `openspec validate <change-id> --strict` when the OpenSpec CLI is available.

**Output**

Summarize the change ID, files created or updated, selected features, dropped features, validation result, and the next command to run after review.

End with `blocked_on_user_direction` asking the user to review or approve the OpenSpec change, then run `/opsx:apply <change-id>` when approved. Do not edit product code, live-test scripts, xUnit tests, or docs.