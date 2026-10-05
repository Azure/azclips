---
summary: List all configuration settings.
status: ga
schema: 1.0.0
online version:
---

Description: Display all current configuration values. By default, only explicitly-set values are shown. Use --all to include default values. Standard output formats such as JSON, table, TSV, and YAML are supported.

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

### List configuration as a table

```bash
az config list --output table
```
