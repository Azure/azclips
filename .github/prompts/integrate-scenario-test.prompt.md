---
name: "integrate-scenario-test"
description: "Summarize upstream live tests into Azclips scenario test cases and stop with a test case table."
agent: "integrate-custom-code"
tools: [read, search, web, todo]
argument-hint: "OpenSpec change ID, or Azclips commands / command group / module, plus optional upstream test hints"
---

Run the agent's `Scenario-Test Command Behavior`. This command is read-only: do not create or edit files.

**Input**: either an approved OpenSpec change ID with optional feature IDs, or Azclips commands, a command group, or a module when the custom-code workflow was skipped. Command names are the most direct anchor. Optional hints may include repo-relative upstream test paths.

Summarize with `azclips-scenario-test-converter`.

End with the `test_case_summary` report. Its `Test cases` section is the input of `/scenario-test-spec`.
