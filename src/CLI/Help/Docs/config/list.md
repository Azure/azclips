---
summary: List all configuration settings.
status: ga
schema: 1.0.0
online version:
---

Description: Display all current configuration values. By default, only explicitly-set values are shown. Use --all to include default values. Output can be formatted as JSON for scripting.

### List current configuration values

```bash
azv2 config list
```

### List all configuration including defaults

```bash
azv2 config list --all
```

### List configuration as JSON

```bash
azv2 config list --json
```
