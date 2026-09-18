---
summary: Rebuild the local help catalog used by AI recovery and command-help lookups.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool help generate` rebuilds the local help catalog from the currently discoverable bundled and installed extensions. It uses the CLI's extension version selection and registry pins, then publishes validated catalog files under `~/.azclips/agent-cache/` or the corresponding `AZCLIPS_HOME` directory. On success, the command returns JSON with `generated: true` and the number of discovered commands. It does not call a model or query Azure resources.

The Agent and `az agent tool help` regenerate missing, stale, or corrupt catalog files on demand. Run this command directly to rebuild the catalog or diagnose a generation failure.

### Rebuild the help catalog

```bash
az agent tool help generate
```
