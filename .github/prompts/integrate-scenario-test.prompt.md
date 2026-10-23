---
name: "integrate-scenario-test"
description: "Summarize upstream live tests into Azclips scenario test cases and stop with a test case table."
agent: "integrate-custom-code"
tools: [read, search, web, todo]
argument-hint: "Service module and operation group, optional actions such as get or list, plus optional upstream test hints"
---

Run the agent's `Scenario-Test Command Behavior`. This command is read-only: do not create or edit files.

**Input**: the service module and operation group, such as `Resources / deployment operations`: the REST operation group the spec is named after. Optionally one or more actions, such as `get` or `list`, to test only those commands of the operation group; without actions, the whole operation group is in scope. Optional hints may include repo-relative upstream test paths.

Summarize with `azclips-scenario-test-converter`.

End with the `test_case_summary` report. Its `Test cases` section is the input of `/scenario-test-spec`.
