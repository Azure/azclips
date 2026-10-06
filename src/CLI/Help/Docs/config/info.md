---
summary: Display information about valid configuration keys.
status: ga
schema: 1.0.0
online version:
---

Description: Show metadata about available configuration keys including their descriptions, default values, and allowed value constraints. Use --keys-only to show only valid key names.

### Display configuration schema

```bash
az config info
```

### Display only valid configuration key names

```bash
az config info --keys-only
```

### Display configuration schema as a table

```bash
az config info --output table
```
