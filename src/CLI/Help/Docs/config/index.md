---
summary: Manage local Azure CLI configuration settings and preferences.
status: ga
schema: 1.0.0
online version:
---

Description: View, set, update, or delete configuration values used by the Azure CLI. Configuration keys are organized in dot notation (e.g., core.logging.level, clients.cli.output.format). Configuration settings persist across CLI invocations and are stored in your local configuration file.

### Get a configuration value

```bash
az config get --key clients.cli.output.format
```

### List all configuration settings

```bash
az config list
```

### Set a configuration value

```bash
az config set --key clients.cli.output.format --value json
```
