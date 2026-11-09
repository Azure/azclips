---
summary: Rebuild the local help catalog used by AI recovery and command-help lookups.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool docs generate` rebuilds the local help catalog from the currently discoverable bundled and installed extensions. It uses the CLI's extension version selection and registry pins, then publishes validated catalog files under `~/.azclips/agent/help/`. On success, the command returns JSON with `generated: true` and the number of discovered commands. It does not call a model or query Azure resources.

The Agent, `az agent tool docs list`, and `az agent tool docs show` regenerate missing, stale, or corrupt catalog files on demand. Run this command directly to rebuild the catalog or diagnose a generation failure.

### Rebuild the help catalog

```bash
az agent tool docs generate
```
