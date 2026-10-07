---
name: "integrate-scenario-test"
description: "Generate or update Azclips record/playback scenario tests for approved custom-code behavior."
agent: "integrate-custom-code"
tools: [read, search, edit, execute, web, todo]
argument-hint: "OpenSpec change ID, or Azclips commands / command group / module, plus optional upstream test hints"
---

Run the agent's `Scenario-Test Command Behavior`. This command writes scenario tests only, within the agent's `Test Output Scope`.

**Input**: either an approved OpenSpec change ID with optional feature IDs, or Azclips commands, a command group, or a module when the custom-code workflow was skipped. Command names are the most direct anchor. Optional hints may include repo-relative upstream test paths.

Convert with `azclips-scenario-test-converter`, and follow `tests/Scenario.Tests/README.md` for the record/playback shape.

End with the `scenario_test_review` report and `blocked_on_user_direction` asking the user to review the scenarios. Iterate on their feedback, then ask whether to record; record only what they approve.
