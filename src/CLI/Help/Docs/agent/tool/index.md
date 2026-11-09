---
summary: Agent internal tools exposed as commands for external AI-tool integration.
status: ga
schema: 1.0.0
online version:
---

Description: The `az agent tool` command group exposes the azclips agent's internal tools (environment probe, help lookup, resource search) as direct CLI commands that emit JSON. External AI tools such as Copilot CLI and Claude Code can invoke these commands as skills instead of spawning a full agent conversation loop, keeping cost and latency low. Output is byte-identical to what the same tool returns inside the agent's AI conversation.

### Capture the current environment snapshot

```bash
az agent tool env show
```

### Look up help for a specific command

```bash
az agent tool docs show --name "vm create"
```

### Find a resource by name

```bash
az agent tool resource find --name prod
```
