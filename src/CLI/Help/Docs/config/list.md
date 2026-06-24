---
summary: List all configuration settings.
status: ga
schema: 1.0.0
online version:
---

Description: Display all current configuration values. By default, only explicitly-set values are shown. Use --all to include default values. Output can be formatted as JSON for scripting.

### List current configuration values

```bash
az config list
```

### List all configuration including defaults

```bash
az config list --all
```

### List configuration as JSON

```bash
az config list --json
```
