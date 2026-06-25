---
summary: List discovered installed Cirrus extensions.
status: ga
schema: 1.0.0
online version:
---

Description: Display every installed extension package discovered on disk, overlaid with user preferences from the registry. By default, output is grouped by package; use `--by-verb` to regroup by each contributed CLI verb. Built-in host functionality is not listed here unless installed separately as an extension package.

### List installed extensions

```bash
az extension list
```

### Group by verb

```bash
az extension list --by-verb
```

### Emit structured JSON

```bash
az extension list --output json
```
