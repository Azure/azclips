---
summary: Retrieve a configuration value.
status: ga
schema: 1.0.0
online version:
---

Description: Get the current value of a configuration setting by key. Configuration keys use dot notation (e.g., output.format, core.debug). If the key is not found, an error is displayed.

### Get output format setting

```bash
az config get --key output.format
```

### Get output as JSON

```bash
az config get --key output.format --json
```

### Get output as a table

```bash
az config get --key output.format --output table
```
