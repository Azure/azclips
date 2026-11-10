---
summary: Return a JSON snapshot of the current CLI environment (azclips version, auth context, subscription, tenant, shell, working directory).
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool env show` returns the same environment snapshot the internal agent gathers before proposing a plan. Fields include azclips version, OS, active subscription and tenant IDs, current working directory, and shell. Intended for external AI tools (Copilot CLI skills, Claude Code) that need to reason about the user's Azure context without spawning a full agent conversation.

### Capture the current environment

```bash
az agent tool env show
```
