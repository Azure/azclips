---
name: "scenario-test-spec"
description: "Create or update the OpenSpec change for scenario test cases."
agent: "integrate-custom-code"
tools: [read, search, edit, execute, todo]
argument-hint: "Test case content: one block per test case; optional change ID"
---

Run the agent's `Scenario-Test Spec Command Behavior`. This command writes OpenSpec artifacts only.

**Input**: test case content in the `Input Format` below. Optionally a preferred OpenSpec change ID.

End with the `openspec_review` report and `blocked_on_user_direction` asking the user to review or approve the change, then run `/opsx:apply <change-id>`.

## Input Format

The `Test cases` section of an `/integrate-scenario-test` summary is already in this format. A maintainer adding tests by hand uses the same fields, numbering the cases `T1`, `T2`, and so on.

```text
## T1 <title, optional>
Surface: <both | cli-only | powershell-only>
CLI:
1. <Azclips CLI command and arguments>
   - expect: <assertion on this step's result>
2. <next command>
PowerShell:
1. <Azclips PowerShell command and parameters>
   - expect: <assertion on this step's result>
Reference: <optional: upstream test link>
```

Field rules:

- Every case needs its ID, so the OpenSpec review and the tasks can name it. The title is optional.
- Write the `CLI` section for `both` and `cli-only`, and the `PowerShell` section for `both` and `powershell-only`. A case on both surfaces gives each surface its own steps and expectations, because the same behavior can surface differently on each.
- Steps run in the order written, setup and cleanup included. Each step is one command, using the keys of the module's `ExtensionIndex.cs` command tables and the parameter names each command declares. CLI keys carry no `az` prefix, and PowerShell keys may carry a `_<ParameterSet>` suffix. The commands also locate the module and the spec, so no target line is needed.
- Put each `expect:` line under the step whose result it checks; each one becomes one assertion. A step without one only prepares or cleans up.
- An expectation asserts only what stays stable across recordings: a property of the step's output model, content of the request it sent, or the error it fails with. Never console text, and never service-generated values such as timestamps, etags, or generated IPs.
- When a step needs a value from an earlier step's output, refer to it as `<step N: property path>`, such as `<step 1: id>`.
