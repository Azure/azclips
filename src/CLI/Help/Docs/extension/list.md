---
summary: List discovered installed extensions.
status: ga
schema: 1.0.0
online version:
---

Description: Display every extension the CLI can currently load off disk, overlaid with user preferences from the registry. Includes both packages bundled with the `az` install (under `<install>/Extensions/`) and packages installed by the user via `az extension add` (under `~/.azclips/extensions/`). By default, output is grouped by package; use `--by-verb` to regroup by each contributed CLI verb. Each row includes a `Location` field with the absolute path to the primary DLL that would be loaded for that extension, a `selectedVersion` field naming the version dispatch will route to, and a `candidates` array listing every discovered candidate for the package with its tier (Bundled or Installed) and location.

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
