---
name: "integrate-scenario-test"
description: "Generate or update Azclips record/playback scenario tests for approved custom-code behavior."
agent: "integrate-custom-code"
tools: [read, search, edit, execute, web, todo]
argument-hint: "OpenSpec change ID, selected feature IDs, and optional upstream test hints"
---

Run the agent's `Scenario-Test Command Behavior`. This command writes scenario tests only, within the agent's `Test Output Scope`.

**Input**: an approved OpenSpec change ID or selected feature IDs, plus the target service module, resource, and action. Usually run after `/opsx:apply <change-id>` when product-code changes were required. Optional hints may include repo-relative upstream test paths.

Convert with `azclips-scenario-test-converter`, and follow `tests/Scenario.Tests/README.md` for the record/playback shape.

End with the `scenario_test_review` report and `blocked_on_user_direction` asking the user to review the scenarios. Iterate on their feedback, then ask whether to record; record only what they approve.
