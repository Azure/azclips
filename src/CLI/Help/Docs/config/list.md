---
summary: List all configuration settings.
status: ga
schema: 1.0.0
online version:
---

Description: Display all effective configuration values, including built-in defaults for unset keys. Environment variables override the configuration file, which overrides defaults. Use --configured-only to omit entries whose source is default; values configured in either the file or environment are retained, even when equal to their default. Standard output formats such as JSON, table, TSV, and YAML are supported.

### List current configuration values

```bash
az config list
```

### List only explicitly configured values

```bash
az config list --configured-only
```

### List configuration as JSON

```bash
az config list --output json
```

### List configuration as a table

```bash
az config list --output table
```

### Migration from the previous default

Defaults are now included automatically. Replace `az config list --all` with `az config list`. To retain the previous configured-only behavior, use `az config list --configured-only`.
