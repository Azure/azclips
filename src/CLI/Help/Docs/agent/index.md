---
summary: Agent internal tools exposed as commands, primarily for integration with external AI tools (Copilot CLI, Claude Code) as skills.
status: ga
schema: 1.0.0
online version:
---

Description: The `az agent tool` command group exposes the azclips agent's internal tools (environment probe, help lookup, resource search, SKU availability) as direct CLI commands that emit JSON. External AI tools such as Copilot CLI and Claude Code can invoke these commands as skills instead of spawning a full agent conversation loop, keeping cost and latency low. Output is byte-identical to what the same tool returns inside the agent's AI conversation.

### Discover agent commands

```bash
az agent --help
```

### Capture the current environment snapshot

```bash
az agent tool env show
```
